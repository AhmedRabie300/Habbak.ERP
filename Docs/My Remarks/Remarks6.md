# Remarks6.md — إضافة: تحسينات شاشة فاتورة المشتريات

> **المرجع**: إضافة على `Remarks5.md`.
> **الأولوية**: 🔴 عاجل (ضمن نفس الـ Batch).
> **العلاقة**: يعتمد على المرحلة 1 في `Remarks5.md` (Unit Conversion) — لازم تتحدّث الأول أو في نفس الوقت.
> **الهدف**: ربط ذكي بين فاتورة المشتريات وأوامر الشراء + تحميل تلقائي للأصناف.

---

## 1. السياق

شاشة **فاتورة المشتريات** حاليًا بتشتغل بشكل منفصل:
- المستخدم يختار المورد.
- المستخدم يختار أمر الشراء (لو حابب).
- المستخدم يدخل الأصناف يدوي.

**المشكلة**:
1. مفيش ربط ذكي بين المورد وأوامر الشراء المفتوحة.
2. حتى لو اختار أمر شراء، مفيش تحميل تلقائي للأصناف.
3. المستخدم مضطر يعيد إدخال نفس البيانات الموجودة في أمر الشراء.

**الهدف**:
- **تسريع إدخال الفاتورة**.
- **تقليل الأخطاء** (الكميات، الوحدات، الأسعار).
- **ضمان تطابق** الفاتورة مع أمر الشراء.

---

## 2. المطلوب — التفصيل

### 2.1 سلوك حقل "أمر الشراء" (`PurchaseOrderId`)

#### أ) عند اختيار المورد (`SupplierId`)

1. **يتم تحميل قائمة أوامر الشراء** المرتبطة بهذا المورد، بشرط:
   - الأمر **غير مكتمل الأصناف** — يعني فيه سطور لسه ما اتحمّلتش بالكامل في فواتير سابقة.
   - الأمر **ليس ملغي** (`Status != Cancelled`).
   - الأمر **ليس مغلق** (`Status != Closed`).
   - (اختياري) الأمر **معتمد** (`Status == Approved` أو `PartiallyReceived` أو `PartiallyInvoiced`) — حسب دورة العمل المعتمدة.

2. **تعريف "غير مكتمل الأصناف"**:
   - لكل سطر في أمر الشراء (`PurchaseOrderLine`)، عندنا:
     - `OrderedQuantity` (الكمية المطلوبة).
     - `InvoicedQuantity` (الكمية اللي اتحمّلت في فواتير سابقة).
   - السطر **مكتمل** لو `InvoicedQuantity >= OrderedQuantity`.
   - الأمر **غير مكتمل** لو فيه **سطر واحد على الأقل** `InvoicedQuantity < OrderedQuantity`.

3. **عرض القائمة**:
   - كل أمر يظهر بـ: رقم الأمر، التاريخ، إجمالي القيمة، عدد السطور المتبقية.
   - (اختياري) يظهر نسبة الإكمال.

#### ب) عند مسح المورد

- يتم تفريغ قائمة أوامر الشراء.
- يتم تفريغ سطور الفاتورة (بعد تأكيد المستخدم لو فيه سطور).

---

### 2.2 سلوك حقل "أمر الشراء" المختار (تحميل السطور)

**عند اختيار أمر شراء (`PurchaseOrderId`)**:

1. **يتم تحميل الأصناف المتبقية فقط** (اللي `InvoicedQuantity < OrderedQuantity`) في سطور الفاتورة.

2. **لكل سطر يتم تحميله**:
   - `ItemId` (الصنف).
   - `UnitId` (الوحدة — من أمر الشراء).
   - `UnitFactor` (معامل التحويل — من أمر الشراء أو من `ItemUnitConversion`).
   - `Quantity` = **الكمية المتبقية** = `OrderedQuantity - InvoicedQuantity` (بالوحدة المختارة).
   - `UnitCost` (سعر الوحدة — من أمر الشراء).
   - `BaseQuantity` = `Quantity × UnitFactor`.
   - `BaseUnitCost` = `UnitCost ÷ UnitFactor`.
   - `DiscountPercentage` / `DiscountAmount` (لو موجودة في أمر الشراء).
   - `TaxId` / `TaxPercentage` (لو موجودة).
   - `PurchaseOrderLineId` (للتتبع).

3. **الكمية قابلة للتعديل**:
   - المستخدم يقدر **يقلل** الكمية (استلام جزئي).
   - المستخدم **مايقدروش يزود** عن الكمية المتبقية (إلا لو فيه إعدادات تسمح).
   - لو زاد → تحذير + رفض الحفظ.

4. **الوحدة قابلة للتعديل** (لو الصنف له أكثر من وحدة):
   - لكن **افتراضيًا** تكون وحدة أمر الشراء.
   - عند تغيير الوحدة → إعادة حساب `BaseQuantity` و `BaseUnitCost` (مع الحفاظ على إجمالي الكمية بالوحدة الأساسية).

5. **السعر قابل للتعديل**:
   - افتراضيًا = سعر أمر الشراء.
   - لو اتغيّر → يظهر تحذير (اختياري) "السعر مختلف عن أمر الشراء".
   - (اختياري) صلاحية مخصصة للتعديل.

6. **منع التكرار**:
   - لو فيه سطر بنفس `PurchaseOrderLineId` موجود بالفعل في الفاتورة → **مايتضافش تاني**، أو يتم تحديثه (حسب السياسة).
   - **السياسة المقترحة**: **تحديث الكمية** بدل الإضافة.

7. **السلوك عند تغيير أمر الشراء**:
   - يتم **مسح السطور الحالية** (بعد تأكيد المستخدم).
   - يتم **تحميل سطور الأمر الجديد**.
   - (اختياري) لو فيه سطور يدوية → يتم الحفاظ عليها أو مسحها (حسب السياسة).
   - **السياسة المقترحة**: **مسح الكل + تحذير**.

---

### 2.3 سلوك الحفظ (`CreatePurchaseInvoiceCommand`)

**عند حفظ الفاتورة**:

1. **التحقق من كل سطر مرتبط بأمر شراء**:
   - `Quantity <= (OrderedQuantity - InvoicedQuantity)`.
   - لو مخالف → رفض بـ `PUR-INVOICE-QTY-EXCEEDS-ORDER`.

2. **تحديث `InvoicedQuantity`** في `PurchaseOrderLine`:
   - `InvoicedQuantity += Quantity` (لكل سطر).
   - ده بيتم **داخل نفس Transaction** بتاعة حفظ الفاتورة.

3. **تحديث حالة أمر الشراء**:
   - لو كل السطور `InvoicedQuantity >= OrderedQuantity` → `Status = FullyInvoiced`.
   - لو بعض السطور → `Status = PartiallyInvoiced`.
   - لو مفيش → `Status = Open` (أو `Approved`).

4. **الترحيل (Posting)**:
   - يستخدم `BaseQuantity` و `BaseUnitCost` (زي ما هو مطلوب في المرحلة 1).

---

### 2.4 سلوك التعديل (`UpdatePurchaseInvoiceCommand`)

**عند تعديل فاتورة موجودة**:

1. **قبل الحفظ**:
   - يتم **إرجاع** الكميات القديمة من `PurchaseOrderLine.InvoicedQuantity` (عكس العملية).
   - ثم **تطبيق** الكميات الجديدة.

2. **لو الفاتورة ملغية (`Cancelled`)**:
   - يتم إرجاع الكميات بالكامل.
   - `InvoicedQuantity -= Quantity` لكل سطر.

3. **لو الفاتورة معدّلة بعد الترحيل**:
   - سياسة خاصة (ممنوع التعديل بعد الترحيل إلا بإذن).

---

## 3. الكيانات المتأثرة

| # | الكيان | التغيير |
|---|---|---|
| 1 | `PurchaseOrderLine` | إضافة `InvoicedQuantity` (لو مش موجود) + تحديث `Status` |
| 2 | `PurchaseOrder` | تحديث `Status` (`Open` / `PartiallyInvoiced` / `FullyInvoiced`) |
| 3 | `PurchaseInvoiceLine` | إضافة `PurchaseOrderLineId` (لو مش موجود) |
| 4 | `PurchaseInvoice` | إضافة `PurchaseOrderId` (لو مش موجود) |

**ملاحظة**: لو `InvoicedQuantity` موجود بالفعل → تأكد إنه بيتحدّث صح.

---

## 4. الـ Application Layer

### 4.1 Queries جديدة

| # | Query | الوصف |
|---|---|---|
| 1 | `GetOpenPurchaseOrdersBySupplierQuery` | يرجع أوامر الشراء غير المكتملة لمورد معين |
| 2 | `GetPurchaseOrderLinesForInvoiceQuery` | يرجع سطور أمر الشراء مع الكميات المتبقية |

**`GetOpenPurchaseOrdersBySupplierQuery`**:
Input: SupplierId
Output: List<PurchaseOrderDto>

Id, Number, Date, TotalAmount

RemainingLinesCount

CompletionPercentage
Filter:

SupplierId == input.SupplierId

Status in (Approved, PartiallyReceived, PartiallyInvoiced, Open)

HasRemainingLines == true

text

**`GetPurchaseOrderLinesForInvoiceQuery`**:
Input: PurchaseOrderId
Output: List<PurchaseOrderLineForInvoiceDto>

PurchaseOrderLineId

ItemId, ItemName

UnitId, UnitName, UnitFactor

OrderedQuantity, InvoicedQuantity, RemainingQuantity

UnitCost

DiscountPercentage, DiscountAmount

TaxId, TaxPercentage

AvailableUnits (من ItemUnitConversion)
Filter:

InvoicedQuantity < OrderedQuantity

text

### 4.2 Commands محدّثة

- `CreatePurchaseInvoiceCommand` → يتحقق من الكميات + يحدّث `InvoicedQuantity` + يحدّث `PurchaseOrder.Status`.
- `UpdatePurchaseInvoiceCommand` → نفس المنطق مع عكس القديم.
- `CancelPurchaseInvoiceCommand` → يرجّع `InvoicedQuantity` + يحدّث `PurchaseOrder.Status`.

### 4.3 Validators جديدة

- `PurchaseInvoiceLineQuantityValidator`:
  - `Quantity > 0`.
  - `Quantity <= RemainingQuantity`.
  - رفض بـ `PUR-INVOICE-QTY-EXCEEDS-ORDER`.

- `PurchaseOrderStatusValidator`:
  - التأكد إن الأمر مش `Cancelled` ولا `Closed`.

---

## 5. الـ Frontend

### 5.1 شاشة `PurchaseInvoiceEditPage`

**التغييرات**:

1. **حقل المورد (`SupplierId`)**:
   - عند التغيير → استدعاء `GetOpenPurchaseOrdersBySupplierQuery`.
   - تحميل قائمة أوامر الشراء.

2. **حقل أمر الشراء (`PurchaseOrderId`)**:
   - `Dropdown` بيعرض الأوامر المفلترة.
   - كل خيار: `"PO-2026-001 — 15/09/2026 — 3,500 ج.م — 2 سطور متبقية"`.
   - (اختياري) زر "تحديث" لإعادة التحميل.

3. **عند اختيار أمر شراء**:
   - استدعاء `GetPurchaseOrderLinesForInvoiceQuery`.
   - تحميل السطور في الجدول.
   - تعبئة الحقول تلقائيًا.

4. **الجدول**:
   - عمود جديد: "أمر الشراء" (رقم السطر).
   - عمود "الكمية المتبقية" (للمرجع).
   - تمييز السطور اللي عليها تحذير (لو الكمية > المتبقي).

5. **زر "إضافة سطر يدوي"**:
   - يسمح بإضافة سطر مش مرتبط بأمر شراء (اختياري).
   - أو يتم منعه (حسب السياسة).

### 5.2 مكوّنات جديدة

- `PurchaseOrderSelect`: Dropdown بيعرض أوامر الشراء المفتوحة.
- `PurchaseOrderLineLoader`: منطق تحميل السطور.

---

## 6. الـ Database

### 6.1 Migration جديدة

1. **`PurchaseOrderLine.InvoicedQuantity`** (لو مش موجود):
   - `decimal(18,4) NOT NULL DEFAULT 0`.
   - تعبئة القيم الحالية من الفواتير الموجودة (لو ممكن).
   - أو `0` كبداية.

2. **`PurchaseInvoiceLine.PurchaseOrderLineId`** (لو مش موجود):
   - `int NULL` (FK to `PurchaseOrderLine`).
   - تعبئة القيم من الفواتير الموجودة (لو ممكن).

3. **`PurchaseInvoice.PurchaseOrderId`** (لو مش موجود):
   - `int NULL` (FK to `PurchaseOrder`).

4. **Indexes**:
   - `IX_PurchaseOrderLine_PurchaseOrderId_InvoicedQuantity`.
   - `IX_PurchaseInvoiceLine_PurchaseOrderLineId`.

---

## 7. Integration Tests المطلوبة (10+)

| # | الاختبار |
|---|---|
| 1 | اختيار مورد → قائمة أوامر الشراء المفتوحة بتتفلتر صح |
| 2 | اختيار أمر شراء → السطور بتتحمّل بالكميات المتبقية |
| 3 | إدخال كمية > المتبقي → رفض بـ `PUR-INVOICE-QTY-EXCEEDS-ORDER` |
| 4 | حفظ فاتورة → `InvoicedQuantity` بيتحدّث صح |
| 5 | حفظ فاتورة كاملة → `PurchaseOrder.Status = FullyInvoiced` |
| 6 | حفظ فاتورة جزئية → `PurchaseOrder.Status = PartiallyInvoiced` |
| 7 | إلغاء فاتورة → `InvoicedQuantity` بيرجع + الحالة بتتحدّث |
| 8 | تعديل فاتورة → عكس القديم + تطبيق الجديد |
| 9 | أمر شراء مكتمل → مايظهرش في القائمة |
| 10 | أمر شراء ملغي → مايظهرش في القائمة |

---

## 8. القيود

1. **الـ Backward Compatibility**: الفواتير القديمة اللي مش مرتبطة بأمر شراء → تفضل شغالة عادي.
2. **الـ Transaction**: تحديث `InvoicedQuantity` + حفظ الفاتورة → **نفس Transaction**.
3. **الـ Concurrency**: لو أكتر من مستخدم بيعمل فاتورة لنفس الأمر → **قفل** على `PurchaseOrderLine`.
4. **الـ Validation**: قبل مسح السطور القديمة في التعديل.
5. **الـ UI**: تجربة مستخدم سلسة — مفيش إعادة إدخال يدوي.

---

## 9. الترتيب مع المرحلة 1

**المرحلة 8 تعتمد على المرحلة 1** (Unit Conversion):
- لأن السطور المحمّلة لازم يكون فيها `UnitFactor` و `BaseQuantity` و `BaseUnitCost`.

**الترتيب المقترح**:
1. المرحلة 1 (Unit Conversion) — الأساس.
2. المرحلة 8 (Purchase Invoice Enhancements) — يعتمد على 1.
3. المرحلة 2 (TypeScript Errors) — مستقل.

**أو**:
- يتم دمج 1 و 8 في نفس الـ Sprint (لأنهم مترابطين).

---

## 10. المخرجات الإضافية

1. **Query جديدة**: `GetOpenPurchaseOrdersBySupplierQuery`.
2. **Query جديدة**: `GetPurchaseOrderLinesForInvoiceQuery`.
3. **Commands محدّثة**: `CreatePurchaseInvoiceCommand`, `UpdatePurchaseInvoiceCommand`, `CancelPurchaseInvoiceCommand`.
4. **Validators جديدة**: `PurchaseInvoiceLineQuantityValidator`.
5. **Migration جديدة**: `InvoicedQuantity`, `PurchaseOrderLineId`, `PurchaseOrderId`.
6. **Frontend محدّث**: `PurchaseInvoiceEditPage` + مكوّنات جديدة.
7. **Integration Tests** (10+).
8. **تقرير** في `docs/Modules/PurchaseInvoice-Enhancements.md`.

---

## 11. تأكيدات إضافية

| # | السؤال | الاقتراح |
|---|---|---|
| 1 | **موافق على فلترة أوامر الشراء بـ "غير مكتمل الأصناف"**؟ | ✅ |
| 2 | **موافق على تحميل السطور المتبقية فقط** (مش كل السطور)؟ | ✅ |
| 3 | **موافق على منع الكمية > المتبقي**؟ | ✅ |
| 4 | **موافق على تحديث `PurchaseOrder.Status` تلقائيًا**؟ | ✅ |
| 5 | **موافق على دمج المرحلة 8 مع المرحلة 1** في نفس الـ Sprint؟ | ✅ |
| 6 | **موافق على سياسة "مسح السطور عند تغيير أمر الشراء"**؟ | ✅ |
| 7 | **موافق على السماح بالسطور اليدوية** (غير مرتبطة بأمر شراء)؟ | ⚠️ يحتاج قرار |

---

**ابدأ التنفيذ بعد قراءة `Remarks5.md`، وأرسل تقرير كامل عند الانتهاء.**