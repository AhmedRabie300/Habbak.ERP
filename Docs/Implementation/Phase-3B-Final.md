# Phase 3B — تكامل أجهزة البصمة — تقرير نهائي

> `Docs/Implementation/HR-MASTER-PLAN.md § Phase 3B`. البحث الأولي المعتمد: `Phase-3B-Research.md`.

---

## 1. ملخص تنفيذي

Phase 3B (3B.2 → 3B.8) اتنفذت كاملة في هذه الجلسة (بيئة Cloud، Linux، بدون SQL Server حقيقي —
راجع §6 "قيود البيئة" قبل اعتماد التقرير نهائيًا):

1. **3B.2** — 4 كيانات Domain جديدة + Migration (شاملة الـFK الناقص على `TimeEntry.DeviceId` من Phase 3).
2. **3B.3** — CRUD كامل لإدارة الأجهزة وربط الموظفين + الاستقبال الفعلي (Push، ADMS-compatible + JSON Fallback) بمصادقة سر مخصصة (مش JWT).
3. **3B.4** — Background Job يترجم RawPunch → TimeEntry، كل 10 دقايق افتراضيًا.
4. **3B.5** — استيراد يدوي من ملف (CsvHelper + ClosedXML) كـFallback شغّال من اليوم الأول.
5. **3B.6/3B.7** — شاشات إدارة الأجهزة/الربط/سجل المزامنة/التسوية + تبويب "ماكينة البصمة" في شاشة الموظف.
6. **3B.8** — اختبارات (تكامل + API) + هذا التقرير.

**صفر انحراف عن قرارات §7 المعتمدة** في `Phase-3B-Research.md` (كلها اتنفذت بالتوصية المذكورة هناك).

---

## 2. النطاق المُنفَّذ

### 2.1 Domain — 4 كيانات (`src/Habbak.ERP.Domain/Attendance/AttendanceDeviceEntities.cs`)

| الكيان | ملاحظة |
|---|---|
| `AttendanceDevice` | Lookup (`ILookupEntity`) — `SerialNumber` فريد على مستوى الـDB كله (مفتاح مطابقة الـPush)، `DeviceSecretHash` (BCrypt عن طريق `IPasswordHasher` الموجود، مش Hash جديد) |
| `AttendanceDeviceLog` | سجل كل محاولة Sync (Push/FileImport) — يتسجل وقت الاستقبال نفسه، مش وقت ترجمة RawPunch→TimeEntry |
| `EmployeeDeviceMapping` | فريد على (`AttendanceDeviceId`, `DeviceUserId`) |
| `RawPunch` | خام، مابيتعدّلش بعد الإدخال. Dedup: Unique Index على (`AttendanceDeviceId`, `DeviceUserId`, `PunchTimestampUtc`) |

`TimeEntry.DeviceId` بقاله FK حقيقي على `AttendanceDevices(Id)` (كان عمود خام من Phase 3، معلَّق عليه
صراحة إنه لـPhase 3B). **Migration**: `AttendanceDevices` (20260928051730) — مولَّدة عن طريق
`dotnet ef migrations add` ضد الـModel بس (مفيش اتصال DB حقيقي وقت التوليد، ومفيش تطبيق فعلي حصل).

### 2.2 Application

- **CRUD**: `AttendanceDevice` (Create يرجّع السر الخام مرة واحدة، Update، Delete بيرفض لو فيه Mappings مرتبطة، RegenerateSecret)، `EmployeeDeviceMapping` (Create/Update/Delete، فحص تكرار DeviceUserId).
- **الاستقبال**: `RawPunchIngestor.IngestAsync` (Static داخلي، مشترك بين Push وFile Import — مفيش نداء ISender متداخل، نفس فلسفة باقي الكود) — Get-or-Create Dedup قبل الإدخال، بيسجّل `AttendanceDeviceLog` مع كل استقبال.
- **الملف**: `RawPunchFileParser` (CsvHelper لـcsv/txt/dat بمحدد مختلف، ClosedXML لـxlsx) — صف مش قابل للتفسير (على الأرجح Header) بيتخطّى بهدوء.
- **الـJob**: `AttendanceDeviceJobs.ProcessCompanyAsync` — منطق قابل للاختبار المباشر بـ`IApplicationDbContext` (بدون DI Container)، بينادي `RecomputeDailyAttendanceCommandHandler` (Phase 3) نداء مباشر بعد كل دفعة.
- **Reconciliation**: `GetRawPunchReconciliationQuery` — تجميع حسب `ProcessingStatus`/`SkipReason` + قائمة استثناءات.

### 2.3 API

| Controller | Route | ملاحظة |
|---|---|---|
| `AttendanceDevicesController` | `/api/v1/hr/attendance-devices` | `[Screen("HR_ATTENDANCE_DEVICES")]` — CRUD + `regenerate-secret` + `import` (رفع ملف) |
| `EmployeeDeviceMappingsController` | `/api/v1/hr/employee-device-mappings` | `[Screen("HR_EMPLOYEE_DEVICE_MAPPINGS")]` |
| `AttendanceDeviceLogsController` | `/api/v1/hr/attendance-device-logs` | `[Screen("HR_ATTENDANCE_DEVICE_LOGS", LookupReads=true)]` — عرض بس |
| `AttendanceReconciliationController` | `/api/v1/hr/attendance-reconciliation` | `[Screen("HR_ATTENDANCE_RECONCILIATION", LookupReads=true)]` — عرض بس |
| `IClockPushController` | `/iclock/cdata` | **Anonymous** — بروتوكول ADMS/iClock المتوافق (GET Handshake + POST Tab-Separated)، السر عن طريق `?key=` |
| `AttendanceDevicePushJsonController` | `/api/v1/hr/attendance-devices/push-json` | **Anonymous** — JSON Fallback، السر عن طريق Header `X-Device-Secret` |

كل الشاشات مسجّلة في `ScreenCodeCatalog.cs` (`HR_ATTENDANCE_DEVICES`، Lookup مُرقَّم) أو
`ScreenSeedData.cs ExtraScreens` (الباقي)، وMenuItemSeedData.cs (أوردرات 20-23 جوه HR).

**🔧 خلل حقيقي اتكشف وانصلح أثناء كتابة الاختبارات (3B.8)**: الـController الاتنين الـAnonymous
مكنش عليهم لا `[Screen]` ولا `[AnySignedInUser]` — ده كان هيكسر اختبار موجود بالفعل
(`SecurityTests.Every_controller_says_which_screen_it_belongs_to`) اللي بيتأكد إن كل Controller في
النظام معلَّم بواحد من الاتنين. الحل: `[AnySignedInUser]` على مستوى الكلاس (نفس نمط `AuthController`
بالظبط) — تأثيره صفر وقت التشغيل الفعلي (`[AllowAnonymous]` على كل Action بيقفل `ScreenPermissionFilter`
قبل ما يوصل للفحص ده أصلًا)، بس بيرضي الفحص الثابت. اتأكد بفحص Reflection مباشر على الـDLL المبني
(الجلسة دي مفيهاش LocalDB لتشغيل `SecurityTests` نفسه) — صفر Controller غير مُصنَّف.

### 2.4 Frontend (3B.6/3B.7)

- `features/hr/attendanceDevices` (List/Edit + كشف السر مرة واحدة + رفع ملف الاستيراد)
- `features/hr/employeeDeviceMappings` (شاشة إدارة مستقلة + مُستخدَمة برضو جوه تبويب الموظف)
- `features/hr/attendanceDeviceLogs`, `features/hr/attendanceReconciliation` (عرض بس)
- تبويب "ماكينة البصمة" (`FingerprintTab.tsx`) في `EmployeeEditPage`
- Routes في `routeTable.tsx`، مفاتيح i18n في `en.json`/`ar.json` (تعديلات مستهدفة، مش إعادة توليد الملف كله — عشان الفورمات الأصلي فيه أجسام JSON مضغوطة سطر واحد ماكنتش هتتحافظ عليها لو استخدمنا `json.dump` عادي).

---

## 3. القرارات المتبعة (من §7 في Phase-3B-Research.md، كلها بالتوصية المذكورة هناك)

1. **بروتوكول الـPush**: ADMS/iClock المتوافق (`/iclock/cdata`، GET Handshake + POST Tab-Separated) + JSON Fallback مستقل.
2. **اتجاه البصمة (In/Out)**: `RawStatus` لو معروف (0=In, 1=Out) وإلا تبادل تلقائي على مستوى (موظف، يوم) شامل أي TimeEntry تاني.
3. **مكتبات الاستيراد**: CsvHelper 33.1.0 + ClosedXML 0.105.1 (أُضيفوا لـ`Habbak.ERP.Application.csproj`).
4. **مصادقة الجهاز**: `DeviceSecretHash` (BCrypt) — تحقق يدوي داخل الـController، مش `[Authorize]` عادي.
5. **Interval الـJob**: 10 دقايق افتراضيًا، قابل للتهيئة (`AttendanceDeviceJob:IntervalMinutes`)، إيقاف عن طريق `AttendanceDeviceJob:Enabled=false` (مُفعَّل في الـApiTests hosts زي `Maintenance:Enabled`).

---

## 4. الأرقام

| | العدد |
|---|---|
| كيانات Domain جديدة | 4 |
| ملفات Application (Commands/Queries/Helpers) | 7 |
| Controllers | 6 (4 Screen-gated + 2 Anonymous) |
| Migrations | 1 (`AttendanceDevices`) |
| ملفات Frontend | 9 (4 مجلد Feature + تبويب الموظف + Routes/i18n) |
| اختبارات تكامل (`AttendanceDevicesTests.cs`) | 9 (تحتاج LocalDB حقيقية) |
| اختبارات Parsing خالصة (`RawPunchFileParserTests.cs`) | 3 (**اتشغّلوا فعليًا في هذه الجلسة ونجحوا**) |
| اختبارات API-over-HTTP (`AttendanceDevicesApiTests.cs`) | 4 (تحتاج LocalDB حقيقية) |

---

## 5. Tests

### 5.1 اللي اتشغّل فعليًا في هذه الجلسة

`RawPunchFileParserTests` (3 اختبارات، مفيهاش أي اعتماد على DB) — نجحوا كامل
(`dotnet test --filter FullyQualifiedName~RawPunchFileParserTests`): Tab-separated `.dat` مع تخطي
Header، CSV بفاصلة، رفض امتداد غير مدعوم.

كل الباقي (**Domain build + Application build + Infrastructure build + API build + كل مشاريع
الاختبارات الأربعة**) اتأكد إنه **بيبني بنجاح** (`dotnet build`، .NET SDK 10.0.112 اتثبَّت في هذه
الجلسة) — صفر Warning جديد، صفر Error. الـMigration (`AttendanceDevices`) اتولَّدت بنجاح عن طريق
`dotnet ef migrations add` (محتاجة الـModel بس، مش اتصال DB حقيقي).

### 5.2 اللي محتاج الجلسة المحلية (LocalDB حقيقية)

- **9 اختبارات تكامل** (`AttendanceDevicesTests.cs`، `IClassFixture<PostingServiceFixture>`): Dedup
  (نفس الدفعة + دفعة تانية)، تحديث `LastSeenAtUtc`، ترجمة الـJob (RawStatus صريح + تبادل تلقائي)،
  Skip قابل لإعادة المحاولة (NoEmployeeMapping) وإعادة المعالجة بعد إضافة الربط، Recompute تلقائي
  بعد المعالجة (يتأكد من صف `Attendance` نفسه)، وقيود الفرادة (SerialNumber، DeviceUserId).
- **4 اختبارات API-over-HTTP** (`AttendanceDevicesApiTests.cs`، `IClassFixture<AccountingApiFactory>`):
  CRUD كامل + السر بيظهر مرة واحدة بس، إنفاذ صلاحية الشاشة، والأهم — إثبات إن الـPush endpoint
  شغّال فعليًا من غير أي JWT/Company Header (نفس جهاز حقيقي)، ورفضه لسر غلط (401).
- **Regression الكامل** (226+273 اختبار من Phase 3 وقبله) — لازم يتشغّل تاني بعد الـMigration
  للتأكد إن إضافة FK جديد على `TimeEntries.DeviceId` ما كسرتش حاجة.

---

## 6. قيود بيئة هذه الجلسة (Cloud، Linux)

1. **مفيش SQL Server/LocalDB حقيقي** — نفس القيد المذكور في الـHandoff الأصلي. اتحل جزئيًا: .NET SDK
   8.0 و10.0 اتثبَّتوا فعليًا في الجلسة (عن طريق `apt-get`) فبقى ممكن نعمل `dotnet build`/
   `dotnet ef migrations add`/تشغيل الاختبارات اللي مالهاش علاقة بـDB — ده تحسين عن جلسات سابقة
   افترضت إن مفيش `dotnet` خالص هنا. لسه محتاج الجلسة المحلية لأي حاجة فعليًا بتلمس LocalDB (تطبيق
   الـMigration، تشغيل IntegrationTests/ApiTests، Regression الكامل).
2. **`npm install` فشل** — `frontend/package.json` فيه Dependency قديم (`xlsx`) مثبَّت من
   `cdn.sheetjs.com` مباشرة (مش من npm registry)، والـProxy في البيئة دي بيرفض الدومين ده تحديدًا
   (403 على مستوى الـCONNECT نفسه، مش مشكلة npm). ده Dependency موجود من قبل (مالوش علاقة بـPhase
   3B)، فمتعدّلش. النتيجة: **كود الـFrontend الجديد (3B.6/3B.7) اتكتب بس ما اتعملوش `tsc`/`vite build`
   ولا Typecheck في هذه الجلسة** — لازم `npm run build` محلي قبل اعتماد الفرونت إند نهائيًا. الكود
   مبني بمطابقة حرفية لنمط شاشات موجودة وشغّالة بالفعل (`WorkShiftPages.tsx`، `CertificationsTab.tsx`،
   `AttachmentPanel.tsx` لرفع الملفات) تقليلًا لاحتمال أي خطأ Type.
3. **🔴 فجوة موروثة من الـResearch نفسه (Phase-3B-Research.md §2)**: بروتوكول ADMS/iClock
   (Handshake response format، شكل الـTab-Separated بالظبط) مبني على معرفة عامة بالسوق، مش توثيق
   رسمي أو اختبار على جهاز فعلي — **لسه محتاج تأكيد على جهاز ZKTeco حقيقي**. الاستيراد اليدوي (3B.5)
   Fallback شغّال بغض النظر عن نتيجة التأكيد ده.

---

## 7. Issues / Known Limitations

1. **دقة اتجاه الجهاز حقيقي غير مؤكَّدة** — القرار الفرعي 2 (RawStatus/تبادل تلقائي) قرار تصميم
   معقول لكنه مش مُتحقَّق ضد بيانات فعلية من جهاز حقيقي (نفس فجوة §6 بند 3).
2. **مصادقة ADMS عن طريق `?key=` في الـQuery String** — بعض أجهزة ZKTeco القديمة مش بتسمح بتخصيص
   URL كامل شامل Query String في إعداد عنوان السيرفر (بس بورت/IP بس)؛ في الحالة دي محتاج Local
   Agent/Proxy وسيط يضيف الـkey، أو نعتمد بالكامل على JSON Fallback بدل ADMS. موثّق كقرار واعي في
   §7 قرار 1 — مش انحراف.
3. **`AttendanceDeviceLog` بيتسجّل وقت الاستقبال بس (Push/Import)، مش وقت ترجمة RawPunch→TimeEntry**
   (بيحصل في Job منفصل بعد كده) — قرار تصميم متعمَّد (§2.1 هنا) عشان الشاشتين (سجل المزامنة والتسوية)
   يفضلوا مسؤوليتين منفصلتين واضحتين.
4. **مفيش خطوة Reconciliation تلقائية تنبّه المستخدم** — شاشة `HR_ATTENDANCE_RECONCILIATION` قراءة
   يدوية بس (Pull)، مفيش Notification تلقائي لو فيه بصمات Skipped كتير. مؤجَّل لمرحلة تالية لو
   احتاج العميل.

---

## 8. المرحلة التالية (لازم تتم محليًا قبل الاعتماد النهائي)

1. `npm install` + `npm run build` (Frontend) — للتأكد من صفر أخطاء TypeScript في الشاشات الجديدة.
2. تطبيق Migration `AttendanceDevices` على Trial DB (Backup أولًا)، التأكد من Up/Down، ثم Real DB.
3. تشغيل `Habbak.ERP.IntegrationTests` (خصوصًا `AttendanceDevicesTests`) و`Habbak.ERP.ApiTests`
   (خصوصًا `AttendanceDevicesApiTests`) ضد LocalDB حقيقية.
4. Regression كامل لكل المشروع (IntegrationTests + ApiTests) — التأكد من صفر كسر بسبب FK الجديد
   على `TimeEntries.DeviceId` أو أي تعديل تاني.
5. لو متاح جهاز ZKTeco فعلي — تجربة الـPush الحقيقي (`/iclock/cdata`) وتأكيد شكل الـHandshake/
   Tab-Separated الفعلي مقابل الافتراض في §2 من الـResearch، وتعديل `IClockPushController`/
   `ParseAttLog` لو الشكل الفعلي مختلف.

**Phase 3C** — بعد اعتماد هذا التقرير، بنود 1-4 من `Remarks8-HR-Enhancements.md` (العقود والمرفقات).
