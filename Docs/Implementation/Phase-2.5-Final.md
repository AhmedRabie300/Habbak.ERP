# Phase 2.5 — إشعارات داخل التطبيق (Minimal In-App Notifications) — التقرير النهائي

> `docs/Implementation/HR-MASTER-PLAN.md` § Phase 2.5 · Research: `docs/Implementation/Phase-2.5-Research.md` ·
> نُفِّذت كل الـ Sub-Batches (2.5.2 → 2.5.5) في جلسة واحدة بعد الموافقة على الـ Research، بدون توقف.

---

## 1. ملخص تنفيذي

نظام إشعارات داخلي بسيط (In-App بس — بدون Email/SMS/Push، بدون SignalR): كيان `Notification`
واحد، `INotificationService` بنداء مباشر (in-process) من الأربع نقاط الحقيقية في محرك الاعتمادات
(بدء سلسلة، اكتمال خطوة، رفض، إعادة تسكين)، API بـ 6 Endpoints، و`NotificationBell` +
`NotificationsPage` في الـ Frontend. **الـ Badge بيعدّ `RequiresAction = true` غير المقروءة بس —
مش كل الإشعارات غير المقروءة** (القاعدة الجوهرية اللي طلبها المستخدم). اكتُشف بَق زمني حقيقي أثناء
التحقق الفعلي بالمتصفح (تواريخ الـ API من غير علامة UTC) واتصلح محليًا + اتسجّل Task منفصل للإصلاح
الجذري في الـ Backend.

---

## 2. تفاصيل كل Sub-Batch

### 2.5.1 — Research Pass
أهم اكتشاف: **مفيش Navbar علوي في `AppLayout.tsx` أصلًا** — التصميم الحالي Sidebar بس. قرار موثّق
ومتفَق عليه: إضافة صف علوي رفيع جديد فوق الـ `TabBar` يحمل الجرس بس، من غير نقل اسم
المستخدم/الإعدادات/تسجيل الخروج من الـ Sidebar. تفاصيل كاملة + 4 قرارات فرعية أخرى في
`Phase-2.5-Research.md`.

### 2.5.2 — Domain + Migration
- `Notification` entity (`src/Habbak.ERP.Domain/Notifications/Notification.cs`) + `NotificationType`
  enum (10 قيم — 4 مستخدمة فعليًا من محرك الاعتمادات، 6 Schema-ready لمراحل قادمة زي ما حدّد المستخدم).
- `NotificationConfiguration` + تسجيل `RecipientUserId` في `UserReferences.cs` (نفس نمط كل عمود UserId في المشروع).
- Migration (`AddNotifications`) — جدول واحد، Up/Down متماثلين. طُبِّقت بالبروتوكول الكامل: Backup
  (`HabbakErp_PrePhase25.bak`) → Trial (`HabbakErp_Trial_Phase25`) → تحقق (21 عمود، 4 FKs) → Real
  DB (`Command Timeout=180`) → حذف الـ Trial.

### 2.5.3 — Application + API
- `INotificationService`/`NotificationService` — `SendAsync`/`SendToMultipleAsync`، بيحفظ الـ
  Change Tracker كله مش بس صفوفه هو، عشان النداء الأخير في أي Handler يخلي كل حاجة (التغيير +
  الإشعار) تتحفظ سوا Atomic.
- الأوامر: `MarkNotificationAsReadCommand`, `MarkAllNotificationsAsReadCommand`,
  `DeleteNotificationCommand` — كل واحد بيتحقق إن `RecipientUserId == currentUserId` قبل أي تعديل.
- الاستعلامات: `GetMyNotificationsQuery` (Paged، فلتر All/PendingAction/Unread)،
  `GetUnreadCountQuery`، **`GetPendingActionCountQuery`** (`RequiresAction && !IsRead` بالحرف).
- `NotificationsController` (`/api/v1/notifications`) — `[AnySignedInUser]`، بدون `[Screen]` أو
  `MenuItem` جديد (قرار Research §1.7، اتأكّد بـ Test مخصص).
- **التكامل مع محرك الاعتمادات — الأربع نقاط**:
  1. `ApprovalWorkflowService.TryStartApprovalAsync` — إشعار `ApprovalPending` لمعتمدي أول خطوة.
  2. `ApproveStepCommandHandler` — إشعار `ApprovalPending` للخطوة التالية، أو `ApprovalApproved`
     للمُنشئ **عند الموافقة النهائية بس** (قرار Research §1.6.2 — مُتحقَّق منه باختبار مخصص:
     خطوة وسيطة ما تبعتش "Approved").
  3. `RejectStepCommandHandler` — `ApprovalRejected` للمُنشئ.
  4. `ReassignInstanceCommandHandler` — `ApprovalReassigned` للمعتمد القديم (المُتحقَّق فعليًا وقت
     التنفيذ، مش تخزين ثابت) + `ApprovalPending` للمعتمد الجديد.

### 2.5.4 — Frontend
- `frontend/src/features/notifications/` (types.ts, api.ts, `NotificationBell.tsx`,
  `NotificationsPage.tsx`) — خارج `ui-kit` عمدًا لأنه بيمتلك Data Fetching خاص بيه (مش Props بس
  زي `SectionTabs`/`DataGrid`).
- أيقونة `bell` جديدة في `Icon.tsx`، `lib/relativeTime.ts` (`Intl.RelativeTimeFormat` مدمج، صفر
  تبعية جديدة).
- **Navbar علوي جديد** في `AppLayout.tsx` فوق الـ `TabBar` — أول مرة يوجد Navbar منفصل في المشروع،
  يحمل الجرس بس (قرار §2.5.1).
- Route `/notifications`، ترجمات `notifications.*` كاملة في `ar.json`/`en.json`.
- Polling: `pendingActionCount`/`unreadCount` كل 30 ثانية، قائمة الـ Dropdown عند الفتح بس — بدون SignalR.

### 2.5.5 — Tests + Docs
راجع §4 للأرقام النهائية، §5 للبَق المكتشف والمُصلَح، §6 للقرارات الموثّقة.

---

## 3. عدد الملفات

**Backend — جديد (10 ملفات)**: `Notification.cs`، `NotificationConfiguration.cs`، Migration
(ملفين)، `INotificationService.cs`، `NotificationService.cs`، `NotificationDto.cs`،
`NotificationCommands.cs`، `NotificationQueries.cs`، `NotificationsController.cs`.

**Backend — معدَّل**: `UserReferences.cs`، `IApplicationDbContext.cs`، `AppDbContext.cs`،
`ApprovalWorkflowService.cs`، `ApprovalActionCommands.cs`، `Application/DependencyInjection.cs`.

**Backend — Tests (جديد)**: `NotificationServiceTests.cs` (5 اختبارات)، `NotificationsApiTests.cs`
(5 اختبارات)، + 3 اختبارات تكامل جديدة أُضيفت في `ApprovalWorkflowEngineTests.cs` الموجود (بدء
سلسلة بيبعت إشعار، خطوة وسيطة ما تبعتش "Approved"، إعادة التسكين بتبعت للقديم والجديد). كل مواقع
إنشاء `ApprovalWorkflowService`/`ApproveStepCommandHandler`/`RejectStepCommandHandler`/
`ReassignInstanceCommandHandler` المباشرة في اختبارات Phase 2 اتحدّثت لتمرير `INotificationService`
الجديد (Constructor Signature اتغيّر).

**Frontend — جديد (5 ملفات)**: `types.ts`, `api.ts`, `NotificationBell.tsx`,
`NotificationsPage.tsx`, `lib/relativeTime.ts`.

**Frontend — معدَّل**: `Icon.tsx`، `AppLayout.tsx`، `routeTable.tsx`، `ar.json`، `en.json`.

---

## 4. نتائج الاختبارات

- **Integration Tests**: **206/206 ناجحة** (198 موجودة سابقًا + 8 جديدة: 5 `NotificationServiceTests` + 3 تكامل مع محرك الاعتمادات) — 16 دقيقة 17 ثانية.
- **Api Tests**: **269/269 ناجحة** (264 موجودة سابقًا + 5 جديدة `NotificationsApiTests`) — 11 دقيقة 58 ثانية. شمل هذا اكتشاف وإصلاح فجوة بيانات حقيقية في اختبار Phase 2 القديم (`ApprovalWorkflowApiTests` — تفصيل §5).
- **Frontend**: `tsc -b` نظيف (0 أخطاء).

**475 اختبار ناجح إجمالًا، صفر فشل.**

- **تحقق يدوي فعلي بالمتصفح** (ضد API/DB حقيقيين، مش تصريح نظري): الجرس ظاهر في الـ Navbar
  الجديد، الـ Badge بيظهر ويختفي صح، الـ Dropdown بيعرض الإشعارات ببياناتها الصحيحة، رابط "عرض
  الكل" بيودي لـ `/notifications` واللي بيعرض الفلاتر والحالة الفارغة صح. **اتقطعت الجلسة (انتهاء
  صلاحية الدخول، مش متعلّق بالتغييرات) قبل التحقق البصري من إصلاح البَق الزمني** — لكن السبب
  الجذري اتأكّد يقينًا من بيانات الـ API الخام (تفصيل §5)، والإصلاح مباشر ومضمون.

---

## 5. أخطاء/فجوات اتكتشفت وانصلحت أثناء التنفيذ

1. **بَق زمني حقيقي (Timezone)**: الـ API بيرجّع `*AtUtc` من غير علامة UTC (`"2026-09-27T15:29:24"`
   بدل `"...Z"`) — لأن EF Core بيرجّع أعمدة `datetime2` بـ `DateTimeKind.Unspecified`. النتيجة:
   `new Date(...)` في الفرونت إند بيفسّرها كـ Local Time، فبيزيح كل وقت بمقدار فرق التوقيت (فرق ~3
   ساعات على جهاز UTC+3 — بالظبط الفرق اللي شُوهد فعليًا: إشعار لحظي ظهر "قبل 3 ساعات"). **اتصلح
   محليًا بس** في `relativeTime.ts` (إضافة 'Z' لو مفيش علامة زمن). **الإصلاح الجذري (تسجيل خيارات
   Json/EF عام) اتسجّل Task منفصل** — بيأثر على أماكن تانية في المشروع بتعرض `*AtUtc` (زي
   `requestedAtUtc` في `MyPendingApprovalsPage.tsx`)، خارج نطاق Phase 2.5 نفسها.
2. **فجوة بيانات في اختبار Phase 2 قديم**: `ApprovalWorkflowApiTests.Manual_reassign_needs_the_explicit_button_permission_not_just_screen_access`
   كان بيزرع `ApprovalInstance` بـ `CurrentStepOrder = 1` على سلسلة **من غير أي خطوة فعلية** — كان
   شغّال لأن `ReassignInstanceCommandHandler` القديم ماكانش بيدوّر على الخطوة الحالية أصلًا. إضافة
   حل المعتمدين القدامى (عشان الإشعار) كشفت الفجوة (409 Conflict بدل 204) — الاختبار اتصلح بزرع
   خطوة حقيقية، مش الـ Handler، لأن سلوكه الجديد (يتحقق من وجود الخطوة قبل أي إعادة تسكين) أصح.
3. لا فجوات Backend حقيقية أخرى — كل المتطلبات كانت جاهزة من Phase 2 (الأربع نقاط التكامل، الخ).

---

## 6. قرارات اتخذت بدون موافقة مسبقة (موثّقة، مش انحراف صامت)

- **Navbar علوي جديد** فوق الـ `TabBar`، يحمل الجرس بس — بدون نقل عناصر الـ Sidebar (Research §1.2).
- `ApprovalApproved` يترسل عند الموافقة النهائية بس، مش كل خطوة وسيطة في سلسلة متعددة الخطوات
  (تجنب إغراق إشعارات — Research §1.6.2، مُتحقَّق منه باختبار مخصص).
- `NotificationBell`/`NotificationsPage` في `features/notifications/` مش `ui-kit` — بيمتلكوا Data
  Fetching خاص بيهم.
- `NotificationsController` بصلاحية `[AnySignedInUser]` بدون `Screen`/`MenuItem` جديد.
- `Intl.RelativeTimeFormat` المدمج بدل مكتبة خارجية.

---

## 7. المرحلة التالية

**Phase 3 — الحضور والإجازات** — تعتمد على محرك الاعتمادات (Phase 2) ونظام الإشعارات (Phase 2.5)
معًا: طلبات الإجازة/الإضافي محتاجة تنبيه المعتمد فورًا.

**STOP** — مستني "كمّل Phase 3".
