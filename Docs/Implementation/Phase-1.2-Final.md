# تقرير نهائي — Phase 1.2 (Migration الحقول الحرة)

> يغطي Sub-Batches 1.2.1-1.2.6 (2026-09-27). المرجع التخطيطي: `Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.2` و`Docs/Implementation/HR-Core-Plan.md §1.2`. الـ Research Pass الكامل في `Docs/Implementation/Phase-1.2-Research.md`.

## ملخص تنفيذي

Phase 1.2 (تحويل الأربعة حقول الحرة — أرقام موظفين بدون FK حقيقي — لعمود مرتبط حقيقي) خلصت بالكامل، بنفس المنهجية المتبعة في Phase 1.1: Research Pass قبل أي كود (كشف إن البيانات الحقيقية فاضية تقريبًا — `Employees` صفر صف — وإن التوصيف الأصلي مطابق للكود بالحرف)، موافقة صريحة على 3 قرارات مفتوحة، تنفيذ الـ Sub-Batches بالترتيب، Regression كامل قبل التسليم. **انحراف واحد مقصود عن الخطة الأصلية**: شاشة `HR_CUSTODY_MIGRATION` التفاعلية الكاملة (1.2.4) اتأجلت لصالح Component Frontend معزول (`CustodyMigrationBanner.tsx`) — الـ Backend كامل (تقرير تشخيصي + Auto-match) اتبنى زي المخطط بالظبط، بس الشاشة التفاعلية نفسها كانت هتُبنى فوق شاشة `CustodyRegister` غير موجودة أصلًا في الـ Frontend (فجوة سابقة في موديول الحسابات، اتسجّلت في `Known-Issues.md`). **الـ Migration نفسها آمنة جدًا تقنيًا** ونُفذت بنجاح على `HabbakErp` الحقيقية — الفائدة العملية للربط هتفضل صفر لحد ما أول موظف حقيقي يتسجل (Phase 1.5).

اتكشفت مشكلة Regression واحدة أثناء التنفيذ (تفصيل تحت) — Test Data قديم كان معتمد على غياب FK لم يعد موجودًا، اتصلحت بتعديل بيانات الاختبار فقط (صفر تعديل على منطق الإنتاج).

## الـ 6 Sub-Batches — كل واحد عمل إيه

| Sub-Batch | العنوان | المحتوى |
|---|---|---|
| **1.2.1** | Research Pass | فحص مباشر للكود الفعلي (الأربعة حقول) + استعلام مباشر لبيانات `HabbakErp` الحقيقية (`sqlcmd`) — `Employees` صفر صف، `Users` صفين بـ`EmployeeId = NULL`، `CustodyRegisters` أربعة صفوف بـ`EmployeeId = 1` (placeholder بلا معنى)، `CustodyOfficers` صفين (أحدهما `E2E-CO-MAADI` بيانات اختبار متسربة)، `MaintenanceRequests`/`MaintenanceSchedules` فاضيين. أكّد صحة قرار Option A وكشف انحراف واحد (فجوة شاشة `CustodyRegister`). |
| **1.2.2** | `CustodyOfficer` + `Maintenance` | `CustodyOfficer.EmployeeId?` جديد + FK لـ`Employees` · `MaintenanceRequest.TechnicianId`/`MaintenanceSchedule.TechnicianId` (موجودين من قبل) اتاخدوا FK حقيقي لأول مرة · `FreeFieldDiagnosticReport.cs` (Query + Command جديدين): تقرير تشخيصي بيصنّف كل صف (`AlreadyLinked`/`ExactMatch`/`Ambiguous`/`NoMatch`) بمطابقة اسم بيقين (Case/Whitespace-insensitive على `NameAr` أو `NameEn`، نفس الشركة)، و`ApplyFreeFieldAutoMatchCommand` بيربط الـ`ExactMatch` بس (Idempotent) · مكشوفين عن طريق `HrFreeFieldMigrationController` (يعيد استخدام صلاحية `HR_EMPLOYEES` الموجودة، بدل شاشة جديدة بلا Frontend يستخدمها). |
| **1.2.3** | `CustodyRegister` (Option A) | عمود جديد `EmployeeIdLinked` (`long?`, بدون FK) جنب `EmployeeId` القديم — القديم **اتأكد إنه فضل بقيمته الأصلية بالظبط** (Test صريح). |
| **1.2.4** | Frontend — Banner | `CustodyMigrationBanner.tsx` (`frontend/src/features/hr/components/`) — Component معزول (`count`/`onMigrate` كـ Props)، مبني على `ui-kit/Alert` الموجود، بدون أي Integration فعلية دلوقتي (TODO صريح جوه الملف). **أول Test Frontend في المشروع** — احتاج إضافة `vitest`/`@testing-library/react` كأدوات تطوير (مفيش أي بنية اختبار Frontend قبل كده). |
| **1.2.5** | `User.EmployeeId` | توثيق فقط — XML Comment صريح `[DEPRECATED — READ ONLY]` فوق الخاصية، بدون أي تعديل كود فعلي (الفحص أكّد صفر كتابة/قراءة فعلية عليه في الكود والبيانات). |
| **1.2.6** | Migration + Tests + Docs | Migration واحدة (`20260927040544_HrFreeFieldMigration`) بنفس بروتوكول Backup → Trial DB → Real DB · 6 اختبارات Integration جديدة (`HrFreeFieldMigrationTests`) تغطي: التطابق البيّن + الربط التلقائي، الغموض (اسمين متطابقين) بيوقف الربط، عدم التطابق، الربط لا يتكرر (Idempotency)، وتأكيد صريح إن `CustodyRegister.EmployeeId` القديم فضل زي ما هو و`EmployeeIdLinked` كله `null` · تحديث `Known-Issues.md`/`HR-Core-Plan.md`/`HR-MASTER-PLAN.md`. |

## الـ Migration — التفاصيل

| البند | القيمة |
|---|---|
| **الاسم** | `20260927040544_HrFreeFieldMigration` |
| **البروتوكول** | Backup كامل (`HabbakErp_Backup_Phase1_2_20260927.bak`) → Restore على `HabbakErp_Trial_HrFreeFieldMigration` → تطبيق + تحقق → تطبيق على `HabbakErp` بـ`--connection` صريح (تفاديًا لمشكلة `DesignTimeDbContextFactory` المسجّلة في `Known-Issues.md`) → حذف قاعدة الـ Trial بعد التأكد |
| **التغييرات** | عمود `CustodyRegisters.EmployeeIdLinked` (`bigint`, Nullable, **بدون FK**) · عمود `CustodyOfficers.EmployeeId` (`bigint`, Nullable) + FK لـ`Employees` · FK جديد على `MaintenanceRequests.TechnicianId` و`MaintenanceSchedules.TechnicianId` لـ`Employees` (الأعمدة كانت موجودة من قبل، الـ FK فقط جديد) |
| **التحقق بعد التطبيق (Trial + Real، النتيجة مطابقة في الاتنين)** | `CustodyRegisters.EmployeeId` = `1,1,1,1` (زي قبل الـ Migration بالظبط) · `EmployeeIdLinked` = `NULL` في الأربعة صفوف · `CustodyOfficers.EmployeeId` = `NULL` في الصفين · الثلاث FKs الجديدة موجودة في `sys.foreign_keys` |
| **الرجوع (Down)** | مكتوبة ومتماثلة مع الـ Up (Drop للـ 3 FKs + الـ 2 Index + الـ 2 عمود) |

## المشكلة المكتشفة والمصلَّحة أثناء الـ Regression

**Test Data قديم معتمد على غياب FK لم يعد موجودًا** — `FixedAssetsMaintenanceTests.CreateMaintenanceRequestAsync` (Helper مشترك لـ 5 اختبارات) كان بيبعت `technicianId = 77L` وهمي بدون موظف حقيقي مقابل — كان مقبول قبل 1.2.2 لأن العمود كان بلا FK. بعد إضافة `FK_MaintenanceRequests_Employees_TechnicianId`، الـ Insert بدأ يفشل بـ `500 Internal Server Error` (FK Violation) في 5 اختبارات (`The_board_shows_open_reports_and_jobs_in_their_columns`, `A_fault_report_follows_its_job_from_inspection_to_repaired`, `A_stocked_spare_part_is_costed_at_the_warehouse_average_when_it_is_issued`, `A_job_above_the_approval_threshold_cannot_start_before_it_is_approved`, `Completing_a_job_with_both_an_external_cost_and_stocked_parts_posts_two_separate_entries`).

**الحل** (بعد إبلاغ المستخدم والتوقف طبقًا للقاعدة، ثم موافقة صريحة): `technicianId = 77L` → `technicianId = (long?)null` في الـ Helper، مع إبقاء `technicianName = "فني خارجي"` كما هو — الاختبارات الخمسة بتفحص سلوك الأصول/قطع الغيار/الترحيل، مش ربط الفني بموظف، وفني خارجي بالاسم بدون سجل موظف هو بالظبط الحالة اللي الحقل النصي مصمم لها أصلًا. **صفر تعديل على منطق الإنتاج.**

## الإحصائيات النهائية

| البند | العدد |
|---|---|
| **أعمدة جديدة** | 2 (`CustodyRegisters.EmployeeIdLinked`, `CustodyOfficers.EmployeeId`) |
| **FKs جديدة** | 3 (`CustodyOfficers`→`Employees`, `MaintenanceRequests`→`Employees`, `MaintenanceSchedules`→`Employees`) |
| **ملفات Application جديدة** | 1 (`FreeFieldDiagnosticReport.cs` — Query + Handler + Command + Handler + DTOs) |
| **Controllers جديدة** | 1 (`HrFreeFieldMigrationController`، Endpoint-ين) |
| **Components Frontend جديدة** | 1 (`CustodyMigrationBanner.tsx`) |
| **اختبارات Backend جديدة** | 6 (`HrFreeFieldMigrationTests`، كلها Integration) |
| **اختبارات Frontend جديدة** | 2 (أول اختبارات Frontend في المشروع، `CustodyMigrationBanner.test.tsx`) |
| **إجمالي Suite الكامل — IntegrationTests** | **178/178 ✅** |
| **إجمالي Suite الكامل — ApiTests** | **260/260 ✅** (بعد إصلاح الـ Test Data) |
| **Build Backend** | 0 Errors (تحذيرين سابقين غير متعلقين بهذه المرحلة، لم يُمسّا) |
| **Build Frontend (`tsc -b && vite build`)** | 0 Errors |

## الانحراف المسجّل (تفصيل)

راجع `Docs/Reports-UPDATED/Known-Issues.md` (بندين جديدين) و`Docs/Implementation/HR-MASTER-PLAN.md §7 (Amendments Log)`:
1. **بيانات اختبار E2E متسربة** (`CustodyOfficers.Code = 'E2E-CO-MAADI'`) — توثيق فقط، بدون حذف.
2. **فجوة Frontend**: لا توجد شاشة `CustodyRegister` في الموديول المحاسبي — الـ Banner جاهز ومستني الشاشة دي تُبنى (خارج نطاق HR).

## المرحلة التالية (1.3)

`Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.3` — تفعيل الموظف كطرف في السندات المحاسبية (فك القفل الحالي في `CounterpartyAccountResolver.cs` لـ `CounterpartyType.Employee`). لا Migration مطلوبة — الحساب بيتسجّل عن طريق شاشة `CompanyAccountMapping` الموجودة.

**خلصت Phase 1.2.**
