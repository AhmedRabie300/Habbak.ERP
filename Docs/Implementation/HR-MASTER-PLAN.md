# HR Master Plan — Phase 1.2 → Phase 7

> **المرجع الوحيد لتنفيذ باقي موديول HR.** كل Phase بييتنفذ من هذا الملف بدون Prompt إضافي — اقرأ § الخاص بالمرحلة، نفّذ الـ Workflow Rules (قسم 2)، سلّم، واستنى الموافقة.
>
> **مفيش تقديرات أيام ولا تواريخ في هذا الملف** — الأرقام دي مضللة ومش مرجع تنفيذي حقيقي (تقديرات الجهد التقريبية موجودة، للعلم بس، في `Docs/Modules/10-Module-HR-Payroll.md` ملحق ب.2 ومفيش داعي تتكرر هنا).

---

## 1. ملخص تنفيذي

**إجمالي المراحل**: Phase 0 → Phase 7 (11 مرحلة فعلية، Phase 1 مقسّمة لـ 1.1-1.5 + Phase 3B مضافة بين Phase 3 وPhase 4).

**المنجز**:
- **Phase 0** (0.1, 0.3, 0.4, 0.5, 0.6) — حماية PII، تشفير عمود + HMAC، نطاق البيانات الأساسي، مفاتيح التشفير.
- **Phase 1.1** — الكيانات الأساسية (Employee + التوابع الثلاثة + 8 Lookups) عبر 7 Batches (B1-B7)، بما فيها إصلاح لاحق لثغرة عدم سحب الجلسات في `TerminateEmployeeCommand`. موثّق بالكامل في `Docs/Implementation/HR-Core-Plan.md` (قسم "Phase 1.1 — Final Status") و`Docs/Implementation/Phase-1.1-Final-Report.md`.
- **Phase 1.2** — تحويل الحقول الحرة (أرقام موظفين بدون FK) لعمود مرتبط حقيقي. موثّق في `Docs/Implementation/Phase-1.2-Final.md`.
- **Phase 1.3** — تفعيل الموظف كطرف في السندات المحاسبية. موثّق في `Docs/Implementation/Phase-1.3-Final.md`.
- **Phase 1.4** — إنهاء خدمة إداري مبسّط (تعطيل حساب + فحص عهدة نقدية وأصول) — **الحساب المالي الكامل مش هنا، هو Phase 5**. موثّق في `Docs/Implementation/Phase-1.4-Final.md`.

**المتبقي** (بوصف، بدون أيام):
- **Phase 1.5** — شاشات HR الأساسية (Frontend): Lookups + الموظف + التوابع + معالج التعيين.
- **Phase 2** — محرك الاعتمادات المركزي (بيخدم النظام كله، مش HR بس).
- **Phase 3** — الحضور والإجازات + التلقيم من POS.
- **Phase 3B** — تكامل أجهزة البصمة (Push SDK + File Import) — تغذية `TimeEntry.Source = Device`.
- **Phase 4** — الرواتب + الجداول القانونية المؤرّخة + محرك الحساب.
- **Phase 5** — إنهاء الخدمة الكامل (التسوية المالية) + السلف + الجزاءات + المكافآت.
- **Phase 6** — بوابة الخدمة الذاتية (PWA `/ess` + وضع الكشك).
- **Phase 7** — التقارير.

---

## 2. Workflow Rules — يتنفذ حرفيًا لكل Phase

```
STEP 1: Research Pass
  - اقرأ HR-MASTER-PLAN.md § Phase X
  - افحص الكود الفعلي (مش افتراض — تحقق من كل مسار/كيان مذكور هنا لسه صحيح)
  - اكتب: docs/Implementation/Phase-{X}-Research.md
  - اعرض التقرير + STOP
  - استنى "موافق"

STEP 2: تنفيذ Sub-Batches (بعد الموافقة)
  - لكل Sub-Batch:
      - Domain → Application → API → Tests (بالترتيب ده، ولو الـ Sub-Batch مش محتاج طبقة معينة يتخطاها)
      - Migration (لو محتاج): Backup → Trial DB → Real DB
      - Tests: Targeted (الـ Batch نفسه) + Regression (IntegrationTests + ApiTests كاملين)
      - /clear بعد كل Batch
      - قول "خلصت B{X}.{Y}"
  - متسألنيش في نص الـ Phase — الأسئلة كلها في الـ Research Pass

STEP 3: تقرير Phase
  - اكتب: docs/Implementation/Phase-{X}-Final.md
  - قول "خلصت Phase X"
  - STOP
  - استنى "كمّل Phase X+1"
```

---

## 3. القواعد الإلزامية

**Methodology**:
- Research Pass قبل أي كود — مفيش استثناء حتى لو الـ Sub-Batch شكلها بسيطة.
- Batches صغيرة ومحددة النطاق (سطر واحد لكل Batch في التسليم: "خلصت B{X}.{Y}").
- Tests بعد كل Batch — Targeted أول، بعدين Regression كامل قبل الانتقال للـ Batch اللي بعده.
- `/clear` بين المهام (مش بين كل Batch وبس — لو الـ Context قرب 60% في نص Batch، `/compact` بدل الاستمرار).
- Backup إلزامي قبل أي Migration، بدون استثناء.
- Trial DB قبل Real DB — نفس البروتوكول المتبع من B1.
- افحص الكود الفعلي قبل أي افتراض معماري — لو حاجة في هذا الملف تبين إنها اتغيّرت في الكود الحقيقي، الأولوية للكود، والتغيير يتسجل في قسم 7 (Amendments Log).

**Quality Gates** (كل واحدة لازم تتحقق قبل "خلصت Phase X"):
- Build نظيف: 0 Errors, 0 Warnings.
- كل الاختبارات (Targeted + Regression) ناجحة.
- أي Migration قابلة للتراجع (Down method مكتوبة ومجرّبة).
- التوثيق محدّث (Phase-{X}-Final.md + أي ملف مرجعي زي `10-Module-HR-Payroll.md` لو التنفيذ كشف فرق عن التصميم).
- Regression Suite كامل (IntegrationTests + ApiTests) شغال بدون فشل.

**Stop Conditions**:
- بعد Research Pass → STOP (استنى "موافق").
- بعد كل Phase → STOP (استنى "كمّل Phase X+1").
- عند خطأ كبير (Migration فشلت، Regression كسرت، قرار معماري متضارب) → STOP فورًا وابلّغ قبل أي إصلاح.
- عند اقتراب الـ Context من 60% → `/compact`، أو STOP لو التنفيذ في نقطة حرجة (نص Migration مثلًا).

---

## 4. قائمة المراحل

### Phase 1.2 — Migration الحقول الحرة

**النطاق**: 4 حقول موظفين بدون FK حقيقي — `User.EmployeeId`، `CustodyRegister.EmployeeId`، `CustodyOfficer` (مفيش عمود أصلًا دلوقتي)، `MaintenanceRequest.TechnicianId` + `MaintenanceSchedule.TechnicianId`. **3 أنماط مختلفة**، مش نمط واحد متكرر — راجع الجدول الكامل في المرجع.

**قرار محسوم مسبقًا**: `CustodyRegister.EmployeeId` — **Option A**: عمود جديد `EmployeeIdLinked` (`long?`) جنب القديم، **من غير ما يتلمس العمود الأصلي خالص** ولا يتحط عليه FK دلوقتي. الربط يدوي بس (مفيش إشارة تكفي للمطابقة التلقائية) عن طريق شاشة `HR_CUSTODY_MIGRATION` جديدة.

Sub-Batches:
- **1.2.1** — Research Pass: فحص فعلي لبيانات الإنتاج (كام صف فاضي/غير منطقي في كل حقل)، وتأكيد قرار Option A لسه صالح.
- **1.2.2** — `CustodyOfficer` (+ `EmployeeId?` FK جديد) و`Maintenance.TechnicianId`/`TechnicianName` (FK بمطابقة بالاسم) — تقرير تشخيصي (`FreeFieldDiagnosticReport`) قبل أي FK، تطابق بيقين يترابط تلقائي والباقي يتعلّم لمراجعة يدوية.
- **1.2.3** — `CustodyRegister` (Option A): عمود `EmployeeIdLinked` + Banner تحذيري على شاشة العهد الحالية.
- **1.2.4** — Frontend للربط اليدوي: شاشة `HR_CUSTODY_MIGRATION` (تقرير تفاعلي + Dropdown بحث موظف حقيقي).
- **1.2.5** — `User.EmployeeId`: توثيق بس (قراءة فقط أثناء الانتقال، مايتكتبش فيه من HR — المرجع الرسمي `Employee.UserId`)، بدون تعديل كود فعلي إلا لو الـ Research كشف كتابة فعلية عليه لازم توقف.
- **1.2.6** — Tests + Docs: تحقق صريح إن `CustodyRegister.EmployeeId` القديم فضل بقيمته الأصلية بالظبط بعد الـ Migration، وإن `EmployeeIdLinked` كله `null` في أول نشر.

**مرجع**: `Docs/Implementation/HR-Core-Plan.md §1.2`.

---

### Phase 1.3 — الموظف كطرف في السندات

**النطاق**: `CounterpartyType.Employee = 3` موجود فعلًا في الـ Enum — **مفيش تعديل Enum مطلوب**. النقطة المعطّلة الوحيدة: `CounterpartyAccountResolver.cs` بيرمي `ACC-COUNTERPARTY-RESOLUTION-PENDING` لأي نوع غير Customer/Supplier. الموظف **مالوش حساب فرعي مستقل** (خلافًا لـ Customer/Supplier) — حساب تحكم واحد على مستوى الشركة (`CompanyAccountRole.EmployeeReceivable`، موجود ومستخدم بالفعل)، والتفصيل لكل موظف من سطور المعاملة نفسها.

Sub-Batches:
- **1.3.1** — Research Pass: تأكيد موقع الفرع الناقص في `CounterpartyAccountResolver.cs` (حواليّ سطر 46-50) لسه صحيح.
- **1.3.2** — إضافة فرع `CounterpartyType.Employee`: يرجّع `CompanyAccountMapping` حيث `Role == EmployeeReceivable`، ولو مش مسجّل بعد → خطأ واضح (`ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED`) بدل الرسالة العامة.
- **1.3.3** — Tests: سند بطرف Employee + Mapping مربوط = ترحيل ناجح عن طريق `IPostingService.PostAsync` بدون تعديل الواجهة · اختبار سلبي بدون Mapping.
- **1.3.4** — Docs.

**Migration**: لأ — الحساب بيتسجّل عن طريق شاشة `CompanyAccountMapping` الموجودة.

**مرجع**: `Docs/Implementation/HR-Core-Plan.md §1.3`.

---

### Phase 1.4 — إنهاء الخدمة الإداري (مبسّط — ليس الحساب المالي الكامل)

> ⚠️ **توضيح نطاق مهم**: "الكامل" هنا يقصد بيه **كل خطوات التعطيل الإداري** (المستخدم + الصلاحيات + فحص العهدة)، **مش** حساب المستحقات المالية (بدل إجازات، تعويض، مكافأة تعاقدية) — ده منفصل تمامًا وموثّق كـ **Phase 5** (`EmployeeEndOfService` + تشغيل `FinalSettlement`). أي دمج بين الاتنين وقت الـ Research Pass لازم يتوقف ويتراجع للمستخدم.

**الوضع الفعلي بعد Phase 1.1**: `TerminateEmployeeCommand` **موجود ومبني بالفعل** (B5) وبيعمل: إيقاف الـ User (`Status = Suspended`)، سحب الجلسات (`UserRules.RevokeSessionsAsync` — أُصلحت بعد B7)، تعطيل كل `UserScope` في الشركة، إلغاء `ShiftAssignments` المستقبلية، فحص عهدة مفتوحة (`CustodyRegister.Status == Open` فقط حاليًا)، إغلاق `EmploymentContract` النشطة. **الباقي فعليًا من هذه المرحلة**: توسيع فحص العهدة ليشمل `CustodyOfficer`/`FixedAsset.CustodyOfficerId` بعد ما Phase 1.2 يضيف `CustodyOfficer.EmployeeId`.

Sub-Batches:
- **1.4.1** — Research Pass: تأكيد إن Phase 1.2 خلصت (شرط لفحص عهدة الأصول)، وحصر الفجوة الفعلية المتبقية بدقة (مش افتراض).
- **1.4.2** — توسيع فحص العهدة في `TerminateEmployeeCommandHandler` ليشمل `CustodyOfficer.EmployeeId` → `FixedAsset.CustodyOfficerId` (قاعدة 43 في `10-Module-HR-Payroll.md`).
- **1.4.3** — Tests: موظف عليه أصل عهدة عليه (عن طريق `CustodyOfficer`) = الإنهاء يُرفض لحد ما يتسوّى.
- **1.4.4** — Docs: تحديث `Docs/Implementation/HR-Core-Plan.md §1.4` بحالة "منفّذة بالكامل ضمن نطاقها المبسّط".

**Migration**: لأ.

**مرجع**: `Docs/Implementation/HR-Core-Plan.md §1.4` · `Docs/Modules/10-Module-HR-Payroll.md` قاعدة 43-44.

---

### Phase 1.5 — شاشات HR Core (Frontend)

**النطاق**: 8 Lookups بنمط Hybrid (List/Edit كامل)، الموظف (List/Edit + تبويبات)، 3 تبويبات للتوابع، ومعالج التعيين (أول Wizard في المشروع).

| الشاشة | `Screen.Code` | ملاحظة |
|---|---|---|
| الموظفين | `HR_EMPLOYEES` | تبويبات: بيانات، شخصية (PII `Last4` + زرار إظهار)، عقود، مستندات، شهادات |
| الهيكل التنظيمي | `HR_ORG_UNITS` | شجرة — نمط `ChartOfAccountsPage.tsx` (`useAccountTree`) |
| الوظائف / الدرجات | `HR_JOB_POSITIONS` / `HR_JOB_GRADES` | Lookup بسيط |
| أنواع المستندات | `HR_DOCUMENT_TYPES` | Lookup بسيط |
| مكاتب التأمينات | `HR_INSURANCE_OFFICES` | Lookup بسيط |
| Countries/Cities/Banks | `SETTINGS_COUNTRIES`/`SETTINGS_CITIES`/`SETTINGS_BANKS` | تحت `SETTINGS` مش `HR` (نفس مكان Companies/Currencies) |
| التعيين | `HR_HIRING` | **Wizard** — مفيش قالب موجود، محتاج Design Spike قبل البناء الكامل |
| إعدادات HR | `HR_SETTINGS` | حقول المرحلة 1 بس (`EmployeeCodePrefix`, `EmployeeCodeSequence`, `DefaultProbationDays`, `DefaultBranchId`, `RequireNationalIdForActivation`) — باقي حقول `HrSettings` موجودة في الكيان بس مؤجلة لمراحلها |

> **محدّث 2026-09-27 (بعد Research Pass، `Phase-1.5-Research.md`)**: 9 من 11 شاشة جاهزة Backend-wise فورًا. فيه فجوتين Backend حقيقيتين اتحلّوا بالقرارات دي: **`HrSettings` الكيان نفسه مش موجود خالص** (رغم إن `HR-Core-Plan.md` كانت بتفترض إنه مبني من 1.1) → Sub-Batch جديد **1.5.0** لبنائه. و**خطوة "تأمينات" في Wizard التعيين مالهاش أي كيان تكتب فيه** (مفيش ربط `Employee`↔`InsuranceOffice` خالص، و`EmployeeSocialInsurance` الحقيقي مؤجل لـ Phase 5) → اتشالت من الـ Wizard دلوقتي. وخطوة "راتب" مش لازم تكون خطوة مستقلة — `BasicSalary`/`InsurableWage` موجودين بالفعل كأعمدة على `EmploymentContract` (خطوة "عقد"). كمان اتأكد إن مفيش أي مكوّن Tabs داخل الصفحة في المشروع كله (شاشة الموردين المرجعية مالهاش تبويبات) — هيتبنى Component عام في `ui-kit` قبل 1.5.3.

Sub-Batches:
- **1.5.0** (جديد) — `HrSettings` Backend: الكيان (صف واحد لكل شركة، نفس نمط `PurchaseCycleSettings`) + Migration + Application (Get/Update) + Controller + تسجيل `HR_SETTINGS` في `MenuItemSeedData.cs`. **حقول المرحلة 1 المعروضة 3 بس** (مش 5): `DefaultProbationDays`, `DefaultBranchId`, `RequireNationalIdForActivation` — `EmployeeCodePrefix`/`EmployeeCodeSequence` اتشالوا نهائيًا من الكيان (آلية `ICodeGenerator`/`CodingRule` العامة الموجودة بالفعل تحت `Screen.Code = HR_EMPLOYEES` بتغطي نفس الوظيفة، تكرار مصدر حقيقية غير مطلوب). باقي الحقول المؤجلة (`MonthBasis`, `DefaultCutoffDay`, `KioskSessionSeconds`, `SelfServiceClockInRequiresLocation`, `CompanyDefaultApproverUserId`, `MaxAdvanceInstallmentPercent`) تُبنى على الكيان الآن Nullable/بقيمة افتراضية بس بدون شاشة (`DefaultCutoffDay` أُضيف هنا لأنه موجود في `10-Module-HR-Payroll.md:130` كحقل فعلي على `HrSettings` ومكنش مسجّل في جدول `HR-Core-Plan.md §1.5` الأصلي).
- **1.5.1** — Research + Design: Design Spike لمعالج `HR_HIRING` (أول Wizard في المشروع — قرار نمط التنقل بين الخطوات قبل أي كود) + Design صغير لمكوّن Tabs عام (`ui-kit`) قبل استخدامه في 1.5.3.
- **1.5.2** — Lookup Screens (8، شامل Countries/Cities/Banks تحت Settings).
- **1.5.3** — الموظف (List + Edit + التبويبات الأساسية، باستخدام مكوّن Tabs الجديد من 1.5.1).
- **1.5.4** — 3 تبويبات التوابع (عقود، مستندات، شهادات) داخل شاشة الموظف. **ملاحظة (أُضيفت 2026-09-27)**: تبويب "ماكينة البصمة" هيتضاف لاحقًا في Phase 3B.7 — يحتاج كيانات Phase 3B (`AttendanceDevice`/`EmployeeDeviceMapping`) اللي لسه مش موجودة وقت 1.5.4، فمش من نطاق هذا الـ Sub-Batch.
- **1.5.5** — معالج التعيين (Hiring Wizard): موظف ← عقد (شامل الراتب الأساسي كجزء من العقد، مش خطوة مستقلة) ← مستندات ← مستخدم. **خطوة "تأمينات" اتشالت** — تُبنى لاحقًا في Phase 5 مع `EmployeeSocialInsurance` الحقيقي.
- **1.5.6** — Tests + Docs.

**مرجع**: `Docs/Implementation/HR-Core-Plan.md §1.5` · `Docs/Modules/10-Module-HR-Payroll.md §5.1` (بند 1-5).

---

### Phase 2 — محرك الاعتمادات v1

> **يخدم النظام كله — مش HR بس.** لازم قبل Phase 3 لأن `LeaveRequest`/`OvertimeRequest` محتاجين `ApprovalInstanceId`، ولازم بعد Phase 1 لأن `ApproverType.DirectManager`/`JobGrade` محتاجين `Employee` موجود فعلًا.

**النطاق**: Screen Registry موحّد (بيستبدل مطابقة النص الحالية في `PostingTemplate.ScreenCode`) + `ApprovalWorkflow` (بإصدارات زي `PostingTemplate`) + `ApprovalWorkflowStep` + `ApprovalStepApprover` (4 أنواع: `SpecificEmployee`/`Role`/`DirectManager`/`JobGrade`) + `ApprovalWorkflowAssignment` + `ApprovalInstance` + `ApprovalAction`.

**قواعد جوهرية** (`00-Project-Overview.md §12.4`):
- رفض أي خطوة = رفض نهائي (مستند جديد يبدأ سلسلة جديدة).
- `ApprovalStepMode`: `AnyOne`/`All` بس — **مفيش `Sequential` ولا `RequireMajority`** (الترتيب بين الخطوات هو `StepOrder`، مش Mode).
- **Self-Approval Guard إلزامي**: مستخدم لا يعتمد طلبه هو نفسه تحت أي ظرف.
- `DirectManager` بيتحل وقت التنفيذ الفعلي مش وقت التصميم؛ لو `ManagerId = null` يتصعّد لـ `CompanyDefaultApproverUserId` (مش تخطي ولا رفض تلقائي).
- `JobGrade` من غير أي موظف مُسنَد = تنبيه إعداد صريح، مش تخطي صامت.
- إجراء يدوي احتياطي لإعادة تسكين طلب معلّق لمعتمد آخر (من الإصدار الأول، قبل `Delegation`/`Escalation` الكاملين — دول مؤجّلين عمدًا).
- كل كيان يمر بالسلسلة لازم يحمل `Rejected` صراحة في الـ Enum بتاعه، والرفض ينعكس على حالة الكيان في نفس الـ Transaction.

Sub-Batches:
- **2.1** — Research + Design.
- **2.2** — Domain (6 كيانات: `Screen`, `ApprovalWorkflow`, `ApprovalWorkflowStep`, `ApprovalStepApprover`, `ApprovalWorkflowAssignment`, `ApprovalInstance`, `ApprovalAction` — لاحظ العدد الفعلي 7 كيانات مش 6، يتأكد منه في الـ Research).
- **2.3** — Application (Commands + Queries، شامل حل `DirectManager`/`JobGrade` الديناميكي والـ Self-Approval Guard).
- **2.4** — API.
- **2.5** — Frontend (شاشة إعدادات السلاسل + شاشة "بانتظار اعتمادي" الموحّدة — `00-Frontend-Specs.md §18`).
- **2.6** — Integration: استبدال `NullApprovalWorkflowService` الحالي، وربط `IPostingService` برفض ترحيل أي مستند لسه مش معتمد بالكامل.
- **2.7** — Tests + Docs: حالات إلزامية — مدير فرع بيطلب سلفة لنفسه (الحارس يمنعه)، `ManagerId = null` → معتمد احتياطي، مدير في إجازة → إسناد يدوي، `JobGrade` فاضية → تنبيه.

**مرجع**: `Docs/Modules/00-Project-Overview.md §12`.

---

### Phase 2.5 — إشعارات داخل التطبيق (Minimal In-App Notifications)

> **أُضيفت 2026-09-27** — فجوة حقيقية: Phase 3 → 5 (الحضور، الرواتب، إنهاء الخدمة) كلها بتعتمد
> على محرك الاعتمادات (Phase 2)، والمعتمد لازم يعرف إن فيه طلب مستني اعتماده من غير ما يفتح شاشة
> "بانتظار اعتمادي" بنفسه كل شوية. **Phase 6 فيها نظام إشعارات كامل** (Email/SMS/Push + Templates
> + Preferences + Digest) لكنها متأخرة جدًا في الترتيب. القرار: نسخة داخل التطبيق فقط (In-App)
> هنا، بين Phase 2 و Phase 3، تُبنى عليها Phase 6 لاحقًا بدل ما تتبنى من الصفر.

**النطاق (In-App بس — بدون Email/SMS/Push، بدون SignalR)**:
- `Notification` entity (`RecipientUserId`, `Type`, عناوين/محتوى عربي+إنجليزي، **`RequiresAction`**،
  `RelatedEntityType`/`RelatedEntityId` اختياريين للـ Deep Linking، `IsRead`/`ReadAtUtc`).
- `NotificationType` enum (10 قيم — تفصيل كامل في طلب المستخدم/`Phase-2.5-Research.md`).
- `INotificationService` (`SendAsync`/`SendToMultipleAsync`) + أحداث داخل العملية (in-process، من
  غير MediatR handlers منفصلة) تُطلَق من محرك الاعتمادات (Phase 2): بدء سلسلة، اكتمال خطوة، رفض،
  إعادة تسكين.
- API: `/api/v1/notifications` (List بفلاتر، `unread-count`، **`pending-action-count`**، تفاصيل،
  تعليم مقروء/الكل، حذف).
- Frontend: `NotificationBell` في الـ Navbar (ظاهر بكل الشاشات) — **الـ Badge = عدد
  `RequiresAction = true` بس، مش كل غير المقروء** (فرق جوهري، تفصيل في طلب المستخدم) — + Dropdown
  لآخر 10 إشعارات + `NotificationsPage` (`/notifications`) كاملة. Polling كل 30-60 ثانية، بدون
  SignalR (نفس قرار 2.5 Frontend في Phase 2 — لا SignalR في المشروع أصلًا).

**قاعدة جوهرية**: الإشعار مايحمّلش PII — عنوان/محتوى عمومي + رابط للتفاصيل الفعلية.

Sub-Batches:
- **2.5.1** — Research Pass.
- **2.5.2** — Domain + Migration.
- **2.5.3** — Application + API (شامل ربط أحداث محرك الاعتمادات من Phase 2).
- **2.5.4** — Frontend (`NotificationBell` + `NotificationsPage` + تكامل `AppLayout`).
- **2.5.5** — Tests + Docs.

**مرجع**: تفاصيل الحقول/الـ Endpoints/متطلبات الـ Frontend الكاملة في طلب المستخدم الأصلي
(محفوظ في محادثة الجلسة) و`Docs/Implementation/Phase-2.5-Research.md`. **يعتمد على**: Phase 2
(الأحداث بتتطلق من محرك الاعتمادات). **تسبق**: Phase 3 (طلبات الإجازة/الإضافي محتاجة تنبيه
المعتمد). Phase 6 تبني فوق هذا الأساس (لا تعيد بناءه) عند إضافة Email/SMS/Push/Templates/Preferences.

---

### Phase 3 — الحضور والإجازات

**النطاق**: `WorkShiftDefinition`, `ShiftSchedule`, `TimeEntry`, `Attendance`, `LeaveType`, `LeaveBalance`, `LeaveBalanceHistory`, `LeaveRequest`, `Holiday`, `OvertimeRequest` (10 كيانات).

> **ملاحظة (أُضيفت 2026-09-27)**: `TimeEntry.Source = Device` (بصمة) **مش من نطاق هذه المرحلة** — بيتبنى في **Phase 3B** (تكامل أجهزة البصمة، بعد Phase 3). الـ`TimeEntry` اللي Phase 3 نفسها بتبنيها مصدرها `Manual`/`SelfService`/`Kiosk`/`POSShift` بس.

**قواعد جوهرية** (`10-Module-HR-Payroll.md` قواعد 10-27):
- `TimeEntry` مصدر الحقيقة الوحيد للحضور — **مابيتعدّلش ولا بيتمسح**، التصحيح بصف جديد (`IsCorrection`/`CorrectsTimeEntryId`).
- الوردية (`POS.Shift`) بتلقّم `TimeEntry` تلقائيًا (فتح=دخول مقترح، قفل=خروج مقترح) **بدون أي تعديل على `POS.Shift`** نفسه — التلقيم بحدث بعد الحفظ بس.
- تقرير تعارض يومي (كاشير فاتح وردية وحضوره Absent) — كاشف احتيال مش مجرد تدقيق.
- `Attendance` (الملخص اليومي المحسوب من `TimeEntry`) هو **الوحيد** اللي بيدخل الرواتب لاحقًا.
- استحقاق الإجازة الشهري بـ Job Idempotent لكل موظف وشهر؛ `AnnualDays` الفعلي من `LeaveEntitlementRule` (Phase 4) مش `LeaveType.AnnualDays` مباشرة.
- كل تغيير في الرصيد = صف `LeaveBalanceHistory`؛ `LeaveBalance` مايتعدّلش مباشرة أبدًا.

Sub-Batches:
- **3.1** — Research Pass.
- **3.2** — Domain (10 كيانات).
- **3.3** — Commands + Queries (شامل `LeaveRequest`/`OvertimeRequest` مع `ApprovalInstanceId` من Phase 2).
- **3.4** — API.
- **3.5** — POS Integration (Shift → TimeEntry، تقرير التعارض اليومي).
- **3.6** — Frontend.
- **3.7** — Tests + Docs.

**مرجع**: `Docs/Modules/10-Module-HR-Payroll.md §2.2` + قواعد 10-27 + §4.2. **يعتمد على**: Phase 2 (سلاسل الاعتماد).

---

### Phase 3B — تكامل أجهزة البصمة (Fingerprint Device Integration)

> **أُضيفت 2026-09-27** — فجوة في الخطة الأصلية: Phase 3 بتذكر `TimeEntry.Source = Device` كقيمة Enum موجودة (`10-Module-HR-Payroll.md` قرار 4)، لكن من غير أي تصميم تكامل فعلي خلفها. مهمة لأن **90% من الكافيهات المصرية بتستخدم أجهزة بصمة** (ZKTeco غالبًا) — بدونها، تسجيل الحضور هيفضل يدوي/Kiosk بس رغم إن العميل أصلًا عنده جهاز بصمة شغّال. **متلمسش Phase 3 الحالية ولا كياناتها** — دي مصدر إضافي لـ`TimeEntry`، مش إعادة بناء.

**النطاق**: أربع كيانات جديدة —
- `AttendanceDevice` — سجل الأجهزة (الموديل، الفرع، الحالة).
- `AttendanceDeviceLog` — سجل عمليات الـ Sync (نجاح/فشل، عدد البصمات المستلمة، وقت آخر اتصال).
- `RawPunch` — بيانات البصمة الخام كما وصلت من الجهاز، قبل أي معالجة أو تفسير.
- `EmployeeDeviceMapping` — ربط `Employee` (من Phase 1.1) برقم المستخدم (`UserId` على الجهاز نفسه، مش نفس `User.Id` في النظام).

**التكامل** (القرار موثّق بالتفصيل في `Docs/ERP-Review/12-HR-Payroll-Analysis.md §4.2`):
- **Push SDK (الأساسي)**: الجهاز بيبعت كل بصمة لحظة حدوثها بـ HTTP POST لواجهة مخصصة — Real-time. يبدأ بـ **ZKTeco** (الأكتر انتشارًا في السوق المصري)؛ **Suprema** (BioStar 2) لاحقًا، مش في الإصدار الأول.
- **File Import (Fallback، شغّال من أول يوم)**: استيراد ملف XLS/CSV من الجهاز عن طريق USB — لأي جهاز مش بيدعم Push، أو وقت عطل الشبكة. **مش خيار ثانوي مؤجّل — لازم يشتغل من اليوم الأول** لأنه أبسط طريق يضمن استمرار تسجيل الحضور بغض النظر عن حالة الشبكة/الجهاز.
- **Pull via SDK (مؤجّل)**: يحتاج Local Agent (برنامج على جهاز في الفرع يسحب من الجهاز فعليًا بدل ما الجهاز يبعت هو) — برّه نطاق هذه المرحلة تمامًا.

**Jobs**:
- `RawPunch → TimeEntry`: Job دوري (كل 5-15 دقيقة) بيحوّل البصمة الخام لـ`TimeEntry` حقيقي (`Source = Device`) — بنفس مبدأ الـ Idempotency المتبع في `DepreciationRunKeys.For` (Fixed Assets، موديول 08).
- **كشف تكرار**: نفس البصمة (نفس الجهاز/الموظف/اللحظة تقريبًا) من مصدرين (Push مزدوج، أو Push + Import لنفس البيانات) ما تتحولش لأكتر من `TimeEntry` واحد.
- **Reconciliation**: تقرير مطابقة بين `RawPunch` والـ`TimeEntry` الناتجة — بيوضح أي بصمة خام ما اتحولتش لسبب (موظف مش مربوط في `EmployeeDeviceMapping`، جهاز مش مسجّل في `AttendanceDevice`...).

Sub-Batches:
- **3B.1** — Research Pass: مسح أجهزة السوق المصري (ZKTeco تحديدًا أولًا) — بروتوكول الـ Push الفعلي، شكل البيانات الخام، أي SDK/مكتبة رسمية متاحة.
- **3B.2** — Domain (الأربع كيانات: `AttendanceDevice`, `AttendanceDeviceLog`, `RawPunch`, `EmployeeDeviceMapping`).
- **3B.3** — Push API + Auth (Endpoint يستقبل من الجهاز مباشرة — مصادقة مخصصة للجهاز، مش JWT مستخدم عادي).
- **3B.4** — RawPunch Processing Job (التحويل لـ`TimeEntry` + كشف التكرار + الـ Idempotency).
- **3B.5** — File Import (الـ Fallback الشغّال من اليوم الأول).
- **3B.6** — Frontend (إدارة الأجهزة + سجل الـ Sync/الأخطاء).
- **3B.7** — تحديث شاشة الموظف (Employee Fingerprint Tab، أُضيفت 2026-09-27): تبويب "ماكينة البصمة" جديد في `EmployeeEditPage` (بُنيت في Phase 1.5.3/1.5.4) — نفس نمط تبويبات العقود/المستندات/الشهادات (Phase 1.5.4) بالحرف. يعرض الأجهزة المرتبطة بالموظف (`EmployeeDeviceMapping`)، إضافة/تعديل/حذف الربط، وعرض آخر Punches (Read-only). **Validation**: `DeviceUserId` مش مكرر (نفس الجهاز، رقم مستخدم مكرر لموظفين مختلفين = رفض).
- **3B.8** — Tests + Docs.

**Migration**: نعم — 4 جداول جديدة.

**مرجع**: `Docs/ERP-Review/12-HR-Payroll-Analysis.md §4.2` (قرار "ربط أجهزة البصمة") · `Docs/Modules/10-Module-HR-Payroll.md` قرار 4 (`TimeEntrySource.Device`). **يعتمد على**: Phase 3 (`TimeEntry`/`Attendance` لازم يكونوا موجودين الأول — هذه المرحلة بتغذي مصدر إضافي بس، مش إعادة بناء الكيانات الأساسية). **الترتيب**: بعد Phase 3 مباشرة، قبل Phase 4.

---

### Phase 4 — الرواتب

**النطاق** (محدّث بعد `Phase-4-Research.md` والقرارات المعتمدة 2026-09-28): **12 جدول قانوني مؤرّخ** —
`MinimumWage`, `SocialInsuranceRate`, `InsurableWageLimit`, `PayrollTaxBracketSet`+`PayrollTaxBracket`
(جدولين حقيقيين، العدد النهائي 10 مش "9 أو 10")، `MartyrsFundRate`, `OvertimeRate`,
`LeaveEntitlementRule`, `PenaltyDeductionCap`, `NoticePeriodRule`، + **`EndOfServicePolicy`** (أُضيف
هنا رغم إنه مش معدود صراحة في أي Phase أصلًا — Phase-4-Research.md §2.2 — لأن قالب مخصص نهاية الخدمة
الشهري محتاج `PolicyType` عشان يقرر يرحّل ولا لأ) + **`OvertimeLimitRule`** (جدول 12، اتكشف لازم
أثناء بناء المحرك نفسه في 4.4 — رقم 20 بيسمّيه "جدول مؤرّخ" منفصل بس §2.4 ماسردوش بالاسم أصلًا) —
+ `SalaryComponent`/`SalaryStructure`(+Line)/
`EmployeeSalary` + `PayrollPeriod`/`PayrollRun`/`PayrollLine`/`Payslip` + **`EmployeeTaxProfile`**
(أُضيف هنا من Phase 5 — قاعدة 37 والـ`AnnualTaxSettlement` محتاجينه فعليًا للتراكم السنوي، والقسائم
بعد الاعتماد بتتجمّد فمفيش مجال لتقريب مؤقت يتصحح لاحقًا) + `TipsDistribution`+`TipsDistributionLine`
(أُضيفوا هنا مش Phase 5 — Phase-4-Research.md §2.4، رقمهم جوه مدى قواعد الرواتب 28-40 والقالب السابع
من §6.2) + محرك الحساب + التكامل المحاسبي.

**`PayrollLine.SalaryComponentId` قرار: Nullable** (مش إلزامي زي ما كان مخطط بادئ ذي بدء) — الخصومات
القانونية (تأمينات/ضريبة/صندوق شهداء) مالهاش `SalaryComponent` حقيقي أصلًا (`CalculationMethod`
الحالي: `Fixed`/`PercentOfBasic`/`Hourly`/`Formula` — ولا واحدة فيهم تمثّل "من جدول قانوني")، فبتتسجّل
بـ`SalaryComponentId = null` ومصدرها من enum جديد **`PayrollLineSource`** (`EmployeeSalary`/
`Overtime`/`Tips`/`Penalty`/`AdvanceInstallment`/`LegalSocialInsurance`/`LegalTax`/`LegalMartyrsFund`/
`Absence`/`PriorPeriodAdjustment`) بدل نص حر.

**قواعد جوهرية** (قواعد 28-40):
- المعادلة: أساسي + بدلات (`EmployeeSalary`) + إضافي (`Attendance`/`OvertimeRequest`) + بقشيش
  (`TipsDistribution`) − غياب (`Attendance`) − جزاءات (`EmployeePenalty`، **Phase 5 — صفر فعليًا لحد
  ما الكيان يتبني**) − أقساط سلف (`AdvanceInstallment`، **Phase 5 — صفر فعليًا**) − تأمينات
  (`LegalSocialInsurance`) − ضريبة (`LegalTax`، عن طريق `EmployeeTaxProfile` التراكمي) − صندوق شهداء
  (`LegalMartyrsFund`) = صافي.
- كل `PayrollLine` بيحتفظ بـ `RateSnapshot` (JSON، `System.Text.Json` يدوي — مفيش `HasConversion`
  جاهز في الكود) — إعادة طباعة قسيمة قديمة بتطلع زي ما هي حتى لو الجداول اتغيّرت.
- **🔴 Idempotency إلزامي على 3 مستويات**: مفتاح التشغيل (`PayrollRun.IdempotencyKey`، نفس نمط
  `DepreciationRunKeys.For`) + مفتاح الترحيل (`PostingKeys.For(..., "PayrollRun.Post", ...)`) + مفتاح
  الصرف (`"PayrollRun.Pay"`). `Regular` واحد بس لكل شركة وشهر؛ `Supplementary`/`FinalSettlement`
  بـ `ReferenceId`؛ `AnnualTaxSettlement` واحد لكل سنة.
- Cutoff: بعد `PayrollPeriod.CutoffDate` الفترة `Locked`، وأي إدخال متأخر = سطر تسوية في الشهر اللي
  بعده (`PayrollLineSource.PriorPeriodAdjustment`، مفيش إعادة حساب لتشغيل مرحّل).
- مراجعة استثناءات إلزامية قبل الاعتماد (صافي سالب، تحت الحد الأدنى، تغيّر مفاجئ، موظف `Active` بدون
  تسجيل تأميني) — بتطلق `NotificationType.PayrollExceptionReview` (موجود من Phase 2.5، Schema-ready
  من غير أي مُرسِل لحد دلوقتي).
- خطوة اعتماد إضافية على سلسلة الإضافي لو `OvertimeRequest` تجاوز الحد اليومي أو الشهري (`OvertimeLimitRule`
  الجدول 12، مؤجّلة من Phase 3، `Phase-3-Final.md §7` بند 5) — **مش خطوة `ApprovalWorkflow` حقيقية
  تانية**: `ApprovalWorkflowAssignment` دلوقتي مستوى واحد بس لكل شاشة (Phase-4-Research.md §1.8)،
  فمفيش طريقة تعبّر عن "خطوة إضافية بس لو تجاوز". البديل المطبَّق: `OvertimeRequest.ExceedsLimit` +
  `HrOverrideApprovedByUserId` — تشغيل الرواتب بيتجاهل أي طلب متجاوز من غير Override صريح من HR.

**التكامل المحاسبي** (محدّث بعد 4.5، `IPostingTemplateEngine`، `CostCenterId` عن طريق Dimensions
[`PostingGroupItem.Dimensions`] مش عمود مفرد، Phase-4-Research.md §1.2) — **قالبين حقيقيين فعليًا
اتبنوا** على شاشة واحدة (`PAY_PAYROLL_RUNS`، حقل `Stage` بيفرّق بينهم): **استحقاق الرواتب** (وقت
الاعتماد، بمركز تكلفة لكل موظف) و**صرف الرواتب** (وقت الصرف، `CompanyAccountRole.Cash` مباشرة —
سند صرف، v1 حسب رقم 40). البقشيش (رقم 39) **جوه قيد الاستحقاق نفسه** (تسوية `TipsPayable`→`SalariesPayable`)،
مش قالب مستقل — نص §6.2 نفسه: "داخل قيد الاستحقاق". **باقي القوالب الأربعة الأصلية اتأجّلت** (تفصيل
في §7 Amendments Log): صرف سلفة (Phase 5 مع `EmployeeAdvance`)، تسوية نهاية الخدمة (Phase 5 مع
`EmployeeEndOfService`)، ومخصص الإجازات الشهري ومخصص نهاية الخدمة الشهري (الاتنين محتاجين استعلام
رصيد فعلي من دفتر الأستاذ — "قيد الفرق" — مش مبني في المشروع، وبناءه بشكل خاطئ (إضافة كامل المبلغ كل
شهر بدل الفرق) هيطلّع رصيد التزام غلط في الميزانية بمرور الوقت — قرار مؤجّل عمدًا، مش نسيان).

Sub-Batches:
- **4.1** — Research Pass ✅ (`Phase-4-Research.md`، تأكيد 10 جداول قانونية حقيقية).
- **4.2** — Legal Tables ✅ (11 جدول شامل `EndOfServicePolicy` + نمط `AsOf(date)`/
  `IEffectiveDatedEntity` من الصفر).
- **4.3** — Domain (`SalaryComponent`/`SalaryStructure`+Line/`EmployeeSalary`/`PayrollPeriod`/
  `PayrollRun`/`PayrollLine`/`Payslip` + `TipsDistribution`+Line + **`EmployeeTaxProfile`** [مسحوب من
  Phase 5] + enum **`PayrollLineSource`**).
- **4.4** — Payroll Engine (المحرك + الـ Idempotency الثلاثي + مراجعة الاستثناءات + خطوة اعتماد
  الإضافي فوق الحد).
- **4.5** ✅ — Posting Integration (11 دور جديد في `CompanyAccountRole` + قالبين حقيقيين
  [استحقاق/صرف، Stage-gated] — التفاصيل والتأجيلات في §7 Amendments Log).
- **4.6** — API (Controllers + تسجيل الشاشات + `FieldPermissionCatalog` + `HrSettings`: 3 حقول).
- **4.7** — Frontend (شاشات الرواتب + خطوة "راتب" جديدة في `HR_HIRING` Wizard).
- **4.8** — Tests + Docs.

**مرجع**: `Docs/Modules/10-Module-HR-Payroll.md §2.3` + §2.4 + قواعد 28-40 + §4.3 + §6.2 +
`Docs/Implementation/Phase-4-Research.md`. **يعتمد على**: Phase 3 (`Attendance` بتغذي الحساب) +
Phase 2 (اعتماد التشغيل).

---

### Phase 5 — إنهاء الخدمة الكامل والسلف والجزاءات

> **هنا** — مش Phase 1.4 — مكان الحساب المالي الكامل لإنهاء الخدمة.

**النطاق**: `EmployeeAdvance`+`AdvanceInstallment`, `EmployeePenalty`, `EmployeeBonus`, `EmployeeDeduction`, `EmployeeEndOfService`, `EndOfServiceHeir`, `EmployeeSocialInsurance` (+ `PenaltyType`/`BonusType`/`DeductionType` Lookups). **`EmployeeTaxProfile` مش هنا** — اتسحب لـ Phase 4.3 (2026-09-28، قسم 7 Amendments Log) لأن قاعدة 37 و`AnnualTaxSettlement` محتاجينه فعليًا وقت حساب الرواتب العادي، مش بس وقت التسوية.

**قواعد جوهرية** (قواعد 36، 41-49):
- مستحقات نهاية الخدمة مكوّنات صريحة (`LeaveCashOut`, `NoticePay`, `Compensation`, `ContractualGratuity`) — `ContractualGratuity` بس لو `EndOfServicePolicy.PolicyType = Contractual` (**⏸️ Pending Legal**).
- الحساب بيطلع من تشغيل `FinalSettlement` (نوع من `PayrollRun`)، مش حساب منفصل.
- الإنهاء **مايتعتمدش نهائيًا** طول ما فيه عهدة مفتوحة أو أصل عهدته على الموظف، إلا لو اتسوّت.
- حالة الوفاة: `BeneficiaryType = Heirs`، الطرف الدائن `HeirsPayable` بتوزيع `EndOfServiceHeir`.
- السلفة: مفيش سلفة جديدة مع سلفة مفتوحة (إلا بصلاحية استثنائية)، القسط ≤ `MaxAdvanceInstallmentPercent`.
- الجزاء محتاج `InvestigationNotes` قبل الاعتماد.

**سلاسل اعتماد هذه المرحلة** (`§4.8`): سلفة (`DirectManager`→HR→مالية لو ≥ حد)، جزاء (`DirectManager`→HR)، مكافأة (`DirectManager`→HR→مالية→مدير عام لو > حد)، تعديل راتب (`DirectManager`→HR→مالية، دايمًا)، إنهاء خدمة (`DirectManager`→HR→مالية، دايمًا)، تسوية عهدة (`DirectManager`→مالية).

Sub-Batches:
- **5.1** — Research Pass.
- **5.2** — Domain (9 كيانات).
- **5.3** — Commands (شامل `EmployeeEndOfService` الكامل وربطه بتشغيل `FinalSettlement`).
- **5.4** — API.
- **5.5** — Frontend.
- **5.6** — Tests + Docs (شامل حالة الوفاة والورثة، وحالة مدير الفرع بيطلب سلفة لنفسه).

**مرجع**: `Docs/Modules/10-Module-HR-Payroll.md §2.3` + قواعد 36، 41-49 + §4.4، §4.6 + §4.8. **يعتمد على**: Phase 4 (`FinalSettlement` نوع `PayrollRun`) + Phase 2 (سلاسل الاعتماد).

---

### Phase 6 — بوابة الخدمة الذاتية

**النطاق**: `EmployeeRequest` (رأس موحّد لكل الطلبات) + `CertificateRequest` + `EmployeeSelfUpdate` + `KioskDevice` + `EmployeeKioskPin` + ESS API (نطاق `Self`/`Team`) + PWA Frontend (`/ess`، شاشات 26-35) + وضع الكشك + الخدمة الذاتية للمدير.

**قواعد جوهرية**:
- كل اعتماد عن طريق المحرك المركزي — **مفيش `RequestApproval` منفصل**.
- `EmployeeSelfUpdate` مابيتطبّقش إلا بعد الاعتماد؛ تغيير الحساب البنكي محتاج اعتماد HR + تأكيد 2FA + إشعار على القناة القديمة (قاعدة 48).
- كشك الفرع: دخول برقم موظف + PIN (هاش)، جلسة بتخلص بعد مهلة قصيرة ومحدودة الشاشات (حضور، قسيمتي، إجازاتي، طلب إجازة بس) — **مفيش تخزين محلي لأي بيانات** (لا Cache ولا IndexedDB ولا localStorage للبيانات، الـ Service Worker للأصول الثابتة بس).
- كل Endpoint في الخدمة الذاتية له اختبار API صريح: موظف أ بيطلب بيانات موظف ب = `404`.

Sub-Batches:
- **6.1** — Research Pass.
- **6.2** — ESS API.
- **6.3** — PWA Frontend.
- **6.4** — Kiosk Mode.
- **6.5** — Manager Self-Service (نطاق Team: لوحة فريقي، حضور وإجازات الفريق).
- **6.6** — Tests + Docs.

**مرجع**: `Docs/Modules/10-Module-HR-Payroll.md §2.5` + §5.2 + §7.1 + §6.3. **يعتمد على**: Phase 2 (اعتماد الطلبات) + بيانات Phase 3/4/5 (المعروضة في البوابة).

---

### Phase 7 — التقارير

**النطاق**: 17 تقرير إداري/ذاتي مرقّم (`§8`) + تقارير رقابة وامتثال (تعارض حضور، بدون تسجيل تأميني، مستندات منتهية، تحت الحد الأدنى، تدقيق إظهار PII، توزيع بقشيش لكل فرع، دوران عمالة).

Sub-Batches:
- **7.1** — Research Pass.
- **7.2** — HR Reports (سجل موظفين، أرصدة إجازات، حضور شهري، شهادات...).
- **7.3** — Payroll Reports (كشف رواتب، ضريبة مرتبات، تأمينات، سلف مفتوحة، تكلفة عمالة).
- **7.4** — Self-Service Reports (قسيمتي، رصيد إجازاتي، حضوري، حضور الفريق، تقويم إجازات الفريق).
- **7.5** — Compliance Reports (تعارض الوردية، بدون تأمين، مستندات منتهية، تدقيق PII، بقشيش الفرع).
- **7.6** — Tests + Docs.

**مرجع**: `Docs/Modules/10-Module-HR-Payroll.md §8`.

---

## 5. Checkpoint Pattern

```
User: "كمّل Phase X"
  ↓
Claude Code:
  - Read HR-MASTER-PLAN.md § Phase X
  - Execute Research Pass
  - Send report
  - STOP
  ↓
User: "موافق"
  ↓
Claude Code:
  - Execute Batches X.1, X.2, ...
  - /clear between batches
  - Send "خلصت Phase X"
  - STOP
  ↓
User: "كمّل Phase X+1"
  ↓
... (يكرر)
```

---

## 6. حالة التنفيذ

**منجزة**:
- Phase 0 (0.1, 0.3, 0.4, 0.5, 0.6)
- Phase 1.1 (B1-B7 + إصلاح لاحق لثغرة `TerminateEmployeeCommand`/Session Revocation)
- Phase 1.2 (1.2.1 Research → 1.2.6 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-1.2-Final.md`)
- Phase 1.3 (1.3.1 Research → 1.3.4 Docs — تفصيل كامل في `Docs/Implementation/Phase-1.3-Final.md`، صفر انحراف عن الخطة)
- Phase 1.4 (1.4.1 Research → 1.4.4 Docs — تفصيل كامل في `Docs/Implementation/Phase-1.4-Final.md`، صفر انحراف عن الخطة)
- **Phase 1.5** (1.5.0 Research → 1.5.6 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-1.5-Final.md`. **1.5.4 اتلغت** — مكررة بالكامل مع 1.5.3، بقرار صريح من المستخدم. صفر انحراف معماري عن الخطة المعدّلة بعد Research Pass، فجوتين Backend صغيرتين اتكشفوا واتصلحوا أثناء التنفيذ (`AttachmentEntityTypes` في 1.5.3، تفاصيل كاملة في التقرير))
- **Phase 2** (2.1 Research → 2.7 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-2-Final.md`. محرك الاعتمادات v1 كامل: 7 كيانات، `ApprovalWorkflowService` الحقيقي بدّل `NullApprovalWorkflowService`، متكامل مع `IPostingService`. 3 قرارات صغيرة اتخذت أثناء الـ Research موثّقة في `Docs/Implementation/Phase-2-Research.md`، صفر Stop Conditions)
- **Phase 2.5** (2.5.1 Research → 2.5.5 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-2.5-Final.md`. إشعارات داخل التطبيق: كيان `Notification` + `INotificationService` متكامل مباشرة مع نقاط محرك الاعتمادات الأربع (بدء/موافقة/رفض/إعادة تسكين)، `NotificationBell` + `NotificationsPage` جديدين. Navbar علوي جديد اتضاف لأول مرة فوق الـ TabBar (لم يكن موجودًا قبل كده) ليحمل الجرس بس — قرار موثّق في `Docs/Implementation/Phase-2.5-Research.md §1.2`. بَق زمني حقيقي اتكتشف أثناء التحقق بالمتصفح (تواريخ الـ API من غير علامة UTC) واتصلح محليًا في `relativeTime.ts` + اتسجّل Task منفصل للإصلاح الجذري)
- **Phase 3** (3.1 Research → 3.7 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-3-Final.md`. الـ10 كيانات الأصلية + 4 Amendments من `Remarks8-HR-Enhancements.md` بنود 5-8 نُفذت **جوه Phase 3 نفسها** قبل الفرونت إند: `ShiftSchedule`/`Holiday` بقوا فترات (`StartDate`/`EndDate?`)، كيان `EmployeeWeeklyRestDays` جديد، شاشة مولّد جماعي لجداول الورديات (`HR_SHIFT_SCHEDULE_GENERATOR`). صفر انحراف عن القواعد الجوهرية)
- **Phase 3B** (3B.1 Research → 3B.8 Tests+Docs — تفصيل كامل في `Docs/Implementation/Phase-3B-Final.md` + `Phase-3B-Cloud-Report.md`. تكامل أجهزة البصمة: 4 كيانات جديدة، Push API (ADMS/iClock-compatible + JSON Fallback)، Job دوري `RawPunch → TimeEntry`، File Import (CsvHelper/ClosedXML)، شاشات إدارة كاملة + تبويب "ماكينة البصمة" في شاشة الموظف. **Approved رسميًا** بعد جولتين تحقق (Cloud Session ثم Local Session — أول مرحلة تمر بـ"2B. Cloud/Local Workflow" الإلزامي، راجع الملاحظة تحت)
- **Phase 3C** (3C.2 → 3C.6 — تفصيل كامل في `Docs/Implementation/Phase-3C-Final.md`. بنود 1-4 من `Remarks8-HR-Enhancements.md`: كيان `EmploymentContractLine` جديد [بدلات/خصومات فوق الراتب الأساسي، Replace-All]، `AttachmentId?` على `EmploymentContract`/`EmployeeCertification` [نفس نمط `EmployeeDocument`]، أوامر `SetAttachment` ضيقة بدل Update كامل، Line Editor في تاب العقود + Hiring Wizard. **Approved رسميًا** بعد جولتين تحقق (Cloud ثم Local — نفس "2B. Cloud/Local Workflow" المعتمد من Phase 3B): 627 تشغيلة اختبار (96 HR + 245 IntegrationTests + 277 ApiTests + 9 Frontend)، صفر Failures، وتحقق يدوي كامل End-to-End (بنود العقد، مرفقات العقود/الشهادات، Hiring Wizard) على الجهاز المحلي

> **ملاحظة عن Workflow جديد (بدأ مع Phase 3B)**: بداية من هذه المرحلة، كل Phase بتمر إلزاميًا بـ2 Sessions منفصلتين — **Cloud** (تنفيذ الكود + Build + Migration Generation + Commit/Push فقط، بدون Migration Apply ولا Tests ضد DB حقيقية) ثم **Local** (`git pull` فقط بدون أي تعديل، Migration Apply كامل [Backup → Trial → Real]، Regression كامل، تقرير Verification منفصل). Phase X+1 ما تبدأش إلا بعد اعتماد صريح من الـLocal Session. التفاصيل الكاملة والقيود في `Phase-3B-Cloud-Report.md §9` وتقرير الـLocal المقابل.

**قرار 1.5.4 (`3 Followers Tabs`)**: **اتلغت — مكررة بالكامل**. التبويبات الثلاثة (عقود/مستندات/شهادات) اتبنت كاملة الوظائف من 1.5.3 نفسها (مش Placeholders محتاجة تكملة)، زي ما طلب المستخدم صراحة ("لو 1.5.3 خلصت التبويبات كاملة → 1.5.4 مكررة"). الانتقال المباشر لـ 1.5.5.

**Phase 4 (جارية)** — `Phase-4-Research.md` (4.1) + 4.2 (11 جدول قانوني + `IEffectiveDatedEntity`) +
4.3 (`SalaryComponent`..`Payslip` + `TipsDistribution`+Line + `EmployeeTaxProfile` + enum
`PayrollLineSource`) + **4.4 (Payroll Engine)** منجزين: `PayrollCalculationService` (المعادلة كاملة
عن طريق قاعدة 28)، `PayrollRunKeys`/Idempotency، `CreatePayrollPeriodCommand`/`CreatePayrollRunCommand`/
`CalculatePayrollRunCommand`/`SubmitPayrollRunForApprovalCommand`، `PayrollRunApprovalOutcomeHandler`،
جدول قانوني 12 جديد `OvertimeLimitRule` + `OvertimeRequest.ExceedsLimit`/`HrOverrideApprovedByUserId`
(بديل عملي لخطوة اعتماد إضافية، قاعدة 20) + **4.5 (Posting Integration)** منجز: 11 دور جديد في
`CompanyAccountRole` (+ `ExpectedAccountType`)، `SourceDocumentType.TipsDistribution`، `PostPayrollRunCommand`/
`PayPayrollRunCommand` (قالبين حقيقيين، Stage-gated على شاشة `PAY_PAYROLL_RUNS` واحدة) — البقشيش
جوه قيد الاستحقاق، صرف سلفة/تسوية EOS/مخصص الإجازات الشهري/مخصص EOS الشهري كلهم مؤجّلين (تفصيل في
Amendments Log). صفر Migration لـ4.5 (نمو Enum بس). Migrations اتولّدت ضد الـModel بس (Cloud، بدون
Apply). تفاصيل كل Sub-Batch في §7 Amendments Log تحت.

**قادمة (لسه محدش بدأ فيها)**:
- باقي Phase 4 (4.6 → 4.8) ثم Phase 5 → Phase 7.

> ملاحظة دقة: القالب الأصلي لهذا الملف افترض إن Phase 1.2 "جارية" — ده مش صحيح فعليًا وقت كتابة هذا الملف، لسه محدش بدأ فيها. صُحّح هنا بدل ما يتكرر الافتراض.

---

## 7. Amendments Log

| التاريخ | التعديل | السبب |
|---|---|---|
| 2026-09-27 | Phase 1.4 اتحدد نطاقها صراحة كـ"مبسّط" (تعطيل إداري بس)، والحساب المالي الكامل اتأكد إنه Phase 5 مش جزء من 1.4 | تجنّب تكرار عمل — `HR-Core-Plan.md §1.4` نفسه بيوصف 1.4 بـ"مبسّط عمدًا"، بينما صياغة "الكامل" ممكن تتفهم غلط إنها تشمل التسوية المالية |
| 2026-09-27 | عدد جداول Phase 4 القانونية اتوصف كـ"9 أو 10" بدل رقم ثابت | `10-Module-HR-Payroll.md §2.4` بيسرد 9 صفوف لكن أحدهم (`PayrollTaxBracketSet`+`PayrollTaxBracket`) فعليًا جدولين — نفس نمط "اتأكد بالعدّ الفعلي" اللي ظهر في B7 (18 مش 15/22) |
| 2026-09-27 | Phase 1.2، sub-batch 1.2.4: شاشة `HR_CUSTODY_MIGRATION` التفاعلية الكاملة اتأجلت، اتبنى بدالها `CustodyMigrationBanner.tsx` كـ Component معزول بس (+ Backend التشخيص/الربط التلقائي كامل زي المخطط) | Research Pass (`Phase-1.2-Research.md §6`) أكّد إن شاشة `CustodyRegister` نفسها (المحاسبية) مش موجودة في الـ Frontend خالص — بناء شاشة ربط تفاعلية فوق شاشة مش موجودة سابق لأوانه؛ الفجوة مسجّلة في `Known-Issues.md` كبند منفصل خارج نطاق HR |
| 2026-09-27 | إضافة Phase 3.5 — Fingerprint Device Integration | الفجوة الأصلية في Master Plan: الوقت مذكور Device كمصدر بس بدون تصميم تكامل. مهم لأن 90% من الكافيهات المصرية بتستخدم أجهزة بصمة (ZKTeco غالبًا) |
| 2026-09-27 | إعادة تسمية Phase 3.5 → **Phase 3B** (والـ Sub-Batches 3.5.1-3.5.7 → 3B.1-3B.7) | تفادي تضارب أرقام مع Sub-Batch 3.5 الموجود بالفعل جوه Phase 3 نفسها (POS Integration) — الاسمين كانوا هيتلخبطوا مع بعض في أي مرجع مستقبلي |
| 2026-09-27 | إضافة Sub-Batch **3B.7 — تحديث شاشة الموظف (Employee Fingerprint Tab)**، وإعادة ترقيم Tests+Docs القديمة من 3B.7 لـ **3B.8** + ملاحظة جديدة في Phase 1.5.4 | ربط الموظف بماكينة البصمة (`EmployeeDeviceMapping`) لازم يكون مرئي/قابل للتعديل من شاشة الموظف نفسها، مش بس من شاشة إدارة الأجهزة (3B.6) — التبويب محتاج كيانات Phase 3B فمش يصح يتبنى في Phase 1.5 (اللي بتسبق 3B) |
| 2026-09-27 | إضافة Sub-Batch **1.5.0 — `HrSettings` Backend** (كيان جديد بالكامل + Migration + Application + Controller)، وتصحيح افتراض `HR-Core-Plan.md §1.5` إن الكيان "مبني من 1.1" | `Phase-1.5-Research.md` أكّد إن `HrSettings` **مش موجود خالص** في الكود (صفر Migration، صفر Controller) رغم إن التوثيق كان بيقول "Migration: لأ" — الفجوة Backend حقيقية ولازم تُبنى قبل أي Frontend لشاشة `HR_SETTINGS` |
| 2026-09-27 | حذف `EmployeeCodePrefix`/`EmployeeCodeSequence` نهائيًا من حقول `HrSettings` المرحلة 1 (بقيت 3 حقول بس)، وإضافة `DefaultCutoffDay` للحقول المؤجلة | آلية `ICodeGenerator`/`CodingRule` العامة الموجودة بالفعل (`CreateEmployeeCommand.cs:52`) بتغطي توليد كود الموظف — حقلين مخصصين على `HrSettings` لنفس الوظيفة تكرار مصدر حقيقية. `DefaultCutoffDay` موجود في `10-Module-HR-Payroll.md:130` كحقل فعلي على `HrSettings` ومكنش مسجّل في جدول `HR-Core-Plan.md §1.5` |
| 2026-09-27 | حذف خطوة "تأمينات" من Hiring Wizard (1.5.5)، ودمج خطوة "راتب" جوه خطوة "عقد" | `Phase-1.5-Research.md` أكّد إن مفيش أي ربط `Employee`↔`InsuranceOffice` في الكود، و`EmployeeSocialInsurance` الحقيقي مؤجل لـ Phase 5 — كتابة بيانات تمهيدية دلوقتي هتحتاج Migration تهجير لاحقة من غير فايدة حقيقية. `BasicSalary`/`InsurableWage` أصلًا أعمدة على `EmploymentContract` بالفعل، فمفيش خطوة "راتب" مستقلة لازمة. |
| 2026-09-28 | Phase 3B — اعتماد "2B. Cloud/Local Workflow" الإلزامي لأول مرة (Cloud: كود+Build+Migration Generation+Push فقط، Local: Pull+Migration Apply+Regression+Verification فقط) | LocalDB تقنية Windows-only — الـCloud Session (Linux) اكتشف إن أي Migration Apply/Test ضد DB حقيقية مستحيل تقنيًا هناك (`PlatformNotSupportedException`). بدل الاعتماد على حلول مؤقتة (Docker يدوي)، اتحدد Workflow دائم يفصل المسؤوليات بوضوح بين الجلستين لكل Phase قادمة |
| 2026-09-28 | Phase 3B — 3 إصلاحات إضافية اتكشفت أثناء التحقق (Vault dev-mode fallback بيتجاهل Token فاضي مش null، `xlsx` من CDN استُبدل بـ`exceljs`، بنية اختبارات جديدة OS-aware [LocalDB/Testcontainers] عشان الـCloud يقدر يشغّل Tests فعليًا) | تفاصيل كاملة في `Phase-3B-Cloud-Report.md §5`. التحقق المحلي أثبت إن الـ19 فشل اللي ظهروا في تشغيلة الـCloud الكاملة (238/238 Local مقابل 233/238+19 فشل في Cloud) كانوا Known Limitation خاص بإعداد Vault في الـCloud Sandbox بس (Data Protection/JWT Signing)، مش Code Bug — Phase 3B نفسها 12/12 في كل التشغيلات |
| 2026-09-28 | **Phase 3B اعتُمدت رسميًا (Approved)** بعد جولتين تحقق كاملتين (Cloud ثم Local) | `Phase-3B-Final.md` + `Phase-3B-Cloud-Report.md` (قسم Local Verification Results) — IntegrationTests 238/238، ApiTests 277/277، Frontend 9/9 على الجهاز المحلي. الانتقال لـ**Phase 3C** (بنود 1-4 من `Remarks8-HR-Enhancements.md`) |
| 2026-09-28 | Phase 3C (بنود 1-4 من `Remarks8-HR-Enhancements.md`) — الجانب الخاص بالـCloud منجز (3C.2 → 3C.6): `EmploymentContractLine`، `AttachmentId?` على العقد/الشهادة، `SetAttachment` Commands، Line Editor في تاب العقود + Hiring Wizard | تفاصيل كاملة في `Phase-3C-Research.md` (القرارات المعتمدة) و`Phase-3C-Final.md` (التنفيذ). Migration واحدة شاملة (`EmploymentContractLinesAndAttachments`) طبقًا لقرار المستخدم؛ 7 اختبارات تكامل جديدة نجحت 7/7 ضد SQL Server حقيقي (Testcontainers) في الـCloud. بانتظار Local Verification قبل اعتماد المرحلة رسميًا |
| 2026-09-28 | **Phase 3C اعتُمدت رسميًا (Approved)** بعد جولتين تحقق كاملتين (Cloud ثم Local) | `Phase-3C-Final.md §6` (Local Verification Results) — 627 تشغيلة اختبار (96 HR + 245 IntegrationTests + 277 ApiTests + 9 Frontend)، صفر Failures، صفر Bugs مكتشفة. تحقق يدوي End-to-End أكّد الأربع بنود كاملة (بنود العقد عن طريق Renew، مرفقات العقود/الشهادات، Hiring Wizard) شامل صحة الترميز العربي. لا يوجد Blocker لبدء Phase 4 |
| 2026-09-28 | Phase 4 Research Pass: عدد الجداول القانونية اتثبّت 10 (مش "9 أو 10")، `EndOfServicePolicy` أُضيف كجدول قانوني 11، `TipsDistribution`+`TipsDistributionLine` اتسحبوا لـ Phase 4 (مش Phase 5)، قالب "صرف سلفة" أُجّل لـ Phase 5 مع `EmployeeAdvance` نفسه (Phase 4 = 6 قوالب ترحيل مش 7)، خطوة اعتماد "الإضافي فوق الحد الشهري" (مؤجّلة من Phase 3) اتضافت لـ 4.4، و3 حقول `HrSettings` (`MonthBasis`/`DefaultCutoffDay`/`CompanyDefaultApproverUserId`) هتتفتح في شاشة `HR_SETTINGS` عن طريق 4.6 | `Phase-4-Research.md §2.1-§2.7`، كل القرارات دي اتاخدت صراحة بعد "موافق" المستخدم على الـ Research Pass |
| 2026-09-28 | `EmployeeTaxProfile` اتسحب من الـ9 كيانات بتاعة Phase 5 لـ Phase 4.3، و`PayrollLine.SalaryComponentId` اتغيّر لـ Nullable مع enum جديد `PayrollLineSource` (`LegalSocialInsurance`/`LegalTax`/`LegalMartyrsFund`/`PriorPeriodAdjustment`/...) | اكتُشف أثناء تصميم محرك الحساب (4.4، قبل أي كود): قاعدة 37 ("الضريبة بتتحسب سنويًا وبتتقسّط شهريًا (`EmployeeTaxProfile` التراكمي)") و`PayrollRunType.AnnualTaxSettlement` (مبني فعليًا في 4.3) بيعتمدوا على تراكم سنوي حقيقي مش تقريب شهري، والقسائم المرحّلة بتتجمّد (قاعدة 29) فمفيش مجال لتصحيح لاحق. وبشكل منفصل، `SalaryComponent.CalculationMethod` (`Fixed`/`PercentOfBasic`/`Hourly`/`Formula`) مفيهوش حالة "من جدول قانوني"، فخصومات التأمينات/الضريبة/صندوق الشهداء مالهاش `SalaryComponent` حقيقي تتربط بيه — قرار المستخدم: `SalaryComponentId` Nullable بدل فرض Seeding لمكونات نظام وهمية |
| 2026-09-28 | Phase 4.4 — جدول قانوني 12 جديد `OvertimeLimitRule` (مش معدود في §2.4 الأصلي، رقم 20 بيسمّيه "جدول مؤرّخ" بس بدون تفصيل) + `OvertimeRequest.ExceedsLimit`/`HrOverrideApprovedByUserId` بدل خطوة `ApprovalWorkflow` حقيقية تانية | `ApprovalWorkflowAssignment` (Phase 2) بيدعم سلسلة واحدة بس لكل Screen (فهرس فريد مفلتر)، فمفيش آلية جاهزة تعبّر عن "خطوة إضافية بس لو تجاوز حد" لنفس الشاشة — مُوثّق أصلًا كفجوة في Phase-4-Research.md §1.8. القرار: Override يدوي من HR بدل إعادة تصميم محرك الاعتمادات (ده كان هيوسّع نطاق 4.4 لتغيير جوهري في Phase 2 يأثر على شاشات تانية زي السلف/المكافآت) |
| 2026-09-28 | Phase 4.5 — من قوالب §6.2 السبعة الأصلية (بعد استبعاد صرف السلفة، Phase 5)، **قالبين بس فعليًا اتبنوا** (استحقاق + صرف، Stage-gated على شاشة واحدة `PAY_PAYROLL_RUNS`)، والباقي اتأجّل: البقشيش بقى جوه قيد الاستحقاق نفسه (مش قالب مستقل — نص §6.2 صريح "داخل قيد الاستحقاق")؛ تسوية نهاية الخدمة اتأجّلت لـ Phase 5 مع `EmployeeEndOfService` (زي صرف السلفة بالظبط)؛ مخصص الإجازات الشهري ومخصص نهاية الخدمة الشهري (الاتنين) اتأجّلوا لأنهم محتاجين "إعادة قياس شهرية = قيد الفرق" (§6.2) وده محتاج استعلام رصيد فعلي من دفتر الأستاذ — بناءه غلط (إضافة كامل المبلغ كل شهر بدل الفرق) هيطلّع رصيد التزام مضاعَف في الميزانية شهريًا | مفيش قرار مستخدم صريح سابق على تأجيل مخصص الإجازات/EOS تحديدًا — قرار تنفيذي أثناء 4.5 نفسها لتجنّب بناء قيد محاسبي غلط بثقة، بدل ما يتوقف التنفيذ. Task منفصل اتسجّل (`task_eadb2c17`، "Build Leave/EOS monthly provision posting") لبناء استعلام رصيد دفتر الأستاذ اللازم قبل تفعيل القالبين دول |
| — | — | — |
