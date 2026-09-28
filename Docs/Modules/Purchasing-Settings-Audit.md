# جرد إعدادات دورة المشتريات — وتفعيل غير المفعّل

**المرجع:** `Docs/My Remarks/Remarks4.md` — البند 6
**التاريخ:** 2026-09-21
**الكيان:** `PurchaseCycleSettings` (صف واحد لكل شركة) · الشاشة: `/purchasing/settings/purchase-cycle`

---

## 1. الجرد — الحالة قبل الدفعة

الجرد اتعمل بالبحث عن كل خاصية في الكود كله (`src`) خارج شاشة الإعدادات نفسها (الـ DTO والـ command والـ controller بتاعها مش "استخدام" — دي مجرد حفظ وقراءة).

| # | الإعداد | النوع | الافتراضي | كان بيتقري فين؟ | الحالة |
|---|---|---|---|---|---|
| 1 | `CycleType` | enum (Full/Direct/OrderBased/RequestBased/Simplified) | Full | لا مكان | ❌ غير مفعّل |
| 2 | `RequiresPurchaseRequest` | bool | false | `CreatePurchaseOrderCommand` | ✅ مفعّل |
| 3 | `RequiresQuotation` | bool | false | لا مكان | ❌ غير مفعّل |
| 4 | `RequiresPurchaseOrder` | bool | true | لا مكان | ❌ غير مفعّل |
| 5 | `RequiresGoodsReceipt` | bool | true | لا مكان | ❌ غير مفعّل |
| 6 | `AllowInvoiceWithoutOrder` | bool | false | `CreatePurchaseInvoiceCommand` | ✅ مفعّل |
| 7 | `AllowReceiptWithoutInvoice` | bool | true | لا مكان | ❌ غير مفعّل |
| 8 | `AutoCreateReceiptOnInvoicePost` | bool | false | `PostPurchaseInvoiceCommand` | ✅ مفعّل |
| 9 | `AutoCreateInvoiceOnReceipt` | bool | false | `PostGoodsReceiptCommand` | ✅ مفعّل |
| 10 | `RequiresApprovalForPurchaseOrder` | bool | false | لا مكان | ❌ غير مفعّل |
| 11 | `RequiresApprovalForInvoice` | bool | false | `PostPurchaseInvoiceCommand` | ✅ مفعّل |
| 12 | `DefaultPaymentTerms` | enum | Net30 | لا مكان | ❌ غير مفعّل |
| 13 | `CapitalizeAdditionalCosts` | bool | true | لا مكان (كان مذكور في تعليق على `PurchaseInvoice` بس) | ❌ غير مفعّل |

**الخلاصة:** 5 مفعّلين من 13، و**8 غير مفعّلين** — الشاشة كانت بتقول "محفوظ لكن غير مفعّل" على 6 منهم ببادج تحذير، والاتنين التانيين (`CycleType` و`DefaultPaymentTerms`) مكانش عليهم أي تنبيه.

---

## 2. اللي اتعمل — تفعيل الثمانية

### 2.1 `CycleType` — بقى هو اللي بيحدّد مستندات الدورة

نمط الدورة كان اسم مكتوب جنب أعلام ممكن تناقضه (شركة تختار "دورة مباشرة" وبرضه النظام يطلب منها أمر شراء). دلوقتي النمط بيملك **ستة أعلام** هي تعريفه:

| النمط | طلب شراء | عروض أسعار | أمر شراء | إذن إضافة | فاتورة بدون أمر | إذن بدون فاتورة |
|---|---|---|---|---|---|---|
| كامل (Full) | ✔ | ✔ | ✔ | ✔ | ✘ | ✔ |
| مباشر (Direct) | ✘ | ✘ | ✘ | ✔ | ✔ | ✔ |
| بالأمر (OrderBased) | ✘ | ✘ | ✔ | ✔ | ✘ | ✔ |
| بالطلب (RequestBased) | ✔ | ✘ | ✔ | ✔ | ✘ | ✔ |
| مبسّط (Simplified) | ✘ | ✘ | ✘ | ✘ | ✔ | ✔ |

- الجدول ده في `PurchaseCyclePresets` (Application) وبيتقرأ من `GET /purchasing/settings/purchase-cycle/presets`، فالشاشة والسيرفر بيتكلموا من نفس المصدر.
- الشاشة بتطبّق النمط أول ما تختاره، والأعلام الستة بقت **للعرض فقط** ببادج "من نمط الدورة".
- الحفظ بتركيبة متناقضة مرفوض: `PUR-CYCLE-TYPE-MISMATCH` (409).

### 2.2 `RequiresQuotation` — أمر الشراء لازم يطلع من عرض سعر راسي

- `PurchaseOrder.RFQId` كان عمود موجود من غير علاقة ولا استخدام ("لسه مش مُفعَّل"). دلوقتي بقى مفتاح حقيقي لـ `RequestsForQuotation`، وأمر الشراء بياخده.
- لما الإعداد مفعّل: أمر من غير RFQ مرفوض — `PUR-ORDER-RFQ-REQUIRED`.
- وفي كل الأحوال لما الأمر بيشاور على RFQ: لازم يكون `Awarded` (`PUR-ORDER-RFQ-NOT-AWARDED`) ومورد الأمر هو صاحب العرض المختار (`PUR-ORDER-RFQ-SUPPLIER-MISMATCH`).

### 2.3 `RequiresPurchaseOrder` — مفيش استلام على فاتورة مالهاش أمر

`CreateGoodsReceiptCommand`: لو الإعداد مفعّل والإذن جاي من فاتورة مالهاش أمر شراء → `PUR-RECEIPT-ORDER-REQUIRED`.

### 2.4 `RequiresGoodsReceipt` — الفاتورة اللي على أمر مابتترحّلش قبل ما البضاعة توصل

`PostPurchaseInvoiceCommand`: لو الإعداد مفعّل و**الفاتورة مرتبطة بأمر شراء** ومفيش إذن إضافة (لا عليها ولا على أمرها) → `PUR-INVOICE-RECEIPT-REQUIRED`.

> **ليه على الفواتير المرتبطة بأمر بس؟** في الدورة المباشرة البضاعة بتتستلم **على الفاتورة نفسها**، وإذن الإضافة ده مايتعملش إلا بعد ترحيلها — فلو منعنا الترحيل قبل الاستلام هيحصل قفلة مستحيلة. الدورة المباشرة بتحقق الإعداد عن طريق `AutoCreateReceiptOnInvoicePost` أو بإذن بيتعمل بعد الترحيل. الاختبار `Requiring_a_goods_receipt_does_not_deadlock_the_direct_cycle` بيثبّت السلوك ده.

### 2.5 `AllowReceiptWithoutInvoice` — لما يتقفل، الورق قبل البضاعة

`CreateGoodsReceiptCommand`: لو الإعداد مقفول والإذن على أمر شراء لسه مالوش أي فاتورة → `PUR-RECEIPT-INVOICE-REQUIRED`.

### 2.6 `RequiresApprovalForPurchaseOrder` — إرسال الأمر للمورد محتاج صلاحية اعتماد

- حالات `PurchaseOrderStatus` مافيهاش خطوة `Approved`، ومحرك الـ Approval Workflow (موديول 07) لسه مابُنيش.
- فالتفعيل اتعمل بنفس نمط وحدة الأصول الثابتة: `SendPurchaseOrderCommand` بيطلب صلاحية **الاعتماد** على شاشة أوامر الشراء لما الإعداد مفعّل → `PUR-ORDER-APPROVAL-REQUIRED` (403).
- لما محرك الـ Approval Workflow يتبني، ده المكان اللي هيتربط فيه بدل الصلاحية المباشرة.

### 2.7 `DefaultPaymentTerms` — بقى فعلاً الافتراضي

`PaymentTerms` بقى اختياري في إنشاء أمر الشراء وفاتورة الشراء، والترتيب: **المستند ← المورد ← إعدادات الدورة ← Net30**. وفي التعديل: لو مابعتّهاش، المستند بيحتفظ بشروطه.

### 2.8 `CapitalizeAdditionalCosts` — المصروفات الإضافية بقت توصل لتكلفة المخزون

- كانت بتتوزّع على بنود الفاتورة (`AllocatedAdditionalCost`) وبس — ومكانتش بتدخل تكلفة المخزون أبدًا، يعني الافتراضي (`true`) كان كذب.
- دلوقتي `PostGoodsReceiptCommand`: لو الإذن مربوط بفاتورة والشركة بتخين التكاليف، نصيب البند من المصروفات الإضافية **لكل وحدة أساسية** بيتضاف لتكلفة حركة المخزون.
- اختبارين بيثبتوا الاتجاهين: 1000 جرام بـ 0.50 + 100 مصروفات → متوسط التكلفة **0.60** لما الإعداد مفعّل، و**0.50** لما يتقفل.

---

## 3. الحالة بعد الدفعة

| الإعداد | الحالة | المكان اللي بينفّذه |
|---|---|---|
| `CycleType` | ✅ | `UpdatePurchaseCycleSettingsCommand` + `PurchaseCyclePresets` |
| `RequiresPurchaseRequest` | ✅ | `CreatePurchaseOrderCommand` |
| `RequiresQuotation` | ✅ | `CreatePurchaseOrderCommand` |
| `RequiresPurchaseOrder` | ✅ | `CreateGoodsReceiptCommand` |
| `RequiresGoodsReceipt` | ✅ | `PostPurchaseInvoiceCommand` (للفواتير المرتبطة بأمر) |
| `AllowInvoiceWithoutOrder` | ✅ | `CreatePurchaseInvoiceCommand` |
| `AllowReceiptWithoutInvoice` | ✅ | `CreateGoodsReceiptCommand` |
| `AutoCreateReceiptOnInvoicePost` | ✅ | `PostPurchaseInvoiceCommand` |
| `AutoCreateInvoiceOnReceipt` | ✅ | `PostGoodsReceiptCommand` |
| `RequiresApprovalForPurchaseOrder` | ✅ | `SendPurchaseOrderCommand` (صلاحية الاعتماد) |
| `RequiresApprovalForInvoice` | ✅ | `PostPurchaseInvoiceCommand` |
| `DefaultPaymentTerms` | ✅ | `CreatePurchaseOrderCommand` · `CreatePurchaseInvoiceCommand` |
| `CapitalizeAdditionalCosts` | ✅ | `PostGoodsReceiptCommand` |

**13 من 13.** بادج "غير مفعّل" اتشال من الشاشة لأنه بقى بلا معنى.

---

## 4. ⚠️ تغييرات سلوك لازم تاخد بالك منها

الإعدادات دي ليها **قيم افتراضية مفعّلة** في الصفوف الموجودة بالفعل، فتفعيلها بيغيّر سلوك شغّال:

1. **`RequiresGoodsReceipt` افتراضيًا `true`** — أي شركة عندها صف إعدادات هتلاقي ترحيل فاتورة مربوطة بأمر شراء مرفوض لحد ما يتسجّل إذن إضافة. لو ده مش المطلوب: اقفل الإعداد من الشاشة، أو فعّل "إنشاء إذن إضافة تلقائيًا عند ترحيل الفاتورة".
2. **`RequiresPurchaseOrder` افتراضيًا `true`** — الاستلام على فاتورة مالهاش أمر بقى مرفوض.
3. **`CapitalizeAdditionalCosts` افتراضيًا `true`** — متوسط تكلفة المخزون هيزيد بنصيب البضاعة من مصروفات الشحن والجمارك. ده الصح محاسبيًا وهو اللي الإعداد بيقوله من الأول، لكنه **رقم مختلف عن قبل الدفعة**.
4. **نمط الدورة بيفرض أعلامه** — أول حفظ من الشاشة هيظبّط الأعلام الستة على النمط المختار. لو شركة عندها تركيبة قديمة متناقضة، لازم تختار النمط اللي يعبّر عنها.

---

## 5. الاختبارات

في `src/Habbak.ERP.ApiTests/OperationalCycle/PurchasingQaBatch20260921Tests.cs`:

| الاختبار | بيثبت |
|---|---|
| `The_named_cycle_owns_its_document_flags` | تركيبة متناقضة مع النمط مرفوضة |
| `The_cycle_presets_are_what_the_screen_applies` | الـ presets اللي الشاشة بتطبّقها |
| `Requiring_quotations_refuses_an_order_that_has_no_rfq` | `RequiresQuotation` |
| `An_order_from_an_rfq_the_supplier_did_not_win_is_refused` | التحقق من رسو العرض على المورد |
| `Requiring_a_goods_receipt_refuses_posting_an_ordered_invoice_nobody_received` | `RequiresGoodsReceipt` |
| `Requiring_a_goods_receipt_does_not_deadlock_the_direct_cycle` | إن القاعدة مابتقفلش الدورة المباشرة |
| `Auto_creating_the_receipt_satisfies_the_receipt_requirement` | التفاعل بين الإعدادين |
| `Refusing_receipts_before_the_invoice_blocks_receiving_against_an_uninvoiced_order` | `AllowReceiptWithoutInvoice` |
| `Requiring_an_order_blocks_receiving_against_an_invoice_that_has_none` | `RequiresPurchaseOrder` |
| `An_order_with_no_payment_terms_falls_back_to_the_supplier_then_the_settings` | `DefaultPaymentTerms` |
| `Capitalised_additional_costs_reach_the_stock_cost` | `CapitalizeAdditionalCosts` = true |
| `Additional_costs_stay_out_of_the_stock_cost_when_the_company_expenses_them` | `CapitalizeAdditionalCosts` = false |

وكمان اتظبطت اختبارات قديمة كانت بتبعت تركيبة "Full" وهي عايزة دورة مباشرة (`PurchaseInvoiceFlowTests`، `PurchasingUnitsTests`) — بقت بتطلب `Direct` بالاسم، ونفس السلوك بالظبط.
