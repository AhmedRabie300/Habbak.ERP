# Phase 2 — محرك الاعتمادات المركزي v1 — التقرير النهائي

> `Docs/Modules/00-Project-Overview.md` §12 · Research: `docs/Implementation/Phase-2-Research.md` ·
> بدأ التنفيذ بعد موافقة المستخدم على تقرير الـ Research، ونُفِّذت كل الـ Sub-Batches (2.2 → 2.7)
> في جلسة واحدة بدون توقف، طبقًا للتعليمات.

---

## 1. ملخص تنفيذي

محرك اعتمادات مركزي كامل: 7 كيانات جديدة (`Screen`, `ApprovalWorkflow`, `ApprovalWorkflowStep`,
`ApprovalStepApprover`, `ApprovalWorkflowAssignment`, `ApprovalInstance`, `ApprovalAction`)،
`ApprovalWorkflowService` الحقيقي بدّل `NullApprovalWorkflowService` بالكامل (الملف اتشال)، متكامل
فعليًا مع `IPostingService` (`PostDraftEntryAsync` فقط — تفصيل القرار في §5)، شاشتين Frontend
جديدتين (إعدادات السلاسل + بانتظار اعتمادي)، و12 اختبار جديد (10 Integration + 2 API) تغطي كل
الحالات الإلزامية السبعة من طلب المستخدم. صفر انحراف عن قرارات الـ Research الموافق عليها.

---

## 2. تفاصيل كل Sub-Batch

### 2.2 — Domain
- 7 كيانات في `src/Habbak.ERP.Domain/Approvals/` (ملفين: `ApprovalWorkflow.cs`,
  `ApprovalInstance.cs`) + `Screen` في `src/Habbak.ERP.Domain/Common/Screen.cs`.
- `EmploymentContractStatus.Rejected = 5` أُضيف (قرار §3.2 من تقرير الـ Research).
- Migration واحدة (`AddApprovalWorkflowEngine`) — 7 جداول جديدة، Up/Down كاملين ومتماثلين
  (Down بيعمل Drop لكل الجداول بترتيب عكسي صحيح). طُبِّقت حسب بروتوكول الـ Migration الإلزامي
  بالكامل: Backup الـ DB الحقيقية (`HabbakErp_PrePhase2.bak`) → Restore على
  `HabbakErp_Trial_Phase2` → تطبيق + تحقق من الـ Schema (7 جداول + 21 FK) → تطبيق على
  `HabbakErp` الحقيقية بـ `Command Timeout=180` → حذف الـ Trial DB.
- `ScreenSeedData.cs` — يزرع سجل `Screen` من `ScreenCodeCatalog` (68 شاشة) + 4 شاشات إضافية
  (`HR_SETTINGS`, `HR_HIRING`, `SETTINGS_APPROVAL_WORKFLOWS`, `APPROVAL_MY_PENDING`) — إضافي بنفس
  فلسفة `MenuItemSeedData`، مش كل شاشات النظام، يُوسَّع لاحقًا عند الحاجة.

### 2.3 — Application
- `ApprovalWorkflowDefinition` + `ApprovalWorkflowDefinitionValidator` — سلسلة كاملة (خطوات +
  معتمدين) في نداء واحد، بنفس نمط `PostingTemplateDefinition` (**قرار توحيد**: الخطة الأصلية
  اقترحت أوامر CRUD منفصلة لكل خطوة/معتمد — تم دمجها في نداء واحد، موثّق في الكود).
- `ApprovalStepResolutionService` — يحل المعتمدين المؤهلين فعليًا لأي خطوة: `SpecificEmployee`،
  `Role` (عبر `UserRole`)، `DirectManager` (مع تصعيد `CompanyDefaultApproverUserId` عند غياب
  المدير أو غياب حساب دخول له — تغطية أوسع من القاعدة 8 الحرفية، موثّقة)، `JobGrade` (كل موظف
  Active على الدرجة). `Manual Fallback` (`ManualReassignedToUserId`) بيتجاوز الكل لما يكون موجود.
- `ApprovalWorkflowService` (الاستبدال الحقيقي لـ `IApprovalWorkflowService`) — يحل الشاشة من
  `ApprovalTriggerScreenMap`، يفحص `ApprovalWorkflowAssignment` النشطة + `MinAmount`، ينشئ
  `ApprovalInstance` على أول خطوة.
- `IApprovalOutcomeHandler` — Registry مكتشف تلقائيًا (نفس نمط Resolvers محرك الترحيل) لتطبيق
  نتيجة الاعتماد/الرفض على الكيان الفعلي؛ `JournalEntryApprovalOutcomeHandler` هو التطبيق الوحيد
  حاليًا (الكيان الوحيد المتكامل فعليًا — باقي الكيانات المذكورة في §1.3 من تقرير الـ Research
  لسه Schema-only لحد ما موديولاتها بتاعتها تبدأ تستخدم المحرك، زي ما الخطة نفسها حددت في Phase 5).
- الأوامر: `CreateApprovalWorkflowCommand`, `UpdateApprovalWorkflowCommand` (بالإصدارات),
  `SetApprovalWorkflowActiveCommand` (دمج Activate/Deactivate، نفس نمط `PostingTemplate`),
  `AssignWorkflowToScreenCommand`, `UnassignWorkflowFromScreenCommand`, `ApproveStepCommand`,
  `RejectStepCommand`, `ReassignInstanceCommand`.
- الاستعلامات: `GetApprovalWorkflowsListQuery`, `GetApprovalWorkflowByIdQuery`,
  `GetScreensListQuery`, `GetWorkflowAssignmentsListQuery`, `GetMyPendingApprovalsQuery`,
  `GetApprovalInstanceByIdQuery`, `GetApprovalHistoryQuery`.
- **Self-Approval Guard إلزامي** — `ApprovalActionGuards.ValidateActingUserAsync` بيتحقق قبل أي
  Approve/Reject: `actingUserId == instance.RequestedByUserId` → `ForbiddenException`.
- **JobGrade/DirectManager بلا معتمد فعلي** — `APPROVAL-STEP-NO-ELIGIBLE-APPROVERS` صريح، مش
  تخطي صامت (قاعدة 9).

### 2.4 — API
- `ApprovalWorkflowsController` (`/api/v1/approvals/workflows` — CRUD + activate/deactivate +
  assignments)، `ApprovalInstancesController` (`/api/v1/approvals/instances` — pending-for-me،
  GetById، history، approve/reject/reassign)، `ScreensController` (`/api/v1/screens`،
  LookupReads).
- `ReassignInstanceCommand` محمي بـ `[ScreenButton("SETTINGS_APPROVAL_WORKFLOWS", "ManualReassign")]`
  (نفس نمط `RevealPii`) — صلاحية إدارية منفصلة عن صلاحيات الشاشة العادية (قاعدة 10).
- **فجوة مكتشفة وانصلحت أثناء 2.7**: `ButtonPermissionCatalog`'s الـ `FallbackAction` لازم يطابق
  الـ `ScreenAction` الفعلي اللي `ScreenPermissionFilter.Required()` بيحسبه من الـ Route segment —
  اختبار موجود بالفعل (`FieldButtonPermissionsTests`) كشف إن `"reassign"` مش في `ApproveSegments`،
  فاتضاف ليها (`src/Habbak.ERP.API/Auth/ScreenPermissionFilter.cs`).

### 2.5 — Frontend
- `frontend/src/features/settings/approvals/` (types.ts, api.ts,
  `ApprovalWorkflowsListPage.tsx` — تبويبين "السلاسل"/"الربط بالشاشات" عبر `SectionTabs`،
  `ApprovalWorkflowEditPage.tsx` — محرر خطوات/معتمدين ديناميكي بـ `useFieldArray` متداخل).
- `frontend/src/features/approvals/MyPendingApprovalsPage.tsx` — "بانتظار اعتمادي"، Polling كل
  30 ثانية (`refetchInterval`)، Modal مخصص لسبب الرفض الإلزامي.
- **قرار بنية تحتية من الـ Research اتنفّذ**: **Polling بدل SignalR** — لا SignalR في المشروع
  أصلًا، بناءه خارج نطاق هذه المرحلة.
- Routes: `/settings/approval-workflows`, `/settings/approval-workflows/:id`, `/approvals/pending`.
- i18n: شجرة `approvals.*` كاملة في `ar.json`/`en.json`.
- **تحقق فعلي في المتصفح** (مش تصريح نظري): إنشاء سلسلة كاملة (كود+اسمين+خطوة+معتمد) وحفظها
  نجح فعليًا، والربط بشاشة "القيود اليومية" نجح وظهر في الجدول، وشاشة "بانتظار اعتمادي" عرضت
  حالتها الفارغة الصحيحة — كل ده على الـ API الحقيقي (`localhost:5299`) والـ DB الحقيقية.
  **بَق اتكتشف وانصلح أثناء التحقق**: مفتاحين i18n (`common.savedSuccessfully`, `common.select`)
  اتنسيوا في التحويل لمساحة `approvals.*` الجديدة، ظهروا كنص خام ("common.savedSuccessfully")
  في الـ Toast الفعلي — اتصلحوا فورًا وأُعيد التحقق.

### 2.6 — Integration
- استبدال `NullApprovalWorkflowService` بـ `ApprovalWorkflowService` الحقيقي — الملف القديم
  اتشال، التسجيل في `Application/DependencyInjection.cs` (`AddApprovalEngine`) بدل
  `Infrastructure/DependencyInjection.cs`.
- `ApprovalWorkflowTrigger` اكتسب حقل `RequestedByUserId` (كان ناقص من التصميم الأصلي — ضروري
  للـ Self-Approval Guard).
- **قرار معماري موثّق (مش Stop Condition)**: `PostingService.PostAsync` (قيد جديد لسه مش
  محفوظ، `entry.Id == 0`) بيترحّل فورًا بغض النظر عن أي Assignment — نفس السلوك قبل هذه المرحلة.
  السبب: عمل `SaveChangesAsync` إضافي جوّه `PostingService` عشان نجيب Id حقيقي كان هيكسر الـ
  Atomicity المفترضة بين القيد ومستنده المصدر عبر عشرات نقاط الاستدعاء الحالية — مخاطرة رجعية
  حقيقية مش في نطاق Phase 2. `PostDraftEntryAsync` (قيد له Id حقيقي بالفعل) هو المسار المتكامل
  فعليًا — تفصيل كامل في تعليق الكود بـ `PostingService.cs`.

### 2.7 — Tests + Docs
- **Backend**: 10 اختبارات Integration جديدة (`ApprovalWorkflowEngineTests.cs`) تغطي كل الحالات
  الإلزامية السبعة حرفيًا (Self-Approval، `ManagerId=null`, Manual Reassign، JobGrade فاضية،
  AnyOne، All، الإصدارات مرتين، الرفض ينعكس على `JournalEntry`، استعلام Pending). 2 اختبار API
  جديد (`ApprovalWorkflowApiTests.cs`) للمسار الكامل HTTP + صلاحية الزرار الإداري.
- **Regression كامل** — راجع §4 للأرقام النهائية.
- **Frontend**: لا اختبار Vitest جديد — نفس السابقة الموثّقة في `Phase-1.5-Final.md`: مفيش
  مكوّن `ui-kit` جديد اتبنى في هذه المرحلة (كل الشاشات استخدمت مكوّنات موجودة بالفعل)، فمفيش
  حاجة جديدة تستاهل اختبار وحدة مستقل زي `SectionTabs` في 1.5.1 — بدلها تحقق فعلي في متصفح حقيقي
  ضد الـ API/DB الحقيقيين (تفصيل في §2.5 أعلاه، شامل بَق اتكتشف وانصلح).
- `HR-MASTER-PLAN.md §6` تحدّث.

---

## 3. عدد الملفات

**Backend — جديد (25 ملف)**: 3 Domain (`Screen.cs`, `ApprovalWorkflow.cs`, `ApprovalInstance.cs`)
+ 3 Infrastructure Configuration + `ScreenSeedData.cs` + Migration (ملفين) + 13 Application
(`Approvals/` كاملة) + 3 API Controllers.

**Backend — معدَّل**: `Enums.cs` (HR)، `AppDbContext.cs`، `IApplicationDbContext.cs`،
`IApprovalWorkflowService.cs`، `ButtonPermissionCatalog.cs`، `ScreenPermissionFilter.cs`،
`PostingService.cs`، `SystemDataSeeder.cs`، `MenuItemSeedData.cs`، الـ Application/Infrastructure
`DependencyInjection.cs` (اتنين). **محذوف**: `NullApprovalWorkflowService.cs`.

**Backend — Tests**: ملفين جديدين (12 اختبار) + 5 ملفات اختبار موجودة اتعدّلت (استبدال
`NullApprovalWorkflowService` بالتطبيق الحقيقي في الإنشاء المباشر).

**Frontend — جديد (5 ملفات)**: `types.ts`, `api.ts`, `ApprovalWorkflowsListPage.tsx`,
`ApprovalWorkflowEditPage.tsx`, `MyPendingApprovalsPage.tsx`.

**Frontend — معدَّل**: `routeTable.tsx`, `ar.json`, `en.json`.

---

## 4. نتائج الاختبارات

- **Integration Tests**: **198/198 ناجحة** (188 موجودة سابقًا + 10 جديدة) — 16 دقيقة 13 ثانية.
- **Api Tests**: **264/264 ناجحة** (262 موجودة سابقًا + 2 جديدة) — 17 دقيقة 35 ثانية. شمل هذا
  الرقم اكتشاف وإصلاح فجوة `ButtonPermissionCatalog`/`ScreenPermissionFilter` الحقيقية (§2.4)
  عبر اختبار `FieldButtonPermissionsTests` الموجود مسبقًا.
- **Frontend**: `tsc -b` نظيف (0 أخطاء) — تحقق كامل في المتصفح ضد الـ API/DB الحقيقيين، تفصيل في §2.5.

**462 اختبار ناجح إجمالًا، صفر فشل** — تشغيل كامل مرتين (قبل وبعد إصلاح
`ButtonPermissionCatalog`)، وتشغيل جزئي إضافي للتأكد من كل تعديل بشكل منفصل قبل التشغيل الكامل.

كل الاختبارات القديمة فضلت خضراء رغم استبدال `NullApprovalWorkflowService` بتطبيق حقيقي —
دليل عملي إن محرك الترحيل والموديولات التانية ما اتأثرتش (رول 4: شاشة بدون Assignment = نفس
السلوك القديم تمامًا).

---

## 5. أخطاء/فجوات اتكتشفت وانصلحت أثناء التنفيذ

1. **`EmploymentContractStatus` بدون `Rejected`** (Domain) — فجوة حقيقية اتكشفت في الـ Research،
   اتصلحت في 2.2.
2. **`ApprovalWorkflowTrigger` بدون `RequestedByUserId`** — العقد الأصلي (قبل Phase 2) ماكنش فيه
   حقل لمين قدّم الطلب — إضافة ضرورية لتفعيل الـ Self-Approval Guard، اتضافت في 2.3/2.6.
3. **`entry.Id == 0` عند `PostAsync`** — فجوة معمارية موثّقة من مؤلف الكود الأصلي نفسه (تعليق في
   الكود قبل هذه المرحلة) — اتحلّت بقرار نطاق واضح (تفصيل في §2.6) مش بتغيير خطر في `PostingService`.
4. **`ButtonPermissionCatalog`'s `FallbackAction` لازم يطابق `ScreenAction` الفعلي** — اختبار
   Regression موجود مسبقًا (`FieldButtonPermissionsTests`) كشف عدم تطابق حقيقي لـ `"reassign"` —
   اتصلح بإضافتها لـ `ApproveSegments`.
5. **بَق i18n في الـ Frontend** (`common.savedSuccessfully`/`common.select` منسيين) — اتكشف
   بالتحقق الفعلي في المتصفح (Toast عرض النص الخام)، مش بـ `tsc` (مفاتيح i18n مش مفحوصة بالـ
   Type System) — اتصلح فورًا.

كل الفجوات دي اتحلّت داخل نطاق Phase 2 نفسه — مفيش أي Stop Condition اتفعّل.

---

## 6. قرارات اتخذت بدون موافقة مسبقة (موثّقة، مش انحراف صامت)

- دمج أوامر CRUD المنفصلة للخطوات/المعتمدين في `ApprovalWorkflowDefinition` واحد (§2.3).
- دمج Activate/Deactivate في أمر واحد `SetApprovalWorkflowActiveCommand` (نفس نمط `PostingTemplate`).
- تسمية `GetScreensListQuery`/`GetWorkflowAssignmentsListQuery` بدل "ResolveScreenQuery" المذكورة
  في الخطة الأصلية — تسمية أوضح ومتّسقة مع باقي الاستعلامات.
- توسيع تصعيد `DirectManager` ليشمل "مدير موجود بلا حساب دخول"، مش `ManagerId = null` بس (Research §3.4).
- إضافة `"reassign"` لـ `ApproveSegments` في `ScreenPermissionFilter` (§2.4).

---

## 7. ملاحظات إضافية

- **الشاشة الوحيدة المتكاملة فعليًا مع المحرك حاليًا هي `ACCOUNTING_JOURNAL_ENTRIES`** (عبر
  `PostDraftEntryAsync`) — باقي الشاشات (`PurchaseOrder`, `PurchaseRequest`, ...) عندها
  `ApprovalInstanceId` Schema-only بردو، زي ما كانت قبل Phase 2؛ ربطها الفعلي بالمحرك مسؤولية
  الفيز/الموديول بتاعها (Phase 5 مثلًا لسلاسل السلف/الجزاءات).
- **`ScreenSeedData` مش شاملة كل شاشات النظام** — 72 شاشة بس (68 من `ScreenCodeCatalog` + 4)،
  إضافية بنفس فلسفة `MenuItemSeedData` — أي شاشة جديدة تحتاج Assignment تتضاف وقتها.
- **مفيش Notifications** — لو حد بقى معتمد مؤهل لطلب جديد، مفيش تنبيه فوري بيوصله؛ لازم يفتح
  "بانتظار اعتمادي" بنفسه (أو ينتظر الـ Polling). موثّق كقدرة نظامية منفصلة مؤجلة (§12.7).

---

## 8. المرحلة التالية

**Phase 3 — الحضور والإجازات** (`WorkShiftDefinition`, `TimeEntry`, `LeaveRequest`, ...) —
تعتمد على Phase 2 (سلاسل اعتماد طلبات الإجازة/الإضافي).

**STOP** — مستني "كمّل Phase 3".
