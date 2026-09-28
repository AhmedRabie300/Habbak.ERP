# Phase 3 — الحضور والإجازات — Research Pass

> `Docs/Implementation/HR-MASTER-PLAN.md` § Phase 3 + `Docs/Modules/10-Module-HR-Payroll.md` §2.2
> + قواعد 10-27 + §4.2. فحص فعلي كامل للكود — لا يوجد كود جديد في هذه الخطوة.

---

## 1. حالة الكود الفعلية

### 1.1 الكيانات العشرة — صفر موجود، صفحة بيضاء تمامًا

بحث كامل في المشروع كله (`Domain`/`Application`/`Infrastructure`/`API`/كل مشاريع الاختبار) عن
الأسماء العشرة (`WorkShiftDefinition`, `ShiftSchedule`, `TimeEntry`, `Attendance`, `LeaveType`,
`LeaveBalance`, `LeaveBalanceHistory`, `LeaveRequest`, `Holiday`, `OvertimeRequest`) طلع **صفر
نتائج للعشرة كلهم** — مفيش حتى Stub أو تعليق ميت. نفس الكلام لكل الـ Enums المطلوبة
(`TimeEntrySource`, `LeaveAccrualMethod`, `HrRequestStatus`, وأي `Status` تانٍ خاص بيهم) — **مفيش
حد فيهم موجود في الكود دلوقتي**، رغم إنهم موثّقين في `10-Module-HR-Payroll.md §2.6`. الفجوة
الوحيدة الحقيقية هي **التكامل** مع 3 أنظمة موجودة بالفعل (`POS.Shift`، محرك الاعتمادات، الإشعارات)
— مفصّلة تحت.

### 1.2 `POS.Shift` — مصدر التلقيم

**الملف**: `src/Habbak.ERP.Domain/POS/Shift.cs`. الحقول المهمة: `CashierUserId` (`long` عادي،
**بدون FK** لـ `User`/`Employee` — تعليق الكود صريح إن ده كان لأن موديول HR مكنش موجود وقت بناء
الشاشة، وده **بقى قديم دلوقتي** لأن `Employee`/`User` موجودين من Phase 1.1)، `ClosedByUserId`
(نفس الوضع)، `OpenedAtUtc`/`ClosedAtUtc` (`DateTime`)، `BranchId` (`long?`)، `Status`
(`ShiftStatus`: `Open=1, PendingCloseApproval=2, Closed=3, Rejected=4, Cancelled=5`).

الفتح: `OpenShiftCommandHandler` (`Application/POS/Shifts/Commands/OpenShift/`). القفل:
`CloseShiftCommandHandler` (`Application/POS/Shifts/Commands/CloseShift/`) — بيحسب فرق الكاش
وبيحدد `PendingCloseApproval` بمقارنة صريحة مع حد ثابت، **مش عن طريق محرك الاعتمادات** (`Shift.
ApprovalInstanceId` موجود في الكيان بس **ميت تمامًا** — مُعلَن لاكتمال المخطط بس، مفيش أي كود
بيقرأه أو يكتبه). هذا خارج نطاق Phase 3 تمامًا (مذكور للسياق بس).

**مفيش أي آلية Domain Event / Event Bus في المشروع كله.** بحث شامل عن `IDomainEvent`/`IEventBus`/
`INotificationHandler<T>` طلع صفر — الـ MediatR في المشروع ده بيتستخدم كـ CQRS Mediator بس
(Command/Query)، مش Pub/Sub. **التلقيم المذكور في `10-Module-HR-Payroll.md` قاعدة 11 ("الوردية
بتلقّم") والمهمة الحالية ("Event-driven... in-process event") لازم يتنفّذوا بنفس نمط Phase 2.5
الفعلي**: نداء مباشر (Direct Call) من جوه الـ Handler نفسه، مش حدث حقيقي — تفصيل في §3.1.

### 1.3 `Employee` (Phase 1.1) — جاهز 100%

**الملف**: `src/Habbak.ERP.Domain/HR/Employee.cs`. كل الحقول المطلوبة موجودة بالظبط
(`BranchId`, `OrgUnitId`, `JobPositionId`, `JobGradeId`, `ManagerId`, `UserId`, `HireDate`,
`EmploymentType`, `Status`, `CostCenterDimensionValueId`, `TerminationDate`). `ILookupEntity`
(`Common/ILookupEntity.cs`)، `AuditableEntity`/`ICompanyScopedEntity`/`IBranchScopedEntity`
(`Common/`) بنفس الشكل الموثّق. `IEmployeeScopedEntity` (`long EmployeeId { get; }`) موجود
ومستخدَم بالفعل في `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` — **كيانات
Phase 3 اللي عندها `EmployeeId` (`TimeEntry`, `Attendance`, `LeaveBalance`, `LeaveRequest`,
`OvertimeRequest`, `ShiftSchedule`) هتنفّذه بنفس الطريقة** عشان تاخد فلترة `DataScope.Self`
التلقائية.

`ICodeGenerator.ResolveCodeAsync(screenCode, manualCode, ct)` مستخدَم في
`CreateEmployeeCommand.cs:52` بـ `screenCode = "HR_EMPLOYEES"` — نفس النمط لازم يتكرر لكيانات
Lookup الجديدة (`WorkShiftDefinition`, `LeaveType`, `Holiday`) بـ `Screen.Code` بتاعهم.

### 1.4 محرك الاعتمادات (Phase 2) — النمط الوحيد المطبَّق فعليًا هو `JournalEntry`

`IApprovalWorkflowService.TryStartApprovalAsync(ApprovalWorkflowTrigger)` — الـ Trigger فيه
`CompanyId`, `EntityType` (نص حر), `EntityId`, `Amount` (`decimal` إلزامي — بيتقارن مع
`ApprovalWorkflowAssignment.MinAmount` لو موجود), `RequestedByUserId`. بيرجّع `long?`
(`ApprovalInstance.Id` لو اتعمل Gate، أو `null` لو مفيش Workflow مربوط فينفّذ على طول).

`ApprovalTriggerScreenMap.ScreenCodeFor(entityType)` = `Map.GetValueOrDefault(entityType,
entityType)` — **لو `EntityType` مُمرَّر هو نفسه `Screen.Code`، مفيش داعي لأي Entry في الـ Map
خالص** (تعليق الكود نفسه بيقول كده صراحة). النمط الوحيد المطبَّق فعليًا: `PostingService.
FinalizePostingAsync` بيبعت `EntityType = "JournalEntry"` (مش Screen.Code بتاعه فعليًا، عشان كده
محتاج Entry في الـ Map) + `JournalEntryApprovalOutcomeHandler : IApprovalOutcomeHandler`
(`EntityType`, `ApplyApprovedAsync(entityId, ct)`, `ApplyRejectedAsync(entityId, ct)`).

**حارس الاعتماد الذاتي** (`ApprovalActionGuards.ValidateActingUserAsync`) موجود وشغّال بلا شرط:
`actingUserId == instance.RequestedByUserId` → `ForbiddenException("APPROVAL-SELF-APPROVAL-
FORBIDDEN")`. لا حاجة تُبنى من جديد.

`Screen` بيتزرع من `ScreenSeedData.cs` — تنسيق `MODULE_ENTITY_PLURAL` (`HR_LEAVE_REQUESTS`،
`HR_OVERTIME`... إلخ، مطابقين حرفيًا لأسماء `Screen.Code` في جدول §4.8 من `10-Module-HR-
Payroll.md`).

### 1.5 الإشعارات (Phase 2.5) — نفس نمط "نداء مباشر"، لا يوجد Event حقيقي

`INotificationService.SendAsync`/`SendToMultipleAsync(recipientUserId(s), NotificationType,
titleAr, titleEn, bodyAr, bodyEn, requiresAction, relatedEntityType?, relatedEntityId?, ct)`.
`NotificationType` (10 قيم، كل الأربعة اللي هيستخدمهم Phase 3 موجودين بالفعل): `ApprovalPending
=1, ApprovalApproved=2, ApprovalRejected=3, ApprovalReassigned=4, ...`. **صفر حاجة جديدة مطلوبة
في `NotificationType` لـ Phase 3** — الأربعة أحداث (بدء/موافقة نهائية/رفض/إعادة تسكين) بيتغطّوا
بالكامل بالقيم الموجودة، وده بيتم تلقائيًا لأي كيان بيستخدم محرك الاعتمادات (الأربع نداءات
موجودين جوه `ApprovalWorkflowService`/`ApprovalActionCommands` نفسها — مش حاجة LeaveRequest/
OvertimeRequest بتستدعيها بنفسها). **الفرصة المهمة**: طالما `LeaveRequest`/`OvertimeRequest`
بيستخدموا نفس `TryStartApprovalAsync`/`ApproveStepCommand`/`RejectStepCommand`، الإشعارات هتشتغل
تلقائيًا **من غير أي كود إضافي في Phase 3 نفسها** — بس التنفيذ الفعلي لـ `IApprovalOutcomeHandler`
بتاعهم (تفصيل في §3.3).

### 1.6 نمط الشاشات/الصلاحيات

`[Screen("HR_XXX")]` على الـ Controller (`ScreenPermissionFilter`)، الصلاحية بتتحدد تلقائيًا من
الـ HTTP Verb + آخر Segment (`approve`/`reject`/... مصنّفة `Approve` تلقائيًا). `MenuItemSeedData.
cs` فيه مجموعة `HR` جاهزة (`Code="HR"`) — الشاشات الجديدة (~10) بتتضاف كـ `Leaf(hrGroup, "HR_
ATTENDANCE", ...)` بنفس النمط.

### 1.7 نمط Lookup — مرجع `JobPosition`

`ILookupEntity` + `ICompanyScopedEntity` + `IEntityTypeConfiguration<T>` مستقل لكل كيان (ملف
مشترك لكل موديول)، فهرس فريد `(CompanyId, Code)` مفلتر بـ `IsDeleted = 0` للـ Lookups
المرتبطة بالشركة. **`LeaveType` مش موجود** (اتأكد بالفحص) — Phase 3 هتبنيه من الصفر بنفس النمط
بالظبط.

### 1.8 مكان الكيانات — قرار موثّق مسبقًا (مش حاجة تتقرر دلوقتي)

`10-Module-HR-Payroll.md` سطر 22 بيحدد صراحة: **`src/Habbak.ERP.Domain/{HR,Attendance,Payroll,
SelfService}/`** — يعني الـ 10 كيانات بتاعة Phase 3 مكانها **`src/Habbak.ERP.Domain/Attendance/`**
(موديول مستقل عن `HR`)، مش جوه `HR/` الموجود. نفس التقسيم في `Application/Attendance/` و
`Infrastructure/Persistence/Configurations/Attendance/`. هذا مش قرار جديد، بس توثيق تأكيد إن
الفولدر ده **مش موجود بعد** (لازم يتعمل من الصفر) وإن الاسم/الموقع محسوم بالفعل.

---

## 2. الفجوات

لا توجد فجوة **Backend حقيقية** تمنع التنفيذ — كل الأنظمة اللي Phase 3 محتاجة تتكامل معاها
(الاعتمادات، الإشعارات، `Employee`، `ICodeGenerator`، `IEmployeeScopedEntity`) جاهزة ومُختبرة من
مراحل سابقة. الفجوات كلها من نوع **"لسه محتاج يُبنى من الصفر"** (متوقّع، مش مفاجأة):

1. الـ 10 كيانات + الـ Enums المصاحبة (`TimeEntrySource`, `LeaveAccrualMethod`, `HrRequestStatus`,
   `MovementType`, `OvertimeType`...) — صفحة بيضاء.
2. `POS.Shift.CashierUserId`/`ClosedByUserId` **بدون FK** لـ `User` — لازم Join يدوي (`Employee.
   UserId == Shift.CashierUserId`) بدل Navigation Property جاهزة.
3. مفيش Domain Event Bus — التلقيم من `POS.Shift` لازم يتنفّذ بنداء مباشر (تفصيل §3.1).
4. مفيش جدول قيود إضافي شهري/يومي للإضافي (`10-Module-HR-Payroll.md` قاعدة 20 نفسها معلّمة
   "للتأكيد") — يأثّر على خطوة الاعتماد الإضافية في `OvertimeRequest` (تفصيل §3.7).

---

## 3. القرارات الفرعية (تحتاج تأكيد صريح مع "موافق")

### 3.1 🔴 آلية تلقيم `POS.Shift → TimeEntry` — نداء مباشر مش Event

النطاق الأصلي (وصياغة المهمة الحالية) بيقول "Event-driven... in-process event" — **الكود الفعلي
مفيهوش أي بنية Event خالص**، ونمط Phase 2.5 الموثّق فعليًا رفض بناء واحدة عمدًا لـ 4 نقاط استدعاء
بس ("a real domain-event/handler indirection would be over-engineering"). **التوصية**: نفس المبدأ
بالظبط — حقن `ITimeEntryFeedService` (اسم مقترح) جوه `OpenShiftCommandHandler`/
`CloseShiftCommandHandler`، ونداء مباشر بعد الـ `SaveChangesAsync` بتاع الـ Shift نفسه (نفس ترتيب
`INotificationService` في Phase 2.5 — نداء أخير، Best-Effort). **صفر تعديل على `Shift` Entity
نفسه أو أي عمود فيه** — التعديل الوحيد سطر واحد إضافي في كل Handler، بالظبط زي ما حصل مع
الإشعارات في محرك الاعتمادات من غير ما يتلمس `ApprovalInstance`.

### 3.2 مطابقة Best-Effort — مفيش رفض ولا استثناء لو الربط ناقص

لو `Shift.CashierUserId` مالوش `Employee` مربوط (`Employee.UserId == CashierUserId` مفيش نتيجة)،
أو `Shift.BranchId` فاضي — **التلقيم بيتجاهل بصمت (بدون `TimeEntry` مقترح، وبدون أي خطأ)**. قاعدة
"بدون أي تعديل على `POS.Shift`" (10-27) بتعني كمان إن الوردية **مايتوقفش فتحها/قفلها أبدًا** بسبب
حاجة ناقصة في HR — التعارض ده بالظبط اللي "تقرير التعارض اليومي" (3.5) مصمم يكشفه، مش يمنعه وقت
الحدث.

### 3.3 Idempotency التلقيم — Existence Check مش عمود مفتاح جديد

بدل عمود Idempotency Key منفصل على `TimeEntry`: قبل إنشاء `TimeEntry` مقترح جديد، فحص "فيه صف
`TimeEntry` بـ `POSShiftId = X` و`EntryType = In/Out` موجود بالفعل؟" — لو موجود (حتى لو
`Dismissed`)، تجاهل. أبسط من مفتاح مُحسَب، وكافي هنا لأن المصدر (`Shift.Id`) نفسه فريد أصلًا
(مختلف عن `PayrollRun.IdempotencyKey` اللي بيحتاج صيغة مركّبة لأنه مش مربوط بصف واحد فريد).

### 3.4 محرك الاعتمادات — `EntityType = Screen.Code` مباشرة، صفر Map Entries

`LeaveRequest`/`OvertimeRequest` هيبعتوا `EntityType = "HR_LEAVE_REQUESTS"` / `"HR_OVERTIME"`
(نفس `Screen.Code` بالظبط) بدل اسم الكيان الخام — كده صفر حاجة تتضاف لـ `ApprovalTriggerScreenMap`
(بيرجع نفس القيمة لو مش موجودة في الـ Dictionary أصلًا). `Amount`: `0` لـ `LeaveRequest` (مفيش حد
مالي في §4.8 صف 1)، و`PlannedMinutes` (كـ `decimal`) لـ `OvertimeRequest` (يفتح الباب لخطوة إضافية
لو `ApprovalWorkflowAssignment.MinAmount` اتحط بعدد دقايق — تفصيل الحد نفسه في §3.7).

`IApprovalOutcomeHandler` مستقل لكل الاتنين (`LeaveRequestApprovalOutcomeHandler`,
`OvertimeRequestApprovalOutcomeHandler`) بنفس نمط `JournalEntryApprovalOutcomeHandler` — الموافقة
النهائية على الإجازة بتحوّل `Pending → Used` (حركة `Usage` في `LeaveBalanceHistory`)، الرفض بيفك
الـ `Pending` من غير حركة تاريخية (تفصيل §3.6).

### 3.5 مكان الـ Enums الجديدة

- `src/Habbak.ERP.Domain/Attendance/Enums.cs` (ملف جديد): `TimeEntrySource`, `TimeEntryEntryType`
  (`In`/`Out`)، `TimeEntryStatus` (`Suggested`/`Accepted`/`Dismissed`)، `AttendanceStatus`
  (`Present`/`Absent`/`Leave`/`Holiday`/`RestDay`)، `LeaveAccrualMethod`, `LeaveBalanceMovementType`
  (`Accrual`/`Usage`/`Reversal`/`CarryOver`/`Expiry`/`CashOut`/`Adjustment`), `OvertimeType`.
- `src/Habbak.ERP.Domain/HR/Enums.cs` (الملف الموجود بالفعل، بيتوسّع): `HrRequestStatus`
  (`Draft`/`Pending`/`Approved`/`Rejected`/`Cancelled`) — لأنه هيتشارك مع `EmployeeAdvance`/
  `EmployeePenalty`/... في Phase 5 (موديول Payroll مش Attendance)، فمكانه الطبيعي ملف الـ Enums
  المشترك اللي `EmployeeStatus`/`EmploymentType` أصلًا فيه، مش ملف خاص بموديول Attendance.

### 3.6 دفتر `LeaveBalance.Pending` — Hold بدون History، `Used` بـ History إلزامي

`Pending` **مش** من ضمن قيم `LeaveBalanceMovementType` (قاعدة 26 بتوثّق الحركات النهائية بس) —
يعني هو عمود "حجز مؤقت" بيتغيّر مباشرة على `LeaveBalance` نفسه (زيادة عند `Create` الطلب، إنقاص
عند `Reject`/`Cancel`)، **من غير** صف `LeaveBalanceHistory`. أما `Used` فأي تغيير فيه (وقت
الموافقة النهائية) **لازم** صف `LeaveBalanceHistory` (`MovementType = Usage`) — ده تفسير حرفي
لقاعدة 26، مش قرار جديد، موثّق هنا عشان يكون صريح وقت الكود.

### 3.7 خطوة اعتماد "الإضافي فوق الحد الشهري" — مؤجّلة لـ Phase 4

قاعدة 20 (`10-Module-HR-Payroll.md`) نفسها معلّمة **"للتأكيد"**، والجدول القانوني اللي المفروض
يحمل الحد اليومي/الشهري **مش من ضمن جداول Phase 4 §2.4 المسمّاة صراحة** (`PenaltyDeductionCap`/
`NoticePeriodRule` بس المذكورين، مفيش جدول Overtime Cap مستقل). **التوصية**: Phase 3.3 تبني
`OvertimeRequest` بخطوة اعتماد واحدة بس (`DirectManager`، مطابق لصف #5 في جدول §4.8 كحد أدنى)،
والخطوة الإضافية "لو فوق الحد الشهري" **تتأجل لحد ما الجدول القانوني نفسه يتبني في Phase 4** —
بناء منطق حد شهري دلوقتي هيحتاج رقم مُخترَع مش موثّق، وده عكس مبدأ "الأرقام كلها للتأكيد مع
المستشار قبل الإدخال" (`§2.4`).

### 3.8 حساب `LeaveRequest.Days` — عدّ تقويمي شامل (V1 مبسّط)

مفيش تفصيل في القواعد 10-27 لاستثناء يوم الراحة الأسبوعي (`ShiftSchedule.IsRestDay`) أو
`Holiday` من عدد أيام الإجازة المطلوبة. **V1**: `Days = (EndDate.DayNumber - StartDate.DayNumber)
+ 1` (عدّ تقويمي شامل بسيط، بدون استثناء أيام الراحة/الأعياد). هذا تبسيط متعمَّد يستاهل تنويه —
لو محتاج استثناء لاحقًا (مطابقة `ShiftSchedule`/`Holiday` وقت الحساب)، ده تحسين مؤجّل، مش Stop
Condition دلوقتي.

---

## 4. ترتيب Sub-Batches

| Sub-Batch | المحتوى | يعتمد على |
|---|---|---|
| 3.1 | Research Pass (هذا التقرير) | — |
| 3.2 | Domain (10 كيانات + الـ Enums §3.5) + Migration واحدة | 3.1 |
| 3.3 | Commands + Queries (شامل `IApprovalOutcomeHandler` لـ Leave/Overtime، §3.4/3.6) | 3.2، محرك الاعتمادات (Phase 2، موجود) |
| 3.4 | API (9 Controllers + تسجيل Screens في `ScreenSeedData`/`MenuItemSeedData`) | 3.3 |
| 3.5 | POS Integration (`ITimeEntryFeedService` نداء مباشر §3.1-3.3 + تقرير التعارض) | 3.2 (`TimeEntry`)، 3.3 (Attendance compute لو التعارض محتاجه) |
| 3.6 | Frontend (9 شاشات + Calendar + Conflicts + MyAttendance placeholder) | 3.4 |
| 3.7 | Tests + Docs | كل ما سبق |

لا اعتماد دائري. 3.5 ممكن يبدأ بالتوازي مع نهاية 3.3 (مفيش تعارض ملفات) بس هيتنفّذ بالترتيب
المذكور في المهمة (بعد 3.4) تجنبًا لتعقيد غير لازم.

---

## 5. Stop Conditions — الفحص

| الحالة | النتيجة |
|---|---|
| Backend ناقص يمنع التنفيذ | **لا** — كل الأنظمة المُعتمَد عليها (اعتمادات، إشعارات، `Employee`) جاهزة ومُختبرة. |
| قرار معماري متضارب | **جزئي** — صياغة "Event-driven" في المهمة الأصلية بتتعارض مع غياب أي Event Bus في الكود؛ اتحل بالتوصية في §3.1 (نداء مباشر، نفس فلسفة Phase 2.5) — مش تعارض يستدعي توقف، بس يستاهل تأكيد صريح مع "موافق". |
| Migration فشلت | لسه ماحصلش — أول Migration في 3.2. |
| Context قرب حد | لا يوجد حاليًا. |

**لا يوجد Stop Condition حقيقي — جاهز للتنفيذ الكامل بعد "موافق"، شامل تأكيد القرارات الثمانية في
§3 (الأهم: §3.1 نداء مباشر بدل Event، و§3.7 تأجيل خطوة الإضافي الشهري لـ Phase 4).**
