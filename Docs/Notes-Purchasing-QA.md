# ملاحظات اختبار QA — موديول المشتريات

تاريخ الاختبار: 2026-09-12
طريقة الاختبار: استدعاء API مباشرة (عبر المتصفح) على بيئة التطوير المحلية (`http://localhost:5299`)، وليس قراءة كود فقط — كل نتيجة هنا تم إعادة إنتاجها فعليًا وليست افتراضًا نظريًا.

---

## 1. اختبار دورة شراء كاملة يدويًا

**النتيجة: ✅ تعمل بشكل صحيح** (باستثناء الباگات الموضحة أدناه).

تم تنفيذ السلسلة الكاملة بنجاح:

| الخطوة | المستند | الحالة النهائية | ملاحظات |
|---|---|---|---|
| 1 | PurchaseRequest (طلب شراء) | Draft → Submitted → Approved → **Converted** (تلقائيًا عند ربط أمر شراء به) | يعمل كما هو متوقع |
| 2 | PurchaseOrder (أمر شراء) | Draft → Sent → Confirmed | يعمل كما هو متوقع |
| 3 | GoodsReceipt (إذن استلام) | Draft → Posted | حدّث `ReceivedQuantity` في بنود أمر الشراء بشكل صحيح، وحوّل حالة الأمر إلى `FullyReceived`/`PartiallyReceived` حسب الكمية، وزاد رصيد المخزن بشكل صحيح |
| 4 | PurchaseInvoice (فاتورة شراء) | Draft → Submitted → Posted | أنشأ سجل `SupplierPriceHistory` تلقائيًا عند الترحيل — يعمل كما هو متوقع |
| 5 | SupplierPayment/Voucher (سند دفع) | Draft → Posted | يكتب فعليًا إلى `PurchaseInvoice.AmountPaid`/`Status` (راجع Bug #1 — الكتابة موجودة لكنها غير آمنة عبر العملات) |

**تصحيح لتقرير سابق**: تقرير `04-Purchasing-Module.md` كان وضع علامة ⚠️ على أن آلية الكتابة من السند إلى الفاتورة (Voucher → PurchaseInvoice) "غير مؤكدة" لأن البحث وقتها كان مقتصرًا على مجلد Purchasing. الكود الفعلي موجود في `PostVoucherCommand.cs` تحت موديول Accounting (وليس Purchasing) ويعمل — لكنه يحمل عيبًا حقيقيًا مختلفًا (Bug #1 أدناه).

---

## 2. تأكيد فجوات CycleType — Bug #2 (خطورة: متوسطة)

**السؤال: هل فعلًا لا يغيّر `CycleType` وبقية أعلام `PurchaseCycleSettings` السلوك؟**
**الإجابة: نعم، مؤكد حيًا. أغلبها بلا أي تنفيذ فعلي.**

من أصل ~12 علمًا (flag) في `PurchaseCycleSettings`، ثلاثة فقط لهما أي تأثير فعلي على الكود:

| العلم (Flag) | مُنفَّذ؟ | مكان التنفيذ |
|---|---|---|
| `RequiresPurchaseRequest` | ✅ نعم | `CreatePurchaseOrderCommand.cs` |
| `AllowInvoiceWithoutOrder` | ✅ نعم | `CreatePurchaseInvoiceCommand.cs` |
| `RequiresApprovalForInvoice` | ✅ نعم | `PostPurchaseInvoiceCommand.cs` |
| `CycleType` (enum: Full/Direct/OrderBased/RequestBased/Simplified) | ❌ لا — تجميلي بالكامل | لا يوجد أي `if`/`switch` عليه في أي Handler |
| `RequiresQuotation` | ❌ لا | — |
| `RequiresPurchaseOrder` | ❌ لا | — |
| `RequiresGoodsReceipt` | ❌ لا | — |
| `AllowReceiptWithoutInvoice` | ❌ لا | — |
| `AutoCreateReceiptOnInvoicePost` | ❌ لا | — |
| `AutoCreateInvoiceOnReceipt` | ❌ لا | — |
| `RequiresApprovalForPurchaseOrder` | ❌ لا | — |
| `CapitalizeAdditionalCosts` | ❌ لا | — |
| `DefaultPaymentTerms` | ❌ لا | — |

**إثبات حي**: إعدادات دورة الشراء الفعلية للشركة رقم 1 لديها `requiresGoodsReceipt: true`. رغم ذلك تم إنشاء فاتورة شراء (PurchaseInvoice #8) مرتبطة بأمر شراء #5 **بدون أي `goodsReceiptId`** ونجحت العملية (200 OK) — أي أن العلم لا يُفعِّل أي تحقق فعلي.

**ملاحظة موثّقة سابقًا (وليست بحاجة اختبار)**: قاعدة "لا يمكن ترحيل فاتورة شراء بدون استلام" (Rule 4) موثقة صراحة في تعليق XML داخل `CreatePurchaseInvoiceCommand.cs` كـ"عدم اتساق في المواصفة" تم تجاهله عمدًا (لا يوجد حقل `AllowInvoiceWithoutReceipt` أصلًا) — هذا قرار مسجل من مطور سابق، وليس سهوًا.

---

## 2.1 مراجعة الدورة تحت إعدادات مختلفة (أنماط الدورة الخمسة)

بما أن `CycleType` تجميلي بالكامل (مؤكد الآن أيضًا على مستوى الحفظ: `UpdatePurchaseCycleSettingsCommandHandler` يخزّن قيمته حرفيًا بدون أي منطق يشتق منها باقي الأعلام)، فإن "الأنماط الخمسة" ليست فعليًا سوى تسميات لمجموعات مختلفة من نفس 3 الأعلام الحقيقية. تم اختبار الدورة الكاملة حيًا تحت 4 تركيبات مختلفة من هذه الأعلام (بتغيير علم واحد في كل مرة والتحقق من انعكاس السلوك فورًا)، لإثبات أنها تعمل بشكل مستقل وصحيح في الاتجاهين (تفعيل وإلغاء):

| التركيبة | `RequiresPurchaseRequest` | `AllowInvoiceWithoutOrder` | `RequiresApprovalForInvoice` | النتيجة الحية |
|---|---|---|---|---|
| **كامل (Full)** — الأساس المُختبر في القسم 1 | true | false | true | PR إلزامي قبل PO ✅، PO إلزامي قبل الفاتورة ✅، Submit إلزامي قبل Post ✅ |
| **مباشر (Direct)** | false | true | false | أمر شراء #7 أُنشئ بدون طلب شراء (200) ✅، فاتورة #10 أُنشئت بدون أمر شراء ومُرحّلة مباشرة من Draft بدون Submit (200 → 204) ✅ |
| **بالأمر (OrderBased)** | false | **false** (أعيد تفعيله وحده) | true (أعيد تفعيله وحده) | فاتورة بدون أمر شراء رُفضت بـ 409 `PUR-INVOICE-ORDER-REQUIRED` ✅، وفاتورة Draft بدون Submit رُفضت بـ 409 `PUR-INVOICE-APPROVAL-REQUIRED` ✅ — كلا العلمين يعملان بشكل مستقل |
| **بالطلب (RequestBased)** | **true** (أعيد تفعيله وحده) | false | true | أمر شراء بدون طلب شراء رُفض بـ 409 `PUR-ORDER-REQUEST-REQUIRED` ✅ |

**الخلاصة**: الأعلام الثلاثة الحقيقية تعمل بشكل صحيح ومستقل تمامًا عن بعضها وعن `CycleType`، وتنعكس فورًا عند التبديل بينها (تفعيل ↔ إلغاء) عبر كل التركيبات المختبرة. لا يوجد أي تأثير جانبي لتغيير `CycleType` نفسه على أي من هذه النتائج.

---

## Bug #4 — إنشاء فاتورة شراء مرتبطة بأمر شراء لم يُرسَل ولم يُعتمَد بعد (خطورة: 🟡 منخفضة-متوسطة)

**الملف**: `src/Habbak.ERP.Application/Purchasing/PurchaseInvoices/Commands/CreatePurchaseInvoice/CreatePurchaseInvoiceCommand.cs` (أثناء اختبار تركيبة "بالأمر" أعلاه)

عند إنشاء فاتورة شراء مرتبطة بأمر شراء (`PurchaseOrderId`)، الكود يتحقق فقط من **وجود** الأمر:
```csharp
if (request.PurchaseOrderId is { } purchaseOrderId
    && !await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, cancellationToken))
{
    throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);
}
```
بدون أي تحقق من **حالته** (`Status`). بالمقارنة، `CreateGoodsReceiptCommand.cs` يتحقق بشكل صحيح أن الأمر بحالة `Confirmed` أو `PartiallyReceived` قبل السماح بالاستلام عليه (`src/Habbak.ERP.Application/Purchasing/GoodsReceipts/Commands/CreateGoodsReceipt/CreateGoodsReceiptCommand.cs:72`) — أي أن الانضباط موجود في مكان واحد وغائب في آخر لنفس نوع العلاقة.

**إعادة الإنتاج الحي**: أمر الشراء #7 بقي في حالة **Draft** (لم يُرسَل للمورد أبدًا عبر `/send`، ولا اعتماده عبر `/confirm`). رغم ذلك، تم إنشاء فاتورة شراء (#12) مرتبطة به بنجاح (200 OK).

**الأثر**: في أي شركة تعتمد على وجود أمر شراء كضمان أن الشروط (السعر، الكمية، الشروط التجارية) تم الاتفاق عليها فعليًا مع المورد، يمكن حاليًا إصدار فاتورة استنادًا إلى أمر لم يُرسَل بعد ولم يوافق عليه أحد — يُبطل الغرض الفعلي من اشتراط "أمر شراء قبل الفاتورة".

**تم الإصلاح ✅**: تمت إضافة تحقق من حالة أمر الشراء في `CreatePurchaseInvoiceCommand.cs` — يُشترط الآن أن يكون الأمر بحالة `Confirmed`، `PartiallyReceived`، أو `FullyReceived` (رُفض `Draft`/`Sent`/`Cancelled`/`Rejected`) قبل السماح بربط فاتورة به، برمي `BusinessRuleException("PUR-INVOICE-ORDER-NOT-CONFIRMED", ...)` عند المخالفة.

**تم التحقق حيًا**:
- أمر شراء جديد (#8) بحالة `Draft` → محاولة فوترة عليه → **409 `PUR-INVOICE-ORDER-NOT-CONFIRMED`**.
- نفس الأمر بعد `/send` ثم `/confirm` (أصبح `Confirmed`) → فوترة عليه → **200 نجاح** (فاتورة #13).
- أمر شراء آخر (#5) بحالة `FullyReceived` من اختبار سابق → فوترة عليه → **200 نجاح** (فاتورة #14) — لا تراجع/Regression على المسار الطبيعي (فوترة بعد استلام كامل).

---

## Bug #1 — سند دفع بعملة مختلفة عن الفاتورة يُطبَّق بقيمته الاسمية بدون تحويل (خطورة: 🔴 حرجة)

**الملف**: `src/Habbak.ERP.Application/Accounting/Vouchers/Commands/PostVoucher/PostVoucherCommand.cs`

عند ترحيل سند دفع مرتبط بفاتورة شراء (`RelatedInvoiceId`)، الكود ينفذ:
```csharp
invoice.AmountPaid += voucher.Amount;
invoice.Status = invoice.AmountPaid >= invoice.TotalAmount ? Paid : PartiallyPaid;
```
بدون أي تحقق من أن `voucher.CurrencyCode == invoice.CurrencyCode`، وبدون أي تحويل عبر سعر الصرف. `voucher.Amount` بعملة السند، بينما `invoice.TotalAmount` بعملة الفاتورة — تتم المقارنة والجمع مباشرة كأن العملتين متطابقتان دائمًا.

**السبب الجذري**: لا يوجد حقل "معادل بالعملة الأساسية" (Base Currency Equivalent) على `PurchaseOrder`/`PurchaseInvoice` (فقط `CurrencyCode` + `ExchangeRate` مخزّنان بدون استخدام فعلي لسعر الصرف في حساب `TotalAmount`)، بينما `Voucher` لديه فعليًا حقل `BaseCurrencyAmount` مطلوب. عدم التماثل هذا هو ما يسمح بالخلل.

**إعادة الإنتاج الحي**:
- فاتورة شراء #7 بعملة USD، `TotalAmount = 1000`.
- تم ترحيل سند دفع (#4) بمبلغ **1000 جنيه مصري (EGP)** فقط — أي أقل بحوالي 50 ضعفًا من القيمة الفعلية بسعر الصرف (50 EGP = 1 USD وقتها).
- النتيجة: النظام و ضع حالة الفاتورة **"مدفوعة بالكامل" (Paid)** رغم سداد 1/50 فقط من قيمتها الحقيقية.

---

## Bug #3 — تقارير المشتريات تجمع مبالغ بعملات مختلفة بدون تحويل (خطورة: 🟠 عالية)

**الملف**: `src/Habbak.ERP.Application/Purchasing/Reports/Queries/PurchasesAnalysisQueries.cs` (تقرير `المشتريات حسب المورد` — ونفس النمط محتمل في تقارير أخرى تجمع `TotalAmount`)

الاستعلام يجمع:
```csharp
TotalAmount = g.Sum(i => i.TotalAmount)
```
بدون `GroupBy CurrencyCode` وبدون أي تحويل — أي تجميع أعمى للقيم الاسمية بعملات مختلفة في رقم واحد بلا معنى.

**إعادة الإنتاج الحي**: المورد QATEST1 لديه فاتورتان — واحدة USD (1000) وأخرى EGP (5000). استعلام `purchases-by-supplier` أرجع:
```json
{"supplierCode":"QATEST1","totalAmount":6000,"totalPaid":1000}
```
الرقم 6000 = 1000 + 5000 مباشرة، بدون أي اعتبار لسعر الصرف — رقم مضلل تمامًا لأي قارئ للتقرير.

---

## 4. اختبار RowVersion (Concurrency)

**النتيجة: ✅ يعمل بشكل صحيح — لا يوجد باگ.**

تم اختبار `PurchaseOrder` (المسار `PUT /api/v1/purchasing/purchase-orders/{id}`، مُنفَّذ في `UpdatePurchaseOrderCommand.cs`):

1. إنشاء أمر شراء جديد (#6) والحصول على `rowVersion` الحالي (`AAAAAAAApWY=`).
2. تنفيذ تعديل أول باستخدام هذا الـ RowVersion → **204 نجاح**، و`rowVersion` تغيّر فعليًا إلى (`AAAAAAAApWo=`).
3. تنفيذ تعديل ثانٍ باستخدام نفس الـ RowVersion **القديم/المنتهي** → **409 Conflict**:
   ```json
   {"errorCode":"CONCURRENCY_CONFLICT","message":"تم تعديل هذا السجل من مستخدم آخر — يُرجى إعادة تحميل الصفحة والمحاولة مجددًا."}
   ```

الآلية سليمة تقنيًا: الكود يضبط `db.Entry(order).Property(nameof(PurchaseOrder.RowVersion)).OriginalValue` يدويًا من قيمة العميل قبل الحفظ، مما يجعل EF Core يقارن القيمة الأصلية الحقيقية بقيمة العميل (وليس بقيمة مُحمَّلة حديثًا من قاعدة البيانات، وهو خطأ شائع كان سيُبطل الفحص بالكامل).

⚠️ **ملاحظة نطاق للمستقبل (ليست باگًا الآن)**: هذا الاختبار غطّى `PurchaseOrder` فقط. باقي أوامر التعديل التي تحتوي حقل `RowVersion` (`UpdatePurchaseInvoiceCommand`, `UpdatePurchaseRequestCommand`, `UpdatePurchaseReturnCommand`, `UpdateRFQCommand`, `UpdateSupplierContractCommand`, `UpdateSupplierEvaluationCommand`, `UpdatePurchaseExpenseCommand`) تتبع نفس النمط في القراءة الأولية للكود، لكن لم يتم اختبارها حيًا واحدة تلو الأخرى. كذلك لوحظ أن `UpdateSupplierCommand` **لا يحتوي على حقل `RowVersion` في الطلب أصلًا** — أي أن تعديل بيانات المورد لا يخضع لأي فحص تزامن (كل طلب يُحمّل ويحفظ في نفس اللحظة، فلا يمكن عمليًا حدوث تعارض تزامن يُكتشف). هذا متسق مع بساطة شاشة المورد ولا يُصنَّف كباگ ما لم يُقرَّر أن شاشة المورد تحتاج حماية تزامن أيضًا.

---

## 5. اختبار العملات المتعددة (سعر الصرف)

**النتيجة: ⚠️ الحقول موجودة ومخزّنة بشكل صحيح، لكن الاستخدام الفعلي لسعر الصرف في الحسابات معطوب — راجع Bug #1 و Bug #3 أعلاه.**

تفاصيل الإعداد المستخدم للاختبار: تم إنشاء عملة USD جديدة، ثم بناء دورة شراء كاملة (طلب شراء → أمر شراء → استلام → فاتورة) بعملة USD وسعر صرف 50، لإثبات أن:
- إنشاء المستندات بعملة غير افتراضية يعمل بدون مشاكل.
- لكن أي عملية لاحقة تُقارن أو تُجمِّع مبالغ عبر عملات مختلفة (سداد، تقارير) تفشل بصمت لأن `ExchangeRate` مخزّن فقط ولا يُستخدم أبدًا في أي حساب.

---

## ملخص الباگات

| # | الوصف | الخطورة | الملف الرئيسي | الحالة |
|---|---|---|---|---|
| 1 | سداد سند بعملة مختلفة عن الفاتورة يُطبَّق بالقيمة الاسمية بدون تحويل | 🔴 حرجة | `PostVoucherCommand.cs` | تم الإصلاح ✅ |
| 2 | معظم أعلام `PurchaseCycleSettings` (و`CycleType` نفسه) بلا أي تنفيذ فعلي | 🟠 متوسطة | عدة Handlers | تم توضيحه في الواجهة ✅ |
| 3 | تقارير المشتريات تجمع مبالغ بعملات مختلفة بدون تحويل | 🟠 عالية | `PurchasesAnalysisQueries.cs` | تم الإصلاح ✅ |
| 4 | فاتورة شراء يمكن ربطها بأمر شراء لم يُرسَل/يُعتمَد بعد (لا تحقق من حالة الأمر) | 🟡 منخفضة-متوسطة | `CreatePurchaseInvoiceCommand.cs` | تم الإصلاح ✅ |

RowVersion (بند 4 من طلب الاختبار الأصلي) لم يُسجَّل كباگ لأنه يعمل بشكل صحيح.

---

## الإصلاحات المُنفَّذة

### Bug #1 — إصلاح
**الملف**: `src/Habbak.ERP.Application/Accounting/Vouchers/Commands/PostVoucher/PostVoucherCommand.cs`

تمت إضافة تحقق يمنع ترحيل سند دفع مورد إذا كانت عملته لا تطابق عملة الفاتورة المرتبطة، برمي `BusinessRuleException("PAY-VOUCHER-CURRENCY-MISMATCH", ...)` قبل تطبيق `AmountPaid`. تم اختيار هذا الحل (المنع الصريح) بدلًا من التحويل التلقائي عبر سعر الصرف لأن لا `PurchaseInvoice` ولا `Voucher` لديهما حقل "معادل بالعملة الأساسية" مشترك يمكن التحويل عبره بأمان دون افتراضات إضافية.

**تم التحقق حيًا**:
- سند EGP على فاتورة USD → **409 `PAY-VOUCHER-CURRENCY-MISMATCH`**، الفاتورة تبقى بدون سداد (`amountPaid: 0`).
- سند USD على نفس الفاتورة USD (نفس العملة) → **200 نجاح**، الفاتورة تحوّلت إلى `Paid` بشكل صحيح (لا تراجع/Regression).

### Bug #2 — إصلاح
**الملفات**: `frontend/src/features/purchasing/settings/PurchaseCycleSettingsPage.tsx`، `frontend/src/i18n/ar.json`، `frontend/src/i18n/en.json`

بدلًا من تعطيل الحقول (التي كانت مصممة عمدًا لتُستخدم لاحقًا عند بناء المزيد من الـ Handlers، حسب تعليق الكود الأصلي في الشاشة)، تمت إضافة:
- شارة "⚠️ غير مُفعّل بعد" بجانب كل Checkbox غير مُنفَّذ فعليًا في أي Handler.
- تنبيه عام أعلى الشاشة يوضح أن الأعلام الثلاثة الوحيدة المُفعَّلة فعليًا هي: طلب الشراء إلزامي، السماح بفاتورة بدون أمر شراء، وموافقة فاتورة الشراء.

هذا يحل مشكلة "الواجهة توحي بسلوك غير موجود" دون حذف الإعدادات أو منع حفظها (تبقى قابلة للتعديل للاستخدام المستقبلي).

### Bug #3 — إصلاح
**الملفات**: `src/Habbak.ERP.Application/Purchasing/Reports/Queries/PurchasesAnalysisQueries.cs`، `frontend/src/features/purchasing/reports/types.ts`، `frontend/src/features/purchasing/reports/PurchasingReportsPage.tsx`، ملفات i18n

تقرير "مشتريات حسب المورد" أصبح يُجمِّع حسب `(SupplierId, CurrencyCode)` بدلًا من `SupplierId` فقط — كل مورد له عملات متعددة يظهر بصف منفصل لكل عملة، مع عمود "العملة" جديد في الجدول والتصدير. لا يوجد تحويل عملات (نفس سبب Bug #1 — لا يوجد حقل معادل بعملة أساسية)، لكن الأرقام لم تعد تُخلَط ببعضها.

**تم التحقق حيًا**: نفس المورد (QATEST1) الذي كان يُظهر `totalAmount: 6000` مضلِّلًا، أصبح يُظهر صفين منفصلين وصحيحين: `EGP: 5000` و`USD: 1200`.

⚠️ **ملاحظة نطاق**: هذا الإصلاح غطّى تقرير "مشتريات حسب المورد" فقط (المُثبَت أنه معطوب حيًا). تقرير "مشتريات حسب الصنف" (`GetPurchasesByItemReportQuery`) يُجمِّع `TotalValue`/`AverageUnitPrice` بنفس الطريقة عبر كل الموردين/العملات بدون فصل حسب العملة — لم يُختبر حيًا بمثال بيانات متعدد العملات، لكن بناءً على قراءة الكود يبدو عرضة لنفس المشكلة نظريًا. يُنصح بمراجعته بنفس الأسلوب إذا ظهرت بيانات فعلية بعملات متعددة لهذا التقرير تحديدًا.
