# Phase 2.5 — إشعارات داخل التطبيق (Minimal) — Research Pass

> `docs/Implementation/HR-MASTER-PLAN.md` § Phase 2.5 (أُضيفت هذه الجلسة). فحص فعلي للكود —
> لا يوجد كود جديد في هذه الخطوة.

---

## 1. حالة الكود الفعلية

### 1.1 مفيش `Notification` من أي نوع حاليًا
بحث كامل في `src/Habbak.ERP.Domain` و`src/Habbak.ERP.Application/Common/Interfaces` طلع صفر
نتائج — صفحة بيضاء تمامًا، مفيش تعارض أو كيان مشابه لازم يُدمج معاه.

### 1.2 فجوة حقيقية في متطلب "Navbar" — لازم قرار

طلب المستخدم بيقول صراحة: **"1. المكان: Navbar (Header ثابت في `AppLayout.tsx`)... ظاهر في كل
الشاشات... جنبه اسم المستخدم + الإعدادات + تسجيل خروج."**

**الكود الفعلي (`frontend/src/app/AppLayout.tsx`) مفيهوش Navbar/Header علوي أصلًا.** التصميم
الحالي: `<aside>` جانبي (Sidebar) بعرض 240px بيحتوي على كل حاجة — الشعار، شجرة القائمة،
اسم المستخدم + تغيير الباسورد + التحقق بخطوتين + تبديل اللغة + الثيم + تسجيل الخروج — كلهم
جوه الـ Sidebar، مفيش صف علوي (Header) خالص. المنطقة الرئيسية (`flex:1`) فيها بس `TabBar`
(شريط مستندات مفتوحة) فوق المحتوى.

**التوصية**: إضافة صف علوي رفيع جديد (Navbar حقيقي، مش موجود قبل كده) فوق الـ `TabBar` في
منطقة المحتوى الرئيسية، يحتوي `NotificationBell` بس — **بدون** نقل اسم المستخدم/الإعدادات/
تسجيل الخروج من الـ Sidebar (ده تغيير أكبر وأخطر، مش مطلوب صراحة، وهيبوّظ تصميم موجود ومُختبر).
الهدف الجوهري من طلب المستخدم — "ظاهر في كل الشاشات، جنب سياق المستخدم" — بيتحقق: الـ Navbar
الجديد هيبقى ظاهر مع كل تبويب مفتوح (فوق كل الشاشات)، والـ Sidebar (اللي فيه اسم المستخدم بالفعل)
ظاهر جنبه طول الوقت أصلًا. **قرار مطلوب تأكيده مع "موافق"** — مش Stop Condition لأنه قرار تصميم
بسيط وإضافي، لكنه انحراف واضح عن الصياغة الحرفية ("جنبه اسم المستخدم") يستاهل تنويه صريح.

### 1.3 مفيش أيقونة Bell في `ui-kit/Icon.tsx`
القائمة الحالية: `plus, save, trash, chevronsLeft, chevronLeft, chevronRight, chevronsRight,
printer, download, upload, paperclip, search, check, x, reverse, balance`. هتتضاف `bell` (SVG
Path جديد، نفس نمط الباقي).

### 1.4 مفيش مكتبة تاريخ نسبي ("من 5 دقائق")
مفيش `date-fns`/`dayjs`/`moment` في `package.json`. الحل: `Intl.RelativeTimeFormat` (مدمج في
كل المتصفحات الحديثة، صفر تبعية جديدة) — بيدعم عربي/إنجليزي تلقائيًا حسب `i18n.language`.

### 1.5 Polling — نمط موجود بالفعل من Phase 2
`useMyPendingApprovals` (`frontend/src/features/settings/approvals/api.ts`) بالفعل بيستخدم
`refetchInterval: 30_000` — نفس النمط هيتكرر هنا لـ `pending-action-count` (React Query
`refetchInterval`)، بدون أي تغيير في البنية التحتية.

### 1.6 مسار التكامل الفعلي مع محرك الاعتمادات (Phase 2)

طلب المستخدم بيتكلم عن "Events" (`ApprovalInstanceCreatedEvent`, `ApprovalStepCompletedEvent`, ...)
لكن بيأكد صراحة **"in-process, no MediatR handlers"** — يعني مش أحداث MediatR حقيقية، بس نداء
مباشر لـ `INotificationService.SendAsync` من جوه نفس الـ Handler. نقاط الاستدعاء الفعلية
الأربعة (كلها موجودة بالفعل من Phase 2):

1. `ApprovalWorkflowService.TryStartApprovalAsync` (`Application/Approvals/Services/`) — بعد
   إنشاء `ApprovalInstance` على أول خطوة، يحل معتمدي الخطوة دي فعليًا (عبر
   `IApprovalStepResolutionService`، **مش مُحقَنة حاليًا في هذا الكلاس** — لازم تتضاف كـ
   Dependency جديدة) ويبعت `ApprovalPending` لكل واحد فيهم.
2. `ApproveStepCommandHandler` (`Application/Approvals/Commands/ApprovalActionCommands.cs`) —
   بعد ما خطوة تكتمل (`stepSatisfied`): لو فيه خطوة تالية → `ApprovalPending` لمعتمديها. لو
   مفيش (الطلب اتوافق عليه نهائيًا) → `ApprovalApproved` لمقدّم الطلب. **قرار تفسير**: طلب
   المستخدم كتب "ApprovalStepCompletedEvent → إشعار للمعتمد التالي + إشعار للمُنشئ (Approved)"
   في نفس السطر، ظاهرها إن كل خطوة تتوافق عليها تبعت "Approved" لمقدّم الطلب حتى لو لسه فيه
   خطوات باقية — ده هيبقى إغراق إشعارات على سلسلة متعددة الخطوات، وبيتعارض مع اسم النوع نفسه
   `ApprovalApproved` (المعنى الطبيعي: "طلبك اتوافق عليه نهائيًا"). **التوصية**: `ApprovalApproved`
   يترسل لمقدّم الطلب بس لما الطلب يوصل لحالة `Approved` النهائية (مفيش خطوة تالية) — مش عند كل
   خطوة وسيطة. قرار بسيط، موثّق، مش Stop Condition.
3. `RejectStepCommandHandler` (نفس الملف) — `ApprovalRejected` لمقدّم الطلب،
   `requiresAction: false` (مفيش شاشة إعادة تقديم في نطاق هذه المرحلة).
4. `ReassignInstanceCommandHandler` (نفس الملف) — يحل المعتمدين المؤهلين الحاليين **قبل** تعيين
   `ManualReassignedToUserId` (عشان يعرف مين "القديم")، يبعت `ApprovalReassigned` لكل واحد فيهم
   (عدا المعتمد الجديد لو كان أصلًا من ضمنهم — تجنّب ازدواج)، و`ApprovalPending` للمعتمد الجديد.

كل الأربعة موجودين بالفعل من Phase 2 ومحتاجين حقن `INotificationService` (+`IApprovalStepResolutionService`
في حالة #1 بس) — **صفر تغيير في تصميم محرك الاعتمادات نفسه**، إضافة نداء واحد أو اتنين في كل Handler.

### 1.7 نطاق شاشة الإشعارات — `[AnySignedInUser]` مش `[Screen]`
فحص `ScreenGuard`/`resolveScreenCode` (`frontend/src/features/auth/access.tsx`) أكّد: مسار
مالوش `Screen` مسجّل بيرجع `code = null`، والـ `ScreenGuard` بيسمح بالمرور تلقائيًا في الحالة
دي. يعني **مفيش داعي لتسجيل `Screen`/`MenuItem` جديد لـ `/notifications`** — نفس نمط
`AttachmentsController`/`FieldLabelsController`/`NavigationController` الموجودين بالفعل
(`[AnySignedInUser]`, بيانات شخصية بحتة خاصة بصاحب الجلسة، مش شاشة بصلاحيات Role-based).

### 1.8 `Notification.RecipientUserId` — FK لازم يتسجّل
`UserReferences.cs` (`Infrastructure/Persistence/Configurations/Settings/`) هو المكان الموحّد
لكل عمود بيحمل UserId (تعليق الكود صريح: "a new user column is added here and nowhere else").
`RecipientUserId` هيتسجّل هناك بنفس نمط `PurchaseRequest.RequestedByUserId`.

---

## 2. الفجوات

لا توجد فجوة Backend حقيقية تمنع التنفيذ — كل المتطلبات (محرك الاعتمادات، User، IApplicationDbContext،
نمط Polling، نمط AnySignedInUser) جاهزة بالفعل. الفجوة الوحيدة هي فجوة **تصميم UI** (§1.2) —
اتحلّت بتوصية واضحة، مش Backend ناقص.

---

## 3. القرارات الفرعية (موجزة، كلها موثّقة أعلاه بالتفصيل)

1. Navbar جديد فوق الـ TabBar (بدون نقل عناصر الـ Sidebar) — §1.2.
2. `ApprovalApproved` يترسل عند الموافقة النهائية بس، مش كل خطوة وسيطة — §1.6.2.
3. `ReassignInstanceCommandHandler` يحل "المعتمدين القدامى" من التنفيذ الفعلي وقت الرفض/التسكين،
   مش من تخزين ثابت — §1.6.4.
4. `NotificationsController` بـ `[AnySignedInUser]`، بدون `Screen`/`MenuItem` جديد — §1.7.
5. `Intl.RelativeTimeFormat` بدل أي مكتبة خارجية للتاريخ النسبي — §1.4.

---

## 4. ترتيب Sub-Batches — معتمَد كما في الخطة

2.5.1 (هذا التقرير) → 2.5.2 Domain+Migration → 2.5.3 Application+API (شامل الحقن في الأربع نقاط
من §1.6) → 2.5.4 Frontend → 2.5.5 Tests+Docs. لا اعتماد دائري، كل خطوة تعتمد على اللي قبلها بس.

---

## 5. Stop Conditions — الفحص

| الحالة | النتيجة |
|---|---|
| Backend ناقص | **لا** — كل شيء جاهز (§2). |
| قرار معماري متضارب | **لا** — فجوة تصميم UI واحدة (§1.2)، اتحلّت بتوصية واضحة مش تعارض حقيقي. |

**لا يوجد Stop Condition — جاهز للتنفيذ الكامل بعد الموافقة، شامل تأكيد قرار الـ Navbar (§1.2).**
