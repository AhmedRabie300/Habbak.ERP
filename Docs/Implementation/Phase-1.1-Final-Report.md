# تقرير نهائي — Phase 1.1 (HR Core Entities + PII Reveal)

> يغطي Batches B1-B7 (2026-09-26 لحد 2026-09-27). المرجع التخطيطي: `Docs/Implementation/HR-Core-Plan.md` (قسم "Phase 1.1 — Final Status"). التفاصيل الكاملة لكل Bug اتكشف وانصلح موثّقة في `Docs/Reports-UPDATED/Known-Issues.md`.

## ملخص تنفيذي

Phase 1.1 (الكيانات الأساسية لموديول HR) و1.1b (إظهار PII مدقَّق) خلصوا بالكامل — Domain وApplication وAPI للـ 9 كيانات HR الأساسية + 13 كيان مرجعي جديد، عبر 6 Batches تنفيذ (B1-B6) وBatch سابع للـ Regression والتوثيق (B7). كل Batch اتبنى بنفس المنهجية: بحث في الكود الفعلي أولًا (مش تخمين)، خطة، موافقة صريحة، تنفيذ، اختبار (Integration فور كل Batch)، Regression كامل (Integration + API) قبل التسليم. **مفيش Batch اتسلّم من غير Regression أخضر بالكامل.** اكتُشفت وانصلحت 5 مشاكل حقيقية أثناء التنفيذ (تفصيل تحت) — واحدة منها (EF Core Model Cache) كانت هتسيب تشفير الرقم القومي معطّل بصمت في أي بيئة اختبار، وواحدة تانية (SystemDataSeeder) كانت خلّت 9 شاشات عبر 5 Batches ما توصلش خالص للقاعدة الحقيقية. الـ Backend جاهز بالكامل ومُختبر — الباقي (Frontend، 1.5) برّه نطاق هذه المرحلة عمدًا.

## الـ 6 Batches — كل واحد عمل إيه

| Batch | العنوان | المحتوى |
|---|---|---|
| **B1** | 13 Lookup + Migration شاملة | 4 كيانات Organization (`Country`/`City`/`Nationality`/`Bank`، نظام-عام) + 9 كيانات HR (`JobGrade`/`JobPosition`/`OrgUnit`/`EmployeeDocumentType` Company-Scoped + `RelationshipType`/`MilitaryStatus`/`QualificationType`/`InsuranceOffice`/`TerminationReason` نظام-عام) + Migration واحدة (22 جدول مع كيانات B1 نفسها) + `MenuItemSeedData`/`ScreenCodeCatalog` + Seed للـ 5 Seed-Only. Domain فقط — مفيش Commands/Controllers. |
| **B2** | `Employee` + `EmployeePersonalData` | أول استخدام فعلي لـ `EncryptedStringConverter` (تشفير عمود، 0.3) و`IPiiHasher` (HMAC، 0.6) — `NationalIdEncrypted`/`BankIbanEncrypted` مشفّرين، `NationalIdHash` فريد لكل شركة. `Employee.UserId` فريد (قاعدة 1)، `Employee.ManagerId` بفحص حلقة (`OrgUnitHierarchy.WouldCreateCycle`). |
| **B3** | الـ 3 "Followers" | `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` — أول استخدام فعلي لـ `IEmployeeScopedEntity` (بُنيت في Phase 0.1، فضلت من غير استهلاك لحد B3). كل الـ 3 بيمنعوا حذف الـ `Employee` بتاعهم (FK Restrict). |
| **B4** | Full Vertical للـ 8 Lookups | Commands (Create/Update/Delete) + Queries (List/ById، من غير Pagination — نفس نمط `Currency`) + 8 Controllers في ملف واحد. `OrgUnit` وحده عنده فحص "لو فيه Children" عند الحذف (استثناء متعمَّد، مش قاعدة عامة). |
| **B5** | Full Vertical لـ `Employee`/`EmployeePersonalData` | Activate/Terminate/AssignUser/SetManager + `HR_REVEAL_PII` (1.1b) — `HrPiiController.Reveal` بيفك تشفير + `AuditLog` إلزامي (بدون تخزين القيمة نفسها). `GetEmployeesListQuery` مُرقّم (Paginated) — الموظفين مش زي الـ Lookups، ممكن يكبروا فعلًا. |
| **B6** | Full Vertical للـ 3 Followers | Renew (عقد جديد مربوط بالقديم، القديم → `Expired`) + Terminate على مستوى العقد لوحده (منفصل عن `TerminateEmployeeCommand`) + Nested Routes (`/hr/employees/{id}/contracts`\|`documents`\|`certifications`). |
| **B7** | Regression + أداء + توثيق | `xunit.runner.json` لضبط التوازي (تفصيل تحت) + Regression كامل نهائي + تحديث `HR-Core-Plan.md`/`Known-Issues.md`/`10-Module-HR-Payroll.md` + هذا التقرير + Backup نهائي. |

## الإحصائيات النهائية (اتفحصت بعدّ الملفات فعليًا، مش تقدير)

| البند | العدد | التفصيل |
|---|---|---|
| **الجداول الجديدة** | **18** | 13 Lookup (B1) + `Employee` + `EmployeePersonalData` (B2) + `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` (B3) |
| **Commands** | **45** | 36 في موديول HR + 9 في موديول Organization (`Country`/`City`/`Bank`) |
| **Queries** | **26** | 20 في موديول HR + 6 في موديول Organization |
| **Controllers** | **14** | `HrLookupControllers.cs` (8) + `EmployeesController.cs`+`EmployeesLookupController` (2) + `HrPiiController.cs` (1) + `HrEmploymentControllers.cs` (3) |
| **اختبارات Integration خاصة بـ HR** | **50** | عبر 7 ملفات (`HrCoreLookupsBatch1Tests`، `OrgUnitHierarchyTests`، `EmployeeBatch2Tests`، `EmploymentContractsBatch3Tests`، `HrLookupsBatch4Tests`، `EmployeeBatch5Tests`، `EmploymentFollowersBatch6Tests`) |
| **اختبارات API خاصة بـ HR** | **10** | عبر 3 ملفات (`HrLookupsApiTests`، `EmployeeApiTests`، `EmploymentFollowersApiTests`) |
| **إجمالي Suite الكامل — Integration** | **171/171 ✅** | كل المشروع، مش HR بس |
| **إجمالي Suite الكامل — API** | **260/260 ✅** (قبل B7) | كل المشروع، مش HR بس — الرقم النهائي بعد B7 في قسم "الأداء" تحت |

> **ملاحظة على عدّ الجداول**: كانت في التقدير الأولي حيرة بين 15 و18 و22 — **18 هو العدد الصحيح المتحقَّق منه** (فحص مباشر لأسماء الجداول في الـ 3 Migrations). الـ 22 كان يشمل خطأً كل جداول B1 (13 Lookup + 9 كيانات HR الأصلية المذكورة في المواصفة، بعضها مبني من قبل الخطة دي) — مش إضافة صافية.

## الأخطاء الخمسة المكتشفة والمصلَّحة أثناء التنفيذ

1. **Migration Timeout (B1)** — `SettingsPermissionsPhase3` بتاخد 35 ثانية (أكتر من الـ 30 ثانية Default) على قاعدة فاضية جديدة، بسبب SQL ديناميكي O(n) على `sys.columns`. الحل: `Command Timeout=180` صريح وقت `dotnet ef database update` بس (مش على الـ Connection String الدائم).
2. **EF Core Model Cache Bypass (B2)** — EF Core بيعمل Cache للموديل حسب نوع الـ `DbContext` بس، مش حسب أي Constructor Parameter. `EmployeePersonalData`'s `EncryptedStringConverter` بيعتمد على `ISecretProtector` المحقون، فأول `AppDbContext` يتبني في نفس الـ Process (حتى لو بـ Protector وهمي `NoOp`) كان بيفرض الموديل بتاعه على كل الـ Instances التانية — يعني التشفير ممكن يبقى معطّل بصمت. الحل: `IModelCacheKeyFactory` مخصص بياخد الـ Protector نفسه كجزء من مفتاح الـ Cache.
3. **SQL Server NULL-Uniqueness (B2)** — فهرس فريد مفلتر على `(CompanyId, UserId)` رفض موظف تاني بـ `UserId = NULL` — SQL Server بيعامل NULL متعددة كـ"متطابقة" في الفهارس الفريدة (عكس بعض قواعد البيانات التانية). الحل: `AND [UserId] IS NOT NULL` صريح في الفلتر.
4. **SystemDataSeeder Silent Bypass (B5)** — الزرع كان بيشتغل مرة واحدة بس لو جدول `MenuItems` فاضي بالكامل. بما إن القاعدة الحقيقية فيها صفوف من زمان، **9 شاشات عبر B1-B5** (مجموعة `HR` + 6 شاشات تحتها + 3 شاشات Settings) ما وصلتش للقاعدة الحقيقية خالص. الحل: زرع لكل عنصر ناقص لوحده (Pass اتنين: Groups ثم Leaves) + Backfill يدوي مرة واحدة للقاعدة الحقيقية.
5. **ButtonPermissionCatalog FallbackAction (B5)** — الـ `FallbackAction` المكتوب في الـ Catalog توثيقي بس، الفحص الفعلي بياخد الصلاحية من الـ HTTP Verb/Route Segment. `HrPiiController.Reveal` كان محتاج `[ScreenAction(Approve)]` صريح عشان الفحص يتطابق فعليًا مع القصد.

**فجوة واحدة لسه مفتوحة** (اتكشفت أثناء B7 نفسها، مش B1-B6): `TerminateEmployeeCommand` (B5) بيوقف الـ `User` من غير ما يسحب جلساته النشطة فعليًا (`UserRules.RevokeSessionsAsync` مش متسدّاة) — موثّقة بالتفصيل في `Known-Issues.md`، تعديل كود بسيط برّه نطاق B7 (توثيق فقط).

## إصلاح الأداء (`xunit.runner.json`)

الـ Full ApiTests Suite (260 اختبار) كان بياخد **31+ دقيقة** بدل الـ 15-17 المتوقعة — كل اختبار بيعمل قاعدة LocalDB منفصلة، والتوازي الافتراضي (بدون حد أقصى) كان بيحمّل الـ Memory لحد 97% + Swap. الحل: `xunit.runner.json` في `Habbak.ERP.ApiTests/` و`Habbak.ERP.IntegrationTests/` (`maxParallelThreads: 4`)، مربوط في الـ`.csproj` بـ`<None Update="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />`.

| Suite | قبل B7 | بعد B7 | التغيّر |
|---|---|---|---|
| IntegrationTests (171 اختبار) | ~8-9 دقيقة | **8 د 52 ث** | زي ما هو تقريبًا — مكانتش هي المشكلة أصلًا، والـ Cap لنفس القيمة (4) ماأثّرش عليها بشكل ملحوظ |
| ApiTests (260 اختبار) | 31 د 35 ث | **11 د 9 ث** | **-65%** — أحسن من الـ 15-20 دقيقة المتوقعة |

كل الـ 431 اختبار (171 + 260) عدّوا بنجاح كامل بعد الإصلاح — مفيش أي تراجع في النتائج، الفرق وقت التنفيذ بس.

## المرحلة التالية (1.2 → 1.5)

| المرحلة | المحتوى | التقدير |
|---|---|---|
| **1.2** | Migration الحقول الحرة (`CustodyRegister.EmployeeId` Option A، `CustodyOfficer.EmployeeId`، `Maintenance.TechnicianId`) | 5 أيام |
| **1.3** | الموظف كطرف في السندات (`CounterpartyType.Employee`) | 2 يوم |
| **1.4** | إنهاء الخدمة الإداري — **جزء كبير منه اتعمل بالفعل في B5** (`TerminateEmployeeCommand`)؛ الباقي: سحب الجلسات (الفجوة أعلاه) | أقل من يومين المتبقيين أصلًا |
| **1.5** | 8 شاشات HR (Frontend) | 10-12 يوم |

**ملاحظة مهمة لـ 1.4**: الخطة الأصلية كانت حاسبة 1.4 كمرحلة منفصلة كاملة (يومين)، لكن B5 (اللي في الأساس كان "Full Vertical لـ Employee") امتص معظم محتواها فعليًا (`TerminateEmployeeCommand` بمنطق إيقاف User + إلغاء Scopes + منع العهدة المفتوحة). الباقي من 1.4 هو بس سطر واحد (سحب الجلسات) — تحديث التقدير مهم قبل جدولة 1.4 كمرحلة كاملة منفصلة.
