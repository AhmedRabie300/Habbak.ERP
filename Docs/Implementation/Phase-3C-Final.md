# Phase 3C — تقرير نهائي (Cloud)

**التاريخ**: 2026-09-28
**المرجع**: `Docs/Implementation/Phase-3C-Research.md` (Research Pass المعتمد) + `Docs/My Remarks/Remarks8-HR-Enhancements.md` (بنود 1-4)

---

## 1. النطاق

بنود Remarks8 رقم 1-4:

| # | البند | الحالة |
|---|---|---|
| 1 | بنود العقد (بدل سكن، انتقالات، إلخ) بدل الراتب الأساسي بس | ✅ منجز |
| 2 | مرفقات تاب العقود | ✅ منجز |
| 3 | مرفقات تاب الشهادات | ✅ منجز |
| 4 | بنود العقد في Hiring Wizard | ✅ منجز |

---

## 2. Sub-Batches المنفذة

### 3C.2 — Domain + EF Config + Migration (Commit `7c8ba96`)

- `EmploymentContractLine` (كيان جديد، `AuditableEntity`) — `NameAr/NameEn/Amount/Type/IsTaxable/IsInsurable/Order`، علاقة `EmploymentContract.Lines` (1:N، `DeleteBehavior.Cascade`، نفس نمط `BranchRequestLine`).
- `ContractLineType` enum (`Earning = 1, Deduction = 2`) — `Domain/HR/Enums.cs`.
- `EmploymentContract.AttachmentId` + `EmployeeCertification.AttachmentId` (كلاهما `long?`، `DeleteBehavior.Restrict`) — نفس نمط `EmployeeDocument.AttachmentId` لكن Nullable لأن العقد/الشهادة بيتحفظوا من غير مرفق وقت الإنشاء.
- إضافتين جوه `AttachmentEntityTypes.All`: `EmploymentContract`, `EmployeeCertification`.
- Migration واحدة شاملة: `20260928102253_EmploymentContractLinesAndAttachments` — جدول `EmploymentContractLines` + عمودين `AttachmentId` + الفهارس + الـ FKs الثلاثة.

### 3C.3-3C.4 — Application Layer (Commit `8b7a458`)

- `CreateEmploymentContractCommand`/`UpdateEmploymentContractCommand`/`RenewEmploymentContractCommand` الثلاثة ياخدوا `Lines` اختياري (`IReadOnlyList<ContractLineInput>?`):
  - **Create/Renew**: بيضيفوا البنود المعطاة مباشرة (عقد جديد، مفيش بنود قديمة تتشال).
  - **Update**: Replace-All حقيقي (`RemoveRange` + إعادة إضافة) **فقط لو `Lines` مش null** — تمرير `null` بيسيب البنود الحالية زي ما هي (تحديث بيانات العقد الأساسية من غير المساس بالبنود).
- `SetEmploymentContractAttachmentCommand(long Id, long? AttachmentId)` و`SetEmployeeCertificationAttachmentCommand(long Id, long? AttachmentId)` — أوامر ضيقة مخصصة (عمود واحد بس)، بدل استخدام أوامر الـUpdate الحالية، لأن مفيش شاشة Update كاملة لأي الكيانين لحد دلوقتي (خطر الكتابة فوق باقي الحقول من فورم قديم).
- `EmploymentContractDto`/`EmployeeCertificationDto` اتحدّثوا (`AttachmentId`, `Lines`) — قائمة العقود المقسّمة صفحات (`GetEmploymentContractsListQuery`) بتسيب `Lines` فاضية عمدًا (نفس قرار `BranchRequestListItemDto.LineCount`)، بينما `GetEmploymentContractByIdQuery` بيرجّع البنود كاملة مرتّبة بـ`Order` ثم `Id`.
- `IApplicationDbContext`/`AppDbContext`: `DbSet<EmploymentContractLine> EmploymentContractLines` جديد.
- `HrEmploymentControllers.cs`: الحقل `Lines` اتضاف لثلاث Request records (`Create`/`Update`/`RenewEmploymentContractRequest`)، وEndpoint جديد `PUT .../{id}/attachment` على كل من `EmploymentContractsController` و`EmployeeCertificationsController`.

### 3C.5 — Frontend (Commit `e1d6967`)

- `ContractLinesEditor.tsx` (مشترك) — `useState` Array بسيط (Add/Remove Row)، مش `react-hook-form`/`useFieldArray`، اتساقًا مع فورم `ContractsTab`/`ContractStep` الحاليين (الاتنين مبنيين بـ`useState` من الأول). مُستخدَم جوه الاتنين.
- عند التجديد (Renew): البنود بتتنسخ تلقائيًا من العقد القديم عن طريق `useContractById` جديد (لأن الـList المقسّم صفحات بيسيب `Lines` فاضية) — قابلة للتعديل قبل الحفظ (القرار المعتمد).
- `AttachmentCell.tsx` (مشترك) — خلية شبكة برفع/استبدال/تنزيل مرفق، مُستخدَمة كعمود جديد جوه `ContractsTab` و`CertificationsTab` (أول تعديل جزئي على سجل موجود من الفرونت إند لحد دلوقتي، عن طريق أوامر `SetAttachment` الضيقة، مش شاشة Update كاملة).
- i18n: `hr.employees.contracts.lines.*` و`hr.employees.attachment.*` في `ar.json`/`en.json`.
- `npm run build` + `tsc --noEmit` + `npm run test` (Vitest): كلهم نظاف/ناجحين (9/9، بدون تغيير عن Phase 3B).

### 3C.6 — Tests + Docs (هذا الملف)

- `Phase3CContractLinesAndAttachmentsTests.cs` (جديد، 7 اختبارات، نفس نمط استدعاء الـHandlers المباشر مثل `EmploymentFollowersBatch6Tests`):
  1. `CreateEmploymentContract_persists_lines_ordered_by_Order`
  2. `UpdateEmploymentContract_replaces_all_lines_when_Lines_is_provided`
  3. `UpdateEmploymentContract_leaves_lines_untouched_when_Lines_is_null`
  4. `RenewEmploymentContract_copies_the_Lines_it_is_given_into_the_new_contract`
  5. `Deleting_a_contract_cascades_to_its_lines`
  6. `SetEmploymentContractAttachment_sets_clears_and_validates_the_attachment_exists`
  7. `SetEmployeeCertificationAttachment_sets_and_validates_the_attachment_exists`

  **نتيجة التشغيل في الـCloud (Docker/Testcontainers، SQL Server 2022 حقيقي)**: **7/7 ناجحة** ✅.

---

## 3. القرارات المؤكَّدة من المستخدم (مرجع سريع)

| # | القرار |
|---|---|
| 1 | Renewal ينسخ البنود تلقائيًا من العقد القديم، قابلة للتعديل قبل الحفظ |
| 2 | مفيش Update UI كامل — `SetAttachment` Commands ضيقة بدلًا منها |
| 3 | `AttachmentPanel.tsx` (Polymorphic) متلمسش — نمط `DocumentsTab.tsx` (Scalar FK) هو المستخدَم |
| 4 | Migration واحدة شاملة للتغييرات الثلاثة |
| 5 | `Order` ترتيب عرض حر (بدون Unique) |
| 6 | `BasicSalary`/`InsurableWage` يفضلوا Scalar، ماينتقلوش لبند |

---

## 4. Build/Test النهائي (Cloud)

| الخطوة | النتيجة |
|---|---|
| `dotnet build` (كل مشاريع الـBackend) | ✅ نظيف، صفر Error |
| `dotnet test` — Phase3CContractLinesAndAttachmentsTests (Docker/Testcontainers) | ✅ 7/7 |
| `dotnet test --filter "FullyQualifiedName~HR"` (كل اختبارات HR + Phase 3C الجديدة، Docker/Testcontainers) | ✅ 96/96 — صفر Regression |
| `tsc --noEmit` (Frontend) | ✅ نظيف |
| `npm run build` (Frontend) | ✅ نظيف |
| `npm run test` (Vitest) | ✅ 9/9 |

---

## 5. Commits

```
e1d6967 Phase 3C.5: contract line editor + attachment upload UI
8b7a458 Phase 3C.3-3C.4: Application layer for contract lines + attachment commands
7c8ba96 Phase 3C.2: EmploymentContractLine entity + Contract/Certification AttachmentId
22b045d docs: Phase 3C Research Pass (Contract Lines + Attachments + Hiring Wizard)
```

---

## 6. المطلوب من الـLocal Session (2B Workflow)

نفس الـWorkflow المعتمد من Phase 3B:

1. `git pull` على فرع `main-wsqv76`.
2. **Backup** قبل أي Migration (`HabbakErp_PrePhase3C.bak`).
3. **Trial DB**: `dotnet ef database update` (Command Timeout=180) — تحقق من:
   - جدول `EmploymentContractLines` (الأعمدة + الفهرس + الـFKs الثلاثة — العقد Cascade، Users Restrict).
   - عمودي `AttachmentId` على `EmploymentContracts`/`EmployeeCertifications` + فهارسهم + FK على `Attachments` (Restrict).
   - اختبار `Down()` ثم إعادة `Up()` (Round-trip).
4. حذف Trial DB، تطبيق Migration على `HabbakErp` الحقيقية.
5. **Full Regression**: `dotnet test` (كل المشاريع) + `npm run build`/`npm run test` (Frontend).
6. تقرير `Phase-3C-Local-Verification.md` بنفس شكل تقرير Phase 3B.
7. **STOP** بعد كده — انتظار القرار التالي من المستخدم.
