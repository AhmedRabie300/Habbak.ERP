# Phase 1.5 — شاشات HR Core (Frontend) — تقرير نهائي

**التاريخ**: 2026-09-27
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md § Phase 1.5` · `Docs/Implementation/Phase-1.5-Research.md` (Research Pass)

---

## 1. ملخص تنفيذي

Phase 1.5 حوّلت الـ Backend الجاهز من Phase 1.1 (Employee + التوابع الثلاثة + 8 Lookups) لشاشات Frontend فعلية، بالإضافة لسد فجوتين Backend صغيرتين اتكشفوا بس أثناء التنفيذ ومكانوش في الخطة الأصلية.

**Sub-Batches المنفّذة**: 1.5.0 → 1.5.3 → 1.5.5 → 1.5.6 (**1.5.4 اتلغت** — التبويبات الثلاثة اتبنت كاملة الوظائف من 1.5.3 نفسها، فتنفيذها تاني في 1.5.4 كان تكرار صرف، بقرار صريح من المستخدم وقت التخطيط).

**الحالة**: ✅ **خلصت بالكامل** — صفر Regression Failures طول الطريق (188 IntegrationTests + 262 ApiTests ناجحين في كل نقطة تفتيش)، `tsc -b` نظيف في كل Batch، تحقق يدوي شامل في المتصفح ضد الـ API/DB الحقيقيين لكل شاشة.

---

## 2. تفاصيل كل Sub-Batch

### 1.5.0 — `HrSettings` Backend (جديد، مش في الخطة الأصلية بالشكل ده)

الـ Research Pass (`Phase-1.5-Research.md`) اكتشف إن `HrSettings` — رغم إن `HR-Core-Plan.md` كانت بتفترضه "مبني من Phase 1.1" — **مش موجود خالص** في الكود (صفر Entity، صفر Migration، صفر Controller). اتبنى من الصفر:

- **الكيان**: `src/Habbak.ERP.Domain/HR/HrSettings.cs` — صف واحد لكل شركة (نفس نمط `PurchaseCycleSettings`/`BranchPOSSettings`)، بكل الحقول من الأول لكن الشاشة بتعرض 3 حقول بس مرحلة 1 (`DefaultProbationDays`, `DefaultBranchId`, `RequireNationalIdForActivation`).
- **قرار محسوم أثناء الـ Research**: حقلين `EmployeeCodePrefix`/`EmployeeCodeSequence` اتشالوا نهائيًا من التصميم — آلية `ICodeGenerator`/`CodingRule` العامة الموجودة بالفعل بتغطي نفس الوظيفة، فحقلين مخصصين تكرار مصدر حقيقية.
- **Migration**: `20260927072409_AddHrSettings` — بروتوكول Backup → Trial DB → Real DB كامل، Down مكتوبة ومجرّبة.
- **Application + Controller**: `GetHrSettingsQuery`/`UpdateHrSettingsCommand` + `HrSettingsController` (`/api/v1/hr/settings`)، تسجيل `HR_SETTINGS` في `MenuItemSeedData.cs`.
- **Tests**: 5 Integration Tests (`HrSettingsTests.cs` — Get defaults، Update Insert/Update، عزل الشركات، FK Validation لـ`DefaultBranchId`) + 2 API Tests (`HrSettingsApiTests.cs` — Round Trip كامل + صلاحية الشاشة).

### 1.5.1 — Design Spike

قرارين معماريين قبل أي كود تنفيذي فعلي لباقي المرحلة:

1. **`SectionTabs`** — Component عام جديد في `frontend/src/ui-kit/SectionTabs.tsx` (Presentational، Controlled، `role="tablist"/"tab"`، Badge اختياري) — الـ Research Pass أكّد إن مفيش أي مكوّن Tabs داخل صفحة في المشروع كله (شاشة الموردين المرجعية مالهاش تبويبات فعليًا رغم إن التوثيق القديم افترض عكس كده). استُخدم لاحقًا في 1.5.3.
2. **قرار عدم بناء Component عام لـ Wizard** — Consumer واحد بس (`HR_HIRING`) في المشروع كله، فبناء تجريد عام سابق لأوانه. منطق التنقل اتكتب مباشرة جوه صفحة الـ Wizard نفسها وقت 1.5.5.

### 1.5.2 — الشاشات البسيطة الثمانية

8 شاشات List/Edit كاملة (نمط `AssetCategoryPages.tsx`) + شاشة شجرة واحدة:

| المجموعة | الشاشات |
|---|---|
| HR | الدرجات الوظيفية، الوظائف، أنواع المستندات، مكاتب التأمينات (List/Edit بسيط) + الهيكل التنظيمي (شجرة، نمط `ChartOfAccountsPage.tsx`) |
| Settings | الدول، المدن، البنوك |

**صفر Backend جديد** — كل الـ 9 Controllers (شامل الشجرة) كانت جاهزة بالكامل من Phase 1.1 Batch B4، اتأكد بالفحص الفعلي. الملفات: `api.ts` (React Query hooks) + صفحة/صفحتين لكل شاشة، Routes في `routeTable.tsx`، مفاتيح ترجمة `hr.*`/`settings.*`.

### 1.5.3 — شاشة الموظف الكاملة (`HR_EMPLOYEES`)

الشاشة الأكبر في المرحلة — List مُرقّم فعليًا من السيرفر (أول شاشة HR بترقيم سيرفر حقيقي، مختلف عن نمط الـ 8 البسيطة) + Edit بـ5 تبويبات عن طريق `SectionTabs`، **كل تبويب كامل الوظائف من الأول** (مش Placeholder):

| التبويب | الوظيفة |
|---|---|
| بيانات | CRUD الموظف + Activate/Terminate + ربط مستخدم |
| شخصية | `EmployeePersonalData` Create/Update + PII Reveal (صلاحية زرار مستقلة `RevealPii`) |
| عقود | List مُرقّم من السيرفر + إنشاء/تجديد/إنهاء عقد |
| مستندات | رفع ملف (عن طريق Endpoint المرفقات العام) + ربطه بسطر `EmployeeDocument` |
| شهادات | List + إضافة/حذف |

**1.5.4 اتلغت هنا** — التبويبات الثلاثة (عقود/مستندات/شهادات) خلصت كاملة الوظائف من الأول، فمفيش حاجة تتعمل تاني.

### 1.5.5 — معالج التعيين (`HR_HIRING`)

5 خطوات (`HiringWizardPage.tsx` + `steps/`)، **صفر Command/Controller Backend جديد** — كل خطوة بتنادي Endpoint موجود بالفعل من 1.5.3: موظف → بيانات شخصية → عقد (شامل الراتب الأساسي، مفيش خطوة منفصلة له) → مستندات إلزامية بس → ربط مستخدم (اختياري). تصميم التنقل للأمام بس، بدون "رجوع" — كل خطوة إنشاء فعلي على الـ Backend، فالتصحيح بعدها من شاشة الموظف الكاملة (1.5.3) مش من الـ Wizard.

### 1.5.6 — Tests + Docs (هذا الـ Sub-Batch)

راجع قسم 4 (Tests) وقسم 6 (التوثيق المحدّث).

---

## 3. عدد الملفات

**Frontend — جديد بالكامل (35 ملف)**:
- `ui-kit/SectionTabs.tsx` + `.test.tsx` (2)
- `features/hr/listing.ts` + `features/settings/listing.ts` (2)
- `features/hr/{jobGrades,jobPositions,documentTypes,insuranceOffices}/{api.ts,*Pages.tsx}` (8)
- `features/hr/orgUnits/{api.ts,OrgUnitsPage.tsx}` (2)
- `features/settings/{countries,cities,banks}/{api.ts,*Pages.tsx}` (6)
- `features/hr/employees/{api.ts,types.ts,EmployeesListPage.tsx,EmployeeEditPage.tsx}` (4)
- `features/hr/employees/tabs/{BasicInfoTab,PersonalInfoTab,ContractsTab,DocumentsTab,CertificationsTab}.tsx` (5)
- `features/hr/hiring/{HiringWizardPage.tsx,steps/{EmployeeStep,PersonalDataStep,ContractStep,DocumentsStep,UserStep}.tsx}` (6)

**Frontend — تعديل**: `routeTable.tsx` (23 Route جديد)، `ar.json`/`en.json` (مفاتيح `hr.*`/`settings.*` جديدة بالكامل + `status.{OnLeave,Suspended,Terminated}`)، `ui-kit/Badge.tsx` (STATUS_TONE).

**Backend — جديد (10 ملفات + Migration)**:
- `Domain/HR/HrSettings.cs`
- `Infrastructure/Persistence/Configurations/HR/HrSettingsConfiguration.cs`
- `Application/HR/Settings/{Dtos/HrSettingsDto.cs,Queries/GetHrSettings/GetHrSettingsQuery.cs,Commands/UpdateHrSettings/UpdateHrSettingsCommand.cs}`
- `API/Controllers/HR/HrSettingsController.cs`
- Migration `20260927072409_AddHrSettings` (.cs + .Designer.cs)
- `IntegrationTests/HR/HrSettingsTests.cs`
- `ApiTests/HrSettingsApiTests.cs`

**Backend — تعديل**: `IApplicationDbContext.cs`، `AppDbContext.cs`، `MenuItemSeedData.cs` (`HR_SETTINGS` + `HR_HIRING`)، `AttachmentEntityTypes.cs` (`EmployeeDocument`).

---

## 4. الاختبارات

**Backend (Automated)**: 5 Integration Tests جديدة + 2 API Tests جديدة (تفصيل في 1.5.0). Regression كامل اتشغّل **4 مرات** طول المرحلة (بعد 1.5.0، 1.5.3 مرتين لتعديلين منفصلين، 1.5.5) — **188 IntegrationTests + 262 ApiTests ناجحين في كل مرة، صفر فشل**.

**Frontend (Automated)**: 3 اختبارات Vitest جديدة لـ`SectionTabs` (الحالة النشطة، الـ Badge، الحدث `onChange`). **باقي شاشات المرحلة اتحقّقت يدويًا مش بـ Vitest** — قرار واعٍ: المشروع مالوش نمط تأسيسي لاختبار صفحات CRUD كاملة بـ API/React Query Mocked (أول ملفين Vitest في المشروع كله كانوا بس `CustodyMigrationBanner` و`SectionTabs`، Components بسيطة بدون Network)؛ بناء نمط جديد للاختبار (Mock Server، Test Query Client...) كان هيكون نطاق أكبر من مرحلة شاشات، ومقتضاش صراحة في تعليمات هذا الـ Sub-Batch ("Vitest لو موجود — وإلا Manual Verification").

**Manual Verification (في المتصفح، ضد الـ API/DB الحقيقيين — مش Mock)**:
- **1.5.2**: Job Grades CRUD كامل، شجرة Org Units (إنشاء Root + Child، توسيع/طي، حذف)، Countries.
- **1.5.3**: تعيين موظف كامل الحقول → تفعيل (بعد عقد فعّال) → بيانات شخصية + PII Reveal (اتأكد الـ AuditLog اتسجّل) → رفع مستند حقيقي (`File`/`DataTransfer` API) + تحميله تاني → إضافة شهادة → محاولة إنهاء خدمة اترفضت صح برسالة العهدة المفتوحة (تأكيد معالجة أخطاء 400 و409 الاتنين).
- **1.5.5**: تعيين موظف كامل من الصفر بالـ 5 خطوات، وصل لآخر خطوة، اتنقل تلقائيًا لشاشة الموظف الكاملة، واتأكد العقد والبيانات الشخصية فعلاً محفوظين صح من هناك.

---

## 5. Bugs مكتشفة أثناء التنفيذ (اتصلحوا كلهم)

1. **`toPaged` matcher ناقص `.includes()`** (1.5.2) — خطأ Type بسيط في 7 شاشات، اتصلح فورًا (`tsc` كشفه قبل حتى أي اختبار يدوي).
2. **`birthDate` فاضية بتكسر الـ Request بالكامل** (1.5.3) — `CreateEmployeePersonalDataCommand` بياخد `DateOnly` إلزامي، وقيمة فاضية بتفشل في الـ JSON Binding قبل حتى FluentValidation (رسالة ASP.NET خام). اتصلح بإضافة تحقق Client-side صريح.
3. **خطر فقدان بيانات صامت في تبويب "شخصية"** (1.5.3، اتكشف بالتصميم مش بالتجربة) — الـ Update Endpoint بيستبدل كل الحقول بالكامل (مفيش Partial Update)، فلو حقل PII معروض Last4 بس واترسل فاضي، هيمسح القيمة الأصلية بصمت. اتحل بمنع الحفظ (رسالة واضحة) لحد ما المستخدم يعرض القيمة بـ"إظهار" أو يكتب قيمة جديدة، لكل من الرقم القومي وIBAN.
4. **`AttachmentEntityTypes` قائمة مغلقة ناقصة `EmployeeDocument`** (1.5.3) — راجع قسم 6، فجوة Backend رقم 2.

---

## 6. فجوتين Backend اتكشفوا واتصلحوا (مش Stop — امتدادات موثّقة صراحة)

1. **`HrSettings` غير موجود خالص** (1.5.0) — تفصيل كامل في قسم 2 فوق. اتبنى بالكامل كـ Sub-Batch مستقل قبل باقي المرحلة.
2. **`AttachmentEntityTypes.cs` (`src/Habbak.ERP.Domain/Common/`) ناقصة `EmployeeDocument`** (1.5.3) — قائمة مغلقة لأنواع الكيانات المسموح لها ترفع مرفقات عن طريق الـ Endpoint العام، وتعليق الكود نفسه بيقول صراحة "Extend this list... whenever another Edit/New screen adds its own Attachments action". `EmployeeDocument` كان أول Consumer من HR للـ Endpoint ده. الحل: سطر واحد + Array Entry، بدون Migration (const بس).

كلاهما اتصلحوا مباشرة أثناء التنفيذ (مش STOP كامل) لأنهم امتدادات لنقاط توسّع موثّقة صراحة في الكود نفسه، مش قرارات معمارية متضاربة مع الخطة.

---

## 7. ملاحظات إضافية

- **حقول `EmployeePersonalData` بدون Picker حقيقي**: `NationalityId`/`MilitaryStatusId`/`QualificationTypeId`/`EmergencyContactRelationshipTypeId` — الكيانات دي مسجّلة "Seed Only بدون شاشة في هذه المرحلة" أصلًا في `HR-Core-Plan.md §1.5` (قرار سابق قبل هذا الـ Sub-Batch)، ومفيش حتى Endpoint قراءة واحد لأي منهم. نفس منطق "بدون شاشة" اتمد لـ"بدون Picker" في تبويب "شخصية" — الحقول دي اتشالت من النموذج عمدًا لغاية ما توصل مرحلتها.
- **الرقم القومي/IBAN لازم يتكتبوا كاملين في كل Update** — نتيجة مباشرة لبند 3 في قسم 5، موثّق في الكود وفي الـ UI Hint نفسه (`nationalIdUpdateHint`/`bankIbanRequiredForUpdate`).
- **مفيش Redirect لتفعيل الموظف تلقائيًا في نهاية الـ Wizard** — قرار تصميم واعٍ (مش نسيان): "Activate" فعل منفصل بيحتاج عقد فعّال + مستندات إلزامية كاملة، الـ Wizard بيجهّز الاثنين لكن الضغط على "تفعيل" فعل يدوي من شاشة الموظف الكاملة بعد المراجعة.

---

## 8. المرحلة التالية

**Phase 2 — محرك الاعتمادات المركزي v1** (`Docs/Implementation/HR-MASTER-PLAN.md §Phase 2`) — يخدم النظام كله، مش HR بس. لازم قبل Phase 3 لأن `LeaveRequest`/`OvertimeRequest` محتاجين `ApprovalInstanceId`. Sub-Batches: 2.1 (Research + Design) → 2.7 (Tests + Docs).
