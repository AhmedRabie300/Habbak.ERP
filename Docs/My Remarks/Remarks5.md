# طلب: إصلاح بند المشتريات (Unit Conversion) + TypeScript Errors

> **المرجع**: تقرير `QA-Batch-2026-09-19.md` (البند 8: "حاجة لقيتها وماصلّحتهاش").
> **الهدف**: إصلاح Bug حقيقي في المشتريات + تنظيف TypeScript Errors.
> **الأولوية**: 🔴 عاجل.

---

## 1. السياق

### 1.1 المشكلة (بند المشتريات)

**في تقرير QA السابق، تم اكتشاف**:
> "سطور المشتريات (طلب شراء، أمر، استلام، فاتورة، مرتجع) عليها `UnitId` من زمان، بس الترحيل بيضيف الكمية زي ما هي من غير تحويل. يعني استلام '2 شكارة' بيزوّد المخزن 2 جرام."

**التأثير**:
- ✅ **حاليًا**: الموجود على القاعدة كله بالوحدة الأساسية → مفيش ضرر.
- ⚠️ **قريبًا**: أول ما حد يستخدم وحدة شراء أكبر → **الكميات هتبقى غلط**.

**الـ Bug ده حقيقي** — لازم يتصلح قبل أي موديول جديد.

### 1.2 المشكلة (TypeScript Errors)

**في تقرير QA السابق**:
> "tsc -b نضيف، ماعدا الأخطاء القديمة في `PaymentPage` و `DeliveryOrdersListPage` و `POSReturnEditPage` و `PriceListEditPage`."

**الأخطاء دي قديمة** (مش من الشغل الحالي) — بس لازم تتحقق.

---

## 2. المطلوب — المرحلة 1: بند المشتريات

### 2.1 الكيانات المتأثرة (5 كيانات)

| # | الكيان | الشاشات |
|---|---|---|
| 1 | `PurchaseRequestLine` | طلب شراء (جديد + تعديل) |
| 2 | `PurchaseOrderLine` | أمر شراء |
| 3 | `GoodsReceiptLine` | استلام مشتريات |
| 4 | `PurchaseInvoiceLine` | فاتورة مشتريات |
| 5 | `PurchaseReturnLine` | مرتجع مشتريات |

### 2.2 الحل المطلوب

**نفس النمط اللي اتعمل في المخازن** (راجع تقرير QA السابق، قسم 4 "حقل الوحدة"):

#### أ) على مستوى الكيان

**كل كيان من الخمسة** يضاف له:

- `UnitId` (موجود بالفعل — تأكد).
- **`UnitFactor` (`decimal`)** — عدد الوحدات الأساسية في الوحدة المختارة، **يُنسَخ وقت الحفظ** (Snapshot).
- **`BaseQuantity`** (`decimal`، محسوب عند الحفظ) — `Quantity × UnitFactor`.
- **`BaseUnitCost`** (`decimal`، محسوب عند الحفظ) — `UnitCost ÷ UnitFactor`.

**السبب**: لو تحويلات الصنف اتغيّرت بعدين، المستندات القديمة معناها مايتغيّرش.

#### ب) على مستوى الـ Application

**الـ Commands**:
- `CreatePurchaseRequestLineCommand`
- `UpdatePurchaseRequestLineCommand`
- `CreatePurchaseOrderLineCommand`
- `UpdatePurchaseOrderLineCommand`
- `CreateGoodsReceiptLineCommand`
- `UpdateGoodsReceiptLineCommand`
- `CreatePurchaseInvoiceLineCommand`
- `UpdatePurchaseInvoiceLineCommand`
- `CreatePurchaseReturnLineCommand`
- `UpdatePurchaseReturnLineCommand`

**كل Command** يحسب `UnitFactor` و `BaseQuantity` و `BaseUnitCost` عند الحفظ.

**الـ Validators**:
- **الوحدة لازم تكون من وحدات الصنف** (`ItemUnitConversion`).
- لو الوحدة مش متاحة → **رفض الحفظ** بـ `PUR-UNIT-NOT-ALLOWED`.
- السطر من غير وحدة → **الوحدة الأساسية** (Backward Compatible).

#### ج) على مستوى الـ Posting

**الترحيل** (`PostGoodsReceiptCommand`, `PostPurchaseReturnCommand`, ...) لازم:
- يستخدم **`BaseQuantity`** (مش `Quantity`) عند تحديث `StockBalance`.
- يستخدم **`BaseUnitCost`** (مش `UnitCost`) عند حساب التكلفة.
- يمرر **`UnitFactor`** في الـ `StockTransaction` (لو الحقل موجود).
- يمرر **`BaseQuantity`** للـ `AverageCost` calculation.

**المعادلات**:
```
StockBalance.QuantityOnHand += BaseQuantity  (للاستلام)
StockBalance.QuantityOnHand -= BaseQuantity  (للمرتجع)

UnitCost للأصل = BaseUnitCost
```

#### د) على مستوى الـ Database

**Migration جديدة**:
1. إضافة `UnitFactor`, `BaseQuantity`, `BaseUnitCost` على الخمسة كيانات.
2. **تعبئة القيم الحالية**:
   - `UnitFactor = 1` (لأن كل البيانات الحالية بالوحدة الأساسية).
   - `BaseQuantity = Quantity` (نفس القيمة).
   - `BaseUnitCost = UnitCost` (نفس القيمة).

**ملاحظة**: القيم الحالية بالوحدة الأساسية فعلًا، فالمعامل = 1.

#### هـ) على مستوى الـ Frontend

**الـ 5 شاشات** (طلب شراء، أمر شراء، استلام، فاتورة، مرتجع):
- **مكوّن `UnitSelect`** (الموجود بالفعل) بدل الحقل النصي.
- **عند اختيار الصنف** → الوحدة تتظبط على الأساسية.
- **قائمة الأصناف** بترجع الوحدات والمعاملات (من `ItemUnitConversion`).
- **عرض الوحدة**: "شكارة (= 1000 جرام)".

### 2.3 القيود

1. **الـ Backward Compatibility**: السطر من غير وحدة = الأساسية.
2. **الـ Snapshot**: `UnitFactor` يُنسَخ وقت الحفظ، مش يُقرأ وقت الترحيل.
3. **الـ Validation**: قبل مسح السطور القديمة في التعديل.
4. **الـ Stock Movement**: لازم يستخدم `BaseQuantity` دايمًا.
5. **الـ Average Cost**: لازم يستخدم `BaseUnitCost` دايمًا.

### 2.4 الـ Integration Tests المطلوبة (8+)

| # | الاختبار |
|---|---|
| 1 | استلام مشتريات ببند "2 شكارة" بيزوّد المخزن 2000 جرام |
| 2 | فاتورة مشتريات ببند "2 شكارة" بتسجّل التكلفة صح |
| 3 | أمر شراء ببند "2 شكارة" — الكمية والحد بيحسبوا صح |
| 4 | طلب شراء ببند "2 شكارة" — الحد بالوحدة الأساسية |
| 5 | مرتجع مشتريات ببند "2 شكارة" بينقص 2000 جرام |
| 6 | وحدة مش تبع الصنف → رفض بـ `PUR-UNIT-NOT-ALLOWED` |
| 7 | السطر من غير وحدة → بيستخدم الأساسية |
| 8 | تعديل بوحدة مرفوضة → المستند يفضل زي ما هو |

---

## 3. المطلوب — المرحلة 2: TypeScript Errors

### 3.1 الأخطاء في 4 ملفات

| # | الملف | الأولوية |
|---|---|---|
| 1 | `PaymentPage` | 🔴 عاجل |
| 2 | `DeliveryOrdersListPage` | 🔴 عاجل |
| 3 | `POSReturnEditPage` | 🔴 عاجل |
| 4 | `PriceListEditPage` | 🔴 عاجل |

### 3.2 خطوات التحقق

#### أ) تشخيص الأخطاء

**لكل ملف**:
1. افتح الملف.
2. شغّل `tsc --noEmit` على الملف.
3. سجّل كل خطأ:
   - رقم السطر.
   - نوع الخطأ.
   - السبب.

#### ب) التصنيف

**كل خطأ يتصنف**:
- **خطأ حقيقي**: بيؤثر على التشغيل.
- **خطأ نوع (Type Error)**: TypeScript نوع مش دقيق.
- **خطأ قديم**: من قبل الشغل الحالي.

#### ج) الإصلاح

**لكل خطأ**:
- **خطأ حقيقي** → إصلاح فوري.
- **خطأ نوع** → إصلاح النوع (Type).
- **خطأ قديم** → تقييم + إصلاح.

#### د) التحقق النهائي

```
tsc -b   # يجب يكون نضيف تمامًا
```

### 3.3 القيود

1. **لا تكسر أي وظيفة موجودة**.
2. **اتبع نفس نمط المشروع**.
3. **متضيّعش الوقت** على أخطاء تجميلية.
4. **لو فيه خطأ كبير** → وثّقه واطلب قرار.

### 3.4 المخرجات المتوقعة

1. **`tsc -b` نضيف** — مفيش أخطاء.
2. **تقرير** في `docs/Modules/TypeScript-Errors-Report.md`:
   - كل خطأ.
   - السبب.
   - الإصلاح.
   - أي خطأ لم يُصلَح (مع تفسير).

---

## 4. خطوات التنفيذ الإجمالية

### المرحلة 1: بند المشتريات

1. **اقرأ تقرير QA السابق** (`QA-Batch-2026-09-19.md` — قسم 4 "حقل الوحدة").
2. **راجع الكيانات الخمسة** — تأكد من `UnitId`.
3. **أضف الحقول**:
   - `UnitFactor` (`decimal`).
   - `BaseQuantity` (`decimal`).
   - `BaseUnitCost` (`decimal`).
4. **حدّث الـ Commands** (10 commands) — حساب الحقول عند الحفظ.
5. **حدّث الـ Validators** (5 validators) — التحقق من الوحدة.
6. **حدّث الـ Posting** — استخدام `BaseQuantity` و `BaseUnitCost`.
7. **Migration جديدة** — إضافة الحقول + تعبئة القيم الحالية.
8. **تطبيق Migration** على LocalDB + SQL Server.
9. **حدّث الـ Frontend** — استخدام `UnitSelect` في 5 شاشات.
10. **اكتب Integration Tests** (8+).

### المرحلة 2: TypeScript Errors

11. **شخّص الأخطاء** في 4 ملفات.
12. **صنّفها**.
13. **أصلحها**.
14. **تحقق**: `tsc -b` نضيف.
15. **وثّق** في `docs/Modules/TypeScript-Errors-Report.md`.

### المرحلة 3: التقرير النهائي

16. **تقرير** في `docs/Modules/Fixes-Batch-2026-09-19.md`:
    - بند المشتريات (ما تم + اختبارات).
    - TypeScript Errors (ما تم + ملفات).
    - أي مشاكل واجهتها.
    - توصيات.

---

## 5. المطلوب من Claude Code

### 5.1 خطوات التنفيذ

1. **اقرأ تقرير QA السابق** (`QA-Batch-2026-09-19.md`).
2. **نفّذ المرحلة 1** (بند المشتريات) — بالتفاصيل اللي فوق.
3. **نفّذ المرحلة 2** (TypeScript Errors) — بالتفاصيل اللي فوق.
4. **اكتب التقرير النهائي** في `docs/Modules/Fixes-Batch-2026-09-19.md`.

### 5.2 القيود العامة

1. **اتبع نفس نمط الموديولات السابقة**.
2. **لا تكسر أي وظيفة موجودة**.
3. **الـ Backward Compatibility** — السطر من غير وحدة = الأساسية.
4. **الـ Snapshot** — `UnitFactor` يُنسَخ.
5. **الـ Stock Movement** — `BaseQuantity` دايمًا.
6. **الـ Average Cost** — `BaseUnitCost` دايمًا.

### 5.3 المخرجات

1. **5 كيانات محدّثة** (بـ `UnitFactor`, `BaseQuantity`, `BaseUnitCost`).
2. **10 Commands محدّثة**.
3. **5 Validators محدّثة**.
4. **Posting محدّث** — `BaseQuantity` و `BaseUnitCost`.
5. **Migration جديدة** — على LocalDB + SQL Server.
6. **Frontend محدّث** — `UnitSelect` في 5 شاشات.
7. **Integration Tests** (8+).
8. **`tsc -b` نضيف**.
9. **تقرير TypeScript Errors**.
10. **تقرير نهائي** في `docs/Modules/Fixes-Batch-2026-09-19.md`.

---

## 6. ملاحظات للتنفيذ

### 6.1 الأولوية

**بند المشتريات قبل TypeScript** — لأن:
- **بند المشتريات**: Bug حقيقي.
- **TypeScript**: تحسين جودة.

### 6.2 التحقق

**بعد كل مرحلة**:
- **ApiTests**: 160/160 ✅
- **IntegrationTests**: 75/75 ✅
- **`tsc -b`**: نضيف ✅

### 6.3 الـ Migration

**Strategy**:
1. إضافة الحقول `nullable`.
2. تعبئة القيم:
   - `UnitFactor = 1`.
   - `BaseQuantity = Quantity`.
   - `BaseUnitCost = UnitCost`.
3. تحويل إلى NOT NULL.
4. **اختبار على نسخة من القاعدة الحقيقية** قبل التطبيق.

---

## 7. اللي محتاجه منك

### تأكيدات

| # | السؤال | الاقتراح |
|---|---|---|
| 1 | **موافق على النمط** (`UnitFactor` + `BaseQuantity` + `BaseUnitCost`)؟ | ✅ |
| 2 | **موافق على الـ Backward Compatibility** (السطر من غير وحدة = الأساسية)؟ | ✅ |
| 3 | **موافق على استخدام `UnitSelect`** في 5 شاشات؟ | ✅ |
| 4 | **موافق على الـ Migration Strategy** (nullable → fill → NOT NULL)؟ | ✅ |
| 5 | **موافق على ترتيب التنفيذ** (بند المشتريات → TypeScript)؟ | ✅ |

---

**ابدأ التنفيذ، وأرسل تقرير كامل عند الانتهاء.**