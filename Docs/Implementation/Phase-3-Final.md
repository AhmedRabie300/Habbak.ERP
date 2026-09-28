# Phase 3 — الحضور والإجازات — تقرير نهائي (شامل Amendments)

> `Docs/Implementation/HR-MASTER-PLAN.md` § Phase 3. البحث الأولي: `Phase-3-Research.md`.
> الملاحظات الإضافية بعد الـ Research (4 تعديلات على النطاق الأصلي): `Docs/My Remarks/Remarks8-HR-Enhancements.md`.

---

## 1. ملخص تنفيذي

Phase 3 كاملة اتنفذت على مرحلتين:

1. **3.1 → 3.5** (Research → Domain → Application → API → POS Integration) — النطاق الأصلي حسب `Phase-3-Research.md`، شامل الـ 10 كيانات الأصلية والتلقيم المباشر من `POS.Shift`.
2. **Amendments** (4 ملاحظات من المستخدم بعد مراجعة الـ Research، مسجّلة في `Remarks8-HR-Enhancements.md` بنود 5-8) — اتعملت **جوه Phase 3 نفسها** قبل الـ Frontend، مش Phase منفصلة: `ShiftSchedule`/`Holiday` بقوا فترات (`StartDate`/`EndDate?`) بدل يوم واحد، كيان جديد `EmployeeWeeklyRestDays`، وشاشة **مولّد جماعي لجداول الورديات** (`HR_SHIFT_SCHEDULE_GENERATOR`).
3. **3.6** (Frontend) — 9 شاشات أصلية + شاشة `HR_SETTINGS` (اتبنت لأول مرة، كانت فجوة من Phase 1.5) + شاشة المولّد الجماعي الجديدة.
4. **3.7** (Tests + Docs) — 20 اختبار تكامل (`IntegrationTests`) + 4 اختبارات API-over-HTTP (`ApiTests`) + هذا التقرير.

**صفر انحراف عن قواعد HR-MASTER-PLAN.md الجوهرية** (TimeEntry immutable، Attendance computed، POS بدون تعديل، Leave accrual Idempotent، LeaveBalance عن طريق History بس).

---

## 2. النطاق الأصلي (3.1 → 3.5)

### 2.1 Domain — 10 كيانات (+1 من الـ Amendments = 11)

| الكيان | الملف | ملاحظة |
|---|---|---|
| `WorkShiftDefinition` | `Domain/Attendance/AttendanceEntities.cs` | Lookup — وردية عمل (صباحي/مسائي)، مختلف عن `POS.Shift` |
| `ShiftSchedule` | نفس الملف | **فترة** (`StartDate`/`EndDate?`) بعد الـ Amendment، مش يوم واحد |
| `TimeEntry` | نفس الملف | مصدر الحقيقة الوحيد للحضور — Immutable، التصحيح بصف جديد |
| `Attendance` | نفس الملف | الملخص اليومي المحسوب — الوحيد اللي بيدخل الرواتب لاحقًا |
| `LeaveType` | `Domain/Attendance/LeaveEntities.cs` | Lookup — بكل حقول القواعد (استحقاق، ترحيل، قيود جنس/عدد) |
| `LeaveBalance` | نفس الملف | `Available` محسوبة (`Ignore` في EF)، `Pending` حجز بدون History |
| `LeaveBalanceHistory` | نفس الملف | سجل كل حركة نهائية (Accrual/Usage/Reversal/...) |
| `LeaveRequest` | نفس الملف | `ApprovalInstanceId` — بيستخدم محرك الاعتمادات (Phase 2) فعليًا |
| `Holiday` | نفس الملف | **فترة** (`StartDate`/`EndDate?`) بعد الـ Amendment |
| `OvertimeRequest` | نفس الملف | خطوة اعتماد واحدة بس (خطوة "فوق الحد الشهري" مؤجّلة لـ Phase 4) |
| `EmployeeWeeklyRestDays` | نفس الملف | **جديد (Amendment Item 6)** — Pattern افتراضي لأيام الراحة، مصدره الوحيد المولّد الجماعي |

**Migration**: `AttendanceAndLeave` (10 جداول + عمود `LeaveDayCountingMode` على `HrSettings`) — Backup → Trial DB → Real DB، Up/Down اتأكّدوا.

### 2.2 Application (28 ملف — Commands/Queries/Helpers)

- CRUD كامل للـ 3 Lookups (`WorkShiftDefinition`, `LeaveType`, `Holiday`) بنفس نمط `JobPosition`.
- `ShiftSchedule`: Create/Update/Delete + `GenerateWeeklySchedule` (موظف واحد، 7 أيام).
- `TimeEntry`: Create (يدوي) + Correct (صف جديد) + Accept/Dismiss (مقترح من POS).
- `Attendance`: `RecomputeDailyAttendanceCommand` (الأولوية: RestDay → Holiday → Leave معتمدة → Present/Absent) + Approve.
- `LeaveBalance`: `AdjustLeaveBalanceCommand` (تعديل يدوي بسبب) + `AccrueMonthlyLeaveCommand` (Idempotent — مفتاح `SourceType="MonthlyAccrual"` + `SourceId=Year*100+Month` لكل `LeaveBalance`).
- `LeaveRequest`: Create (فحص الرصيد المتاح) + Submit (يستدعي `IApprovalWorkflowService.TryStartApprovalAsync` فعليًا — `EntityType = Screen.Code` مباشرة، صفر إضافات لـ `ApprovalTriggerScreenMap`) + Cancel (قبل البداية بس).
- `OvertimeRequest`: Create + Submit (نفس محرك الاعتمادات) + Cancel.
- `LeaveRequestApprovalOutcomeHandler` / `OvertimeRequestApprovalOutcomeHandler`: تنفيذ `IApprovalOutcomeHandler` — مُكتشَفين تلقائيًا (Assembly Scan)، صفر تسجيل يدوي في DI.
- `LeaveDaysCalculator`: Calendar (افتراضي) / WorkingDays (يستثني الراحة والعطلات) / Fallback لـ Calendar + تحذير لو مفيش `ShiftSchedule` خالص.

### 2.3 API — 11 Controller (ملف واحد `AttendanceControllers.cs`)

`WorkShiftDefinitionsController`, `ShiftSchedulesController`, `TimeEntriesController`, `AttendanceController`, `AttendanceConflictsController`, `LeaveTypesController`, `LeaveBalancesController`, `LeaveRequestsController`, `HolidaysController`, `OvertimeRequestsController`, و**`ShiftScheduleGeneratorController`** (Amendment، شاشة صلاحية منفصلة `HR_SHIFT_SCHEDULE_GENERATOR`).

كل الشاشات مسجّلة في `ScreenCodeCatalog.cs` (Lookups المُرقَّمة: `HR_WORK_SHIFTS`/`HR_LEAVE_TYPES`/`HR_HOLIDAYS`) أو `ScreenSeedData.cs ExtraScreens` (الباقي)، وMenuItemSeedData.cs.

### 2.4 POS Integration (3.5)

`ITimeEntryFeedService`/`TimeEntryFeedService` — نداء مباشر (مش Event، مفيش بنية Event Bus في المشروع) من `OpenShiftCommandHandler`/`CloseShiftCommandHandler` بعد الـ `SaveChangesAsync` بتاعتهم. **صفر تعديل على `Shift` Entity نفسه**. Idempotency: فحص وجود `(POSShiftId, EntryType)` مسبق. Silent Ignore: كاشير من غير Employee مربوط، أو Shift من غير Branch. تعارض: TimeEntry تاني (مش من نفس الوردية) في نفس اليوم → `Status = Suggested` بدل `Accepted`. تقرير التعارض اليومي: `GetAttendanceConflictsQuery` (كاشير فاتح Shift وحضوره Absent/مش مسجّل).

---

## 3. Amendments (Remarks8 — بنود 5-8، نُفذت جوه Phase 3)

راجع `Docs/My Remarks/Remarks8-HR-Enhancements.md` للتفاصيل الكاملة. ملخص:

| البند | التعديل | الحالة |
|---|---|---|
| 5 | `ShiftSchedule.WorkDate` → `StartDate` + `EndDate?` (فترة) | ✅ |
| 6 | `EmployeeWeeklyRestDays` (كيان جديد، Pattern أسبوعي) | ✅ |
| 7 | مولّد جماعي `HR_SHIFT_SCHEDULE_GENERATOR` (OrgUnit + فترة + استثناءات + Preview) | ✅ |
| 8 | `Holiday.Date` → `StartDate` + `EndDate?` (فترة) | ✅ |

**Migration ثانية**: `Phase3Amendments` (RenameColumn — البيانات القديمة اتحافظ عليها — + AddColumn + جدول `EmployeeWeeklyRestDays`) — Backup → Trial DB (Up+Down اتأكّدوا) → Real DB.

**قرار تصميم جوهري**: `EmployeeWeeklyRestDays` **مش** مصدر بيانات لـ `Attendance`/`LeaveDaysCalculator` — دول بيقروا `ShiftSchedule` المُجسَّد بس. `EmployeeWeeklyRestDays` مرجع افتراضي للمولّد الجماعي وبس (صف `ShiftSchedule` الواحد لازم يحمل قيمة `IsRestDay` موحّدة على طول فترته، فمفيش طريقة تمثيل نمط متغيّر داخل صف واحد — المولّد هو اللي بيحوّل النمط لصفوف يومية فعلية).

---

## 4. الأرقام

| | العدد |
|---|---|
| كيانات Domain | 11 (10 أصلية + `EmployeeWeeklyRestDays`) |
| ملفات Application (Commands/Queries/Helpers) | 28 |
| Controllers | 11 |
| Migrations | 2 (`AttendanceAndLeave` + `Phase3Amendments`) |
| ملفات Frontend | 22 (11 مجلد Feature) |
| اختبارات تكامل (`AttendanceAndLeaveTests.cs`) | 20 |
| اختبارات API-over-HTTP (`AttendanceAndLeaveApiTests.cs`) | 4 |

---

## 5. Tests

### 5.1 Integration Tests (20 — كل الـ 20 ناجحة، اتشغّلوا 3 مرات منفصلة بنفس النتيجة)

- TimeEntry: تصحيح بصف جديد بدون تعديل الأصل.
- Attendance: حساب Present (WorkedMinutes صحيح)، Absent (بدون سجلات/جدول/عطلة/إجازة)، RestDay (من الجدول)، **Holiday Range** (Amendment — يوم في نص فترة عطلة متعددة الأيام).
- POS Integration: تلقيم مقبول تلقائيًا + Idempotency (نفس الوردية مرتين = صف واحد)، Silent Ignore (مستخدم حقيقي بدون Employee مربوط — لاحظ: لازم مستخدم حقيقي بسبب FK حقيقي على `CashierUserId`، مش رقم وهمي)، وكشف تعارض (مقترح لما فيه تسجيل تاني في نفس اليوم).
- Leave Accrual: Idempotent لنفس الموظف والشهر (تشغيلتين = صف Accrual واحد بس).
- LeaveRequest: رفض لو الرصيد أقل من المطلوب، اعتماد مباشر لو مفيش Workflow نشطة (Pending→Used + History)، اعتماد عن طريق محرك الاعتمادات الفعلي (Pending أثناء الانتظار، Used بعد الموافقة)، رفض (فك Pending بدون History — قاعدة 26).
- LeaveDaysCalculator: Calendar / WorkingDays (يستثني الراحة) / Fallback.
- **Amendments**: فترة `ShiftSchedule` واحدة تغطي كل يوم جواها، رفض تداخل فترتين، المولّد الجماعي (Mask + استثناءات موظف بعينه)، Idempotency المولّد الجماعي.

### 5.2 API Tests (4)

- CRUD كامل عن طريق HTTP لـ `WorkShiftDefinitionsController` (تمثيلي — باقي الـ Lookups نفس الشكل بالظبط، زي سابقة `HrLookupsApiTests`).
- تطبيق صلاحية الشاشة فعليًا (403 بدون منح، 200 بعد المنح) على `AttendanceController` — يثبت إن الـ 11 Controller الجديدة كلهم واقعين تحت `ScreenPermissionFilter` بشكل صحيح (نفس آلية `SecurityTests.Every_controller_says_which_screen_it_belongs_to` العامة).
- دورة `LeaveRequest` كاملة عن طريق HTTP (Create → CalculateDays → Submit → التحقق من الحالة والرصيد) — تكمّل اختبارات الـ Handler المباشرة بمستوى HTTP فعلي (Serialization + Routing + Permission).
- نطاق Self: `GetMyAttendance` بترجّع سجلات المستخدم المرتبط بس، مش زميله.

### 5.3 Regression

IntegrationTests + ApiTests الكاملين (كل المشروع، مش بس Phase 3) اتشغّلوا كاملين بدون فلترة بعد كل التعديلات (Domain + Amendments + الإصلاح في §6 بند 5):

| Suite | Total | Passed | Failed | Duration |
|---|---|---|---|---|
| `Habbak.ERP.IntegrationTests` | 226 | 226 | 0 | 29 د 29 ث |
| `Habbak.ERP.ApiTests` | 273 | 273 | 0 | 16 د 28 ث |

صفر Regression على أي موديول سابق — يثبت إن الـ Amendments على `ShiftSchedule`/`Holiday` (Rename + إضافة عمود) ما كسرتش أي اختبار HR أو غير-HR كان بيعتمد على الأسماء/الشكل القديم.

---

## 6. Bugs مكتشفة وانصلحت أثناء التنفيذ

1. **`Shift.CashierUserId` عنده FK حقيقي على `Users`** (Convention عامة في المشروع بتضيف FK لأي عمود اسمه `...UserId`) — رغم إن الكيان نفسه مفيهوش Navigation Property. اختبار "Silent Ignore" الأول كان بيستخدم رقم وهمي (999999) فبيفشل بـ FK Violation قبل ما يوصل لمنطق الـ Ignore نفسه. الحل: مستخدم حقيقي بدون Employee مربوط بيه (نفس السيناريو الحقيقي بالظبط).
2. **`POSTerminalId` إلزامي فعليًا** على `Shift` (FK حقيقي) — اختبارات الـ POS Integration الأولى استخدمت `POSTerminalId = 0`. الحل: `AddPOSTerminalAsync` helper بيعمل صف حقيقي.
3. **`Employee.Status` الافتراضي `Draft`** — `AccrueMonthlyLeaveCommand` بيفلتر `Active` بس (قرار صحيح ومقصود)، فموظف اختبار من غير `Status = Active` صريح ما كنش بياخد استحقاق. الحل: `Status = EmployeeStatus.Active` في الـ Helper.
4. **`Screen.Code` فريد على مستوى الـDB كله** (مش لكل شركة) — اختبارين مختلفين في نفس الـ Fixture حاولوا يضيفوا نفس الكود `"HR_LEAVE_REQUESTS"` فحصل تعارض مفتاح. الحل: `GetOrAddScreenAsync` (Get-or-Create) بدل `Add` مباشر.

كل الأربعة اكتُشفوا واتصلحوا في كود الاختبارات نفسه (مش الكود الفعلي) — الاكتشاف الأول عن `CashierUserId` FK مفيد لأي Phase مستقبلية هتتفاعل مع `POS.Shift` في الاختبارات.

5. **`TestAuthHandler` (بنية تحتية عامة للـ ApiTests، موجودة من قبل Phase 3) ما كانتش بتحقن Claim اسمه `EmployeeId` أبدًا** — أي اختبار HTTP بيعتمد على `ICurrentCompanyContext.EmployeeId` (زي `GetMyAttendance`) كان بيرجّع مجموعة فاضية دايمًا بغض النظر عن البيانات، لأن الكود الفعلي (`CurrentCompanyContext.cs`) بيقرأ الـ Claim ده فعلاً من الـ JWT الحقيقي (`JwtAccessTokenIssuer.EmployeeId`) بس التمثيل الوهمي للاختبارات ماكنش بيبعته. الحل: إضافة `X-Test-EmployeeId` Header اختياري لـ `TestAuthHandler` (يماثل تمامًا سلوك الـ JWT الحقيقي)، واستخدامه في اختبار Self-Scope. ده Gap في بنية الاختبارات نفسها (مش في كود Phase 3 أو أي Phase سابقة) — أي Phase مستقبلية عايزة تختبر مسار مربوط بـ Employee الحالي عن طريق HTTP هتحتاج تمرّر الـ Header ده.

---

## 7. Issues / Known Limitations

1. **Self-Service حقيقي مؤجّل لـ Phase 6** — `GetMyAttendance`/`GetMyLeaveBalance` موجودين وبيشتغلوا صح (فلترة تلقائية بالـ `EmployeeId` عن طريق `IEmployeeScopedEntity`)، بس لسه محتاجين صلاحية الشاشة العادية (`HR_ATTENDANCE`/`HR_LEAVE_BALANCES`) مش `[AnySignedInUser]` — يعني موظف عادي من غير صلاحية HR مايقدرش يشوف حضوره هو نفسه دلوقتي. ده مقصود ومؤجّل — الحل الحقيقي (بوابة `/ess` بصلاحيات مختلفة تمامًا) هو **Phase 6** بالتحديد.
2. **`CancelLeaveRequestCommand` بعد بداية الإجازة**: بيرفض صراحة (`HR-LEAVE-CANCEL-AFTER-START`) — يحتاج مسار اعتماد إلغاء منفصل، خارج نطاق Phase 3 (موثّق كقرار في `Phase-3-Research.md §3.8` سياقيًا، ومُلخّص هنا كـ Known Limitation صريح).
3. **`GenerateBulkShiftSchedulesCommand` بيولّد صف مستقل لكل يوم** (مش فترة موحّدة مجمّعة) — تبسيط متعمَّد؛ النمط الأسبوعي بيتغيّر يوم بيوم فمفيش فايدة حقيقية من تجميع الصفوف (Run-Length Encoding) في النطاق الحالي.
4. **اختيار OrgUnit في شاشة المولّد الجماعي قائمة مسطّحة بمسافات بادئة**، مش شجرة تفاعلية كاملة زي `HR_ORG_UNITS` — تبسيط متعمَّد لضيق الوقت، يكفي وظيفيًا.
5. **خطوة اعتماد "الإضافي فوق الحد الشهري"** لسه مؤجّلة لـ Phase 4 (الجدول القانوني نفسه مش موجود بعد) — موثّقة من الـ Research Pass، مفيش تغيير.

---

## 8. القرارات المتخذة (ملخص، تفصيل كامل في Phase-3-Research.md + Remarks8)

- POS Integration: نداء مباشر بدل Event (مفيش Event Bus في المشروع أصلًا).
- الكيانات في `src/Habbak.ERP.Domain/Attendance/` (موديول مستقل عن `HR/`) — موثّق مسبقًا في `10-Module-HR-Payroll.md:22`.
- `EntityType = Screen.Code` مباشرة لمحرك الاعتمادات (صفر إضافات لـ `ApprovalTriggerScreenMap`).
- `LeaveBalance.Pending` حجز بدون History؛ `Used` وحدها بتاخد صف `LeaveBalanceHistory` (تفسير حرفي لقاعدة 26).
- `EmployeeWeeklyRestDays` مرجع للمولّد الجماعي بس، مش مصدر حقيقة لـ Attendance/Leave (الأمندمنت رقم 6).

---

## 9. المرحلة التالية

**Phase 3B — تكامل أجهزة البصمة (Fingerprint)** — مباشرة بعد الموافقة على هذا التقرير، حسب `HR-MASTER-PLAN.md § Phase 3B`.

**Phase 3C** (بعد Phase 3B) — البنود 1-4 من `Remarks8-HR-Enhancements.md` (بنود العقود والمرفقات): `EmploymentContractLine`، مرفقات العقد والشهادات، وبند العقد في معالج التعيين.
