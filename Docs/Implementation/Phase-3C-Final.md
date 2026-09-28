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

## 6. Local Verification Results ✅ (2026-09-28)

الـLocal Session كمّلت التحقق الكامل بعد `git pull` (Commit `7ee1249`) — **Approved رسميًا**.

### 6.1 Migration Apply

Backup (`HabbakErp_PrePhase3C.bak`) → Trial DB (`HabbakErp_Trial3C`، Migration `Up` + تحقق من الجدول/الأعمدة/الـFKs الثلاثة + `Down` كامل رجّع كل حاجة + `Up` تاني) → Real DB (`HabbakErp`). كل خطوة نجحت بدون مشاكل.

### 6.2 نتائج الاختبارات

| Suite | النتيجة |
|---|---|
| `EmploymentContract`/`EmployeeCertification`/`ContractLine` (فلتر مخصص، يشمل الـ7 الجديدة) | **22/22** ✅ |
| HR Tests (`--filter "FullyQualifiedName~HR"`) | **96/96** ✅ (مطابق تمامًا لنتيجة الـCloud) |
| IntegrationTests (كامل) | **245/245** ✅ (238 من Phase 3B + 7 اختبارات Phase 3C الجديدة) |
| ApiTests (كامل) | **277/277** ✅ (بدون تغيير عن Phase 3B — مفيش API Tests جديدة لـPhase 3C) |
| Frontend (`npm run build` + Vitest) | **9/9** ✅ |

**الإجمالي: 627 تشغيلة اختبار، صفر Failures.**

### 6.3 التحقق اليدوي من المتصفح (End-to-End)

- **بنود العقد عن طريق Renew** (الموظف E1 كان عنده عقد فعال بالفعل، فـ"عقد جديد" رجّع `HR-CONTRACT-ALREADY-ACTIVE` كما هو متوقع — سلوك صحيح، مش Bug): تجديد العقد مع بندين ("بدل سكن"، "انتقالات") نجح، العقد القديم بقى "منتهي الصلاحية" والجديد "فعال"، والبنود اتخزّنت صح 100% (تأكيد مباشر عن طريق الـAPI، شامل الترميز العربي).
- **مرفق على عقد موجود**: رفع (`POST /attachments`) → ربط (`PUT .../contracts/{id}/attachment`) → تنزيل (`GET /attachments/{id}/content`) — الثلاثة نجحوا End-to-End، والواجهة عكست الزرار الصحيح ("تصدير" بدل "رفع مرفق") بعد الربط.
- **مرفق على شهادة موجودة**: نفس المسار، نجح End-to-End.
- **Hiring Wizard مع بند عقد**: الويزارد الكامل (بيانات الموظف → شخصية → عقد ببند "بدل سكن" → مستندات → مستخدم) نجح من الأول للآخر، والبند اتخزّن صح.
- **رسائل الخطأ اللي ظهرت أثناء الاختبار** (`HR-CONTRACT-ALREADY-ACTIVE`, `CODE-REQUIRED`) كانت Validation متعمَّدة وصحيحة، مش أعطال.

### 6.4 تنظيف بعد الاختبار

الموظف التجريبي (Id 4) اتمسح بالكامل. مرفقات الاختبار على الموظف الحقيقي E1 اتفكّت (`SetAttachment` بـ`null`) عن طريق الـEndpoint الرسمي. تجديد عقد E1 **اتسابت عمدًا** — نتيجة عملية Renewal حقيقية وصحيحة عن طريق الـUI الرسمي، مش بيانات اختبار وهمية (عكسها كان هيحتاج تلاعب مباشر بالـDB يتجاوز الـBusiness Rules، أخطر من إبقائها).

### 6.5 Bugs مكتشفة

**لا يوجد.** كل رسائل الخطأ كانت قواعد عمل صحيحة ومتعمَّدة.

---

## 7. القرار — Approved، الانتقال لـPhase 4

**Phase 3C معتمدة رسميًا (Approved) — منجزة بالكامل (Cloud + Local).** لا يوجد أي Blocker لبدء Phase 4. الـCloud هيقف هنا وينتظر توجيه صريح من المستخدم قبل بدء أي كود جديد لـPhase 4.
