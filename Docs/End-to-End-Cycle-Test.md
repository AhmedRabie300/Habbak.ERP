# اختبار الدورة التشغيلية الكاملة (End-to-End Operational Cycle Test)

> **تاريخ التنفيذ**: 2026-09-14
> **البيئة**: Development — `Server=DESKTOP-DBL395G\MSSQLSERVER01;Database=HabbakErp`
> **الـAPI**: `http://localhost:5299/api/v1`
> **الشركة**: CompanyId = 1 — **الفرع**: BranchId = 1 (فرع المعادي الجديد)
> **القيد الأساسي**: بدون أي قيود محاسبية، وبدون تعديل أي ملف من ملفات الموديولات.

---

## 0. الخلاصة التنفيذية (اقرأ ده الأول)

الدورة اتنفذت فعليًا على السيستم الحي. **5 خطوات من 7 نجحت بالكامل**، وخطوتين اتكشف فيهم
فجوات جوهرية في الكود — مش مشاكل إعدادات، ده كود ناقص:

| # | الخطوة | النتيجة |
|---|--------|---------|
| 1 | فاتورة شراء مباشرة (بدون أمر شراء) | ✅ نجحت |
| 2 | إذن إضافة يدوي مرتبط بالفاتورة | ❌ **مستحيل في الكود الحالي** — اتعمل Workaround |
| 3 | طلب توريد فرع + اعتماده | ✅ نجحت |
| 4 | أمر تحويل مخزني + ترحيله | ✅ نجحت |
| 5 | استلام التحويل في الفرع | ✅ نجحت |
| 6 | بيع منتج تام الصنع من POS | ⚠️ نجح البيع ماليًا |
| 7 | خصم مكونات الوصفة (RealTime) | ❌ **لم يحدث أي خصم مخزني إطلاقًا** |

**أخطر اكتشاف** (الفجوة #5 في تقرير الفجوات): بيع "لاتيه" من نقطة البيع أتم الدفع وأنشأ فاتورة
بقيمة 50.40 ج.م، ومع ذلك **لم تُخصم ولا وحدة واحدة من المخزون** — لا مكونات الوصفة (بن + لبن)
ولا الصنف نفسه. المخزون بيفضل مرتفع للأبد مهما بعت. التفاصيل في القسم 7 وتقرير الفجوات.

**تصحيح لفرضية وردت في طلب الاختبار**: `IPostingService` **موجود ومبني فعلًا** ومستخدم في موديول
الحسابات (القيود اليدوية، السندات، التحويلات، العهد، التسويات البنكية). اللي مش موجود إنه **مش
موصَّل** بمستندات المشتريات/المخزون/نقاط البيع. يعني شرط "بدون قيود محاسبية" اتحقق تلقائيًا
بحكم إن المستندات دي أصلًا مش بتنادي الخدمة — مش بحكم إن الخدمة ناقصة.

---

## 1. بيانات الاختبار المستخدمة

اتعمل إعداد أولي لأن البيانات المطلوبة مكنتش موجودة:

| العنصر | الحالة | المعرّف |
|---|---|---|
| المورد "شركة البن الذهبي" | **أُنشئ** (`E2E-GOLD`) | SupplierId = 18 |
| مخزن "مخزن خامات المعادي" | **أُنشئ** (`WH-BR-MAADI`, BranchMaterials) | WarehouseId = 17 |
| مسؤول عهدة للمعادي | **أُنشئ** (`E2E-CO-MAADI`) | CustodyOfficerId = 4 |
| المخزن الرئيسي | موجود (`WH-MAIN`) | WarehouseId = 12 |
| نقطة البيع | موجودة (`POS-01`) | POSTerminalId = 1 |

**الخامات** — استُخدمت مكوّنات الوصفة الحقيقية بدل الأسماء الافتراضية في الطلب، عشان الخطوة 7
تبقى قابلة للتحقق فعليًا:

| الصنف | المعرّف | الوحدة |
|---|---|---|
| بن اسبرسو مطحون (`ITM-00053`) | 72 | GM |
| لبن (`ITM-00004`) | 23 | ML |
| سكر بودر (`ITM-00003`) | 22 | GM |

**المنتج التام الصنع**: لاتيه (`ITM-00100`) — ItemId = 119، ItemType = FinishedGood،
سعره 45 ج.م، وله وصفة معتمدة (RecipeId = 95, `REC-00092`, Status = Approved):

| المكوّن | الكمية |
|---|---|
| بن اسبرسو مطحون (72) | 9 GM |
| لبن (23) | 150 ML |

> **ملاحظة إعداد مهمة**: `POS-01.DefaultWarehouseId` كان بيشاور على **المخزن الرئيسي (12)**
> مش على مخزن الفرع. اتعدّل ليشاور على `WH-BR-MAADI (17)` عشان البيع يخصم من مخزون الفرع زي
> ما السيناريو عايز. ده تعديل بيانات إعداد، مش تعديل كود.

---

## 2. الخطوة 1 — فاتورة شراء مباشرة (نمط InvoiceOnly)

### 2.1 تفعيل نمط "فاتورة بدون أمر شراء"

الإعدادات الموجودة كانت `allowInvoiceWithoutOrder = false`. الـendpoint ده **Upsert كامل بدون
دلالات PATCH**، فاتعمل `GET` الأول وكل الحقول اترجّعت زي ما هي مع تغيير الفلاج المطلوب بس.

**GET** `/purchasing/settings/purchase-cycle` → الحالة قبل التعديل:

```json
{
  "cycleType": "Full",
  "requiresPurchaseRequest": true,
  "requiresQuotation": true,
  "requiresPurchaseOrder": true,
  "requiresGoodsReceipt": true,
  "allowInvoiceWithoutOrder": false,
  "allowReceiptWithoutInvoice": false,
  "autoCreateReceiptOnInvoicePost": false,
  "autoCreateInvoiceOnReceipt": false,
  "requiresApprovalForPurchaseOrder": true,
  "requiresApprovalForInvoice": true,
  "defaultPaymentTerms": "Net30",
  "capitalizeAdditionalCosts": true
}
```

**PUT** `/purchasing/settings/purchase-cycle` — التغييرات: `allowInvoiceWithoutOrder: true`،
`requiresPurchaseOrder: false`، `autoCreateReceiptOnInvoicePost: false` (إذن الإضافة يدوي).
→ **200 OK**

### 2.2 إنشاء الفاتورة

**POST** `/purchasing/purchase-invoices`

```json
{
  "branchId": 1,
  "invoiceDate": "2026-09-14",
  "dueDate": "2026-10-14",
  "supplierId": 18,
  "supplierInvoiceNumber": "GOLD-2026-0917",
  "purchaseOrderId": null,
  "goodsReceiptId": null,
  "currencyCode": "EGP",
  "exchangeRate": 1,
  "paymentTerms": 3,
  "taxAmount": 0,
  "additionalCosts": 0,
  "notes": "فاتورة شراء مباشرة بدون أمر شراء - دورة تشغيلية كاملة",
  "lines": [
    { "itemId": 72, "quantity": 5000,  "receivedQuantity": 5000,  "unitPrice": 0.50, "unitId": 17 },
    { "itemId": 23, "quantity": 20000, "receivedQuantity": 20000, "unitPrice": 0.02, "unitId": 18 },
    { "itemId": 22, "quantity": 3000,  "receivedQuantity": 3000,  "unitPrice": 0.03, "unitId": 17 }
  ]
}
```

**الاستجابة**: `200 OK` → `{ "id": 15 }` — الفاتورة `PINV-00015` بحالة `Draft`.

### 2.3 الترحيل — واجهنا بوابة اعتماد

أول محاولة ترحيل رجعت:

```json
{ "errorCode": "PUR-INVOICE-APPROVAL-REQUIRED",
  "message": "دورة المشتريات المفعّلة تتطلب اعتماد فاتورة الشراء قبل ترحيلها." }
```

السبب إن `requiresApprovalForInvoice = true` على الشركة دي. **القرار: عدم تعطيل البوابة** — دي
جزء من الدورة الحقيقية المفروض تتاخد، فاتنفذ المسار الصح:

1. **POST** `/purchasing/purchase-invoices/15/submit` → `200 OK` (الحالة → `PendingApproval`)
2. **POST** `/purchasing/purchase-invoices/15/post` → `200 OK` (الحالة → `Posted`)

### 2.4 التحقق من الأثر

```sql
SELECT Id, InvoiceNumber, Status, TotalAmount, SupplierId, PurchaseOrderId, GoodsReceiptId
FROM PurchaseInvoices WHERE Id = 15;
```

| Id | InvoiceNumber | Status | TotalAmount | SupplierId | PurchaseOrderId | GoodsReceiptId |
|---|---|---|---|---|---|---|
| 15 | PINV-00015 | 3 (Posted) | 2990.0000 | 18 | **NULL** | NULL |

الإجمالي صحيح: (5000×0.50) + (20000×0.02) + (3000×0.03) = 2500 + 400 + 90 = **2990** ✓
و`PurchaseOrderId = NULL` يثبت إن دي فاتورة مباشرة فعلًا.

```sql
SELECT COUNT(*) FROM StockTransactions WHERE SourceDocumentType = 'PurchaseInvoice';  -- = 0
```

**النتيجة**: ✅ الفاتورة اترحّلت، **لم يُخصم/يُضاف أي مخزون**، **ولم يُنشأ أي قيد محاسبي** — بالظبط
زي السلوك المتوقع في الطلب.

---

## 3. الخطوة 2 — إذن الإضافة اليدوي: ❌ فجوة في الكود

### 3.1 المحاولة الأولى — إذن استلام بدون أمر شراء

**POST** `/purchasing/goods-receipts`

```json
{
  "warehouseId": 12,
  "receiptDate": "2026-09-14",
  "lines": [
    { "itemId": 72, "quantity": 5000, "acceptedQuantity": 5000, "rejectedQuantity": 0,
      "unitCost": 0.50, "unitId": 17, "qualityCheckStatus": "Passed" }
  ]
}
```

**الاستجابة**: `400 Bad Request`

```json
{ "errorCode": "VALIDATION_ERROR",
  "details": [ { "field": "PurchaseOrderId", "message": "'Purchase Order Id' must be greater than '0'." } ] }
```

### 3.2 المحاولة الثانية — ربط الإذن بالفاتورة بدل أمر الشراء

اتبعت نفس الطلب مع `"purchaseInvoiceId": 15` → **نفس الخطأ بالظبط**. السبب إن
`GoodsReceipt` **مالهاش حقل اسمه `PurchaseInvoiceId` من الأساس**؛ العلاقة معرّفة في الاتجاه
المعاكس بس (`PurchaseInvoice.GoodsReceiptId`).

### 3.3 السبب الجذري

`src/Habbak.ERP.Domain/Purchasing/GoodsReceipt.cs` سطر 45:

```csharp
public long PurchaseOrderId { get; set; }   // non-nullable
```

والتعليق فوقه بيوضح إن ده **قرار تصميمي مقصود** ("every receipt here is created from a
Confirmed/PartiallyReceived order"). و`CreateGoodsReceiptCommandValidator` بيفرض
`GreaterThan(0)`، والـHandler بيحمّل أمر الشراء ويطابق كل بند عليه.

**الخلاصة**: "إذن إضافة يدوي مرتبط بفاتورة مباشرة" **مش موجود كمسار في الكود إطلاقًا**، ومش
مسألة إعدادات. كمان `AutoCreateReceiptOnInvoicePost` اتأكد إنه **فلاج ميت** — معرّف ومتخزَّن
ومتعرض في الـAPI لكن **مفيش أي كود بيقراه** (تم التأكد بالبحث في كامل الحل).

### 3.4 الـWorkaround المستخدم

عشان الدورة تكمّل، استُخدم `WarehouseDocument` من نوع `StockIn`:

**POST** `/inventory/stock-in` → `{ "id": 32 }`، ثم **POST** `/inventory/stock-in/32/post` → `200 OK`

```json
{
  "documentDate": "2026-09-14",
  "destinationWarehouseId": 12,
  "notes": "إذن إضافة يدوي للخامات المشتراة - بديل إذن الاستلام",
  "lines": [
    { "itemId": 72, "quantity": 5000,  "unitCost": 0.50 },
    { "itemId": 23, "quantity": 20000, "unitCost": 0.02 },
    { "itemId": 22, "quantity": 3000,  "unitCost": 0.03 }
  ]
}
```

> ⚠️ **تكلفة الـWorkaround**: المستند ده **مالوش أي رابط بالمورد ولا بفاتورة الشراء** — مفيش
> `SupplierId` ولا `PurchaseInvoiceId` عليه. يعني حلقة التتبّع بين "اشتريت" و"استلمت" **مقطوعة
> تمامًا**، والحركة بتتسجّل كـ`AdjustmentIn` (تسوية) مش `Purchase` (شراء)، وده بيشوّه أي تقرير
> مشتريات أو تحليل تكلفة لاحقًا.

---

## 4. الخطوة 3 — طلب توريد الفرع واعتماده

### 4.1 الإنشاء والإرسال

**POST** `/inventory/branch-requests`

```json
{
  "branchId": 1,
  "requestDate": "2026-09-14",
  "notes": "طلب توريد خامات لفرع المعادي",
  "lines": [
    { "itemId": 72, "requestedQuantity": 2000 },
    { "itemId": 23, "requestedQuantity": 8000 },
    { "itemId": 22, "requestedQuantity": 1000 }
  ]
}
```

→ `{ "id": 6 }` (`BRQ-00006`, Draft) ثم **POST** `/inventory/branch-requests/6/submit` → `200 OK`

> **ملاحظة على `BranchItemLimit`**: الفحص بيتم عند **الإرسال (Submit)** مش عند الإنشاء. ومفيش
> حدود معرّفة للأصناف دي على فرع 1 (صف واحد بس موجود في الجدول كله)، فالفحص **مرّ بدون تطبيق
> فعلي** — يعني القاعدة موجودة في الكود لكن متغطّاش بيانات.

### 4.2 الاعتماد — وهنا بيتولد أمر التحويل تلقائيًا

**POST** `/inventory/branch-requests/6/approve`

```json
{
  "id": 6,
  "sourceWarehouseId": 12,
  "custodyOfficerId": 4,
  "transferDocumentDate": "2026-09-14",
  "lines": [
    { "lineId": 16, "approvedQuantity": 2000 },
    { "lineId": 17, "approvedQuantity": 8000 },
    { "lineId": 18, "approvedQuantity": 1000 }
  ]
}
```

**الاستجابة**: `200 OK` → `{ "transferOrderId": 33 }`

`BranchRequests.Status` بقت `3` (Approved) ✓ — والاعتماد **أنشأ أمر تحويل `TRF-00008` تلقائيًا**
بحالة Draft، وده سلوك مفيد وموثّق في `ApproveBranchRequestCommand`.

---

## 5. الخطوة 4 — ترحيل أمر التحويل

**POST** `/inventory/transfer-order/33/post` → `200 OK`

```sql
SELECT Id, DocumentNumber, DocumentType, Status, SourceWarehouseId, DestinationWarehouseId, CustodyOfficerId
FROM WarehouseDocuments WHERE Id = 33;
```

| Id | DocumentNumber | DocumentType | Status | Source | Destination | CustodyOfficerId |
|---|---|---|---|---|---|---|
| 33 | TRF-00008 | 3 (TransferOrder) | 2 (Posted) | 12 | NULL | **4** ✓ |

مسؤول العهدة محفوظ ✓ (الحقل إلزامي على أوامر التحويل — قاعدة 30 مطبّقة في الـValidator).
`DestinationWarehouseId` بيفضل NULL عن قصد: الوجهة بتتأكد وقت الاستلام.

الحركات الناتجة — **خروج من المخزن الرئيسي**:

| StockTxn Id | الصنف | المخزن | النوع | الكمية |
|---|---|---|---|---|
| 60 | ITM-00053 | WH-MAIN | 7 (TransferOut) | 2000 |
| 61 | ITM-00004 | WH-MAIN | 7 (TransferOut) | 8000 |
| 62 | ITM-00003 | WH-MAIN | 7 (TransferOut) | 1000 |

---

## 6. الخطوة 5 — استلام التحويل في الفرع

**POST** `/inventory/transfer-receipt`

```json
{
  "relatedWarehouseDocumentId": 33,
  "branchId": 1,
  "documentDate": "2026-09-14",
  "destinationWarehouseId": 17,
  "notes": "استلام تحويل الخامات في مخزن خامات المعادي",
  "lines": [
    { "itemId": 72, "quantity": 2000 },
    { "itemId": 23, "quantity": 8000 },
    { "itemId": 22, "quantity": 1000 }
  ]
}
```

→ `{ "id": 34 }` ثم **POST** `/inventory/transfer-receipt/34/post` → `200 OK`

| Id | DocumentNumber | DocumentType | Status | Destination | CustodyOfficerId | RelatedDoc |
|---|---|---|---|---|---|---|
| 34 | TRC-00005 | 4 (TransferReceipt) | 2 (Posted) | 17 | **4** (منسوخ تلقائيًا) ✓ | 33 ✓ |

### 6.1 التحقق النهائي من أرصدة الخطوات 2→5

```sql
SELECT i.Code, i.NameAr, w.Code AS Warehouse, sb.QuantityOnHand
FROM StockBalances sb JOIN Items i ON i.Id = sb.ItemId JOIN Warehouses w ON w.Id = sb.WarehouseId
WHERE sb.ItemId IN (72, 23, 22) ORDER BY i.Code, w.Code;
```

| الصنف | WH-MAIN | WH-BR-MAADI | التحقق |
|---|---|---|---|
| بن اسبرسو مطحون | 3000 | 2000 | 5000 − 2000 = 3000 ✓ |
| لبن | 12000 | 8000 | 20000 − 8000 = 12000 ✓ |
| سكر بودر | 2000 | 1000 | 3000 − 1000 = 2000 ✓ |

**سلسلة الحركات كاملة ومتّزنة**:

| StockTxn | المستند | النوع | الأثر |
|---|---|---|---|
| 57-59 | StockIn (32) | 4 `AdjustmentIn` | دخول WH-MAIN |
| 60-62 | TransferOrder (33) | 7 `TransferOut` | خروج WH-MAIN |
| 63-65 | TransferReceipt (34) | 2 `TransferIn` | دخول WH-BR-MAADI |

كل الحركات `SourceDocumentType = 'WarehouseDocument'` و`JournalEntryId = NULL` ✓

> ⚠️ **ملاحظة على التكلفة**: حركات `TransferOut`/`TransferIn` اتسجّلت بـ**`UnitCost = 0.0000`**
> رغم إن الشراء كان بتكلفة حقيقية (0.50 / 0.02 / 0.03). السبب إن
> `ApproveBranchRequestCommand` بيستخدم `Item.StandardCost ?? 0`، و`StandardCost` فاضي على
> الأصناف دي. يعني **التكلفة بتضيع بالكامل عند أول تحويل** — تفاصيل في الفجوة #7.

---

## 7. الخطوتان 6 و7 — البيع من POS وخصم المكونات: ❌ الفجوة الحرجة

### 7.1 فتح الشيك وإضافة الصنف

- الوردية: `ShiftId = 5` (مفتوحة بالفعل على POS-01)
- **POST** `/pos/checks/open-standalone` مع `orderType: "Takeaway"` → `{ "id": 28 }` (`CHK-00028`)

> **ملاحظة**: `orderType: "DineIn"` اترفض بـ`"طلبات الصالة لازم ترتبط بطرابيزة عبر شاشة الطرابيزات"`
> — سلوك صحيح ومقصود، فاتحوّل للـTakeaway.

- **POST** `/pos/checks/28/lines` مع `itemId: 119` (لاتيه)، `quantity: 1` → `200 OK`

**وهنا أول ملاحظة**: الطلب كان متوقع إن *"الفحص على نقص المكونات يحصل وقت إضافة الصنف للشيك"*.
بمراجعة `AddCheckLineCommand.cs` بالكامل: **مفيش أي فحص مخزون ولا أي قراءة للوصفة**. البند
بيتضاف لأي صنف ليه سعر، بغض النظر عن توفّر مكوناته. الفحص ده **غير موجود**.

### 7.2 إتمام الدفع

**POST** `/pos/invoices/complete-payment`

```json
{
  "checkId": 28,
  "tipAmount": 0,
  "idempotencyKey": "<guid>",
  "payments": [ { "paymentMethodId": 2, "amount": 50.40, "amountTendered": 100 } ]
}
```

**الاستجابة**: `200 OK` → `{ "id": 12 }` — الفاتورة `POSI-00012` بحالة `Posted`.

الإجمالي 50.40 = 40.00 (سعر البند) + 4.80 (رسم خدمة 12%) + 5.60 (ضريبة 14%) ✓

> **ملاحظة جانبية**: `GET /pos/checks/{id}` بيرجّع `total = 40.00` (إجمالي البنود فقط بدون رسم
> الخدمة والضريبة)، بينما الدفع بيطلب 50.40، وبيرفض بـ`POS-PAYMENT-AMOUNT-MISMATCH`. الفرونت
> بيحسب الفرق ده بنفسه — لكن أي مستهلك تاني للـAPI هيقع في الغلط ده.

### 7.3 التحقق من الأثر المخزني — **صفر**

```sql
SELECT COUNT(*) FROM StockTransactions WHERE SourceDocumentType='POSSale' AND SourceDocumentId=12;
-- = 0

SELECT i.Code, i.NameAr, sb.QuantityOnHand FROM StockBalances sb
JOIN Items i ON i.Id = sb.ItemId WHERE sb.WarehouseId = 17;
```

| الصنف | الرصيد قبل البيع | الرصيد بعد البيع |
|---|---|---|
| بن اسبرسو مطحون | 2000 | **2000** (المتوقع 1991) |
| لبن | 8000 | **8000** (المتوقع 7850) |
| سكر بودر | 1000 | 1000 |

**لا حركة مخزنية واحدة اتسجّلت. المخزون لم يتغير إطلاقًا.**

### 7.4 السبب الجذري

في `CompleteCheckPaymentCommand.cs` (السطور 226-248) — ده **كل** كود الخصم المخزني:

```csharp
foreach (var line in check.Lines)
{
    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == line.ItemId, cancellationToken)
        ?? throw new NotFoundException(nameof(Item), line.ItemId);

    if (!item.IsStocked)
    {
        continue;                      // <-- اللاتيه بيخرج من هنا بالظبط
    }

    await stockMovementService.ApplyMovementAsync(new StockMovementRequest
    {
        WarehouseId = terminal.DefaultWarehouseId.Value,
        ItemId = item.Id,              // <-- الصنف المباع نفسه، مش مكوناته
        TransactionType = TransactionType.POSSale,
        Quantity = line.Quantity,
        ...
    }, cancellationToken);
}
```

مشكلتان متراكبتان:

1. **مفيش أي منطق وصفات إطلاقًا**. الكود بيخصم **الصنف المباع نفسه** فقط. تم البحث في كامل طبقة
   `Application/POS` عن `RecipeLine` / `ComponentItemId` / `ProductionSalesModeSetting`:
   **صفر نتائج**. يعني نمط `RealTime` الموصوف في التوثيق **غير مُنفَّذ بالمرة** — مش إعداد
   متعطّل، ده كود مش مكتوب.

2. **`if (!item.IsStocked) continue;` بيبلع العملية بصمت**. اللاتيه `IsStocked = false` (منطقي —
   منتج بيتحضّر عند الطلب)، فالحلقة بتعدّيه من غير أي خصم ومن غير أي تحذير أو لوج.

**المحصلة**: كل منتج تام الصنع في المنيو (283 صنف قابل للبيع) بيتباع من غير ما يستهلك أي خامة.
المخزون بيفضل مرتفع للأبد، والجرد هيطلع فروقات ضخمة غير مفسَّرة، وتكلفة البضاعة المباعة = صفر.

### 7.5 المسار الصحيح الموجود فعلًا في الكود

`CompleteProductionOrderCommand` **بيعمل الصح بالظبط**: بيقرا الوصفة، بيحسب
`ScalingFactor = ActualQuantity ÷ Recipe.OutputQuantity`، بيصرف كل `RecipeLine` بنوع
`ProductionIssue`، وبيستلم المنتج بنوع `ProductionReceipt`.

يعني المسار الشغّال حاليًا هو نمط **`Stocked`**:

```
أمر إنتاج (يستهلك المكونات) → مخزون منتج تام → بيع POS يخصم المنتج التام
```

وده بيتطلب `IsStocked = true` على المنتج التام. أما نمط `RealTime` (بيع مباشر يستهلك المكونات
بدون أمر إنتاج) فلازم يتبني.

---

## 8. تقرير الفجوات

### 8.1 الفجوات المتوقّعة في الطلب — حالة التأكيد

| # | الفجوة المتوقّعة | الحالة | الملاحظة |
|---|---|---|---|
| 1 | مفيش `IPostingService` | ⚠️ **الفرضية غير دقيقة** | الخدمة **موجودة ومبنية** ومستخدمة في موديول الحسابات. الصحيح: **مش موصَّلة** بمستندات المشتريات/المخزون/POS. النتيجة النهائية واحدة (مفيش قيود)، لكن السبب مختلف. |
| 2 | مفيش تكامل ETA | ✅ مؤكد | `ETAReceiptStatus` بيتسجّل كـ`Pending`/`NotApplicable` بس، مفيش إرسال. |
| 3 | مفيش `AuditLog` كامل | ✅ مؤكد | بس `CreatedBy`/`UpdatedBy` على المستندات. |
| 4 | مفيش `ApprovalWorkflow` حقيقي | ⚠️ **جزئيًا** | فيه بوابات اعتماد فعّالة (`PUR-INVOICE-APPROVAL-REQUIRED` وقفتنا فعلًا)، لكنها فحص حالة بسيط — مفيش محرك workflow بأدوار/مستويات. |
| 5 | مفيش `StockReservation` | ✅ مؤكد **وأسوأ من المتوقع** | مش بس مفيش حجز — مفيش **أي** فحص أو خصم للمكونات وقت البيع (القسم 7). |
| 6 | `AutoCreateReceiptOnInvoicePost = false` | ⚠️ **فلاج ميت** | الحقل موجود ومتخزَّن ومتعرض في الـAPI، لكن **مفيش سطر كود واحد بيقراه**. تغييره مالوش أي أثر. نفس الكلام على `AutoCreateInvoiceOnReceipt`. |

### 8.2 فجوات جديدة اتكشفت

| # | الفجوة | الخطورة | التفاصيل |
|---|---|---|---|
| ~~**G-1**~~ | ~~البيع من POS لا يخصم أي مخزون للمنتجات التامة~~ | ✅ **اتقفلت 2026-09-14** | القسم 11. |
| ~~**G-2**~~ | ~~نمط `RealTime` غير مُنفَّذ~~ | ✅ **اتقفلت 2026-09-14** | القسم 11. |
| ~~**G-3**~~ | ~~إذن استلام يدوي مستحيل~~ | ✅ **اتقفلت 2026-09-14** | القسم 12. |
| ~~**G-4**~~ | ~~حلقة التتبّع شراء↔استلام مقطوعة~~ | ✅ **اتقفلت 2026-09-14** | القسم 12. |
| **G-5** | **مفيش فحص مخزون وقت إضافة البند للشيك** | 🟠 عالية | `AddCheckLineCommand` مفيهوش أي فحص. الطلب كان متوقّع رفض فوري عند نقص المكونات. |
| ~~**G-6**~~ | ~~فلاجات إعدادات ميتة~~ | ✅ **اتقفلت 2026-09-14** | القسم 12. |
| ~~**G-7**~~ | ~~التكلفة بتضيع عند التحويل~~ | ✅ **اتقفلت 2026-09-14** | القسم 13. |
| ~~**G-8**~~ | ~~مفيش `IIdempotentRequest` في المشتريات/المخزون~~ | ✅ **اتقفلت 2026-09-14** | القسم 14. |
| ~~**G-9**~~ | ~~`PaymentMethodId` غير صالح يرجّع 500~~ | ✅ **اتقفلت 2026-09-14** | القسم 15. |
| ~~**G-10**~~ | ~~`GET /pos/checks/{id}.total` ناقص الضريبة ورسم الخدمة~~ | ✅ **اتقفلت 2026-09-14** | القسم 15. |

### 8.3 ملاحظة على البند 5 من قيود الطلب (Transaction صريحة)

الطلب اشترط *"استخدم Transaction صريحة لأي عملية بتلمس رصيد"*. الواقع: العمليات دي بتعتمد على
الـ`SaveChangesAsync` الواحد بتاع EF (اللي بيلف كل التغييرات في transaction ضمنية) مش على
`BeginTransactionAsync` صريحة. ده كافي طالما كل التغييرات في `SaveChanges` واحد — وهو الحاصل
في `PostWarehouseDocumentCommand` و`CompleteCheckPaymentCommand`. الاستثناء المقلق هو
`StockMovementService` اللي بيعتمد على `RowVersion` للتزامن ومش بيعمل retry عند التعارض
(`DbUpdateConcurrencyException` بتطلع للمستخدم كـ500).

---

## 9. التوصيات

مرتّبة حسب الأولوية:

1. **🔴 أعلى أولوية — تنفيذ استهلاك الوصفات عند البيع (G-1 + G-2)**
   في `CompleteCheckPaymentCommand`: اقرأ `ProductionSalesModeSetting` بترتيب الأولوية
   (Item > POS > Branch > Company)؛ لو `RealTime` استهلك `RecipeLine` المقياسة بالكمية المباعة
   بنوع `ProductionIssue`؛ لو `Stocked` اخصم الصنف نفسه زي دلوقتي. والأهم: **شيل
   `if (!item.IsStocked) continue;` الصامت** أو على الأقل سجّل تحذير، لأنه دلوقتي بيخفي
   المشكلة. منطق التقييس موجود جاهز في `CompleteProductionOrderCommand` ينفع يتشارك.

2. **🔴 قبل أي استخدام إنتاجي**: لو الخطوة 1 مش هتتنفذ قريب، **اقفل الثغرة مؤقتًا** بإن أي منتج
   تام الصنع يتباع لازم يعدّي على أمر إنتاج (`Stocked`) مع `IsStocked = true`، وإلا المخزون
   هيبقى بلا معنى تمامًا.

3. **🟠 دعم إذن الاستلام المباشر (G-3, G-4)**: خلي `GoodsReceipt.PurchaseOrderId` nullable وضيف
   `PurchaseInvoiceId?`، مع فرض إن واحد منهم على الأقل موجود. ده بيقفل نمط "فاتورة فقط" ويرجّع
   حلقة التتبّع.

4. **🟠 فحص التوفّر عند إضافة البند (G-5)**: نفس منطق الوصفة في `AddCheckLineCommand` كتحقق
   (بدون خصم)، عشان الكاشير يعرف بدري.

5. **🟡 تنظيف الفلاجات الميتة (G-6)**: نفّذها أو شيلها — وجودها بدون تنفيذ أخطر من غيابها.

6. **🟡 حفظ التكلفة عبر التحويلات (G-7)**: استخدم متوسط تكلفة `StockBalance` الفعلي بدل
   `Item.StandardCost` الفاضي.

7. **🟡 تعميم `IIdempotentRequest` (G-8)** على كل أمر بيحرّك مخزون.

---

## 10. التغطية الاختبارية (Automated Tests)

الدورة دي اتنفذت يدويًا مرة واحدة على السيستم الحي (النتائج فوق)، وبعدين **اتحوّلت لاختبارات
آلية** عشان تفضل محمية. كلها **خضراء** حاليًا:

### 10.1 `Habbak.ERP.ApiTests/OperationalCycle/` — 16 اختبار عبر HTTP حقيقي

| الملف | الخطوة | بيغطي |
|---|---|---|
| `CycleSeed.cs` | — | بيانات أساسية مشتركة (مورد/مخازن/أصناف/وصفة/كاشير). كل الأكواد بتاخد لاحقة برقم الشركة لأن فهرس `Recipes` مش مربوط بالشركة. |
| `PurchaseInvoiceFlowTests` | 1 | الرفض لما `AllowInvoiceWithoutOrder=false` · الترحيل والإجمالي · **مفيش مخزون ومفيش إذن استلام تلقائي** |
| `GoodsReceiptFlowTests` | 2 | **قفل تراجُع للفجوة G-3**: الرفض بدون أمر شراء، والرفض كمان لو اتبعت `purchaseInvoiceId` |
| `BranchRequestFlowTests` | 3 | الاعتماد بيولّد أمر تحويل Draft · `BranchItemLimit` بيقف الإرسال |
| `TransferOrderFlowTests` | 4 | الترحيل بيخصم من المصدر · منع الرصيد السالب |
| `TransferReceiptFlowTests` | 5 | **حفظ الكمية عبر السلسلة** (3000+2000=5000) · رفض الاستلام قبل ترحيل الأمر |
| `POSTerminalSaleFlowTests` | 6 | فاتورة POS مرحّلة والشيك بيتقفل · **إضافة بند بدون أي فحص مخزون** |
| `StockDeductionOnSaleTests` | 7 | **قفل تراجُع للفجوتين G-1/G-2** |

### 10.2 `Habbak.ERP.IntegrationTests/StockMovementCycleTests.cs` — 4 اختبارات

محرك المخزون نفسه: اتجاه الحركة لكل `TransactionType` · الكمية دايمًا موجبة والاتجاه من النوع ·
منع الرصيد السالب (مع السماح بيه لو المخزن مسموح له) · إلزامية رقم الدفعة للأصناف المتتبَّعة.

### 10.3 ملاحظة مهمة على اختبارات "قفل التراجُع"

`StockDeductionOnSaleTests` و`GoodsReceiptFlowTests` **بتأكّد السلوك الغلط الحالي عن قصد**، ومكتوب
جوه كل اختبار الـassertion الصح اللي المفروض يحلّ محله. يعني لما حد يصلّح الفجوة، الاختبارات دي
**هتفشل** — وده المقصود: الفشل ده إشارة "قلب الاختبار للمسار الصح"، مش "امسح الاختبار".

مثال من `StockDeductionOnSaleTests`:

```csharp
// Expected once recipe consumption exists:
//   Assert.Equal(2000m - CycleSeed.EspressoPerLatteGrams, espresso);      // 1991
//   Assert.Equal(8000m - CycleSeed.MilkPerLatteMillilitres, milk);        // 7850
Assert.Equal(2000m, espresso);
Assert.Equal(8000m, milk);
```

### 10.4 التشغيل

```bash
dotnet test src/Habbak.ERP.ApiTests/Habbak.ERP.ApiTests.csproj --filter "FullyQualifiedName~OperationalCycle"
dotnet test src/Habbak.ERP.IntegrationTests/Habbak.ERP.IntegrationTests.csproj --filter "FullyQualifiedName~StockMovementCycleTests"
```

> الاختبارات بتشتغل على LocalDB بقاعدة جديدة لكل class وبتتمسح بعدها، فمش بتلمس قاعدة التطوير.

---

**المعرّفات الناتجة من التنفيذ**: Supplier 18 · Warehouse 17 · CustodyOfficer 4 ·
PurchaseInvoice 15 · StockIn 32 · BranchRequest 6 · TransferOrder 33 · TransferReceipt 34 ·
Check 28 · POSInvoice 12

---

# 11. تحديث 2026-09-14 — تنفيذ استهلاك الوصفات في POS (إغلاق G-1 و G-2)

الأقسام من 1 لـ10 فوق **سجل تاريخي** للاختبار الأصلي وبتفضل زي ما هي. القسم ده بيوثّق الإصلاح.

## 11.1 اللي اتنفذ

**ملف جديد**: `Application/Inventory/Settings/ProductionSalesModeResolver.cs`
بينفّذ **قاعدة 17** (أولوية صنف > نقطة بيع > فرع > شركة). قبل كده الإعداد كان بيتخزَّن ويتعدّل
من الشاشة لكن **مفيش كود بيقراه** — دلوقتي بقى ليه أثر فعلي.

**تعديل**: `CompleteCheckPaymentCommand` — الحلقة اللي كانت بتخصم الصنف المباع بس اتحوّلت لمرحلتين:

1. `BuildStockConsumptionAsync` — بتبني خطة الخصم لكل بند حسب النموذج المحلول:
   - **لحظي + الصنف له وصفة معتمدة** → استهلاك المكوّنات بـ`TransactionType.ProductionIssue`،
     مع تقييس بنفس معادلة قاعدة 15 المستخدمة في `CompleteProductionOrderCommand`
     (`الكمية المباعة ÷ Recipe.OutputQuantity`) عشان المشروب يكلّف نفس التكلفة سواء اتعمل بأمر
     إنتاج أو اتحضّر على الكاشير.
   - **مخزني** (أو لحظي لكن الصنف مالوش وصفة، زي زجاجة مياه) → خصم الصنف نفسه بـ`POSSale` زي
     السلوك القديم بالظبط.
2. `GuardAgainstShortagesAsync` — **قاعدة 18**: بتجمّع المطلوب لكل مكوّن **على مستوى الشيك كله**
   (لاتيه + كابتشينو بياخدوا من نفس اللبن) وبتقارنه بالرصيد، وبترفض البيع **قبل أي خصم** برسالة
   بتسمّي المكوّن الناقص والكميات. بتتخطّى الفحص لو المخزن `AllowNegativeBalance`.

> **ليه الفحص المسبق مع إن `StockMovementService` أصلًا بيمنع السالب؟** لأنه بيمنع **مكوّن واحد في
> كل مرة وبعد ما يكون خصم اللي قبله**، ورسالته مابتقولش إيه الناقص. الكاشير محتاج يعرف إيه اللي
> خلص **قبل** ما نص الوصفة يكون اتخصم.

## 11.2 الافتراضي الآمن — مهم جدًا

`ProductionSalesModeResolver.Default = Stocked`. يعني **التغيير ده مش بيغيّر سلوك أي كاشير شغّال
دلوقتي** لحد ما حد يفعّله صراحةً. السبب: تفعيل النمط اللحظي بيغيّر أثر البيع على المخزون و**ممكن
يبدأ يرفض مبيعات** (قاعدة 18)، فده قرار إداري مش افتراضي صامت.

قاعدة بيانات العميل الحالية فيها فعلًا `Company scope = Stocked` — والكود احترمها في الاختبار
الحي (بيعة لاتيه مخصمتش حاجة، بالظبط زي الإعداد ما بيقول).

**للتفعيل على الشركة كلها**:

```http
POST /api/v1/inventory/settings/production-sales-mode
{ "scopeType": "Company", "scopeId": null, "mode": "RealTime" }
```

**أو على صنف واحد كتجربة** (أوصي بده أولًا): `"scopeType": "Item", "scopeId": <ItemId>`.

## 11.3 التحقق الحي (على قاعدة التطوير بالبيانات الحقيقية)

فعّلت النمط اللحظي على صنف اللاتيه (119) بنطاق **صنف** — وده كمان بيثبت أولوية قاعدة 17 لأن
الشركة نفسها مضبوطة `Stocked`:

| الاختبار | النتيجة |
|---|---|
| بيع **2 لاتيه** (وصفة 95: 9 جم بن + 150 مل لبن) | بن: 2000 → **1982** (−18 ✓) · لبن: 8000 → **7700** (−300 ✓) |
| نوع الحركة | حركتين `ProductionIssue` مربوطتين بـ`POSSale / Invoice 14` ✓ |
| القيد المحاسبي | `JournalEntryId = NULL` ✓ (القيود لسه مؤجَّلة زي ما هو مطلوب) |
| **قاعدة 18**: طلب 60 لاتيه (محتاج 9000 مل، متاح 7700) | **رُفض** بـ`409 POS-COMPONENT-SHORTAGE`:<br>`لا يمكن إتمام البيع — مكوّنات ناقصة: لبن (متاح 7700 / مطلوب 9000)` ✓ |
| الذرّية بعد الرفض | الأرصدة **ما اتغيرتش إطلاقًا** وصفر حركات ✓ |
| البن كان كفاية (540 ≤ 1982) | **ماتسمّاش** في الرسالة — بيسمّي الناقص بس ✓ |

بعد التحقق **اتشال إعداد الصنف التجريبي** وقاعدة البيانات رجعت لإعدادها الأصلي
(`Company = Stocked`).

## 11.4 التغطية الاختبارية

`StockDeductionOnSaleTests` اتعاد كتابته (كان بيأكّد السلوك الغلط، دلوقتي بيغطي المسارين) — **7
اختبارات، كلهم خُضر**:

| الاختبار | بيثبت |
|---|---|
| `Real_time_sale_consumes_the_recipe_components_not_the_finished_item` | المكوّنات بتتخصم، الصنف التام لأ، والحركة `ProductionIssue` |
| `Component_quantities_scale_with_the_quantity_sold` | 3 لاتيه = 27 جم + 450 مل |
| `Sale_is_refused_when_a_component_is_short_and_nothing_is_deducted` | قاعدة 18 + الذرّية + الرسالة بتسمّي المكوّن |
| `Shortage_is_judged_on_the_whole_check_not_line_by_line` | التجميع عبر بنود الشيك |
| `Item_scope_overrides_company_scope` | أولوية قاعدة 17 |
| `With_no_setting_configured_behaviour_is_unchanged` | **الافتراضي الآمن** — ضمانة عدم كسر الكاشيرات الشغّالة |
| `Real_time_item_without_a_recipe_falls_back_to_deducting_itself` | زجاجة مياه على نفس الكاشير |

**السويت كامل**: 26 اختبار API + 18 اختبار Integration = **44/44 خُضر، بدون أي regression**.

## 11.5 اللي لسه مفتوح

- **G-5** (فحص التوفّر وقت **إضافة البند** للشيك) **لسه مفتوحة عن قصد**. قاعدة 18 بتتكلم عن منع
  **البيع**، والبيع بيتم عند الدفع — فالمواصفة اتنفذت. لكن الأفضل للكاشير إنه يعرف بدري بدل ما
  يكتشف عند الدفع. ده امتداد طبيعي: نفس `BuildStockConsumptionAsync` تتنادى من
  `AddCheckLineCommand` كتحقق بدون خصم.
- **G-7** (ضياع التكلفة): حركات الاستهلاك اتسجّلت بـ`UnitCost = 0` لأن `StandardCost` فاضي على
  الخامات دي — نفس الفجوة الموجودة أصلًا في التحويلات، ولسه محتاجة حل (متوسط التكلفة الفعلي).
- **كل الفجوات العشرة اتقفلت**: G-1/G-2 القسم 11 · G-3/G-4/G-6 القسم 12 · G-7 القسم 13 ·
  G-8 القسم 14 · G-9/G-10 القسم 15.

---

# 12. تحديث 2026-09-14 — تفعيل الفلاجات الميتة وحل G-3

## 12.1 المشكلة كانت متشابكة مش منفصلة

الطلب كان "فعّل الفلاجات الميتة" (G-6)، بس طلع إن الفلاجين مش متساويين:

| الفلاج | الاتجاه | الحالة وقت البدء |
|---|---|---|
| `AutoCreateInvoiceOnReceipt` | استلام ← فاتورة | `PurchaseInvoice.GoodsReceiptId` **موجود** → قابل للتنفيذ فورًا |
| `AutoCreateReceiptOnInvoicePost` | فاتورة ← استلام | **محجوب تمامًا** بـG-3 |

الفلاج التاني كان محجوب بحاجتين:
1. `GoodsReceipt.PurchaseOrderId` إلزامي، ومفيش `PurchaseInvoiceId` — الفاتورة المباشرة مالهاش
   أمر شراء تشاور عليه.
2. مفيش `WarehouseId` لا على الفاتورة ولا على أمر الشراء — **مفيش مخزن نستلم فيه أصلًا**.

يعني G-6 ما كانتش تتقفل من غير G-3. القرار كان: **نحل G-3 ونقفل التلاتة مع بعض**.

## 12.2 تغيير الـSchema (migration `AddGoodsReceiptInvoiceSource`)

| التغيير | النوع |
|---|---|
| `GoodsReceipt.PurchaseOrderId` → `long?` | توسيع قيد (آمن) |
| `GoodsReceipt.PurchaseInvoiceId` (جديد) | عمود nullable |
| `PurchaseInvoice.WarehouseId` (جديد) | عمود nullable |

كله **إضافي أو توسيع** — مفيش فقدان بيانات. اتطبّق على القاعدتين، والإذن الموجود سابقًا حافظ
على ربطه بأمر الشراء ✅

**القاعدة الجديدة**: إذن الإضافة بيرتبط بمصدر واحد بالظبط — أمر شراء **أو** فاتورة. الشرط ده
متفروض في الـValidator مش في الـschema (قيد CHECK مش قابل للتعبير عنه من EF هنا).

## 12.3 اللي اتنفذ

- **`CreateGoodsReceiptCommand`**: اتعمله إعادة هيكلة حوالين `ResolveSourceAsync` — بيسطّح المصدر
  (مورد + الكميات المتبقية لكل صنف) فحلقة البنود مابقتش تفرق أمر شراء ولا فاتورة. الفاتورة لازم
  تكون **مُرحَّلة** (المسودة لسه بتتعدّل، مش اتفاق نستلم عليه).
- **`PostGoodsReceiptCommand`**: بقى يتعامل مع `purchaseOrder == null`، وبينفّذ
  `AutoCreateInvoiceOnReceipt` — بيرسم فاتورة **Draft** بالكمية **المقبولة** بس (المرفوض وصل
  فعلًا بس اتحجر، فمالوش لازمة على مديونية)، وتاريخ الاستحقاق حسب شروط دفع المورد.
- **`PostPurchaseInvoiceCommand`**: بينفّذ `AutoCreateReceiptOnInvoicePost` — بيرسم إذن **Draft**
  بالكميات المتبقية، والمخزن من الفاتورة وإلا من `Supplier.DefaultWarehouseId`. **لو مفيش ولا
  واحد → بيرمي خطأ واضح** مش بيتخطّى بصمت، لأن اللي فعّل الفلاج مستني إذن.

**قرار مشترك في الاتنين: المستند المتولّد `Draft` مش `Posted`.** الفاتورة بتقول اشترينا إيه، بس
المخزن وحده اللي يقدر يقول وصل إيه فعلًا وبأي حالة — والترحيل التلقائي كان هيحرّك مخزون محدّش
شافه، وهيسبق التقسيم مقبول/مرفوض اللي الإذن موجود عشانه أصلًا.

## 12.4 التغطية الاختبارية — 15 اختبار جديد

| الملف | بيغطي |
|---|---|
| `AutoCreateInvoiceOnReceiptTests` | 5 اختبارات: الرسم عند الترحيل · **مفيش رسم والفلاج مقفول** · المرفوض مش بيتحسب · الاستلام المرفوض بالكامل مبيرسمش فاتورة فاضية · تاريخ الاستحقاق من شروط المورد |
| `AutoCreateReceiptOnInvoicePostTests` | 5 اختبارات: الرسم عند الترحيل · **الدورة كاملة** (فاتورة مباشرة ← إذن ← مخزون) · الفلاج مقفول · الرجوع لمخزن المورد الافتراضي · **الفشل الواضح** لما مفيش مخزن |
| `GoodsReceiptFlowTests` (اتعاد كتابته) | 5 اختبارات: رفض بدون مصدر · رفض بمصدرين · **الاستلام من فاتورة مباشرة بيحرّك مخزون** · رفض الفاتورة المسودة · رفض صنف مش في الفاتورة |

الملف التالت كان **قفل تراجُع** بيأكّد إن الاستلام من غير أمر شراء مستحيل — اتقلب للمسار الصح
زي ما التعليق اللي جواه كان بيقول بالظبط.

**السويت كامل: 57/57 خضراء** (39 API + 18 Integration)، بدون أي regression.

## 12.5 الأثر على G-4

الـWorkaround القديم (`StockIn`) كان بيخلي الحركة `AdjustmentIn` بدون أي رابط بالمورد أو
الفاتورة. دلوقتي الاستلام من فاتورة مباشرة بيسجّل `Purchase` مع `SourceDocumentType = GoodsReceipt`
وبيحمل `SupplierId` و`PurchaseInvoiceId` — **حلقة التتبّع اتقفلت**.

---

# 13. تحديث 2026-09-14 — محرك التكلفة (إغلاق G-7)

تنفيذ **قواعد 39-41** من `Modules/02-Module-Inventory-Manufacturing.md`.

## 13.1 تناقض في المواصفة اتحسم قبل التنفيذ

قسم 2.1 بيحط `AverageCost` حقل على **`Item`** (قيمة واحدة للشركة)، بينما **قاعدة 40** بتقول نصًا
إنها **«قيمة لكل مخزن، مش قيمة عامة للصنف»** وبتستشهد بقاعدة 21. الاتنين ماينفعش يكونوا صح.

**القرار: لكل مخزن، على `StockBalance`** — لأن منطق حفظ التكلفة عبر التحويل (نفس قاعدة 40)
مالوش معنى أصلًا لو القيمة عامة (هتعيد حساب المتوسط على نفس الرقم)، وقاعدة 42 كمان بتتكلم عن
«`AverageCost` **في المخزن المصدر**».

## 13.2 اللي اتنفذ

**`StockBalance.AverageCost`** (18,4) + **`LastCostUpdateAtUtc`**.

**`StockMovementService`** — الاتجاهين بياخدوا التكلفة من مكانين مختلفين تمامًا:

| الاتجاه | مصدر التكلفة | السبب |
|---|---|---|
| **داخل** | من المُنادي، ولازم **> 0** (قاعدة 41) | دي اللحظة الوحيدة اللي النظام بيعرف فيها البضاعة اتشترت بكام |
| **خارج** | من `StockBalance.AverageCost` — **واللي المُنادي باعته بيتجاهل** | الصرف مش سعر جديد، ده سحب من مخزون متسعّر بالفعل. وكمان بيمنع أي موديول إنه يغلّط في COGS |

النتيجة: **كل مواقع الصرف اتصلّحت لوحدها** من غير ما نلمسها (POS، إنتاج، هالك، تحويل).

**قاعدة 39** — المتوسط المرجّح بيتحدّث جوّه نفس الـtransaction بتاعة الحركة، مش في خطوة لاحقة:
```
New = (OldQty × OldAvg + RecvQty × RecvCost) ÷ (OldQty + RecvQty)
```

**قاعدة 40** — عند ترحيل أمر التحويل، التكلفة المحلولة **بتتكتب رجوع على سطر المستند**، فاستلام
التحويل بينسخ نفس الرقم بالظبط بدل ما يعيد اشتقاقه في المخزن الوجهة.

**`ResolveInboundCostAsync`** — للحالات اللي مفيش فيها سعر شراء أصلًا (المرتجعات، زيادة الجرد):
متوسط المخزن ← التكلفة المعيارية ← **يرمي خطأ** بدل ما يقبل صفر.

**تقرير تقييم المخزون** بقى يقرا `AverageCost` بدل `StandardCost` الفاضية.

## 13.3 إصلاح البيانات القديمة — migration على مرحلتين

المشكلة: كل الأرصدة الموجودة هتبدأ بـ`AverageCost = 0`، وساعتها قاعدة 41 هترفض أول مرتجع أو
زيادة جرد عليها.

| Migration | بيعمل إيه | النتيجة على القاعدة الحية |
|---|---|---|
| `AddStockBalanceAverageCost` | متوسط مرجّح من **تاريخ الحركات** الفعلي | **10 من 13** رصيد رجعت تكلفته الحقيقية (0.50 / 0.02 / 0.03 بالظبط) |
| `RepairTransferOrphanedStockCosts` | الأرصدة اللي وصلت **بتحويل صفري التكلفة** (ضرر G-7 نفسه) بتاخد متوسط الصنف من المخازن التانية | **الـ3 الباقيين اتصلّحوا** |

**النتيجة النهائية على قاعدة العميل**: صفر أرصدة بلا تكلفة، وقيمة المخزون الإجمالية بقت
**243,565.95 ج.م** بدل **صفر**.

## 13.4 التغطية الاختبارية — 8 اختبارات جديدة

| الاختبار | بيثبت |
|---|---|
| `First_receipt_sets_the_average_cost_outright` | أول استلام بيحدد المتوسط |
| `Second_receipt_at_a_different_price_produces_the_weighted_average` | (100×10 + 300×20) ÷ 400 = **17.50** |
| `Issuing_stock_does_not_disturb_the_average` | الصرف مبيغيّرش المتوسط |
| `An_outbound_movement_is_costed_from_the_balance_not_from_the_caller` | المُنادي بعت 999، الحركة اتسجّلت بـ10 |
| `A_receipt_without_a_real_unit_cost_is_refused` | قاعدة 41 |
| `Average_cost_is_tracked_per_warehouse_not_per_item` | نفس الصنف بتكلفتين في مخزنين |
| `Inbound_cost_resolution_prefers_the_warehouse_average_then_standard_cost` | سلسلة الرجوع كاملة |
| `Transferred_stock_carries_its_cost_to_the_destination` | **قاعدة 40** عبر دورة المستندات كاملة |

> اختبار `Full_transfer_chain` القديم **فشل** أول ما اتطبّق المحرك — لأن الـseed كان بيحط مخزون
> بتكلفة صفر، والمحرك رفض ينقله. ده مكانش عطل، ده إثبات إن قاعدة 41 شغالة فعلًا من الطرف للطرف.

**السويت كامل: 65/65 خضراء** (40 API + 25 Integration).

## 13.5 قاعدة 42 — تجميد التكلفة على سطر الفاتورة

`POSInvoiceLine.UnitCost` (إلزامي) و`SalesInvoiceLine.UnitCost` (اختياري) اتضافوا، ودول اللي
محرك الترحيل (`SumLineQuantityTimesUnitCost`) بيقرا منهم COGS من غير ما يعيد حساب أي حاجة.

### أهم تفصيلة: التكلفة اللحظية مش تكلفة الصنف

لاتيه **مش مخزَّن أصلًا** (`IsStocked = false`)، فمتوسط تكلفته **صفر**. أي كود بياخد تكلفة
الصنف نفسه كان هيسجّل بيعة اللاتيه بتكلفة صفر.

الصح إن تكلفة السطر = **مجموع تكلفة المكوّنات اللي اتصرفت فعلًا**:

```
9 جم بن × 0.50  = 4.50
150 مل لبن × 0.02 = 3.00
                  ─────
تكلفة اللاتيه     = 7.50
```

عشان كده `PlannedMovement` بقت بتحمل `CheckLineId`، والحركات بعد ما بتترحّل بتترجّع تكلفتها
الفعلية وتتجمّع لكل سطر شيك، وبعدين تتقسم على الكمية → `UnitCost` لكل وحدة.

### فاتورة المبيعات — انحراف مقصود عن نص القاعدة

القاعدة بتقول «Snapshot لحظة **إنشاء السطر**». ده **مستحيل** لفاتورة المبيعات هنا لأن
`SalesInvoice` **مالهاش `WarehouseId` من الأساس**، والمخزون بيخرج أصلًا عند ترحيل **أمر التسليم**
مش عند ترحيل الفاتورة.

فالتنفيذ بيجمّد التكلفة **عند ترحيل أمر التسليم** — أول لحظة فيها مخزن معروف وبضاعة خرجت فعلًا.
`UnitCost = null` معناها لسه مفيش تسليم، وبالتالي مفيش تكلفة بيع تتقيّد أصلًا. (`POSInvoiceLine`
غير اختياري لأن البيع والصرف بيحصلوا في نفس اللحظة.)

### التغطية — 5 اختبارات

| الاختبار | بيثبت |
|---|---|
| `A_real_time_sale_costs_the_line_at_what_its_recipe_consumed` | **7.50** من المكوّنات مش صفر من الصنف |
| `Component_cost_scales_with_the_quantity_sold` | `UnitCost` معدّل لكل وحدة مش إجمالي |
| `A_stocked_sale_costs_the_line_at_the_items_own_average` | المسار المخزني |
| `The_frozen_cost_survives_a_later_change_to_the_warehouse_average` | التكلفة اتجمّدت فعلًا |
| `Total_cost_of_sale_can_be_read_straight_off_the_invoice_lines` | COGS من السطور = اللي خرج من المخزن بالظبط |

**السويت كامل: 70/70 خضراء** (45 API + 25 Integration).

## 13.6 محرك الترحيل بقى جاهز

`SumLineQuantityTimesUnitCost` في `00-Posting-Engine-Architecture.md` (قسم 4.1) دلوقتي عنده
مصدر حقيقي يقرا منه — وده كان آخر شرط مخزني ناقص قبل ما القوالب 1، 2، 6، 7 (اللي كلها فيها
سطور COGS) تبقى قابلة للترحيل فعليًا.

---

# 14. تحديث 2026-09-14 — حماية الترحيل من التكرار (إغلاق G-8)

## 14.1 ليه بقت أهم من الأول

الفجوة كانت: نمط `IIdempotentRequest` موجود وشغّال، بس **POS بس** هو اللي مستخدمه. يعني أي
إعادة إرسال لترحيل إذن إضافة أو أمر تحويل كانت هتكرّر الحركة المخزنية.

وبعد شغل التكلفة (القسم 13) الخطورة زادت: استلام بيتطبّق مرتين **مش بس بيضاعف الكمية** — بيدخّل
نفس الشحنة في المتوسط المرجّح تاني، والرقم ده **مبيرجعش تاني** لوحده.

## 14.2 اللي اتعمل

الأوامر اللي بتحرّك مخزون كلها بقت `IIdempotentRequest`:

| الموديول | الأوامر |
|---|---|
| المخزون | `PostWarehouseDocument` · `CloseInventoryCount` · `CompleteProductionOrder` · `CreateWasteRecord` |
| المشتريات | `PostGoodsReceipt` · `PostPurchaseInvoice` |
| المبيعات | `PostDeliveryOrder` · `PostSalesReturn` |
| نقاط البيع | `CreatePOSReturn` |

**النقل عبر هيدر `Idempotency-Key`** مش في الـbody — لأن نقاط النهاية دي (`POST /{id}/post`)
**مالهاش body أصلًا**. العميل بيبعته **بس** لما يكون بيعيد إرسال طلب مش متأكد إنه وصل (retry بعد
timeout، أو طابور offline بيتفرّغ).

> **من غير الهيدر السلوك زي ما هو بالظبط** — وده اللي بيخلي إضافته آمنة على كل النقاط دفعة واحدة
> من غير ما أي عميل حالي يتأثر.

## 14.3 تفرقة مهمة اتوثّقت في اختبار

`A_different_key_is_a_different_request_and_is_not_short_circuited` — مفتاح جديد = طلب جديد،
فالـhandler بيشتغل فعلًا، واللي بيمنع الترحيل التاني هو **حارس حالة المستند نفسه**
(`INV-WHDOC-NOT-DRAFT`) مش الـidempotency.

الـidempotency **بتمنع تكرار المحاولة الواحدة، مش بتفحص الحالة** — والاتنين محتاجين بعض.

## 14.4 التغطية — 4 اختبارات

| الاختبار | بيثبت |
|---|---|
| `Replaying_a_post_with_the_same_key_moves_stock_only_once` | حركة واحدة بس اتسجّلت |
| `A_replayed_receipt_does_not_skew_the_weighted_average` | المتوسط فضل **0.50** مش اتلخبط |
| `A_different_key_is_a_different_request_and_is_not_short_circuited` | الفرق بين الـidempotency وحارس الحالة |
| `Without_a_key_the_endpoint_behaves_exactly_as_before` | ضمانة عدم كسر العملاء الحاليين |

**السويت كامل: 74/74 خضراء** (49 API + 25 Integration).

---

# 15. تحديث 2026-09-14 — إغلاق G-9 و G-10

## 15.1 G-10: الشيك بقى بيقول المطلوب فعلًا

`GET /pos/checks/{id}` كان بيرجّع **إجمالي البنود بس**، والدفع بيطلب ده + رسم الخدمة + الضريبة.
يعني الرقم الموثَّق **مكانش أبدًا المبلغ المستحق**، والفرونت كان بيعوّض بإنه **بيعيد حساب النسب
بنفسه** — وده مصدر انحراف حقيقي: أي فرق تقريب بين الطرفين بيرفض كل دفعة بـ
`POS-PAYMENT-AMOUNT-MISMATCH`.

**`CheckTotalsCalculator`** جديد، بنفس مبدأ `ManualDiscountCalculator` و`LoyaltyRedemptionCalculator`
الموجودين: حساب واحد مشترك بين المعاينة والخصم الفعلي، فمستحيل يختلفوا.

حقول جديدة على الشيك: `serviceChargeAmount` · `taxAmount` · **`payableTotal`**
(و`total` فضل زي ما هو = صافي البنود بعد الخصومات، عشان مايكسرش أي مستهلك حالي).

**والفرونت بقى يقرا الأرقام دي بدل ما يحسبها** — الحاجة الوحيدة اللي لسه بيملكها شاشة الدفع هي
**البقشيش**، لأنه بيتدخل فيها أصلًا.

## 15.2 G-9: طريقة دفع غلط بقت خطأ مفهوم

`paymentMethodId` مش موجود كان بيعدّي كل التحققات ويموت على قيد الـFK، فيطلع للكاشير **500 خام**.
دلوقتي بيتفحص **قبل إنشاء أي كيان** ويرجّع `POS-PAYMENT-METHOD-NOT-FOUND`.

## 15.3 التحقق الحي

شاشة الدفع على شيك حقيقي (`CHK-00021`، فرع فيه خدمة 12% وضريبة 14%):

| البند | القيمة |
|---|---|
| الإجمالي الفرعي | 102.50 |
| رسم الخدمة (12%) | **12.30** |
| ضريبة القيمة المضافة (14%) | **14.35** |
| الإجمالي | **129.15** |
| شريط التوازن | `المدفوع: 129.15 — المطلوب: 129.15 · متزن ✓` |

## 15.4 التغطية — 6 اختبارات

| الاختبار | بيثبت |
|---|---|
| `The_check_reports_service_charge_vat_and_what_is_actually_payable` | 40 → 4.80 + 5.60 → **50.40** |
| `Paying_exactly_the_reported_payable_total_is_accepted` | **العميل يقرا الرقم ويدفعه من غير أي حساب** |
| `Paying_the_pre_tax_total_is_still_rejected_with_the_figure_to_use` | الرفض لسه بيقول الرقم الصح |
| `With_no_branch_settings_payable_equals_the_line_total` | فرع بلا إعدادات |
| `An_unknown_payment_method_is_a_business_error_not_a_500` | G-9 |
| `A_rejected_payment_method_leaves_the_check_untouched` | الشيك فضل قابل للدفع بعد الرفض |

**السويت كامل: 80/80 خضراء** (55 API + 25 Integration).
