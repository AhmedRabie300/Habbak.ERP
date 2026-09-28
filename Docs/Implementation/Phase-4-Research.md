# Phase 4 — الرواتب — Research Pass

> `Docs/Implementation/HR-MASTER-PLAN.md` § Phase 4 + `Docs/Modules/10-Module-HR-Payroll.md` §2.3
> + §2.4 + قواعد 28-40 + §4.3 + §6.2. فحص فعلي كامل للكود (Domain/Application/Infrastructure/API
> + كل مشاريع الاختبار) — لا يوجد كود جديد في هذه الخطوة.

---

## 1. حالة الكود الفعلية

### 1.1 موديول الرواتب — صفحة بيضاء تمامًا، لكن بأربع نقاط ربط جاهزة ومنتظرة

بحث شامل في المشروع كله عن `SalaryComponent`/`SalaryStructure`/`EmployeeSalary`/`PayrollPeriod`/
`PayrollRun`/`PayrollLine`/`Payslip`/`MinimumWage`/`SocialInsuranceRate`/`InsurableWageLimit`/
`PayrollTaxBracket*`/`MartyrsFundRate`/`OvertimeRate`/`LeaveEntitlementRule`/`PenaltyDeductionCap`/
`NoticePeriodRule` طلع **صفر نتائج للكل** — صفحة بيضاء بالكامل، زي ما اتوقّع. لكن أربع موديولات
سابقة سابت نقاط ربط جاهزة (Schema-ready) بانتظار Phase 4 تحديدًا:

- `SourceModule.Payroll = 6` (`src/Habbak.ERP.Shared/Enums/SourceModule.cs:14`) — موجود من بنية
  الترحيل العامة، مش مستخدَم لحد دلوقتي.
- `SourceDocumentType.Payroll = 5` (`src/Habbak.ERP.Domain/Accounting/Enums.cs`، 20 قيمة إجمالي)
  — موجود، مش مستخدَم. `EmployeeAdvance`/`EndOfService`/`TipsDistribution` **مش موجودين** لسه
  (هيتضافوا هنا أو Phase 5 حسب القرار في §2.5).
- `CompanyAccountRole.EmployeeReceivable = 6` و`TipsPayable = 16` (`Accounting/CompanyAccountMapping.cs:33-53`،
  18 قيمة إجمالي) — موجودين ومستخدَمين فعليًا (`CounterpartyAccountResolver.cs:48-59` للأول). الـ11
  دور جديد المطلوبين لـ Phase 4 (`SalariesExpense`, `SalariesPayable`, `SocialInsuranceExpense`,
  `SocialInsurancePayable`, `PayrollTaxPayable`, `MartyrsFundPayable`, `LeaveExpense`,
  `LeaveProvision`, `EOSExpense`, `EOSProvision`, `HeirsPayable`) **كلهم مش موجودين**. إضافة دور =
  قيمة Enum جديدة + سطر جديد في `CompanyAccountRoleExtensions.ExpectedAccountType`
  (`CompanyAccountMapping.cs:64+`، سويتش بيحدد نوع الحساب المتوقع لكل دور) — **مفيش Migration** (تعليق
  الكيان نفسه صريح: "صف لكل دور... إضافة دور = قيمة Enum مش Migration").
- `NotificationType.PayrollExceptionReview = 6` و`PayrollRunReadyForApproval = 9`
  (`src/Habbak.ERP.Domain/Notifications/Notification.cs:49,58`) — موجودين من Phase 2.5، معلّقين
  صراحة بتعليق `"Schema-ready for Phase 4 — not sent by anything yet"`. Phase 4 محتاج يطلقهم فعليًا
  (مراجعة الاستثناءات قبل الاعتماد، ودخول التشغيل `PendingApproval`).

### 1.2 التكامل المحاسبي — `IPostingService`/`PostingTemplate` جاهز، بس `CostCenterId` مش عمود مفرد

`IPostingService` (`src/Habbak.ERP.Application/Common/Interfaces/IPostingService.cs:15-39`):
`PostAsync`/`PostDraftEntryAsync`/`ReverseAsync` — ولا واحدة فيهم بتعمل `SaveChangesAsync`. المسار
الفعلي المستخدَم من كل الموديولات هو `IPostingTemplateEngine.PostIfConfiguredAsync(TemplatePostingRequest, ct)`.

`PostingTemplate.ScreenCode` لسه **نص خام** (`src/Habbak.ERP.Domain/Posting/PostingTemplate.cs:27`)،
مش FK لكيان `Screen` (الموجود ومستخدَم في `ApprovalWorkflowAssignment.ScreenId` بس، توحيدهم قرار
مؤجّل صراحة في تعليق `Screen.cs`). **قرار Phase 4**: يتبع نفس النمط الحالي حرفيًا — `ScreenCode`
نص (`"PAY_PAYROLL_RUNS"` إلخ)، مفيش توحيد.

**"`CostCenterId` إلزامي على كل سطر" (قاعدة 51) اتنفّذت فعليًا كـ Dimension، مش عمود واحد**:
`PostingGroupItem(long AccountId, decimal Amount, IReadOnlyDictionary<long,long>? Dimensions)`
(`PostingContext.cs:125`) و`TemplatePostingRequest.Dimensions` (dimension-id → value-id، مطبَّق
على كل سطر ما يحدّدوش هو Dimension بتاعته بنفسه). مثال فعلي من الإهلاك
(`DepreciationCommands.cs:185-214`): `IdempotencyKey = PostingKeys.For(companyId, "DepreciationRun.Post", run.Id)`،
والـ Cost Center بييجي من `FixedAssetRules.CostCenterAsync` لكل أصل. **نفس النمط بالظبط لـ Phase 4**:
مركز تكلفة كل موظف (`Employee.CostCenterDimensionValueId` لو موجود، وإلا فرعه) بيتحط في
`Dimensions` لكل `PostingGroupItem`/سطر `PayrollLine`، مش عمود `CostCenterId` منفصل على الكيان.

### 1.3 مفتاح الـ Idempotency — نمط `DepreciationRunKeys`/`PostingKeys` جاهز للتكرار

`DepreciationRunKeys.For(companyId, year, month)` (`src/Habbak.ERP.Application/FixedAssets/DepreciationCommands.cs:28-40`):
`Guid` من أول 16 بايت لـ `SHA256("{companyId}:DepreciationRun:{year:D4}-{month:D2}")`. فحص وجود
مسبق (`ExistingAsync`، فلتر `Status != Reversed`) + `catch (DbUpdateException)` عند تعارض فعلي
(`"FA-RUN-EXISTS"`). **قاعدة 30 محتاجة توسيع المفتاح** بـ `RunType` + `ReferenceId?` — نفس بنية
الـ Hash بس نص أطول (`"{companyId}:PayrollRun:{year:D4}-{month:D2}:{runType}:{referenceId}"`).

`PostingKeys.For(companyId, purpose, documentId)` (`PostingKeys.cs:13-29`) — نفس الإنشاء، ومستخدَم
فعليًا بصيغة `"{Entity}.{Action}"` (`"DepreciationRun.Post"`) — يتكرر حرفيًا لـ `"PayrollRun.Post"`
و`"PayrollRun.Pay"` (قاعدة 31، المفتاح التالت).

### 1.4 الجداول القانونية المؤرّخة — **مفيش سابقة حقيقية في الكود لنمط `EffectiveFrom`/`EffectiveTo` "الساري وقت X"**

بحث شامل عن `EffectiveFrom` في الـ Domain كله رجّع 4 نتائج، وكلهم إما تاريخ واحد بدون نهاية
(`Recipe.EffectiveFromDate`، `Discount.EffectiveFromDate`، `PriceList.EffectiveFromDate`) أو زوج
`EffectiveFrom`/`EffectiveTo` على `EmployeeWeeklyRestDays` (`Attendance/AttendanceEntities.cs:57-58`)
**بس من غير أي Query "الساري في تاريخ X" بيقرا منه** — الكيان ده Pattern افتراضي للمولّد الجماعي
بس (Remarks8 Item 6)، مش بيتقرأ بمنطق "AsOf". **يعني الجداول القانونية العشرة (§1.5) هي أول
استخدام حقيقي لنمط "صف ساري في فترة، وطلب `AsOf(date)` بيرجّع الصف الصحيح" في المشروع كله** —
هيتصمّم من الصفر، مفيش نمط جاهز يتقلّد.

**`RateSnapshot` (JSON على كل `PayrollLine`)**: السابقة الوحيدة الموجودة فعلًا هي
`PostingTemplate.TemplateSnapshotJson` (`Posting/PostingTemplate.cs:134-146`) — `string` عادي
(EF بيحوّله `nvarchar(max)` تلقائيًا لغياب أي `[Column(TypeName=...)]`)، من غير أي `HasConversion`
JSON Serializer جاهز في المشروع كله. Phase 4 هيحتاج يسريلايز يدوي (`System.Text.Json.Serialize`)
لعمود `string` بسيط، نفس الأسلوب بالظبط.

### 1.5 الكيانات المُستهلَكة من Phase 3 — جاهزة، وفيها Breadcrumb صريح واحد لـ Phase 4

- `Attendance` (`src/Habbak.ERP.Domain/Attendance/AttendanceEntities.cs:95-116`): `WorkedMinutes`،
  `LateMinutes`، `EarlyLeaveMinutes`، `OvertimeMinutes` (كلهم `int`)، `Status`
  (`AttendanceStatus`: `Present/Absent/Leave/Holiday/RestDay`)، `IsApproved`. تعليق الكيان نفسه
  صريح: "الوحيد اللي بيدخل الرواتب لاحقًا".
- `OvertimeRequest` (`Attendance/LeaveEntities.cs:115-133`): `ActualMinutes` (nullable، بيتملى من
  `RecomputeDailyAttendance`)، `OvertimeType` **موجود ومطابق بالظبط** لمفاتيح جدول `OvertimeRate`
  المطلوب (`Attendance/Enums.cs:25`: `Day=1, Night=2, RestDay=3, PublicHoliday=4`). تعليق الكيان
  (سطر 113-114) **بيأجّل صراحة خطوة اعتماد "الإضافي فوق الحد الشهري" لـ Phase 4** — لسه مفيش جدول
  الحد نفسه ولا خطوة الاعتماد الإضافية (قاعدة 20، تفصيل في §2.6).
- `LeaveType.AnnualDays` موجود كـ Fallback (`Attendance/LeaveEntities.cs:8-30`)، وتعليق الكيان نفسه
  بيحدد إن `LeaveEntitlementRule` (Phase 4) لو موجود بيتقدّم عليه — **مؤكَّد أن `LeaveEntitlementRule`
  مش موجود في الكود خالص** (Grep شامل، مفيش حتى تعليق كيان).
- `LeaveBalance`/`LeaveBalanceHistory`: زي المخطط بالظبط، `Available` محسوبة (`Ignore` في EF).
- `Employee`: كل الحقول المطلوبة موجودة (`BranchId`, `JobGradeId`, `ManagerId`, `UserId`,
  `CostCenterDimensionValueId`, `Status`).
- `TimeEntry.Source` فيه `Device=5` (Phase 3B) جنب الأربعة التانيين — مالوش أثر على Phase 4 (بيوصل
  لـ`Attendance` بنفس الآلية).

### 1.6 `EmploymentContract`/`EmploymentContractLine` (Phase 1.1/3C) — الحدود متروسمة صراحة في الكود نفسه

`EmploymentContract.BasicSalary`/`InsurableWage` (`HR/EmploymentContract.cs:28-29`) أعمدة مباشرة،
مش بنود. `EmploymentContractLine` (Phase 3C، `HR/EmploymentContractLine.cs:12-27`):
`NameAr/NameEn/Amount/Type(Earning|Deduction)/IsTaxable/IsInsurable/Order`. **تعليق الكيان نفسه
(سطر 5-8) بيحسم النقطة دي صراحة**: "allowance/deduction line on top of `EmploymentContract.BasicSalary`...
**not a payroll calculation engine (that's the future Phase 4 `SalaryComponent`)**". يعني القرار
المعماري اتاخد من Phase 3C نفسها: **العقد نص توثيقي (بند واحد ثابت لكل بند، بدون تكرار شهري ولا
معامل حساب)، ومحرك الرواتب الفعلي (`SalaryComponent`/`SalaryStructure`/`EmployeeSalary`) منفصل
تمامًا**. النقطة المفتوحة الوحيدة الباقية: **هل بنود العقد (بدل سكن، مثلًا) لازم تتحوّل تلقائيًا
لصف `EmployeeSalary` وقت التفعيل، ولا الاتنين إدخال يدوي منفصل بالكامل** (تفصيل في §2.3).

### 1.7 `HrSettings` — الستة حقول المؤجّلة موجودة فعلًا كأعمدة، واحد منهم شغّال فعليًا من غير شاشة

`src/Habbak.ERP.Domain/HR/HrSettings.cs:30-55` — كل الستة حقول موجودين كأعمدة (`MonthBasis`,
`DefaultCutoffDay`, `KioskSessionSeconds`, `SelfServiceClockInRequiresLocation`,
`CompanyDefaultApproverUserId`, `MaxAdvanceInstallmentPercent`)، بتعليقات صريحة جوه الكيان نفسه
بتحدد كل واحد لمرحلته ("مرحلة الرواتب" لأول اتنين، "مرحلة السلف" لآخر واحد). لكن `UpdateHrSettingsCommand`/
`HrSettingsDto` (`Application/HR/Settings/`) بيعرضوا 4 حقول بس (`DefaultProbationDays`,
`DefaultBranchId`, `RequireNationalIdForActivation`, `LeaveDayCountingMode`) — **ولا واحد من
الستة معروض**.

**اكتشاف مهم**: `CompanyDefaultApproverUserId` **مش مجرد عمود مؤجّل — محرك الاعتمادات (Phase 2)
بيقرأه فعليًا ومباشرة من الكيان** في `ApprovalStepResolutionService.ResolveDirectManagerUserIdAsync`
(`Application/Approvals/Services/ApprovalStepResolutionService.cs:79-104`): لما `ManagerId = null`
**أو المدير موجود بس من غير `UserId` مربوط**، بيرجع لـ`HrSettingsRows.CompanyDefaultApproverUserId`.
يعني الـ Fallback شغّال ومُختبَر (`ApprovalWorkflowEngineTests.cs:168`) من Phase 2 — **بس مفيش أي
شاشة لأي HR Admin يحطّ القيمة دي فيها**. ده فجوة استخدام حقيقية (مش مجرد حقل مؤجّل نظريًا) هتأثر
على أي سلسلة اعتماد HR بمدير `null` — بما فيها Phase 4 نفسها (تشغيل الرواتب: HR ثم مالية، صف 11).

### 1.8 محرك الاعتمادات (Phase 2) — جاهز بالكامل، نمط الاستدعاء مؤكَّد من Phase 3

الكيانات السبعة (`Screen`, `ApprovalWorkflow`, `ApprovalWorkflowStep`, `ApprovalStepApprover`,
`ApprovalWorkflowAssignment`, `ApprovalInstance`, `ApprovalAction`) موجودة
(`src/Habbak.ERP.Domain/Approvals/ApprovalWorkflow.cs` + `ApprovalInstance.cs`).
`ApprovalApproverType`: `SpecificEmployee=1, Role=2, DirectManager=3, JobGrade=4` — مطابق.

`IApprovalWorkflowService.TryStartApprovalAsync(ApprovalWorkflowTrigger)` — نمط الاستدعاء الفعلي من
Phase 3 (`LeaveRequestCommands.cs:103-106`, `OvertimeRequestCommands.cs:71-74`):
`EntityType = "HR_LEAVE_REQUESTS"` (نص `Screen.Code` مباشرة). `ApprovalTriggerScreenMap.cs:15-20`
فيه Mapping واحد بس (`"JournalEntry"`)، وأي كود تاني بيعدّي زي ما هو (`GetValueOrDefault(entityType, entityType)`)
— **تعليق الملف نفسه (سطر 9-11) بيستنى صراحة إن "advances/penalties/bonuses" هتعمل نفس الحاجة**.
يعني `PayrollRun` (وأي كيان تاني في Phase 4) هيمرّر `EntityType = "PAY_PAYROLL_RUNS"` مباشرة، صفر
تعديل على `ApprovalTriggerScreenMap`.

**حارس الاعتماد الذاتي**: `ApprovalActionGuards.ValidateActingUserAsync`
(`Application/Approvals/Commands/ApprovalActionCommands.cs:259-284`) — فحص غير مشروط
`actingUserId == instance.RequestedByUserId` → `APPROVAL-SELF-APPROVAL-FORBIDDEN`. شغّال ومُختبَر.

**التوجيه بحد مبلغ (`ApprovalWorkflowAssignment.MinAmount`)**: موجود ومُنفَّذ
(`ApprovalWorkflowService.cs:36`: تحت الحد = مفيش سلسلة خالص، مش سلسلة أقصر)، لكن **صفر شاشة بتستخدمه
فعليًا لحد دلوقتي** (Grep: صفر Consumer إنتاجي لـ`MinAmount != null`) — Phase 4/5 (سلفة، مكافأة) أول
مستهلك حقيقي. **قيد مهم**: فهرس فريد مفلتر بيمنع أكتر من `ApprovalWorkflowAssignment` نشط لكل
`Screen` (`AssignWorkflowToScreenCommand.cs:13-14`) — يعني "سلفة تحت 5000 خطوتين، وفوقها 3 خطوات"
(§4.8 صف 2) محتاجة **إعادة تصميم كـ`ApprovalWorkflow` واحدة بخطوة شرطية**، مش تسكينتين منفصلتين
لنفس الشاشة (تفصيل في §2.4 — ده أساسًا Phase 5 بس بيأثر على تصميم `ApprovalWorkflowAssignment`
اللي Phase 4 (تشغيل الرواتب، صف 11) أول من يستخدمه فعليًا).

### 1.9 `ICodeGenerator`/`CodingRule` — جاهز لترقيم `PayrollRun`/`Payslip`

`ICodeGenerator.ResolveCodeAsync(screenCode, manualCode, ct)` (نفس نمط `CreateEmployeeCommand.cs:52`
بـ`screenCode = "HR_EMPLOYEES"`) — يتكرر حرفيًا لـ`"PAY_PAYROLL_RUNS"` لو محتاج ترقيم تلقائي لتشغيل
الرواتب أو قسيمة.

### 1.10 `FieldPermissionCatalog` — جاهز لحقول الأجر، ومش شرط `[PiiField]`

`FieldPermissionCatalog.SensitiveFields` (`src/Habbak.ERP.Domain/Settings/Permissions/FieldPermission.cs:44-61`)
Dictionary ثابت `ScreenCode → EntityType → string[]`. **`[PiiField]` مش شرط للتسجيل** — الاختبار
الآلي (`PiiFieldRegistrationTests.cs:30-33`) بيتحقق اتجاه واحد بس (كل خاصية معلّمة `[PiiField]`
لازم تبقى مسجّلة)، والكتالوج نفسه فيه حقول حساسة (`CreditLimit`, `Phone`, `Email` على
`Customer`/`Supplier`) **من غير أي `[PiiField]`** — نفس النمط ينطبق مباشرة على حقول الأجر (قاعدة 6:
"أي حقل PII أو أجر"): `EmployeeSalary.Amount`، `Payslip.Net/Gross`، إلخ تتسجّل في الكتالوج تحت
`Screen.Code` مناسب (`PAY_PAYROLL_RUNS` أو `HR_SALARY_CHANGES`) من غير حاجة للـ Attribute.

### 1.11 تسجيل الشاشات — نمطين منفصلين، الاتنين لازمين

`MenuItemSeedData.cs:246-247` (Navigation، `HR_LEAVE_REQUESTS`/`HR_OVERTIME`) منفصل عن
`ScreenSeedData.cs` (`ExtraScreens`، كيان `Screen` نفسه اللي `ApprovalWorkflowAssignment.ScreenId`
بيشاور عليه). **التسكين الفعلي لسلسلة اعتماد على شاشة مش Seed خالص** — بيحصل بس عن طريق
`AssignWorkflowToScreenCommand` (`POST /api/v1/approvals/workflows/assignments`)، يعني الـ13 سلسلة
في §4.8 بتتسكّن من شاشة الإدارة نفسها بعد الـ Deploy، مش Migration/Seed.

### 1.12 بنية الاختبارات — مجلد `Payroll/` جديد جنب `Attendance/`

مشروعين اختبار بس (`Habbak.ERP.IntegrationTests`, `Habbak.ERP.ApiTests`) — مفيش `Domain.Tests`
منفصل. اختبارات HR في `IntegrationTests/HR/` (11 ملف)، Phase 3 في `IntegrationTests/Attendance/`
(`AttendanceAndLeaveTests.cs` + `AttendanceDevicesTests.cs`)، محرك الاعتمادات في
`IntegrationTests/Approvals/ApprovalWorkflowEngineTests.cs`. **النمط لـ Phase 4**: مجلد جديد
`IntegrationTests/Payroll/` بنفس شكل `Attendance/` (`*Tests.cs` لكل منطقة فرعية، `ApprovalWorkflowAssignment`
بتتزرع يدوي في الاختبار نفسه زي `AttendanceAndLeaveTests.cs:454,525`).

---

## 2. الفجوات والقرارات المعمارية المحتاجة "موافق" صريح

### 2.1 🔴 عدد الجداول القانونية — 10 مش "9 أو 10"

`10-Module-HR-Payroll.md §2.4` بيسرد 9 صفوف، لكن `PayrollTaxBracketSet`+`PayrollTaxBracket` فعليًا
جدولين منفصلين (Header + سطور شرائح، نفس نمط أي Header/Line تاني في المشروع). **المقترح**: العدد
النهائي **10 جداول حقيقية** — الرقم بيتثبّت في هذا التقرير بدل "9 أو 10" اللي في الخطة.

### 2.2 🔴 `EndOfServicePolicy` — مش معدود في نطاق Phase 4 ولا Phase 5 صراحة، بس Phase 4 محتاجه فعليًا

`§6.2` بتحدد إن قيد "مخصص نهاية الخدمة الشهري" (سادس قالب من السبعة) **بس لو `EndOfServicePolicy ≠ None`**
— يعني محرك الترحيل بتاع Phase 4 لازم يقرا من الكيان ده. لكن **مفيش ذكر لـ`EndOfServicePolicy` في
نطاق Phase 4 ("الجداول القانونية... + SalaryComponent... + محرك الحساب + التكامل المحاسبي") ولا في
الـ9 كيانات المُعدّدة لـ Phase 5**. `EndOfServicePolicy.PolicyType` أصلًا **⏸️ Pending Legal**
(افتراضي `None`).

**المقترح**: يتبني كجدول قانوني إحدى عشر (مش عشرة) جوه **Phase 4، Sub-Batch 4.2** — صف واحد بسيط
لكل شركة (`PolicyType` افتراضي `None`، `FormulaDescription`, `MinServiceYears`, `DaysPerYear`,
`EffectiveFrom`)، عشان القالب السادس يتبني من الأول (حتى لو مش بيرحّل حاجة لحد ما `PolicyType`
يتغيّر لاحقًا — نفس فلسفة "Schema-ready" المتكررة في المشروع). **يحتاج موافقة صريحة** لأنه توسيع
حرفي بسيط على نطاق Phase 4 المكتوب.

### 2.3 🔴 `EmploymentContractLine` ↔ `EmployeeSalary`/`SalaryStructure` — تحويل تلقائي وقت التفعيل ولا لأ؟

الكود حسم إن الاتنين كيانين منفصلين (§1.6)، لكن الخطة الأصلية (`10-Module-HR-Payroll.md`) اتكتبت
قبل ما `EmploymentContractLine` يتبني (Phase 3C اتبنت بعدها). لو الاتنين فضلوا منفصلين تمامًا،
موظف الأجهزة هيدخل "بدل سكن" مرتين: مرة في تاب العقود (توثيقي)، ومرة في `EmployeeSalary`/
`SalaryStructure` (الفعلي اللي بيدخل الحساب) — تكرار إدخال حقيقي، مش مجرد ازدواج تصميمي.

**المقترح**: `EmploymentContractLine` يفضل توثيقي بحت (زي ما الكود قرر فعلًا)، **بدون** أي تحويل
تلقائي لـ`EmployeeSalary` — لأن التحويل التلقائي هيحتاج قواعد مطابقة (اسم البند → `SalaryComponent`
بعينه) مش موجودة، وهيعقّد الـ Idempotency لو العقد اتجدد. `SalaryStructure`/`EmployeeSalary` بيتملوا
يدوي من HR وقت التعيين (نفس ما هو مخطط في `HR_HIRING` Wizard، خطوة "راتب" غير موجودة أصلًا — دُمجت
جوه خطوة "عقد" في Phase 1.5.5، فالـWizard أصلًا مش بيبني `EmployeeSalary`). **يحتاج موافقة صريحة**
— القرار ده بيوضّح إزاي Hiring Wizard هيتوسّع في Phase 4 (خطوة "راتب" لازم ترجع تاني، منفصلة عن
"عقد"، عشان تبني `EmployeeSalary` الفعلية).

### 2.4 🟡 `TipsDistribution`+`TipsDistributionLine` — جوه Phase 4 ولا Phase 5؟

سطر واحد في نطاق Phase 4 المكتوب في `HR-MASTER-PLAN.md` بيقول "الرواتب + الجداول القانونية المؤرّخة
+ محرك الحساب + التكامل المحاسبي" — **من غير ذكر `TipsDistribution` صراحة**. لكن: (أ) قاعدة 39 (توزيع
البقشيش) رقمها جوه مدى قواعد الرواتب المعلن (28-40) في نفس نص الماستر بلان لـPhase 4. (ب) القالب
السابع من السبعة في `§6.2` هو "توزيع البقشيش" بالحرف. (ج) `TipsPayable = 16` دور موجود ومُستخدَم في
الكونتر بارتي، مستني بس مصدر الترحيل.

**المقترح**: `TipsDistribution`+`TipsDistributionLine` **جوه Phase 4** (مش Phase 5) — يتبنوا في
Sub-Batch 4.3 مع باقي كيانات الرواتب، شاشتهم `PAY_TIPS_DISTRIBUTION` (بادئة `PAY_` زي باقي شاشات
الرواتب، مقابل `HR_` لشاشات Phase 5). **يحتاج موافقة صريحة** لأنه بيغيّر نطاق Phase 4 المكتوب
حرفيًا (كيانين إضافيين + قالب ترحيل سابع).

### 2.5 🟡 `EmployeeAdvance`+`AdvanceInstallment` — الترحيل المحاسبي بس، مش الكيان الكامل

`§6.2` بتحط "صرف سلفة" كقالب ترحيل تاني من السبعة تبع Phase 4 — لكن كيان `EmployeeAdvance` نفسه
معدود صراحة جوه الـ9 كيانات بتاعة **Phase 5**. **المقترح**: Phase 4 **يبني بس الأدوار المحاسبية
والقالب** (`EmployeeReceivable` موجود بالفعل، `SourceDocumentType.EmployeeAdvance` جديد)، من غير
الكيان نفسه ولا الشاشة — القالب هيفضل غير قابل للتفعيل فعليًا (Schema-ready) لحد Phase 5. **بديل**:
القالب نفسه يتأجّل لـPhase 5 مع الكيان، ويتبني بس الـ6 قوالب اللي كياناتها فعلًا جوه Phase 4. **يحتاج
قرار صريح** أي الاتنين.

### 2.6 🟡 خطوة اعتماد "الإضافي فوق الحد الشهري" (قاعدة 20، مؤجّلة من Phase 3)

Phase 3 وثّقت صراحة إن الجدول القانوني للحد الأقصى الشهري/اليومي للإضافي وخطوة الاعتماد الإضافية
عند تجاوزه **مؤجّلين لـPhase 4** (`Phase-3-Final.md §7 بند 5`). الماستر بلان الحالي لـPhase 4 مش
بيذكر الرجوع لـ`OvertimeRequest`/`ApprovalWorkflow` بتاعته صراحة. **المقترح**: يتضاف كبند صغير في
Sub-Batch 4.4 (بعد ما جدول `OvertimeRate`/حد الإضافي يتبني في 4.2) — خطوة اعتماد إضافية على سلسلة
الإضافي الموجودة لو التجاوز حصل، بدل سلسلة منفصلة. **يحتاج موافقة صريحة** لأنه Follow-up مش مكتوب
حرفيًا في نطاق Phase 4.

### 2.7 🟡 `HrSettings` — أربعة حقول تتفتح في شاشة `HR_SETTINGS`، مش اتنين بس

النطاق المكتوب لـPhase 4 مايذكرش `HrSettings`، لكن `MonthBasis`/`DefaultCutoffDay` (Phase 4) **ولا
غنى عنهم فعليًا** لمعادلة النسبة والتناسب (قاعدة 34) والـCutoff (قاعدة 33). **إضافة مقترحة**:
`CompanyDefaultApproverUserId` (§1.7 — شغّال فعليًا من Phase 2، بس من غير شاشة) يتفتح في نفس التعديل
— فجوة استخدام حقيقية مكتشَفة، مش توسيع نطاق حقيقي (الحقل أصلًا "منتظر مرحلته" حسب تعليق الكيان
نفسه، ومرحلته فعليًا فاتت من غير ما حد يفتحها). `MaxAdvanceInstallmentPercent` **يفضل مقفول** (تعليق
الكيان: "مرحلة السلف" = Phase 5). **يحتاج موافقة على الإضافة الثالثة (`CompanyDefaultApproverUserId`)
تحديدًا** — التانيين (`MonthBasis`/`DefaultCutoffDay`) أصلًا داخل نطاق Phase 4 بلا خلاف.

### 2.8 ⚪ عمولة المندوبين من الفواتير المحصّلة — تصميم Schema بس، بدون تفعيل

`10-Module-HR-Payroll.md` نفسها بتحط "(سياسة، للتأكيد)" على `SourceType = FromCommissions`. تأكّد
فعليًا إن **مفيش أي آلية "فاتورة محصّلة" أو ربط مندوب-عمولة في موديول المبيعات حاليًا** (`SALES_REP`
دور نظامي بس، مفيش `SalesRepId`/`CommissionRate` على أي كيان مبيعات). **المقترح**: `SalaryComponentSource.FromCommissions`
يتبني كقيمة Enum (Schema-ready، نفس فلسفة `SourceModule.Payroll` قبل Phase 4)، بدون أي منطق حساب
فعلي وراها في هذه المرحلة — القرار الحقيقي (إزاي بتتحسب العمولة) خارج النطاق تمامًا. **مايحتاجش
موافقة منفصلة** (نفس نمط "Schema-ready" متكرر أصلًا في المشروع)، لكن مذكور هنا عشان مايتفهمش غلط
إنه قرار متأجّل بالخطأ.

---

## 3. ترتيب Sub-Batches المقترح (بعد "موافق")

يتبع ترقيم الخطة الأصلية (4.1 → 4.8) بدون تغيير في العدد، مع القرارات اللي اتاخدت في §2 متضمَّنة:

- **4.1** — Research Pass (هذا الملف) + تأكيد نهائي لعدد الجداول (10) وقرارات §2.2/2.3/2.4/2.5/2.6/2.7.
- **4.2** — الجداول القانونية العشرة (+`EndOfServicePolicy` لو اتوافق عليه في §2.2) — تصميم نمط
  `AsOf(date)` من الصفر (مفيش سابقة، §1.4)، شاشة `PAY_LEGAL_TABLES` (تبويب لكل جدول).
- **4.3** — Domain: `SalaryComponent`/`SalaryStructure`(+Line)/`EmployeeSalary`/`PayrollPeriod`/
  `PayrollRun`/`PayrollLine`/`Payslip` + (`TipsDistribution`+Line لو اتوافق عليهم في §2.4).
- **4.4** — Payroll Engine: الحساب (قاعدة 28) + Idempotency الثلاثي (§1.3) + `RateSnapshot` (§1.4)
  + مراجعة الاستثناءات (قاعدة 35، بيطلق `NotificationType.PayrollExceptionReview` الموجود، §1.1) +
  بند خطوة اعتماد الإضافي فوق الحد (§2.6، لو اتوافق عليه).
- **4.5** — Posting Integration: 11 دور جديد في `CompanyAccountRole` (+ `ExpectedAccountType`) + 3
  قيم جديدة في `SourceDocumentType` + 6 أو 7 قوالب (حسب قرار §2.5) عن طريق
  `IPostingTemplateEngine.PostIfConfiguredAsync` بنفس نمط الإهلاك بالظبط (§1.2).
- **4.6** — API: Controllers + تسجيل الشاشات في `MenuItemSeedData.cs`+`ScreenSeedData.cs` (§1.11) +
  تسجيل حقول الأجر في `FieldPermissionCatalog` (§1.10) + توسيع `HrSettings` DTO/Command (§2.7).
- **4.7** — Frontend: شاشات §5.1 صفوف 13-21 (بنود/هياكل الرواتب، الفترات، تشغيلات الرواتب، الجداول
  القانونية، إلخ) + توسيع `HR_HIRING` Wizard بخطوة "راتب" (§2.3) لو اتوافق عليه.
- **4.8** — Tests + Docs: مجلد `IntegrationTests/Payroll/` جديد (§1.12) + Regression كامل +
  `Phase-4-Final.md`.

---

## 4. Stop Conditions — الفحص

- **مفيش أي كود اتكتب في الخطوة دي** — Research Pass بس، حسب الـWorkflow Rules (§2 من
  `HR-MASTER-PLAN.md`).
- **7 قرارات محتاجة "موافق" صريح** (§2.1 → §2.7، بترتيب الأولوية 🔴/🟡)، وواحد للعلم بس (§2.8).
- لا يوجد تعارض مع أي Phase سابقة، ولا انحراف مكتشَف في كيانات Phase 1-3C (كل الكيانات المفحوصة
  مطابقة تمامًا لتوثيقها).
