# طلب: تنفيذ موديول الأصول الثابتة والصيانة (Fixed Assets & Maintenance)
## نسخة مُصحَّحة — بعد مراجعة معمارية

> **المرجع**: `08-Module-Maintenance-FixedAssets.md` (لو موجود) أو هذا المستند.
> **الهدف**: بناء دورة حياة الأصول الثابتة + الصيانة + إدارة الأعطال.
> **ملاحظة**: هذا المستند نسخة مُصحَّحة من الطلب الأصلي — 4 تصحيحات حرجة + توحيد `CustodyOfficer` مع الكيان الموجود بالفعل في المخازن + حسم 4 نقاط كانت مفتوحة ضمنيًا. **الفروق عن النسخة الأصلية مُعلَّمة صراحة بعبارة "تصحيح" أو "جديد" في كل مكان تغيّر فيه.**

---

## 1. السياق

### 1.1 الحالة الحالية

**النظام فيه 7 موديولات مكتملة + محرك الترحيل**:
- ✅ الحسابات العامة، المخازن والتصنيع، المشتريات، المبيعات، نقاط البيع، محرك الترحيل، الإعدادات والصلاحيات.

**مفيش موديول للأصول الثابتة أو الصيانة.**

### 1.2 الهدف
بناء موديول كامل لـ: **الأصول الثابتة** (من الاقتناء للاستبعاد) + **الصيانة** (وقائية وإصلاحية) + **إدارة الأعطال** (البلاغات اليومية).

### 1.3 التكامل
- **الحسابات**: قيود الاقتناء، الإهلاك، الاستبعاد، الصيانة — عبر `IPostingService`.
- **المخازن**: قطع الغيار (`Item`, `Item.AverageCost`)، **ومسؤول العهدة (`CustodyOfficer`) — كيان موجود ومبني بالفعل، يُعاد استخدامه هنا وليس إعادة تعريفه (تصحيح، قسم 2.1.5).**
- **المشتريات**: شراء الأصول.
- **محرك الترحيل**: كل قيد عبر `IPostingService`، حاملًا `CostCenterId` **(جديد — قسم 3.4)** و`IdempotencyKey` **(جديد — قسم 3.5)**.

---

## 2. المرحلة 1 — النطاق المطلوب

### 2.1 الأصول الثابتة

#### 2.1.1 `FixedAssetCategory` (فئة الأصل)

**الحقول المطلوبة**:
- الكود، الاسم (عربي/إنجليزي) — فريد على مستوى الشركة.
- طريقة الإهلاك الافتراضية (`StraightLine` / `DecliningBalance` / `NoDepreciation`).
- **`DefaultDepreciationRate` (`decimal?`) — تصحيح (جديد)**: النسبة الافتراضية لطريقة `DecliningBalance` — **إلزامية لو `DepreciationMethod = DecliningBalance`**، وإلا الطريقة دي مش قابلة للحساب رياضيًا (كانت ناقصة في الطلب الأصلي بالكامل).
- العمر الإنتاجي الافتراضي (بالسنوات) — اختياري.
- نسبة القيمة المتبقية الافتراضية — اختيارية.
- **الحسابات**: حساب الأصل (إلزامي)، حساب مجمع الإهلاك (إلزامي)، حساب مصروف الإهلاك (إلزامي)، حساب أرباح الاستبعاد (اختياري)، حساب خسائر الاستبعاد (اختياري)، **حساب مصروف الصيانة (إلزامي — جديد، مطلوب للقالب في قسم 3.2)**.
- الحالة (فعال / موقوف).

#### 2.1.2 `FixedAsset` (الأصل الثابت)

**الحقول المطلوبة**:
- الكود، الاسم (عربي/إنجليزي).
- الفرع (عمود مباشر، اختياري).
- الفئة (`FixedAssetCategoryId`).
- الرقم التسلسلي (اختياري)، الباركود (اختياري).
- **الاقتناء**:
  - تاريخ الاقتناء.
  - تكلفة الاقتناء (`AcquisitionCost`).
  - **`CurrencyCode` / `ExchangeRate` / `BaseCurrencyAmount` — تصحيح (جديد، قسم 3.6)**: نفس أي معاملة مالية في النظام (`00-Project-Overview.md` قسم 10) — كانت ناقصة تمامًا، وده بالظبط نوع الفجوة اللي ظهرت فعليًا في فاتورة المبيعات وسبّبت باج حقيقي في QA.
  - المورد (اختياري)، فاتورة الشراء (اختياري).
- **الإهلاك**:
  - العمر الإنتاجي (بالسنوات)، القيمة المتبقية، طريقة الإهلاك، `DepreciationRate` (تصحيح — إلزامي لو `DecliningBalance`).
  - تاريخ بدء الإهلاك.
  - **`FirstMonthProrated` (`bool`) — جديد (قسم 3.7)**: هل إهلاك الشهر الأول يُحسب بالتناسب مع الأيام المتبقية من الشهر، ولا شهر كامل بغض النظر عن تاريخ الاقتناء.
  - الإهلاك المتراكم (محسوب)، القيمة الدفترية الصافية (محسوبة).
- **الحالة**: `Active` / `InMaintenance` / `Transferred` / `Disposed` / `WrittenOff`، تاريخ الاستبعاد، سبب الاستبعاد، المبلغ المحصَّل، قيد الاستبعاد.
- **المسؤول**: مسؤول العهدة (`long?` بدون FK — مؤجل لـ HR)، اسم مسؤول العهدة (Snapshot).
- **`CostCenterId` (`long?`) — تصحيح (جديد، قسم 3.4)**: يُستخدم كمصدر بُعد تحليلي افتراضي لكل قيود هذا الأصل (إهلاك، صيانة) ما لم يُحدَّد غير ذلك على مستوى القالب.
- ملاحظات.

#### 2.1.3 `DepreciationSchedule` (جدول الإهلاك)

**الحقول المطلوبة**:
- `FixedAssetId`، رقم الفترة، تاريخ بداية ونهاية الفترة.
- قيمة الإهلاك، الإهلاك المتراكم بعد الفترة، القيمة الدفترية بعد الفترة.
- **`Status` (`enum`) — جديد (قسم 3.8)**: `Scheduled` / `Posted` / `Cancelled` — **الفترات المستقبلية تتحول لـ`Cancelled` (وليس تُحذف أو تُعدَّل) عند استبعاد الأصل قبل نهاية عمره الإنتاجي** — قرار كان مفتوحًا في الطلب الأصلي.
- **الترحيل**: هل تم الترحيل، تاريخ الترحيل، قيد الإهلاك (`JournalEntryId`).

#### 2.1.4 `DepreciationRun` (تشغيل الإهلاك الشهري)

**الحقول المطلوبة**:
- `CompanyId`، رقم التشغيل (فريد)، تاريخ التشغيل، الشهر والسنة.
- **`IdempotencyKey` (`Guid`) — تصحيح حرج (جديد، قسم 3.5)**: يتولّد من تركيبة ثابتة (`CompanyId` + `Year` + `Month`) — **إلزامي**. بدونه، إعادة تشغيل الـJob الشهري بالخطأ (فشل جزئي، إعادة تشغيل يدوية) هتضاعف قيد الإهلاك بالكامل. الـJob (قسم 6.1 بند 11) **لازم** يتحقق من عدم وجود `DepreciationRun` بنفس المفتاح قبل التنفيذ.
- **الحالة**: `Draft` / `Posted` / `Reversed`.
- **الإجماليات**: إجمالي الإهلاك، عدد الأصول.
- **الترحيل**: قيد الإهلاك، تاريخ الترحيل، المستخدم اللي رحّل، تاريخ العكس، المستخدم اللي عكس، قيد العكس.

#### 2.1.5 `AssetTransfer` (نقل أصل)

**الحقول المطلوبة**:
- `CompanyId`، `AssetId`، الفرع المصدر (`FromBranchId`)، الفرع الوجهة (`ToBranchId`)، تاريخ النقل، السبب (اختياري).
- **`CustodyOfficerId` (`long`, FK حقيقي) — تصحيح معماري حرج**: **مرجع فعلي لكيان `CustodyOfficer` الموجود بالفعل في `02-Module-Inventory-Manufacturing.md`** (مبني ومختبَر حسب تقرير حالة الموديولات) — **وليس** حقلًا جديدًا `long?` بدون FK زي الطلب الأصلي كان مقترح. `CustodyOfficer` مُصمَّم أصلًا بالظبط لنفس الفكرة: موظف مسؤول عن أصل مادي أثناء النقل — إعادة تعريفه هنا تكرار غير ضروري، وهيفوّت أي تحسين مستقبلي على الكيان الأصلي (زي ربطه بموديول الموارد البشرية لما يتبني).
- **`CustodyOfficerNameSnapshot`** (نص) — يفضل موجود كـSnapshot تاريخي وقت النقل، حتى لو بيانات `CustodyOfficer` اتغيّرت لاحقًا.
- **الحالة**: `Draft` / `Posted` / `Rejected` / `Cancelled` — **تصحيح: `Rejected` مُضافة (كانت ناقصة)** — الكيان عليه `ApprovalInstanceId` فلازم يحمل حالة رفض صريحة (`00-Project-Overview.md` قسم 12.3).
- قيد النقل (اختياري)، `ApprovalInstanceId` (اختياري).

#### 2.1.6 `AssetDisposal` (استبعاد أصل)

**الحقول المطلوبة**:
- `CompanyId`، `AssetId`، تاريخ الاستبعاد.
- **نوع الاستبعاد**: `Sale` / `Scrap` / `Loss`.
- المبلغ المحصَّل (اختياري)، اسم المشتري (اختياري)، القيمة الدفترية وقت الاستبعاد، الربح أو الخسارة (محسوب).
- **الحالة**: `Draft` / `Posted` / `Rejected` / `Cancelled` — **تصحيح: `Rejected` مُضافة** (نفس سبب `AssetTransfer` أعلاه).
- قيد الاستبعاد، `ApprovalInstanceId` (اختياري)، ملاحظات.
- **عند الترحيل**: كل `DepreciationSchedule.Status = Scheduled` المستقبلية لهذا الأصل تتحول لـ`Cancelled` تلقائيًا (قسم 2.1.3).

#### 2.1.7 `AssetPhysicalCount` (جرد الأصول الفعلي)
نفس الطلب الأصلي بدون تعديل: `CompanyId`، الفرع، رقم الجرد (فريد)، تاريخ الجرد، الحالة (`Draft` / `InProgress` / `Completed` / `Rejected`)، ملاحظات.

#### 2.1.8 `AssetPhysicalCountLine` (سطر الجرد)
نفس الطلب الأصلي بدون تعديل: `AssetPhysicalCountId`، `AssetId`، الموقع المتوقع/الفعلي، هل تم العثور عليه، الحالة (`Good`/`Fair`/`Damaged`)، ملاحظات.

---

### 2.2 الصيانة وإدارة الأعطال

#### 2.2.1 `MaintenanceCategory` (فئة الصيانة)
نفس الطلب الأصلي: `CompanyId`، الكود (فريد)، الاسم، النوع (`Preventive`/`Corrective`/`Inspection`)، الحالة.

#### 2.2.2 `MaintenanceIssue` (بلاغ عطل)
نفس الطلب الأصلي: `CompanyId`، الفرع، رقم البلاغ (فريد)، `AssetId` (اختياري)، اسم الجهاز (لو مش أصل مسجَّل)، تاريخ الإبلاغ، `ReportedByUserId`، وصف العطل، درجة الخطورة (`Low`/`Medium`/`High`/`Critical`)، الحالة (`Reported`/`UnderInspection`/`Repairing`/`Repaired`/`Rejected`/`Cancelled`)، ملاحظات، مرفقات.

#### 2.2.3 `MaintenanceRequest` (طلب صيانة)

**الحقول المطلوبة**:
- `CompanyId`، رقم الطلب (فريد)، `IssueId` (اختياري)، `AssetId`، `MaintenanceCategoryId`.
- تاريخ الطلب، التاريخ المُجدوَل (اختياري)، تاريخ الإنجاز الفعلي (اختياري).
- **الفني المسؤول** (`TechnicianId` — `long?` بدون FK، مؤجل لـ HR — **قرار مقصود منفصل عن `CustodyOfficer`**؛ الفني بيؤدي شغل، مش بيحمل عهدة نقل بضاعة، فمفيش داعي لإجباره على نفس الكيان — راجع قسم 7 نقطة 7 لتفاصيل القرار).
- **اسم الفني** (Snapshot).
- المورد الخارجي (اختياري).
- **الحالة**: `Draft` / `Approved` / `InProgress` / `Completed` / `Rejected` / `Cancelled` (الحالة دي كانت صحيحة أصلًا، `Rejected` موجودة بالفعل).
- **التكاليف**:
  - `EstimatedCost` (اختياري)، `ActualCost` (اختياري، **محسوبة = `LaborCost` + إجمالي تكلفة قطع الغيار، وليست حقلًا يُدخَل يدويًا — تصحيح، قسم 3.1**).
  - `LaborCost` (اختياري) — التكلفة الخارجية (عمالة/مورد خارجي).
  - `SparePartsTotalCost` (محسوبة من `MaintenanceSparePart`، تصحيح — كانت اسمها "تكلفة قطع الغيار" فقط بدون توضيح المصدر).
- ملاحظات، `ApprovalInstanceId` (اختياري)، قيد المصروف (`JournalEntryId`).
- **`IdempotencyKey` (`Guid?`) — تصحيح (جديد، قسم 3.5)**: إلزامية **فقط** للطلبات المُنشأة تلقائيًا من `MaintenanceSchedule` (قاعدة 16) — تتولّد من تركيبة (`MaintenanceScheduleId` + `DueDate`) لمنع إنشاء طلب مكرر لو الـJob اشتغل مرتين لنفس الاستحقاق.

#### 2.2.4 `MaintenanceSparePart` (قطعة غيار مستخدمة)

**الحقول المطلوبة**:
- `MaintenanceRequestId`، `ItemId` (اختياري — لو من المخزون)، الوصف، الكمية.
- **`UnitCost` — تصحيح**: لو `ItemId` محدد، القيمة **تُقرأ من `Item.AverageCost` في `WarehouseId` المصروف منه لحظة الصرف** (Snapshot، مش إدخال يدوي) — نفس مبدأ `SalesInvoiceLine.UnitCost` في `04-Module-Sales.md` (قاعدة 42): **لا تكلفة تُدخَل يدويًا أو تُعاد حسابها خارج مصدرها الوحيد في موديول المخازن**. لو `ItemId` غير محدد (قطعة غير مخزنية، اشتُريت خصيصى)، `UnitCost` تُدخَل يدويًا.
- التكلفة الإجمالية (محسوبة = `Quantity × UnitCost`).
- `WarehouseId` (اختياري، إلزامي لو `ItemId` محدد)، `StockTransactionId` (اختياري — حركة المخزون الناتجة).

#### 2.2.5 `MaintenanceSchedule` (صيانة وقائية مجدولة)
نفس الطلب الأصلي: `CompanyId`، `AssetId`، `MaintenanceCategoryId`، التكرار (`Daily`.../`Annual`)، تاريخ آخر تنفيذ، التاريخ المتوقع القادم (`NextDueDate`)، الفني المسؤول (`long?`)، اسم الفني (Snapshot)، الحالة، ملاحظات.

---

### 2.3 الإعدادات

#### 2.3.1 `AssetSettings` (إعدادات الأصول)

**الحقول المطلوبة**:
- `CompanyId`، تفعيل الإهلاك التلقائي، يوم الشهر لتشغيل الإهلاك (افتراضي 28).
- استبعاد الأصل يحتاج موافقة، نقل الأصل يحتاج موافقة.
- حد تكلفة الصيانة اللي يحتاج موافقة (اختياري).
- تكرار جرد الأصول (اختياري): `Annual`/`SemiAnnual`/`Quarterly`.
- **`DefaultFirstMonthProrated` (`bool`) — جديد (قسم 3.7)**: القيمة الافتراضية لـ`FixedAsset.FirstMonthProrated` عند إنشاء أصل جديد — إعداد على مستوى الشركة بدل ما يتحدد عشوائيًا لكل أصل.

---

### 2.4 Enums

**المطلوبة (مع التصحيحات مُعلَّمة)**:
- `DepreciationMethod`: `StraightLine` / `DecliningBalance` / `NoDepreciation`.
- `FixedAssetStatus`: `Active` / `InMaintenance` / `Transferred` / `Disposed` / `WrittenOff`.
- `DisposalType`: `Sale` / `Scrap` / `Loss`.
- **`AssetTransferStatus`: `Draft` / `Posted` / `Rejected` / `Cancelled`** — تصحيح: أُضيفت `Rejected`.
- **`AssetDisposalStatus`: `Draft` / `Posted` / `Rejected` / `Cancelled`** — تصحيح: أُضيفت `Rejected`.
- `AssetPhysicalCountStatus`: `Draft` / `InProgress` / `Completed` / `Rejected`.
- `AssetCondition`: `Good` / `Fair` / `Damaged`.
- `AssetPhysicalCountFrequency`: `Annual` / `SemiAnnual` / `Quarterly`.
- **`DepreciationScheduleStatus`: `Scheduled` / `Posted` / `Cancelled`** — جديد بالكامل (قسم 2.1.3).
- `MaintenanceType`: `Preventive` / `Corrective` / `Inspection`.
- `IssueSeverity`: `Low` / `Medium` / `High` / `Critical`.
- `MaintenanceIssueStatus`: `Reported` / `UnderInspection` / `Repairing` / `Repaired` / `Rejected` / `Cancelled`.
- `MaintenanceRequestStatus`: `Draft` / `Approved` / `InProgress` / `Completed` / `Rejected` / `Cancelled`.
- `MaintenanceFrequency`: `Daily` / `Weekly` / `Monthly` / `Quarterly` / `SemiAnnual` / `Annual`.

---

## 3. قواعد العمل

### 3.1 قواعد الأصول
1. `FixedAsset.AssetNumber` فريد على مستوى الشركة.
2. `AcquisitionCost` > 0.
3. `UsefulLifeYears` > 0 (إلا لو `DepreciationMethod = NoDepreciation`).
4. **`DepreciationRate` إلزامية وأكبر من صفر لو `DepreciationMethod = DecliningBalance`** — تصحيح، كانت غير قابلة للتنفيذ بدونها.
5. `SalvageValue < AcquisitionCost`.
6. `NetBookValue = AcquisitionCost − AccumulatedDepreciation` — محسوب.
7. `AccumulatedDepreciation` لا يتجاوز `AcquisitionCost − SalvageValue`.
8. الأصل لا يُحذف بعد الاعتماد — فقط `Status` يتغير.
9. `AcquisitionCost` ثابت بعد الاعتماد — إعادة التقييم مؤجلة.
10. **قيد الاقتناء** عبر `IPostingService`: `Dr Fixed Asset` (التكلفة بعملة الشركة الأساسية المعادلة، قسم 3.6) / `Cr Supplier/Cash/Bank`.
11. **قيد الإهلاك الشهري** عبر `IPostingService`: `Dr Depreciation Expense` / `Cr Accumulated Depreciation`.
12. **قيد الاستبعاد** عبر `IPostingService`: `Dr Accumulated Depreciation` + `Dr Cash/Bank` (Proceeds) + `Dr Loss on Disposal` (لو خسارة) / `Cr Fixed Asset` (التكلفة الأصلية) + `Cr Gain on Disposal` (لو ربح).

### 3.2 قواعد الصيانة
13. `MaintenanceIssue` الحالة الافتراضية `Reported`.
14. `MaintenanceRequest` يُنشَأ من `MaintenanceIssue` — مع نسخ البيانات.
15. `MaintenanceRequest` لا يُرحَّل بدون موافقة لو التكلفة تجاوزت `AssetSettings.RequireApprovalForMaintenanceCost`.
16. قطع الغيار من المخزون تُنشئ `StockTransaction`، بتكلفة `Item.AverageCost` (قسم 2.2.4).
17. `MaintenanceSchedule` تُنشئ `MaintenanceRequest` تلقائيًا عند وصول `NextDueDate`، **حاملًا `IdempotencyKey` مشتق من (`MaintenanceScheduleId` + `DueDate`) — تصحيح، قسم 2.2.3**.
18. `MaintenanceRequest` المكتملة تُحدِّث `NextDueDate`.
19. رفض أي `MaintenanceRequest` ينعكس في `Status = Rejected`.
20. **قيد الصيانة — تصحيح جذري (كان الأخطر في الطلب الأصلي)**: القيد عبر `IPostingService` **بسطرين مستقلين حسب مصدر التكلفة، مش سطر واحد عام**:
    - لو فيه تكلفة خارجية (`LaborCost` أو مورد خارجي > 0): `Dr Maintenance Expense` / `Cr Cash`/`Bank`/`Supplier` (بقيمة `LaborCost`).
    - لو فيه قطع غيار من المخزون (`SparePartsTotalCost` > 0): `Dr Maintenance Expense` / `Cr Inventory Account` (بقيمة `SparePartsTotalCost`، المحسوبة من `Item.AverageCost` وقت الصرف — قسم 2.2.4).
    - **الطلب الواحد ممكن يحتوي على السطرين معًا** لو الصيانة فيها تكلفة خارجية وقطع غيار مخزنية في نفس الوقت. **الطلب الأصلي كان بيقترح `Cr Cash/Bank/Supplier` كخيار واحد عام حتى لو المصدر مخزون — ده كان هيسجّل مستحقات أو نقص كاش وهمي مقابل قطع غيار الشركة أصلًا مالكاها.**

### 3.3 قواعد الإهلاك
21. `DepreciationRun` شهري.
22. **قيد الإهلاك مجمّع واحد لكل `DepreciationRun`، ولا يُرحَّل `DepreciationRun` بدون `IdempotencyKey` صحيح وفريد (قسم 2.1.4) — تصحيح حرج.**
23. `DepreciationRun.Status = Posted` بعد الترحيل — لا يُعدَّل.
24. عكس `DepreciationRun` ينشئ قيد عكسي.
25. `AccumulatedDepreciation` يُحدَّث فقط من `DepreciationRun` المعتمد.
26. **إهلاك الشهر الأول يُحسب بالتناسب مع الأيام المتبقية لو `FixedAsset.FirstMonthProrated = true`، وإلا شهر كامل بغض النظر عن تاريخ الاقتناء داخل الشهر — جديد، يحل قرارًا كان مفتوحًا (قسم 2.1.2).**

### 3.4 قواعد مركز التكلفة — جديد بالكامل
27. **كل قيد ناتج من هذا الموديول (اقتناء، إهلاك، استبعاد، صيانة) يحمل `CostCenterId` إلزاميًا** — مصدره `FixedAsset.CostCenterId` لو محدد، وإلا الفرع المرتبط بالأصل (نفس نمط باقي الموديولات، `01-Module-Accounting.md` قسم 2.1) — **لا يوجد قيد في هذا الموديول بدون تصنيف تحليلي**، اتساقًا الكامل مع بقية النظام.

### 3.5 قواعد الاتساق عند التكرار — جديد بالكامل
28. **`DepreciationRun.IdempotencyKey` إلزامية**، مشتقة من (`CompanyId` + `Year` + `Month`) — منع تكرار قيد الإهلاك الشهري بالكامل.
29. **`MaintenanceRequest.IdempotencyKey` إلزامية فقط للطلبات المُنشأة تلقائيًا من `MaintenanceSchedule`**، مشتقة من (`MaintenanceScheduleId` + `DueDate`).

### 3.6 قاعدة العملة — جديد بالكامل
30. **`FixedAsset.AcquisitionCost` يحمل `CurrencyCode`/`ExchangeRate`/`BaseCurrencyAmount` مثل أي معاملة مالية في النظام** (`00-Project-Overview.md` قسم 10) — قيد الاقتناء (قاعدة 10) يُرحَّل بالقيمة المعادلة بعملة الشركة الأساسية دايمًا.

### 3.7 قاعدة الإهلاك النسبي للشهر الأول — جديد بالكامل
31. راجع قاعدة 26 أعلاه — القيمة الافتراضية لكل أصل جديد تُقرأ من `AssetSettings.DefaultFirstMonthProrated`.

### 3.8 قاعدة جدول الإهلاك عند الاستبعاد المبكر — جديد بالكامل
32. **عند ترحيل `AssetDisposal`، كل صفوف `DepreciationSchedule` بحالة `Scheduled` (فترات مستقبلية لم يحن ترحيلها بعد) لنفس `FixedAssetId` تتحول تلقائيًا لـ`Cancelled`** — لا تُحذف (سجل تاريخي) ولا تُترَك `Scheduled` (قد يحاول Job الإهلاك الشهري ترحيلها بالخطأ لأصل مستبعَد فعليًا).
33. **النقل بين الفروع (`AssetTransfer`) لا يؤثر على `DepreciationSchedule` إطلاقًا** — الإهلاك يكمل بنفس الجدول، فقط `FixedAsset.BranchId` (وبالتبعية `CostCenterId` الافتراضي للقيود المستقبلية، قاعدة 27) يتحدّث.

---

## 4. دورة الحياة

### 4.1 الأصل الثابت
```
مُقتنى (Active) → إهلاك دوري → ...
              → نقل (Transferred → Active في فرع تاني، بدون تأثير على جدول الإهلاك — قاعدة 33)
              → صيانة (InMaintenance → Active)
              → استبعاد (Disposed، يُلغي فترات الإهلاك المستقبلية تلقائيًا — قاعدة 32)
              → إعدام (WrittenOff)
```

### 4.2 بلاغ العطل
```
مُبلَّغ (Reported) → قيد الفحص (UnderInspection) → قيد الإصلاح (Repairing)
                  → تم الإصلاح (Repaired)
                  ↘ مرفوض (Rejected)
                  ↘ ملغى (Cancelled)
```

### 4.3 طلب الصيانة
```
مسودة (Draft) → معتمد (Approved) → قيد التنفيذ (InProgress)
              → مكتمل (Completed، يُنشئ قيد الصيانة بسطريه المستقلين — قاعدة 20)
              ↘ مرفوض (Rejected)
              ↘ ملغى (Cancelled)
```

### 4.4 تشغيل الإهلاك
```
مسودة (Draft) → مُرحَّل (Posted، بـIdempotencyKey مُتحقَّق منه — قاعدة 28)
              ↘ معكوس (Reversed)
```

### 4.5 نقل واستبعاد الأصل — جديد
```
AssetTransfer:  مسودة → مرحّل (Posted) ↘ مرفوض (Rejected — نهائي) ↘ ملغى
AssetDisposal:  مسودة → مرحّل (Posted، يُلغي فترات الإهلاك المستقبلية) ↘ مرفوض (Rejected — نهائي) ↘ ملغى
```

---

## 5. الشاشات المطلوبة

نفس القائمة الأصلية بدون تعديل (13 شاشة) — التصحيحات كلها على مستوى الكيانات وقواعد العمل، لا تستدعي شاشات إضافية.

| # | الشاشة | النوع |
|---|---|---|
| 1 | فئات الأصول | List/Edit |
| 2 | الأصول الثابتة | List/Edit (Master/Details) |
| 3 | جدول الإهلاك | عرض تفاعلي |
| 4 | تشغيل الإهلاك | شاشة تفاعلية |
| 5 | نقل الأصول | List/Edit |
| 6 | استبعاد الأصول | List/Edit |
| 7 | جرد الأصول | شاشة تفاعلية |
| 8 | فئات الصيانة | List/Edit |
| 9 | بلاغات الأعطال | List/Edit |
| 10 | طلبات الصيانة | List/Edit (Master/Details) |
| 11 | الصيانة الوقائية المجدولة | List/Edit |
| 12 | متابعة حالة الإصلاح | Kanban تفاعلي |
| 13 | إعدادات الأصول | شاشة إعدادات |

---

## 6. المطلوب من Claude Code

### 6.1 خطوات التنفيذ
1. **اقرأ هذا المستند كاملًا** (النسخة المُصحَّحة) — وليس أي نسخة سابقة.
2. **أنشئ الكيانات (13 + 1 جديد)**:
   `FixedAssetCategory`, `FixedAsset`, `DepreciationSchedule`, `DepreciationRun`, `AssetTransfer`, `AssetDisposal`, `AssetPhysicalCount` + `AssetPhysicalCountLine`, `MaintenanceCategory`, `MaintenanceIssue`, `MaintenanceRequest`, `MaintenanceSparePart`, `MaintenanceSchedule`, `AssetSettings`.
3. **أنشئ Enums** (كل اللي في قسم 2.4، بما فيها `DepreciationScheduleStatus` الجديدة).
4. **`AssetTransfer.CustodyOfficerId` يجب أن يكون FK حقيقي على `CustodyOfficer` الموجودة في موديول المخازن — لا تُنشئ حقلًا جديدًا بدون FK لها.**
5. **أنشئ EF Configurations**، **Migration** واحدة شاملة، **طبّقها** على LocalDB + SQL Server.
6. **أنشئ Seed Data**: فئات الأصول والصيانة الافتراضية، `AssetSettings` افتراضية لكل شركة.
7. **أضف القوالب في محرك الترحيل** (5 قوالب — تصحيح: قالب الصيانة بسطرين منفصلين وليس قالبًا واحدًا عامًا):
   - `FixedAsset-Acquisition`
   - `DepreciationRun`
   - `AssetDisposal`
   - `MaintenanceRequest-ExternalCost` (تكلفة خارجية)
   - `MaintenanceRequest-SpareParts` (قطع غيار من المخزون)
8. **أنشئ Commands + Queries** (CQRS)، **Controllers**.
9. **أضف Job للإهلاك الشهري** — **يتحقق من `IdempotencyKey` قبل أي تشغيل فعلي** (قاعدة 28).
10. **أضف Job للصيانة الوقائية** — **يتحقق من `IdempotencyKey` قبل إنشاء `MaintenanceRequest`** (قاعدة 29).
11. **اكتب Integration Tests (18+)** — تشمل إلزاميًا: اختبار محاولة تشغيل `DepreciationRun` مرتين لنفس الشهر (يجب أن يفشل الثاني أو يرجّع نفس النتيجة)، واختبار قيد الصيانة بسطريه المنفصلين لطلب فيه تكلفة خارجية وقطع غيار معًا.
12. **اكتب تقرير** في `docs/Modules/FixedAssets-Maintenance-Phase1.md`.

### 6.2 القيود
1. اتبع نفس نمط الموديولات السابقة: `PublicId`, `RowVersion`, `IsDeleted`, CQRS + MediatR, FluentValidation, Configurations منفصلة.
2. **`TechnicianId`**: `long?` بدون FK، Snapshot للاسم (`TechnicianName`) — مؤجل لـ HR. **`CustodyOfficerId` على `AssetTransfer` مختلف: FK حقيقي (قيد 4 في قسم 6.1).**
3. القيود عبر `IPostingService` حصريًا — لا يوجد `new JournalEntry()` مباشر.
4. `BranchId` عمود مباشر.
5. لا تلمس أي كيان موجود — الإضافة فقط، **فيما عدا استهلاك `CustodyOfficer` الموجودة كـFK (قيد 4).**

### 6.3 المخرجات المطلوبة
1. 13 كيان + `DepreciationScheduleStatus` Enum جديد.
2. Enums كاملة (بالتصحيحات).
3. EF Configurations، Migration واحدة، Seed Data.
4. **5 قوالب في محرك الترحيل** (تصحيح: كانت 4، بقت 5 بفصل قالب الصيانة).
5. Commands + Queries + Controllers.
6. 2 Jobs (بـIdempotency Check في الاتنين).
7. Integration Tests (18+).
8. تقرير في `docs/Modules/FixedAssets-Maintenance-Phase1.md` — **يجب أن يذكر صراحة نتيجة اختبار تكرار `DepreciationRun`.**

---

## 7. اللي محتاجه منك

### تأكيدات (مُحدَّثة بعد المراجعة)

| # | السؤال | الاقتراح |
|---|---|---|
| 1 | موافق على الكيانات الـ13 + `DepreciationScheduleStatus`؟ | ✅ |
| 2 | موافق على `TechnicianId` بدون FK (مؤجل لـ HR)؟ | ✅ |
| 3 | موافق على Snapshot للأسماء؟ | ✅ |
| 4 | موافق على القيود عبر `IPostingService`، **بقالب صيانة منفصل بسطرين (خارجي/مخزون)**؟ | ✅ |
| 5 | موافق على Jobs (إهلاك + صيانة وقائية)، **بالتحقق من IdempotencyKey إلزاميًا**؟ | ✅ |
| 6 | موافق على الترتيب (الاقتناء → الإهلاك → النقل → الاستبعاد → الصيانة)؟ | ✅ |
| 7 | **جديد — موافق على `AssetTransfer.CustodyOfficerId` كـFK حقيقي على `CustodyOfficer` الموجودة، بدل حقل منفصل جديد؟** | ✅ (موصى به بقوة) |
| 8 | **جديد — موافق على إهلاك الشهر الأول نسبي بالأيام كافتراضي عام (`AssetSettings.DefaultFirstMonthProrated = true`)، قابل للتغيير لكل أصل؟** | ✅ (قابل للنقاش) |

---

**ابدأ التنفيذ على أساس هذا المستند المُصحَّح، وأرسل تقرير كامل عند الانتهاء — مع الإشارة صراحة لو أي تصحيح من التصحيحات السبعة أعلاه اتنفذ بشكل مختلف عن الموصوف هنا.**
