# Phase 1.5 — Research Pass (شاشات HR Core — Frontend)

**التاريخ**: 2026-09-27
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md § Phase 1.5` · `Docs/Implementation/HR-Core-Plan.md §1.5` · `Docs/Modules/10-Module-HR-Payroll.md §5.1`

> **خلاصة مبكرة**: الخطة افترضت إن Phase 1.5 "Frontend بس" لأن كل الـ Backend خلص في 1.1. الفحص الفعلي بيّن إن ده **صحيح لـ 9 من 11 شاشة فقط**. فيه **فجوتين Backend حقيقيتين** (`HR_SETTINGS` بالكامل، وخطوة "تأمينات" في Wizard التعيين) + **فجوة تصميم Frontend إضافية** (مفيش أي مكوّن Tabs داخل الصفحة في المشروع كله، مش بس مفيش Wizard). التفاصيل والأسئلة المطلوب حسمها قبل "موافق" في قسم 6.

---

## 1. الشاشات الـ 8 Lookup + الموظف — حالة الـ Backend الفعلية

| الشاشة | `Screen.Code` | الكيان | الـ Controller/Route | مسجّلة في `MenuItemSeedData.cs`؟ |
|---|---|---|---|---|
| الوظائف/الدرجات | `HR_JOB_POSITIONS`/`HR_JOB_GRADES` | `JobPosition.cs`/`JobGrade.cs` | `HrLookupControllers.cs:216,177` → `api/v1/hr/job-positions`/`job-grades` | ✅ (سطر 210-211) |
| الهيكل التنظيمي | `HR_ORG_UNITS` | `OrgUnit.cs` + `OrgUnitHierarchy.cs` | `HrLookupControllers.cs:255` → `api/v1/hr/org-units` | ✅ (212) |
| أنواع المستندات | `HR_DOCUMENT_TYPES` | `EmployeeDocumentType.cs` (**الاسم الفعلي، مش `DocumentType`**) | `HrLookupControllers.cs:295` → `api/v1/hr/employee-document-types` | ✅ (213) |
| مكاتب التأمينات | `HR_INSURANCE_OFFICES` | `InsuranceOffice.cs` | `HrLookupControllers.cs:334` → `api/v1/hr/insurance-offices` | ✅ (214) |
| الموظفين | `HR_EMPLOYEES` | `Employee.cs` + `EmployeePersonalData.cs` (1:1) | `EmployeesController.cs:34` → `api/v1/hr/employees` | ✅ (220) |
| Countries/Cities/Banks | `SETTINGS_COUNTRIES`/`_CITIES`/`_BANKS` | `Organization/Country.cs`/`City.cs`/`Bank.cs` (**مش تحت HR فعلًا — تحت `Organization`، مطابق للخطة**) | `HrLookupControllers.cs:60,99,138` → `api/v1/organization/{countries,cities,banks}` | ✅ (236-238) |
| **التعيين** | `HR_HIRING` | — | **مفيش Controller ولا `[Screen(...)]` خالص** | ❌ — تعليق صريح في `MenuItemSeedData.cs:205-208`: "بتتسجّل لما الكيانات اللي وراها تُبنى في 1.4/1.5" |
| **إعدادات HR** | `HR_SETTINGS` | **`HrSettings` — الكيان نفسه مش موجود** | **مفيش Controller ولا Migration ولا Application layer** | ❌ (نفس التعليق أعلاه) |

**تبويبات التوابع الثلاثة** (عقود/مستندات/شهادات): الكيانات موجودة (`EmploymentContract.cs`، `EmployeeDocument.cs`، `EmployeeCertification.cs` — **مش `EmployeeCertificate`**) والـ Controllers موجودة (`HrEmploymentControllers.cs`) لكنها **كلها محكومة بـ`[Screen("HR_EMPLOYEES")]`** — مفيش `Screen.Code` مستقل لكل تبويب، وده متوقع وصحيح (نفس شاشة الموظف).

**الاختلاف الوحيد عن نص الخطة هنا**: أسماء كيانين (`EmployeeDocumentType` بدل `DocumentType`، `EmployeeCertification` بدل `EmployeeCertificate`) — تسمية بس، صفر تأثير معماري.

---

## 2. فجوة 1 — `HrSettings` غير موجود خالص (تضارب مباشر مع `HR-Core-Plan.md`)

`Docs/Implementation/HR-Core-Plan.md:255,273` بينص حرفيًا إن الكيان `src/Habbak.ERP.Domain/HR/HrSettings.cs` **مبني بالفعل من Phase 1.1** بكل الـ 10 حقول (5 معروضة + 5 مؤجلة Nullable)، وإن "**Migration: لأ**" لـ Phase 1.5.

**الفحص الفعلي**: صفر نتائج لأي من الاتنين — لا الملف نفسه، ولا أي حقل من الـ 10 (`EmployeeCodePrefix`, `EmployeeCodeSequence`, `DefaultProbationDays`, `DefaultBranchId`, `RequireNationalIdForActivation`, `MonthBasis`, `KioskSessionSeconds`, `SelfServiceClockInRequiresLocation`, `CompanyDefaultApproverUserId`, `MaxAdvanceInstallmentPercent`) — في أي مكان في `src/`. مفيش Migration بينهم. `MenuItemSeedData.cs:206-208` نفسه بيأكّد الفجوة بتعليق صريح: الكيان لسه مبنيش.

**ملاحظة إضافية تقلّل من حجم الفجوة جزئيًا**: `CreateEmployeeCommand.cs:52` بيولّد كود الموظف عن طريق `ICodeGenerator.ResolveCodeAsync("HR_EMPLOYEES", ...)` — يعني آلية توليد الكود **الجاهزة والعامة أصلًا** (`CodingRule` لكل `Screen.Code`) بتغطي فعليًا وظيفة `EmployeeCodePrefix`/`EmployeeCodeSequence` من غير أي حاجة لحقلين مخصصين على `HrSettings`. ده يفتح سؤال: هل الحقلين دول لازم أصلًا؟ (قسم 6، سؤال 1).

**الأثر على النطاق**: شاشة `HR_SETTINGS` **مش ممكن تكون Frontend بس** كما مفروض في الخطة — لازم Batch Backend كامل الأول (Entity + Migration + Commands/Queries + Controller + تسجيل `Screen.Code`/Menu) قبل أي Frontend ليها.

---

## 3. فجوة 2 — خطوتين من الـ 6 في Hiring Wizard مالهمش Backend يدعمهم كما هما موصوفين

نص الخطة (`HR-MASTER-PLAN.md:160`): *"معالج التعيين: موظف ← عقد ← مستندات ← راتب ← تأمينات ← مستخدم."*

| الخطوة | الحالة الفعلية |
|---|---|
| موظف | ✅ `CreateEmployeeCommand` موجود |
| عقد | ✅ `CreateEmploymentContractCommand` موجود — **وبيحمل `BasicSalary` و`InsurableWage` كأعمدة عادية على العقد نفسه** (`EmploymentContract.cs:28-29`) |
| مستندات | ✅ Application layer موجود (`HR/EmployeeDocuments/`) |
| **راتب** | **مفيش خطوة مستقلة لازمة** — `BasicSalary` بالفعل جزء من خطوة "عقد" (السطر فوق). مفيش `SalaryComponent`/`SalaryStructure`/`EmployeeSalary` في الكود خالص (اتأكد بالـ Grep) — دول كيانات **Phase 4** (الرواتب)، لسه مبنيين. `JobPosition.DefaultSalaryStructureId` نفسه معلّق بتعليق صريح "belongs to the future Payroll module" (`JobPosition.cs:20`). |
| **تأمينات** | **فجوة حقيقية** — `InsuranceOffice` Lookup موجود لوحده بس **صفر ربط** — مفيش `InsuranceOfficeId` ولا رقم تسجيل تأميني على `Employee` ولا `EmployeePersonalData` ولا `EmploymentContract`. الكيان اللي المفروض يمثّل تسجيل الموظف الفعلي في التأمينات (`EmployeeSocialInsurance`) موصوف في تعليق الكود نفسه (`InsuranceOffice.cs:9`) كـ"Payroll module, later phase" — **يعني Phase 5**، مش حتى Phase 4. |
| مستخدم | ✅ `AssignUserToEmployeeCommand` موجود بالفعل (Phase 1.1) |

**الخلاصة**: خطوة "راتب" مش خطوة مستقلة أصلًا (تُدمج في خطوة العقد)، وخطوة "تأمينات" **مالهاش أي كيان تكتب فيه دلوقتي** — كتابتها الآن تحتاج تصميم مسبق لبيانات "تأمينات مرحلة 1" (Minimal) قبل Phase 4/5، وهو قرار معماري مش تفصيل تنفيذي (قسم 6، سؤال 2).

---

## 4. فجوة 3 — مفيش أي مكوّن Tabs داخل الصفحة في المشروع كله (مش بس الـ Wizard)

الخطة بتفترض إن نمط `frontend/src/features/purchasing/suppliers/` فيه "List/Edit + تبويبات" جاهز لإعادة الاستخدام لشاشة الموظف (`HR-Core-Plan.md:245`).

**الفحص الفعلي**: `SupplierEditPage.tsx` (262 سطر) **مفيهوش أي Tabs خالص** — فورم واحد فقط جوه `<Card>`. مفيش أي `Tab`/`tab` في الملف. البحث في المشروع كله عن أي مكوّن Tabs داخل صفحة (section tabs) رجّع نتيجة واحدة بس: `frontend/src/ui-kit/TabBar.tsx` — وده شريط تبويبات "مستندات العمل المفتوحة" في `AppLayout.tsx` (زي تابات المتصفح)، **مش** تبويبات داخل صفحة واحدة.

**الأثر**: شاشة `HR_EMPLOYEES` محتاجة تبويبات (بيانات/شخصية/عقود/مستندات/شهادات) لكن **مفيش قالب موجود فعليًا نعيد استخدامه** — ده Design Gap إضافي غير موصوف في الخطة الأصلية (اللي ذكرت الـ Design Spike للـ Wizard بس، مش لتبويبات الموظف). قرار مطلوب في قسم 6 (سؤال 3): نبني مكوّن Tabs بسيط عام (`ui-kit`) الأول كـ Batch صغير قبل 1.5.3، ولا نبنيه Local جوه شاشة الموظف بس مبدئيًا؟

---

## 5. باقي الأنماط المرجعية — تأكيد/دقة إضافية

| النمط | الحالة |
|---|---|
| `ChartOfAccountsPage.tsx` (شجرة `OrgUnit`) | ✅ موجود، لكن **`useAccountTree` (في `api.ts:48`) Hook جلب بيانات بس** — منطق بناء الشجرة الفعلي (تجميع بـ`parentId` في `Map`، توسيع/طي بـ`Set`، إنشاء ابن Inline) **Inline جوه الصفحة نفسها** مش جوه الـ Hook. لبناء `OrgUnitsPage` لازم نعيد استخدام منطق الصفحة، مش الـ Hook بس. |
| `PurchaseCycleSettingsPage.tsx` (+`api.ts`/`types.ts`) | ✅ مطابق تمامًا — `DEFAULTS`/`DOCUMENT_FIELDS`/`CHECKBOX_FIELDS` كمصفوفات Typed زي المتوقع بالحرف. هيتستخدم لـ`HR_SETTINGS` بمجرد ما الـ Backend بتاعها يخلص (فجوة 1). |
| `AssetCategoryPages.tsx` (Lookup بسيط) | ✅ موجود (`frontend/src/features/fixedAssets/AssetCategoryPages.tsx`) — نمط `DataGrid` + `usePermission` قياسي، صالح لل5 شاشات Lookup البسيطة. |
| `frontend/src/features/hr/` | موجود بس **فاضي فعليًا من ناحية Phase 1.5** — بيحتوي بس `components/CustodyMigrationBanner.tsx` (بقايا Phase 1.2، مالهاش علاقة بالموظف/الشاشات). حالة "لسه محدش بدأ فيها" في §6 من الخطة **صحيحة فعليًا**. |
| Wizard/Stepper Pattern | ✅ مؤكّد **صفر نتائج حقيقية** في المشروع كله — مطابق تمامًا لتوصيف الخطة، الـ Design Spike (1.5.1) لسه لازم. |
| Countries/Cities/Banks Frontend | ❌ مش موجودة خالص — `frontend/src/features/settings/` فيها `codingRules/`و`security/` بس. الـ Backend جاهز 100% (جدول 1)، الفرونت لسه مبنيش. |

---

## 6. أسئلة معمارية لازم تُحسم قبل "موافق" (الأسئلة كلها هنا، مش هتُسأل في نص التنفيذ)

1. **حقول `HrSettings.EmployeeCodePrefix`/`EmployeeCodeSequence`** — بما إن `ICodeGenerator`/`CodingRule` العام بالفعل بيغطي توليد كود الموظف (`CreateEmployeeCommand.cs:52`)، هل نلغي الحقلين دول من `HrSettings` (تقليل تكرار)، ولا نسيبهم كـ Override اختياري خاص بـ HR فوق النظام العام؟ **توصيتي**: نلغيهم — الـ`CodingRule` الموجود بالفعل تحت `Screen.Code = HR_EMPLOYEES` كفاية، وأي حقل إضافي هيكون تكرار مصدر حقيقة.
2. **خطوة "تأمينات" في Wizard التعيين** — 3 خيارات: (أ) نشيلها من الـ Wizard خالص دلوقتي ونأجلها لحد Phase 5 (`EmployeeSocialInsurance`)، (ب) نضيف حقلين بسيطين الآن (`InsuranceOfficeId?`, `InsuranceNumber?`) على `EmployeePersonalData` كبيانات تمهيدية بس (مفيهاش أي منطق تأمينات فعلي، تُقرأ لاحقًا من Phase 5)، (ج) نسيبها في الـ Wizard كخطوة اختيارية بحقول حرة بدون FK دلوقتي. **توصيتي**: (أ) — أبسط، وميضيفش بيانات هيحتاج Migration فعلي لاحق يهجّرها.
3. **مكوّن Tabs داخل الصفحة** — نبنيه Batch صغير عام (`frontend/src/ui-kit/Tabs.tsx` أو مشابه) قبل 1.5.3 عشان يُستخدم في شاشة الموظف والمستقبل، ولا Local جوه `EmployeeEditPage` بس دلوقتي؟ **توصيتي**: عام في `ui-kit` — التبويبات هتتكرر في شاشات تانية قريبة (`HR_HIRING` بعدين، شاشات تانية في موديولات لاحقة)، ومفيش سبب نبنيها Local هنا بس نعيد بناءها بعدين.
4. **`HR_SETTINGS` Backend** — بما إنها فجوة Backend حقيقية (قسم 2)، هل تُبنى كـ Sub-Batch جديد (`1.5.0` قبل 1.5.2) ضمن Phase 1.5 نفسها (الأقرب لروح الخطة — "شاشة إعدادات HR" واحدة متكاملة)، ولا Phase 1.5 تُقصر فعليًا على الشاشات اللي Backend بتاعها جاهز (9 من 11) وتُأجّل `HR_SETTINGS`+`HR_HIRING` كـ Phase صغيرة منفصلة بعدين؟ **توصيتي**: Sub-Batch `1.5.0` جوه نفس الـ Phase — الفجوة صغيرة (كيان واحد بـ10 حقول بسيطة، مفيهاش منطق معقّد) ومش عادل نأجّل Phase كاملة لأجلها.

---

## 7. الملفات المتأثرة (تقديرية، هتُدقّق أكتر وقت كل Sub-Batch)

| الغرض | المسار | نوع التغيير |
|---|---|---|
| `HrSettings` Backend (لو اعتمدنا سؤال 4 بـ"نعم") | `src/Habbak.ERP.Domain/HR/HrSettings.cs` (جديد) + `Application/HR/Settings/` (جديد) + Controller + Migration | جديد بالكامل |
| Lookup Screens ×5 | `frontend/src/features/hr/{jobPositions,jobGrades,documentTypes,insuranceOffices}/` | جديد |
| Countries/Cities/Banks | `frontend/src/features/settings/{countries,cities,banks}/` | جديد |
| OrgUnits (شجرة) | `frontend/src/features/hr/orgUnits/` | جديد — منطق مستنسخ من `ChartOfAccountsPage.tsx` |
| Tabs Component (لو اعتمدنا سؤال 3 بـ"عام") | `frontend/src/ui-kit/Tabs.tsx` (جديد) | جديد |
| شاشة الموظف | `frontend/src/features/hr/employees/` (List/Edit + التبويبات) | جديد |
| Hiring Wizard | `frontend/src/features/hr/hiring/` + `Application/HR/Hiring/` (Command تنسيقي إذا لزم) | جديد |
| `HR_SETTINGS` Frontend | `frontend/src/features/hr/settings/` | جديد |
| Menu/Screen Registry | `MenuItemSeedData.cs` (`HR_HIRING`/`HR_SETTINGS`) + `ScreenCodeCatalog.cs` | تعديل |

**Migration مطلوبة؟** نعم — واحدة بس، لـ`HrSettings` الجديد (لو اعتمدنا سؤال 4)، عكس ما كانت الخطة الأصلية بتفترضه ("لأ").

---

## الخلاصة

9 من 11 شاشة **جاهزة للبناء الفرونت مباشرة بدون أي عمل Backend** — Backend مكتمل ومطابق للخطة بدقة (فروقات تسمية بسيطة بس). فيه **فجوتين Backend حقيقيتين** (`HrSettings` بالكامل، وربط تأمينات في الـ Wizard) وفجوة تصميم Frontend واحدة (مفيش Tabs Component). الأسئلة الأربعة في قسم 6 لازم تُحسم الأول — بعد الحسم، التنفيذ يبدأ بـ **1.5.1 (Design Spike Wizard)** و**Sub-Batch جديد مقترح 1.5.0 (`HrSettings` Backend)** بالتوازي مع الشاشات البسيطة (1.5.2) اللي جاهزة فورًا.

**مش جاهز للتنفيذ الفوري بمجرد "موافق"** كما كان الحال في 1.2/1.3/1.4 — محتاج ردود على الأسئلة الأربعة الأول (تقدر تجاوب عليهم كلهم أو تقول "وافق على كل التوصيات" لو التوصيات المذكورة مناسبة).
