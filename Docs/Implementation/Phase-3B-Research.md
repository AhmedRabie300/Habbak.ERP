# Phase 3B — Research Pass (تكامل أجهزة البصمة)

**التاريخ**: 2026-09-28
**المرجع**: `docs/Implementation/HR-MASTER-PLAN.md § Phase 3B` + `Docs/ERP-Review/12-HR-Payroll-Analysis.md` (سطر 183) + `Docs/Modules/10-Module-HR-Payroll.md` قرار 4.

---

## 1. حالة الكود الفعلي

### 1.1 `TimeEntry` / `TimeEntrySource` (موجودين من Phase 3)

- `src/Habbak.ERP.Domain/Attendance/Enums.cs:11` — `TimeEntrySource { Manual=1, SelfService=2, Kiosk=3, POSShift=4, Device=5 }`. **`Device` موجودة بالفعل كقيمة Enum غير مستخدَمة** — مفيش أي كود بيعيّنها حاليًا.
- `src/Habbak.ERP.Domain/Attendance/AttendanceEntities.cs:67-91` — `TimeEntry` عنده بالفعل:
  ```csharp
  public long? DeviceId { get; set; }   // "Phase 3B (AttendanceDevice) — عمود خام بدون FK دلوقتي"
  ```
  يعني تعليق مكتوب مسبقًا وقت Phase 3 بالظبط لهذه المرحلة — العمود موجود، لكن من غير FK حقيقي لسه. **3B.2 لازم تضيف الـ FK على `AttendanceDevices(Id)` عن طريق Migration (`AddForeignKey`، مش عمود جديد)**.
  - كل الأعمدة التانية (`EntryType`, `TimestampUtc`, `Status`, `IsCorrection`, `CorrectsTimeEntryId`) جاهزة وتتقبل صف `Device` من غير أي تعديل تاني على `TimeEntry` نفسها.

### 1.2 `Employee` (من Phase 1.1)

`src/Habbak.ERP.Domain/HR/Employee.cs:16-52` — الحقول ذات الصلة: `Id`, `CompanyId`, `BranchId`, `Code`, `Status` (`EmployeeStatus`، لازم `Active` زي كل مكان تاني في Phase 3)، `UserId?` (منفصل تمامًا عن `EmployeeDeviceMapping.DeviceUserId` — الاتنين "UserId" لكن معناهم مختلف كليًا: واحد لحساب الدخول في النظام، التاني رقم المستخدم على جهاز البصمة نفسه). لا يوجد أي حقل بصمة حاليًا على `Employee` — الربط هيكون بالكامل عن طريق `EmployeeDeviceMapping` الجديدة، بدون أي تعديل على `Employee`.

### 1.3 `AttachmentsPanel` (Frontend)

**مش موجود.** بحث كامل في `frontend/src` ما لقاش أي Component بالاسم ده — تبويبات المستندات/الشهادات (Phase 1.5.4) مبنية بمكوّنات مخصصة لكل تبويب، مفيش Component عام قابل لإعادة الاستخدام. **مش مطلوب لـ Phase 3B أصلًا** (الشاشات المطلوبة هنا: إدارة أجهزة + سجل Sync + تبويب "ماكينة البصمة" في شاشة الموظف — لا واحدة فيهم محتاجة رفع ملفات مرفقات، الاستيراد نفسه له شاشة/Endpoint مخصص في 3B.5). نقطة الفحص دي أُلغيت من الاعتماد.

### 1.4 نمط الـ Jobs الموجود (Precedent لـ 3B.4)

`src/Habbak.ERP.Infrastructure/Services/MaintenanceJobs.cs`:
- `MaintenanceHostedService : BackgroundService` — بيستنى دقيقتين بعد الإقلاع، ثم Loop `while (!stoppingToken.IsCancellationRequested) { RunOnceAsync(); await Task.Delay(interval); }`. بيتقفل عن طريق `configuration["Maintenance:Enabled"] == "false"` (مطفي في ApiTests Host).
- `FixedAssetJobs.RunAsync(today, ct)` — بيجيب كل الشركات النشطة، وبيلف عليهم واحدة واحدة، كل شركة جوه `BackgroundCompanyScope.Begin(companyId)` (نفس نمط الـ Background Jobs العام في المشروع كله)، وأي Exception في شركة واحدة ما بتوقفش الباقي.
- **القرار**: Job الـ `RawPunch → TimeEntry` (3B.4) هيتبني بنفس البنية بالظبط — `AttendanceDeviceProcessingHostedService : BackgroundService` جديد، بينادي `AttendanceDeviceJobs.RunAsync` (لف على الشركات، `BackgroundCompanyScope` لكل شركة)، Interval مستقل عن Maintenance (كل 5-15 دقيقة زي ما الخطة بتقول، مش كل 24 ساعة).

### 1.5 نمط الـ Idempotency الموجود

`src/Habbak.ERP.Application/FixedAssets/DepreciationCommands.cs:28-35` — `DepreciationRunKeys.For(companyId, year, month)`: `Guid` ثابت مُشتَق من `SHA256("{companyId}:DepreciationRun:{year}-{month}")` — نفس المفتاح بالظبط في أي إعادة محاولة، فحص "already exists" قبل الإنشاء. هذا النمط **مناسب لمفتاح تشغيلة الـ Job نفسها** (مثلاً منع تشغيلتين متزامنتين لنفس الشركة)، لكن مش مناسب لمنع تكرار البصمة الواحدة نفسها (ده موضوع Deduplication في §5 تحت، بمفتاح مختلف تمامًا مبني على بيانات البصمة نفسها مش على "شهر التشغيل").

`src/Habbak.ERP.Domain/Common/ProcessedIdempotencyKey.cs` — الجدول العام لأي عملية HTTP حساسة لإعادة الإرسال (`IdempotencyKey` + `OperationType` + `ResultJson`, احتفاظ 7 أيام). **مش الأنسب هنا** — دي مبنية على مفتاح بيُبعت في الـ Request نفسه (Client-generated)، بينما بصمة الجهاز مالهاش مفهوم "Idempotency-Key Header" أصلًا؛ الـ Deduplication هنا لازم يبقى مبني على بيانات البصمة ذات نفسها (جهاز + رقم مستخدم + وقت).

---

## 2. بحث: مكتبات ZKTeco / File Parsing

- **مفيش أي مكتبة/SDK خاصة بـ ZKTeco أو Suprema في المشروع حاليًا** (`grep` كامل على كل `.csproj` رجّع صفر نتائج). المشروع لسه ما اتعاملش مع أي جهاز بصمة فعليًا.
- **مفيش أي مكتبة File Parsing (XLS/CSV) في المشروع حاليًا** (لا ClosedXML، لا EPPlus، لا NPOI، لا CsvHelper). الاستيراد الوحيد الموجود في النظام كله هو رفع مرفقات عامة (`AttachmentsController`, `IFormFile`) — بايتات خام بتتخزن، من غير أي Parsing لمحتواها.
- **معلومة عامة (مش مُتحقَّق منها في الكود، من معرفة عامة بالبروتوكول التجاري لـ ZKTeco، تحتاج تأكيد على جهاز فعلي وقت 3B.3)**:
  - بروتوكول الـ Push المعتاد لأجهزة ZKTeco اسمه **ADMS / iClock**: الجهاز بيتظبط من قايمته الداخلية بعنوان IP وبورت السيرفر، وبيبعت handshake أولي `GET /iclock/cdata?SN=<serial>&options=...`، وبعدين بصمات فعلية بـ `POST /iclock/cdata?SN=<serial>&table=ATTLOG` بجسم **نص خام Tab-Separated** (مش JSON): كل سطر `PIN\tTime\tStatus\tVerifyType\tWorkCode...`. لو ده صحيح فعليًا، الـ Endpoint في 3B.3 يبقى لازم يقبل نفس المسار/الشكل ده بالحرف (الجهاز مش بيتغيّر شكل بعتته)، مش عقد JSON من تصميمنا.
  - التصدير عن طريق USB عادة بيطلع ملف `.dat`/`.txt` (نفس شكل الـ Tab-Separated أعلاه)، مش XLS/CSV بالضرورة زي ما الخطة الأصلية افترضت.
  - **قرار مقترح للمكتبات** (تغطي الحالتين): **`CsvHelper`** للـ Parsing العام لأي ملف نصي مفصول (CSV فعلي، أو `.dat`/`.txt` بمحدد Tab بدل فاصلة — نفس المكتبة تتعامل مع الاتنين بتغيير `Delimiter`)، + **`ClosedXML`** لو عميل معيّن صدّر XLS فعلي من برنامج إدارة الجهاز (بعض واجهات الإدارة بتصدّر XLS جاهز). الاتنين NuGet قياسي، Open Source، بدون تكلفة ترخيص.
  - **🔴 فجوة تحتاج تأكيد فعلي على جهاز حقيقي وقت 3B.3/3B.5** — البروتوكول أعلاه من معرفة عامة بالسوق مش من توثيق رسمي متاح هنا أو اختبار مباشر؛ مفيش جهاز فعلي متاح للفحص وقت هذا الـ Research Pass.

---

## 3. شكل البيانات الخام — الأربع كيانات

### 3.0 الثلاثة كيانات الأخرى (مختصر — التفصيل الكامل جوه 3B.2)

```csharp
public class AttendanceDevice : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string? Model { get; set; }              // "ZKTeco K40" وهكذا — نص حر، مش Enum (تنوع الموديلات كبير)
    public string? SerialNumber { get; set; }        // SN اللي الجهاز بيبعته في البروتوكول (§7 قرار 1)
    public bool IsActive { get; set; } = true;
    public string DeviceSecretHash { get; set; } = null!;  // Hash فقط، زي كلمة مرور — القرار الفرعي 4
    public DateTime? LastSeenAtUtc { get; set; }     // آخر Push/Import ناجح — يغذي شاشة سجل الأجهزة (3B.6)
}

public class AttendanceDeviceLog : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;
    public RawPunchSourceType SyncType { get; set; }        // Push / FileImport — نفس Enum المستخدم في RawPunch
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public AttendanceDeviceLogStatus Status { get; set; }    // Success / PartialFailure / Failed
    public int PunchesReceived { get; set; }
    public int PunchesProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmployeeDeviceMapping : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;
    public string DeviceUserId { get; set; } = null!;   // رقم المستخدم على الجهاز نفسه — لا علاقة له بـ Employee.UserId
}
```

- **`EmployeeDeviceMapping` فريد على `(AttendanceDeviceId, DeviceUserId)`** — نفس رقم المستخدم على نفس الجهاز ميتكررش لموظفين مختلفين (Validation صريحة مذكورة في 3B.7 من الخطة الأصلية).
- موظف واحد ممكن يكون ليه أكتر من صف (أكتر من جهاز/فرع) — مفيش قيد Unique على `EmployeeId` لوحده.

### 3.1 `RawPunch`

الكيان الرابع، الأهم لأنه بيحمل البيانات الخام كما وصلت فعليًا:

```csharp
public class RawPunch : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;

    public string DeviceUserId { get; set; } = null!;      // خام من الجهاز، مش FK — يترجم لـ EmployeeId وقت المعالجة
    public DateTime PunchTimestampUtc { get; set; }
    public int? RawStatus { get; set; }                    // In/Out/Break.. زي ما الجهاز بعتها، خام
    public int? RawVerifyType { get; set; }                // بصمة/بطاقة/وش.. خام، للتوثيق بس

    public RawPunchSourceType SourceType { get; set; }      // Push / FileImport
    public DateTime ReceivedAtUtc { get; set; }
    public RawPunchProcessingStatus ProcessingStatus { get; set; } = RawPunchProcessingStatus.Pending;
    public DateTime? ProcessedAtUtc { get; set; }
    public long? ResultTimeEntryId { get; set; }
    public TimeEntry? ResultTimeEntry { get; set; }
    public RawPunchSkipReason? SkipReason { get; set; }     // NoEmployeeMapping / DeviceNotRegistered / DuplicatePunch
}
```

- **خام ومابيتعدّلش أبدًا بعد الإدخال** (نفس مبدأ `TimeEntry` نفسها — "التصحيح بصف جديد") — عمود `ProcessingStatus`/`ResultTimeEntryId`/`SkipReason` بس هما اللي بيتحدّثوا بعد كده، مش بيانات البصمة الأصلية.
- **Deduplication Key عند الإدخال نفسه** (مش وقت المعالجة): Unique Index على `(AttendanceDeviceId, DeviceUserId, PunchTimestampUtc)` — نفس البصمة بالظبط من مصدرين (Push مزدوج، أو Push + Import لنفس اليوم) بترفض/تتجاهل عند الإدخال (Get-or-Create، نفس نمط idempotent-skip اللي استُخدم في المولّد الجماعي بـ Phase 3)، مش بعد ما تتحوّل لـ `TimeEntry`.

---

## 4. استراتيجية الـ Job — `RawPunch → TimeEntry`

`AttendanceDeviceJobs.RunAsync` (يشتغل كل شركة لوحدها، `BackgroundCompanyScope`):

1. يجيب كل `RawPunch` بحالة `Pending` مرتبة بالوقت.
2. لكل صف: يدوّر عن `EmployeeDeviceMapping` مطابقة (`AttendanceDeviceId` + `DeviceUserId`). مفيش تطابق → `ProcessingStatus = Skipped`, `SkipReason = NoEmployeeMapping` (بيفضل قابل لإعادة المحاولة لاحقًا لو الربط اتضاف بعد كده — الفحص بيتكرر تاني في التشغيلة الجاية، مش Terminal).
3. تطابق موجود → إنشاء `TimeEntry` جديد (`Source = Device`, `DeviceId = AttendanceDeviceId`, `EmployeeId`, `TimestampUtc = PunchTimestampUtc`, `EntryType` مُشتق من `RawStatus` لو موجود قيمة معروفة، وإلا بديل بسيط: أول بصمة في اليوم = In، اللي بعدها بالتبادل — **قرار فرعي يحتاج تأكيد، §7 تحت**). `ProcessingStatus = Processed`, `ResultTimeEntryId` بيتسجل.
4. Recompute اليومي (`RecomputeDailyAttendanceCommand`, موجود من Phase 3) بينادَى لنفس الموظف/اليوم بعد كل دفعة معالجة — نفس ما بيحصل من مصادر `TimeEntry` التانية أصلًا.

---

## 5. Reconciliation

Query جديدة (`GetRawPunchReconciliationQuery(deviceId?, fromDate, toDate)`) بترجّع تجميع حسب `SkipReason`/`ProcessingStatus` — عدد البصمات الخام مقابل عدد `TimeEntry` الناتجة، وقائمة الاستثناءات (بصمة موظف مش مربوط، جهاز مش مسجّل) مع رابط مباشر لعمل الربط الناقص. شاشة عرض بس (Read-only)، جزء من 3B.6 (Frontend).

---

## 6. Sub-Batches (3B.2 → 3B.8)

| Sub-Batch | المحتوى | ملاحظة |
|---|---|---|
| **3B.2** | Domain: 4 كيانات (`AttendanceDevice`, `AttendanceDeviceLog`, `RawPunch`, `EmployeeDeviceMapping`) + Migration (شامل إضافة الـ FK الناقص على `TimeEntry.DeviceId`) | لا تعديل على `TimeEntry`/`Employee` نفسهم غير الـ FK |
| **3B.3** | Push API + Auth مخصص للجهاز (مش JWT) | يحتاج قرار مبدئي عن شكل البروتوكول (§2) — الأقرب للواقع: Endpoint يقبل الشكلين (JSON عام لأي جهاز يدعمه + مسار متوافق مع ADMS لو أجهزة ZKTeco فعلية) |
| **3B.4** | `AttendanceDeviceJobs` (BackgroundService، نفس نمط `MaintenanceJobs`) + Deduplication + Reconciliation Query | |
| **3B.5** | File Import (CsvHelper + ClosedXML) — Endpoint رفع يدوي، نفس مسار المعالجة اللي بيمشي عليه Push بعد كده (مصدر مختلف بس نفس `RawPunch`) | Fallback شغّال من اليوم الأول زي ما الخطة بتطلب |
| **3B.6** | Frontend: إدارة الأجهزة، سجل Sync/الأخطاء، شاشة Reconciliation | |
| **3B.7** | تبويب "ماكينة البصمة" في `EmployeeEditPage` (نفس نمط تبويبات العقود/المستندات) | Validation: `DeviceUserId` غير مكرر لنفس الجهاز |
| **3B.8** | Tests + Docs | |

---

## 7. القرارات الفرعية (تحتاج "موافق" صريح)

1. **بروتوكول الـ Push**: هل نبني Endpoint متوافق مع ADMS/iClock الفعلي (Tab-Separated، مسار `/iclock/cdata`) عشان أجهزة ZKTeco الحقيقية تشتغل من غير أي وسيط، ولا نبني عقد JSON بتصميمنا (أبسط لكن محتاج Local Agent يترجم لاحقًا، يتعارض مع "Push من أول يوم")؟ **الموصى به: ADMS المتوافق**، لأنه الأقرب لهدف "90% من الكافيهات عندها جهاز شغّال بالفعل".
2. **مصدر تحديد اتجاه البصمة (In/Out)**: بعض الأجهزة بتبعت `Status` صريح (0=In, 1=Out) جوه البصمة، وبعضها لأ. القرار: لو `RawStatus` مبعوت ومعروف نستخدمه، وإلا Fallback للتبادل التلقائي (أول بصمة في اليوم In، اللي بعدها Out، وهكذا) — **نفس منطق أي نظام بصمة بسيط، لازم موافقة صريحة قبل 3B.4**.
3. **مكتبات الاستيراد**: `CsvHelper` + `ClosedXML` (اقتراح، مفيش أي منهم في المشروع حاليًا — يحتاج إضافة NuGet جديدة).
4. **مصادقة الجهاز في 3B.3**: `AttendanceDevice.DeviceSecretHash` (مفتاح عشوائي بيتولّد وقت تسجيل الجهاز، بيتخزن Hash بس زي كلمة المرور) بيتبعت في Query String أو Header حسب البروتوكول المُختار في القرار 1 — تحقق يدوي جوه الـ Action نفسه (نفس روح `AuthController` الحالي اللي هو `[AllowAnonymous]` ومصادقته يدوية، مش عن طريق `[Authorize]`/`[Screen]` العاديين لأن مفيش مستخدم/JWT هنا أصلًا).
5. **Interval الـ Job**: 10 دقايق افتراضيًا (نص المدى المذكور في الخطة 5-15)، قابل للتهيئة عن طريق `appsettings` زي `Maintenance:Enabled`.

---

## 8. الخلاصة

مفيش أي Migrations أو تنفيذ حصل في هذا الـ Research Pass. الفجوة الأكبر المكتشفة: **مفيش أي مكتبة/بروتوكول موجود بالفعل في المشروع لأجهزة البصمة أو لتحليل ملفات** — القسم كله (§2، §7 بند 1) مبني على معرفة عامة بالسوق مش على كود أو توثيق رسمي داخل المستودع، ومحتاج تأكيد على جهاز حقيقي أو توثيق SDK فعلي قبل قفل 3B.3 نهائيًا. باقي التصميم (الكيانات، الـ Job، الـ Deduplication) مبني مباشرة على أنماط موجودة وموثّقة فعليًا في الكود (`MaintenanceJobs`, `FixedAssetJobs`, `DepreciationRunKeys`).
