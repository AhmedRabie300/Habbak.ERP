# Remarks7.md — ربط أمر الشراء بطلب الشراء

> **المرجع**: ملاحظة من الاختبار اليدوي 2026-09-25.
> **الأولوية**: 🟡 متوسط.
> **العلاقة**: نفس نمط `Remarks6.md` (فاتورة مشتريات ↔ أمر شراء) — بس دلوقتي (أمر شراء ↔ طلب شراء).
> **الهدف**: تحميل تلقائي لسطور طلب الشراء في أمر الشراء الجديد + تتبع الكميات المصدرة.

---

## 1. السياق

في موديول المشتريات، شاشة **أمر الشراء**:
- المستخدم بيختار **طلب شراء**.
- **المفروض** تتحمّل الأصناف والكميات والوحدات تلقائيًا.
- **المشكلة الحالية**: مفيش تحميل تلقائي، والمستخدم بيدخل كل حاجة يدوي.
- **النتيجة**: بطء + أخطاء + إمكانية إصدار نفس السطر مرتين.

**الهدف**:
- **تسريع إدخال أمر الشراء**.
- **تقليل الأخطاء**.
- **ضمان تتبع الكميات المصدرة**.

---

## 2. المطلوب

### 2.1 سلوك حقل "طلب الشراء" (`PurchaseRequestId`)

#### أ) عند اختيار طلب شراء

1. **يتم تحميل السطور المتبقية** اللي `OrderedQuantity < RequestedQuantity`.
2. **لكل سطر**:
   - `ItemId` (الصنف).
   - `UnitId` (الوحدة — من طلب الشراء).
   - `UnitFactor` (معامل التحويل).
   - `Quantity` = **المتبقي** = `RequestedQuantity - OrderedQuantity`.
   - `UnitCost` (السعر المقدر — من آخر سعر شراء / `SupplierPriceHistory` / `StandardCost`).
   - `BaseQuantity` = `Quantity × UnitFactor`.
   - `BaseUnitCost` = `UnitCost ÷ UnitFactor`.
   - `PurchaseRequestLineId` (للتتبع).

#### ب) عند مسح طلب الشراء

- تفريغ السطور (بعد تأكيد المستخدم).

### 2.2 سلوك الحفظ (`CreatePurchaseOrderCommand`)

1. **التحقق**:
   - `Quantity <= (RequestedQuantity - OrderedQuantity)`.
   - رفض بـ `PUR-ORDER-QTY-EXCEEDS-REQUEST`.
2. **تحديث `OrderedQuantity`**:
   - `OrderedQuantity += Quantity`.
3. **تحديث حالة طلب الشراء**:
   - `PartiallyConverted` (اتحوّل جزئيًا).
   - `FullyConverted` (اتحوّل بالكامل).

### 2.3 سلوك التعديل والإلغاء

- **تعديل أمر الشراء**: عكس القديم + تطبيق الجديد.
- **إلغاء أمر الشراء**: `OrderedQuantity -= Quantity`.

---

## 3. الكيانات المتأثرة

| # | الكيان | التغيير |
|---|---|---|
| 1 | `PurchaseRequestLine` | إضافة `OrderedQuantity` (لو مش موجود) |
| 2 | `PurchaseRequest` | إضافة حالة `PartiallyConverted` (لو مش موجودة) |
| 3 | `PurchaseOrderLine` | إضافة `PurchaseRequestLineId` (لو مش موجود) |

---

## 4. الـ Application Layer

### 4.1 Queries جديدة

| # | Query | الوصف |
|---|---|---|
| 1 | `GetOpenPurchaseRequestsQuery` | طلبات الشراء غير المكتملة |
| 2 | `GetPurchaseRequestLinesForOrderQuery` | سطور الطلب مع الكميات المتبقية |

### 4.2 Commands محدّثة

- `CreatePurchaseOrderCommand` → التحقق + تحديث `OrderedQuantity`.
- `UpdatePurchaseOrderCommand` → عكس القديم + تطبيق الجديد.
- `CancelPurchaseOrderCommand` → إرجاع الكميات.

### 4.3 Validators جديدة

- `PurchaseOrderLineQuantityValidator`:
  - `Quantity <= RemainingQuantity`.
  - رفض بـ `PUR-ORDER-QTY-EXCEEDS-REQUEST`.

---

## 5. الـ Frontend

**`PurchaseOrderEditPage`**:
1. **حقل طلب الشراء** → Dropdown + تحميل تلقائي.
2. **زرار "تحميل سطور الطلب"**.
3. **عمود "المتبقي في الطلب"**.
4. **تحذير لو الكمية > المتبقي**.
5. **تأكيد قبل مسح السطور عند التغيير**.

---

## 6. الـ Database

### Migration جديدة

1. **`PurchaseRequestLine.OrderedQuantity`** (`decimal(18,4)`, default 0).
2. **`PurchaseOrderLine.PurchaseRequestLineId`** (`bigint NULL`, FK).
3. **Indexes**:
   - `IX_PurchaseRequestLine_PurchaseRequestId_OrderedQuantity`.
   - `IX_PurchaseOrderLine_PurchaseRequestLineId`.

### تعبئة البيانات القديمة

1. `OrderedQuantity = 0` (لكل السطور القديمة).
2. `PurchaseRequestLineId = null` (لكل السطور القديمة).
3. **البيانات الحالية مش هتتأثر** — أوامر الشراء القديمة بتفضل زي ما هي.

---

## 7. Integration Tests (10+)

| # | الاختبار |
|---|---|
| 1 | اختيار طلب شراء → تحميل السطور المتبقية |
| 2 | كمية > المتبقي → رفض بـ `PUR-ORDER-QTY-EXCEEDS-REQUEST` |
| 3 | حفظ أمر شراء → `OrderedQuantity` بيتحدّث |
| 4 | أمر شراء كامل → `PurchaseRequest.Status = FullyConverted` |
| 5 | أمر شراء جزئي → `PurchaseRequest.Status = PartiallyConverted` |
| 6 | إلغاء أمر شراء → `OrderedQuantity` بيرجع |
| 7 | تعديل أمر شراء → عكس القديم + تطبيق الجديد |
| 8 | طلب شراء مكتمل → مايظهرش في القائمة |
| 9 | طلب شراء ملغي → مايظهرش في القائمة |
| 10 | أمر شراء بدون طلب شراء → يشتغل عادي |

---

## 8. القيود

1. **Backward Compatibility**: أوامر الشراء القديمة اللي مش مربوطة بطلب شراء → تفضل شغالة.
2. **Transaction**: تحديث `OrderedQuantity` + حفظ أمر الشراء → **نفس Transaction**.
3. **Concurrency**: قفل على `PurchaseRequestLine`.
4. **Validation**: قبل مسح السطور القديمة في التعديل.

---

## 9. علاقة بـ Remarks6

- **نفس النمط** — لكن على مستوى **أمر شراء ← طلب شراء**.
- **نفس الـ Architecture**:
  - `XxxQuantity` على السطر.
  - `XxxLineId` على السطر الجديد.
  - Queries جديدة.
  - Validators جديدة.
  - نفس Logic التحديث.

---

## 10. الأسئلة

| # | السؤال | الاقتراح |
|---|---|---|
| 1 | **موافق على النمط** (`OrderedQuantity` + `PurchaseRequestLineId`)؟ | ✅ |
| 2 | **موافق على الحالات الجديدة** (`PartiallyConverted` / `FullyConverted`)؟ | ✅ |
| 3 | **إيه سياسة السعر؟** | من `SupplierPriceHistory` / آخر سعر / `StandardCost` |
| 4 | **إيه سياسة السطور اليدوية؟** | نفس إعداد `AllowManualInvoiceLines` — بس للسطور اليدوية في أمر الشراء |

---

**ابدأ التنفيذ بعد ما تخلص `Remarks5` + `Remarks6` والاختبار اليدوي.**