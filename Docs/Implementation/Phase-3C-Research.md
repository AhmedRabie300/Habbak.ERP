# Phase 3C — Research Pass (Contract Lines + Contract/Certification Attachments + Hiring Wizard Lines)

**التاريخ**: 2026-09-28
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md § Phase 3C` + `Docs/My Remarks/Remarks8-HR-Enhancements.md` (بنود 1-4، سطور 66-101)

---

## 0. الملفات المفحوصة (مسارات فعلية)

| الملف المطلوب فحصه | المسار الفعلي | ملاحظة |
|---|---|---|
| `EmploymentContract.cs` | `src/Habbak.ERP.Domain/HR/EmploymentContract.cs` | — |
| `EmploymentContractConfiguration.cs` | `src/Habbak.ERP.Infrastructure/Persistence/Configurations/HR/HrConfigurations.cs:147` | مش ملف مستقل — كلاس جوه ملف مشترك |
| `EmployeeCertification.cs` | `src/Habbak.ERP.Domain/HR/EmployeeCertification.cs` | — |
| `EmployeeDocument.cs` | `src/Habbak.ERP.Domain/HR/EmployeeDocument.cs` | نمط `AttachmentId` المرجعي |
| `AttachmentsPanel.tsx` | **غير موجود** — `frontend/src/ui-kit/AttachmentPanel.tsx` | اسم مختلف (بدون s) ونمط مختلف (§1.4) |
| `HiringWizardPage.tsx` | `frontend/src/features/hr/hiring/HiringWizardPage.tsx` | — |
| `ContractStep.tsx` | `frontend/src/features/hr/hiring/steps/ContractStep.tsx` | — |
| `EmployeeEditPage.tsx` | `frontend/src/features/hr/employees/EmployeeEditPage.tsx` | تبويبات فقط، مفيش منطق يخص البنود هنا |
| `ContractsTab.tsx` | `frontend/src/features/hr/employees/tabs/ContractsTab.tsx` | — |
| `CertificationsTab.tsx` | `frontend/src/features/hr/employees/tabs/CertificationsTab.tsx` | — |

بالإضافة لملفات مش مطلوبة صراحة بس ضرورية لفهم التدفّق الكامل: `DocumentsTab.tsx` (نمط الرفع الفعلي)، `AttachmentsController.cs` + `AttachmentEntityTypes.cs` (الـ Endpoint العام)، `HrEmploymentControllers.cs` (طبقة الـ API)، `BranchRequestLine.cs` + `BranchRequestConfiguration.cs` + `UpdateBranchRequestCommand.cs` (نمط 1:N + Replace-All)، وكل أوامر `EmploymentContracts`/`EmployeeCertifications` (Create/Update/Renew/Delete) في `src/Habbak.ERP.Application/HR/`.

---

## 1. حالة الكود الفعلي

### 1.1 `EmploymentContract` (`src/Habbak.ERP.Domain/HR/EmploymentContract.cs`)

عنده فعلًا `BasicSalary` + `InsurableWage` بس (رقمين Scalar)، مفيش أي `ICollection<...>` أو `AttachmentId`. الـ EF Config مش في ملف مستقل (`EmploymentContractConfiguration.cs` غير موجود بالاسم ده) — موجود جوه `src/Habbak.ERP.Infrastructure/Persistence/Configurations/HR/HrConfigurations.cs:147` كـ `EmploymentContractConfiguration` (كلاس داخل ملف مشترك فيه كل تكوينات HR Batch 3، مش ملف منفصل لكل كيان). حاليًا: `ToTable`, `BranchId.IsRequired()`, `BasicSalary`/`InsurableWage` بـ `HasPrecision(18,4)`, وعلاقتين `HasOne` (`Employee`, `PreviousContract`) بـ `DeleteBehavior.Restrict`.

`DeleteEmploymentContractCommand` بيعمل **Hard Delete فعلي** (`db.EmploymentContracts.Remove`) — مش Soft Delete، والحماية الوحيدة من حذف عقد له تجديد هي FK Restrict على `PreviousContractId`.

### 1.2 `EmployeeCertification` (`src/Habbak.ERP.Domain/HR/EmployeeCertification.cs`)

`NameAr`, `NameEn`, `Issuer`, `IssueDate`, `ExpiryDate?`, `CertificateNumber?` — مفيش `AttachmentId`. الـ Config جوه نفس ملف `HrConfigurations.cs:179`.

### 1.3 نمط `EmployeeDocument.AttachmentId` (Phase 1.1، B3)

`EmployeeDocument.AttachmentId` (`long`, **مش nullable**) — FK مباشر Scalar على `Attachment`، مش Polymorphic Lookup (`EntityType`/`EntityId`) رغم إن `Attachment` نفسه مصمم Polymorphic أصلًا. السبب الموثّق في تعليق الكيان: "الطلب صريح إنها علاقة 1:1 مستند-لملف". الـ Config (`HrConfigurations.cs:163`): `HasOne(d => d.Attachment).WithMany().HasForeignKey(d => d.AttachmentId).OnDelete(DeleteBehavior.Restrict)`.

**تدفّق الرفع الفعلي** (`DocumentsTab.tsx:24-27,58-66`): مفيش رفع داخل Endpoint المستند نفسه. الشاشة بترفع الملف الأول عن طريق `/api/v1/attachments` العام (`AttachmentsController.Upload`، `EntityType="EmployeeDocument"`, `EntityId=0` وقتها لأن المستند لسه مش موجود)، وبتاخد الـ `id` الراجع، وبعدين تبعته كـ `attachmentId` جوه `CreateEmployeeDocumentCommand`. `EmployeeDocument` موجودة فعلًا جوه `AttachmentEntityTypes.All` (`src/Habbak.ERP.Domain/Common/AttachmentEntityTypes.cs:24`) — الـ Scalar FK هو العلاقة الحقيقية، لكن سطر الـ Attachment نفسه لازم يحمل قيمة `EntityType` معروفة (مش نص حر) وقت الرفع.

### 1.4 `AttachmentsPanel.tsx` — **الاسم في الطلب غلط، والمكوّن الموجود بيمثّل نمط مختلف تمامًا**

مفيش ملف اسمه `AttachmentsPanel.tsx`. الموجود فعليًا: `frontend/src/ui-kit/AttachmentPanel.tsx` (بدون s) — ده مكوّن **Polymorphic عام متعدد الملفات** (`entityType` + `entityId`، قائمة كاملة برفع/تنزيل/حذف، بيستخدم `useAttachmentsList`/`useUploadAttachment`/`useDeleteAttachment` من `frontend/src/features/common/attachments/api.ts`). ده **مش نفس نمط `EmployeeDocument`** (رفع أول، ثم FK Scalar واحد) — `AttachmentPanel` بيفترض N مرفقات لكل سجل ومفيش FK Scalar على الكيان نفسه خالص. بنود 2 و3 بيطلبوا صراحة نفس نمط `EmployeeDocument.AttachmentId` (FK واحد)، فمكوّن `AttachmentPanel` الحالي **مش المطلوب استخدامه هنا** — الشكل الصح أقرب لزرار الرفع البسيط جوه `DocumentsTab.tsx` نفسه (رفع → `attachmentId` واحد يترّبط بالسجل).

### 1.5 `HiringWizardPage.tsx` + `ContractStep.tsx`

Wizard بخمس خطوات ثابتة (`employee → personalData → contract → documents → user`)، **للأمام بس، مفيش رجوع** (قرار متعمَّد من 1.5.5 — أي تصحيح بعد كده من شاشة الموظف الكاملة). `ContractStep` بينادي `useCreateContract` بنفس حقول `ContractsTab` وقت الإنشاء بالظبط (`contractType`, `startDate`, `endDate?`, `probationEndDate?`, `basicSalary`, `insurableWage`, `workingHoursPerDay`) — **Create دايمًا هنا، مفيش تجديد** (الموظف جديد). لا `ContractStep` ولا `ContractsTab` بيستخدموا `react-hook-form` — الاتنين `useState` بسيط لكائن Form واحد.

### 1.6 `EmployeeEditPage.tsx` + `ContractsTab.tsx` + `CertificationsTab.tsx`

- **`ContractsTab`**: List (`DataGrid`) + Create/Renew/Terminate. **مفيش Update مربوط بالفرونت إند خالص** رغم إن `UpdateEmploymentContractCommand` موجود بالفعل Backend (غير مستخدَم من أي شاشة حاليًا).
- **`CertificationsTab`**: List + Create/Delete بس. تعليق صريح جوه الملف: "مفيش تعديل بعد الإنشاء في هذه المرحلة (حذف وإعادة إضافة بدل Update)" — و`UpdateEmployeeCertificationCommand` كمان موجود Backend بس **غير مستخدَم أبدًا** من أي شاشة.

هذه فجوة مباشرة على بند 2 و3: زرار "رفع مرفق" على سجل **موجود بالفعل** (العقد/الشهادة اتحفظوا من غير مرفق وقتها) محتاج مسار تحديث جزئي — مش عاوزين نجبر المستخدم يعيد كتابة كل حقول العقد/الشهادة بس عشان يرفع ملف، ولا نعتمد على `UpdateEmploymentContractCommand`/`UpdateEmployeeCertificationCommand` الحاليين لأنهم مصممين لتصحيح كل البيانات مرة واحدة (خطر الكتابة فوق حقول بفورم قديم في الذاكرة).

### 1.7 طبقة الـ API Controllers (`HrEmploymentControllers.cs`)

`EmploymentContractsController` (Route: `/api/v1/hr/employees/{employeeId}/contracts`, `[Screen("HR_EMPLOYEES")]`) عنده `record` منفصل لكل نوع Request (`CreateEmploymentContractRequest`, `UpdateEmploymentContractRequest`, `RenewEmploymentContractRequest`) بيعكس توقيع الـ Command بنفس الترتيب حرفيًا — أي حقل جديد (`Lines`, لاحقًا `AttachmentId` عن طريق Endpoint مستقل) لازم يتضاف للثلاثة `record` دول مع بعض + الـ 3 نداءات `mediator.Send` المقابلة، وإلا الـ Controller هيفضل يبعت القيمة الافتراضية بصمت. نفس القصة بالظبط منتظرة جوه `EmployeeCertificationsController` (نفس الملف) لكن أبسط (Create/Update بس، مفيش Renew).

---

## 2. بحث: نمط 1:N الموجود (Line Entities)

`EmploymentContractLine` **غير موجود — مؤكَّد** (صفر نتيجة بحث). `PurchaseOrderLine` نفسه غير موجود كملف كيان مستقل (بس `PurchaseOrderLineBuilder.cs` Helper) — النموذج الأوضح فعليًا 1:N في المشروع هو `BranchRequestLine` (`src/Habbak.ERP.Domain/Inventory/BranchRequestLine.cs` + `BranchRequestConfiguration.cs:20-41`):

```csharp
builder.HasOne(l => l.BranchRequest)
    .WithMany(r => r.Lines)
    .HasForeignKey(l => l.BranchRequestId)
    .OnDelete(DeleteBehavior.Cascade);   // الأب بيتحذف → البنود بتتحذف معاه
```

و**تحديث البنود** (`UpdateBranchRequestCommandHandler`) بيمشي بنمط **Replace-All** ثابت في المشروع: `db.BranchRequestLines.RemoveRange(branchRequest.Lines)` ثم إعادة إضافة القائمة الجديدة كاملة — مش Diff بند-ببند. نفس النمط ده هو المرشّح المباشر لـ `EmploymentContractLine`.

**Frontend**: نمط الـ Line Editor المتكرر في كل الشاشات المشابهة (`BranchRequestEditPage.tsx`, `PurchaseOrderEditPage.tsx`, `WarehouseDocumentEditPage.tsx`, إلخ) هو `react-hook-form` + `useFieldArray` (`fields, append, remove`). **لكن** `ContractsTab`/`ContractStep` الحاليين مبنيين بـ `useState` بسيط بدون `react-hook-form` خالص — إدخال `useFieldArray` هنا معناه إما (أ) تحويل الفورم بالكامل لـ `react-hook-form` (تكلفة أكبر من المطلوب)، أو (ب) `useState<LineFormRow[]>` بسيط بنفس فلسفة باقي الـ Tab (Add/Remove Row يدوي بـ `setForm`). **الموصى به: (ب)** — يحافظ على اتساق الملف نفسه، والـ Line Editor هنا بسيط (صفوف قليلة) مش محتاج آلة `react-hook-form` الكاملة.

`SalaryComponent` **غير موجود فعلًا** ومؤجّل لـ Phase 4 (Payroll) — تأكيد إضافي إن `EmploymentContractLine` هنا مجرد بنود وصفية على العقد نفسه (بدلات/خصومات تظهر في العقد)، مش محرك حساب رواتب — الفصل بينه وبين `SalaryComponent` المستقبلي مقصود ومتوقَّع من الخطة نفسها.

---

## 3. القرارات المقترحة

### 3.1 `EmploymentContractLine` (بند 1 و4)

```csharp
public enum ContractLineType { Earning = 1, Deduction = 2 }   // HR/Enums.cs

public class EmploymentContractLine : AuditableEntity
{
    public long EmploymentContractId { get; set; }
    public EmploymentContract EmploymentContract { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public decimal Amount { get; set; }
    public ContractLineType Type { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsInsurable { get; set; }
    public int Order { get; set; }
}
```

- `EmploymentContract.Lines` (`ICollection<EmploymentContractLine>`), Config جديد `EmploymentContractLineConfiguration` جوه `HrConfigurations.cs` (نفس ملف باقي تكوينات HR B3) — `ToTable("EmploymentContractLines")`, `Amount` بـ `HasPrecision(18,4)`, `HasOne(l => l.EmploymentContract).WithMany(c => c.Lines).OnDelete(DeleteBehavior.Cascade)` (نفس `BranchRequestLine` بالظبط — العقد مبيتحذفش غالبًا لكن لو اتحذف Hard Delete فعلي §1.1، البنود لازم تروح معاه مش تفضل يتيمة).
- **مفيش `ICompanyScopedEntity`/`IBranchScopedEntity` على السطر نفسه** — نفس منطق `BranchRequestLine`/`JournalEntryLine`: الـ Scoping بيتوارث من الأب (`EmploymentContract`) عن طريق الـ FK، مش عمود مكرر على كل سطر.
- **`Create`/`Update`/`Renew` الثلاثة لازم ياخدوا `IReadOnlyList<ContractLineInput> Lines`** (نفس توقيع `UpdateBranchRequestCommand.Lines`)، والـ Handler يبني/يستبدل البنود بنفس نمط Replace-All (§2).

### 3.2 Backfill — `BasicSalary` **ماينقلش لسطر**

**القرار: لأ**. `BasicSalary`/`InsurableWage` يفضلوا أعمدة Scalar منفصلة على `EmploymentContract` زي ما هما بالظبط. الأسباب:
1. `InsurableWage` مرتبط حسابيًا بـ `BasicSalary` تحديدًا (مش بمجموع كل البنود) — دمجه جوه سطر عام بيكسر المعنى ده.
2. الحقلين مستخدَمين في أماكن تانية بالفعل كـ Scalar صريح (`EmploymentFollowersBatch6Tests.cs:129`, `EmployeeBatch5Tests.cs`) — أي Backfill هيحتاج Migration بيانات فعلية بدون فايدة معمارية حقيقية.
3. تعريف الحقول المقترح في `Remarks8-HR-Enhancements.md` نفسه (`NameAr/NameEn/Amount/Type/IsTaxable/IsInsurable/Order`) ما بيذكرش `BasicSalary` خالص — البنود إضافية فوق الراتب الأساسي، مش بديلة عنه.

### 3.3 Contract/Certification Attachments (بند 2 و3)

- `EmploymentContract.AttachmentId` (`long?`) + `EmployeeCertification.AttachmentId` (`long?`) — نفس نمط `EmployeeDocument` تمامًا (`HasOne(...).WithMany().OnDelete(DeleteBehavior.Restrict)`)، لكن **Nullable** (على عكس `EmployeeDocument.AttachmentId` غير الـ Nullable) لأن العقد/الشهادة أصلًا بيتحفظوا من غير مرفق دلوقتي، والمرفق بيتضاف/يتغيّر بعد كده.
- إضافة `EmploymentContract`/`EmployeeCertification` كـ Constants جديدة جوه `AttachmentEntityTypes.All` (`src/Habbak.ERP.Domain/Common/AttachmentEntityTypes.cs`) — نفس سبب وجود `EmployeeDocument` فيها: الرفع العام محتاج قيمة `EntityType` معروفة وقت إنشاء صف `Attachment`، حتى لو العلاقة الحقيقية بعد كده Scalar FK.
- **مسار الرفع**: زرار "رفع مرفق" بسيط في `ContractsTab`/`CertificationsTab` (نفس تصميم `DocumentsTab.tsx` — رفع لـ `/attachments` أولًا، ثم إرسال الـ `id` الراجع). **مش عن طريق `Update...Command` الحاليين** (خطر الكتابة فوق باقي الحقول من فورم قديم، §1.6) — أمر خفيف مخصص لكل كيان: `SetEmploymentContractAttachmentCommand(long Id, long AttachmentId)` و`SetEmployeeCertificationAttachmentCommand(long Id, long AttachmentId)`، كل واحد بيعدّل عمود واحد بس ويستدعي `SaveChangesAsync`.

### 3.4 UI — Hiring Wizard (بند 4)

نفس Line Editor بتاع `ContractsTab` (§3.1) يتحقن جوه `ContractStep.tsx` قبل زرار "التالي" — البنود بتتبعت جوه نفس نداء `useCreateContract` الحالي (مش نداء منفصل)، متسقًا مع فلسفة الـ Wizard "كل خطوة = نداء API حقيقي واحد" (تعليق `HiringWizardPage.tsx:17-20`).

---

## 4. Sub-Batches المقترحة (3C.2 → 3C.6)

| Sub-Batch | المحتوى |
|---|---|
| **3C.2** | Domain + EF Config + Migration واحدة: `EmploymentContractLine` (+ `ContractLineType` enum)، عمودي `AttachmentId?` على `EmploymentContract`/`EmployeeCertification`، إضافتين لـ `AttachmentEntityTypes.All` |
| **3C.3** | Application (Contracts): `Lines` جوه `Create`/`Update`/`RenewEmploymentContract` (Replace-All)، `SetEmploymentContractAttachmentCommand`، تحديث `EmploymentContractDto`/الاستعلامات لتشمل `Lines`+`AttachmentId` |
| **3C.4** | Application (Certifications): `SetEmployeeCertificationAttachmentCommand`، تحديث `EmployeeCertificationDto` ليشمل `AttachmentId` |
| **3C.5** | Frontend: Line Editor (Add/Remove Row، `useState` array) جوه `ContractsTab` + `ContractStep`، زرار رفع مرفق جوه `ContractsTab`/`CertificationsTab` (نمط `DocumentsTab`) |
| **3C.6** | Tests (Integration لـ Replace-All + Attachment set + Cascade Delete) + Docs (`Phase-3C-Final.md`) |

---

## 5. المخاطر / نقاط تحتاج تأكيد قبل التنفيذ

1. **تجديد العقد (`RenewEmploymentContract`) والبنود**: هل البنود بتتنسخ تلقائيًا من العقد القديم للجديد (نفس ما بيحصل مع `contractType`/`basicSalary` في الفورم حاليًا)، ولا العقد الجديد بيبدأ ببنود فاضية والمستخدم يضيفها يدويًا؟ **الموصى به: نسخ افتراضي قابل للتعديل قبل الحفظ** (نفس فلسفة `startCreate`/`useEffect` جوه `ContractsTab.tsx:47-55` اللي بيملى الفورم من العقد القديم فعلًا) — يحتاج "موافق" صريح لأنه سلوك بيانات، مش مجرد UI.
2. **مفيش Update UI لعقد/شهادة موجودين حاليًا** — زرار "رفع مرفق" هيبقى أول عملية تعديل جزئي على سجل موجود من الفرونت إند خالص لحد دلوقتي. الأمر المخصص (§3.3) بيتجنّب المخاطرة، لكن لو المستخدم عايز لاحقًا تعديل بنود عقد بعد إنشائه (مش وقت الإنشاء بس) هيحتاج مسار Update كامل يتفعّل من الفرونت إند — خارج نطاق البنود الأربعة الحالية، يتسجّل كـ Task منفصل لو ظهر الاحتياج.
3. **`AttachmentPanel.tsx` (Polymorphic) لازم ميتلمّسش/يتاخدش كمرجع مباشر** — استخدامه هنا هيدخل نمط N-مرفقات لسجل عنده FK Scalar واحد بس، تضارب مباشر مع بند 2/3 كما هو مكتوب. لو ظهرت رغبة لاحقًا في "أكتر من مرفق واحد" للعقد، ده قرار مختلف تمامًا (Polymorphic زي `AttachmentPanel`) محتاج نطاق منفصل.
4. **Migration واحدة أم أكتر**: زي Phase 3 Amendments، الموصى به Migration واحدة شاملة لكل الثلاثة تغييرات (الجدول الجديد + العمودين) — تُطبَّق بنفس Workflow "2B Cloud/Local" الإلزامي (Cloud: كود+Migration Generation+Push فقط، Local: Apply+Regression) المعتمد من Phase 3B.
5. **`Order` على `EmploymentContractLine`**: مفيش تأكيد صريح إذا كان Unique جوه نفس العقد ولا مجرد ترتيب عرض حر (تكرار مسموح، الفرز بيه بس) — الموصى به: **ترتيب عرض حر بدون Unique Constraint** (نفس `LineNumber` على `JournalEntryLine` اللي مفيهوش Unique Index كمان)، يتأكد وقت 3C.2.

---

## 6. الخلاصة

مفيش أي Migrations أو تنفيذ حصل في هذا الـ Research Pass. أهم اكتشاف: الاسم المطلوب فحصه (`AttachmentsPanel.tsx`) مش موجود، والمكوّن القريب منه (`AttachmentPanel.tsx`) بيمثّل نمط Polymorphic مختلف عن اللي بندين 2/3 محتاجينه فعليًا (نمط `EmployeeDocument.AttachmentId` الأقرب والأنسب). باقي التصميم (الكيان، الـ Migration، الـ Replace-All على التحديث) مبني مباشرة على أنماط موجودة وموثّقة فعليًا في الكود (`BranchRequestLine`, `EmployeeDocument`, `AttachmentEntityTypes`) — صفر حاجة لتخمين خارج المستودع، على عكس Phase 3B (بروتوكول أجهزة خارجية).
