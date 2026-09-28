# خطة تنفيذ HR Core (المرحلة 0 + المرحلة 1)

> مبنية على `Docs/Modules/10-Module-HR-Payroll.md` (المعتمد) + `docs/ERP-Review/12-HR-Payroll-Analysis.md` + `docs/ERP-Review/00-Decisions.md` + الأقسام 6.2، و8.5، و10، و11، و12، و14، و18، و24، و26 من `Docs/Modules/00-Project-Overview.md`.
> **النطاق هنا بس**: ملحق ب.2 من `10-Module-HR-Payroll.md` — المرحلة 0 (المتطلبات السابقة) والمرحلة 1 (HR Core). محرك الموافقات (مرحلة 2 من الموديول)، والحضور والرواتب وإنهاء الخدمة الكامل والخدمة الذاتية **برّه النطاق تمامًا** هنا.
> **كل مسار كود مذكور تحت اتفحص فعليًا في الكود الحالي** (مش من المواصفة) قبل كتابة الخطة دي — الفروق بين المتوقع والواقع متسجّلة صراحة في كل بند.

---

## 0. ملخص تنفيذي

المرحلتين دول أرضية إلزامية قبل أي كيان HR حقيقي يتلمس بيانات موظف فعلي. الجهد المرجعي في `10-Module-HR-Payroll.md` (ملحق ب.2): **8-12 يوم** للمرحلة 0، و**24-35 يوم** للمرحلة 1 — الإجمالي **~32-47 يوم**. الفحص المباشر للكود أكّد الرقم دا معقول، وكشف كمان تفاصيل مهمة:

**أخبار كويسة**: 5 من نقاط التكامل المحاسبي **جاهزة فعليًا من غير أي تعديل**: `CounterpartyType.Employee = 3` موجود (`Enums.cs:68-74`)، و`CompanyAccountRole.EmployeeReceivable = 6` و`TipsPayable = 16` موجودين ومربوطين بـ`AccountType` صح (`CompanyAccountMapping.cs:33-53,64-85`)، و`SourceDocumentType.Payroll = 5` موجود، و`IPostingService` جاهز للاستهلاك من غير تعديل، ونمط `DepreciationRunKeys.For` (`DepreciationCommands.cs:28`) جاهز يتنسخ لـ`PayrollRunKeys` لاحقًا. كل حقول الـ"أرقام الحرة" (`User.EmployeeId`، و`CustodyRegister.EmployeeId`، و`Maintenance.TechnicianId`) **متعلّمة فعليًا في تعليقات الكود نفسها** بإنها مستنية HR — مفيش مفاجآت هنا.

**أخبار محتاجة انتباه**: 5 آليات مطلوبة في المرحلة 0 **مش موجودة خالص فى الكود دلوقتي**، فبناؤها greenfield مش تعديل: **مفيش `Serilog`** (بس `Microsoft.Extensions.Logging` الافتراضي)، **مفيش** أي `EF Core ValueConverter` للتشفير على مستوى العمود، **مفيش** استخدام HMAC غير TOTP (`Totp.cs:34`، حاجة تانية خالص)، **مفيش** Endpoint لإظهار بيانات مخفية، و**مفيش** أي مكوّن Wizard في الفرونت خالص (اتفحص بالبحث، صفر نتائج حقيقية). و`FieldPermissionCatalog` **مش جدول في قاعدة البيانات ولا مسير من إعدادات** — هو `Dictionary` ثابت في الكود (`FieldPermission.cs:37-51`)، فـ"تسجيل حقل PII" معناه فعليًا **تعديل الملف ده يدويًا**، مش شاشة إعداد.

**تصحيح على افتراض في المستند المصدر**: "قاعدتي بيانات" المذكورة في ملاحظات المشروع مش قاعدتين حقيقيتين — فيه `ConnectionStrings:Default` واحد بس (`HabbakErp`، وفيه بيانات عميل حقيقية). اللي بيحصل فعليًا هو **قاعدة تجريبية مؤقتة** (`HabbakErp_Trial_<Name>`) بتتعمل، الـ Migration بيتأكد عليها، وبعدين بتتمسح، وبعدين الـ Migration بيتطبّق على `HabbakErp` الحقيقية — موثّق في `Docs/Reports-UPDATED/Remarks7-Final-Report.md`. الخطة دي بتتبع نفس الأسلوب.

**أعلى نقطة خطر في المرحلتين**: `CustodyRegister.EmployeeId` عمود **إلزامي (`long`, NOT NULL)** فيه بيانات حقيقية دلوقتي (أرقام حرة من غير أي حقل اسم مرافق)، فمفيش أي إشارة تلقائية تتحقق منها المطابقة — التقرير التشخيصي (قاعدة 50) هيسيب الأغلبية "محتاجة مراجعة يدوية" على الأرجح، مش "اتربطت تلقائيًا". ده مختلف عن `Maintenance.TechnicianId` اللي معاه `TechnicianName` نصي ممكن يتقارن بالاسم.

**تحديث 2026-09-26 — 3 قرارات اتحسمت** (تفصيل كامل في قسم 8، وفي 0.6 و1.2 و1.5): (1) مفاتيح التشفير — Vault مع Spike يوم واحد وFallback مؤقت لـ Env Var، وشرط صارم إن المرحلة 1 مايبدأش إلا لما Vault يشتغل فعليًا. (2) `CustodyRegister.EmployeeId` — عمود جديد `EmployeeIdLinked` جنب القديم (Option A)، ربط يدوي عن طريق شاشة `HR_CUSTODY_MIGRATION`، وتنضيف لاحق (Pending Cleanup) بعد أسبوعين من نهاية المرحلة 1. (3) `HR_SETTINGS` — الكيان بكل الحقول من الأول، الشاشة تعرض حقول المرحلة 1 بس. **لسه فاضل قرار واحد صغير مفتوح**: سياسة الحظر عند إنهاء الخدمة لو فيه عهدة مفتوحة (سؤال 4، قسم 8).

**تحديث 2026-09-26 (تنفيذ) — المرحلة 0 خلصت 5/6 (83%)**: ✅ 0.1 (Self/Team scope) · ⏸️ **0.2 اتأجل لـ 1.1** (قرار محسوم — قسم 8، تفصيله الكامل في 1.1b) · ✅ 0.3 (تشفير عمود + HMAC، `EncryptedStringConverter`/`PiiSecretProtector`) · ✅ 0.4 (`PiiFieldAttribute` + اختبار الكتالوج) · ✅ 0.5 (Serilog + `PiiDestructuringPolicy`) · ✅ 0.6 (Vault Full — Spike نجح من أول مرة، مسار Vault الكامل مش الـ Fallback). كل الكود اتبنى واتاختبر فعليًا (`dotnet test`/`dotnet build` نظيفين) — مش خطة نظرية. **المرحلة 1 (1.1 Core HR entities) جاهزة تبدأ.**

---

## 1. تفصيل المرحلة 0

### 0.1 نطاق بيانات Self/Team — ✅ منفّذ

| | |
|---|---|
| **الوصف** | إضافة `DataScope` enum (`Self`/`Team`/`Branch`/`Company`)، وتفعيله فوق آلية الـ Query Filters الموجودة، و`EmployeeId` في الـ JWT Claims. **نطاق محدود عمدًا**: المرحلة 0/1 مفيهاش أي كيان محتاج فلترة `Team` فعليًا (شاشات `ESS_TEAM` في مرحلة لاحقة) — فالـ `Team` بيتبني كـ Enum value + توثيق بس دلوقتي، وحساب "شجرة `ManagerId`" (قسم 7.1 من `10-Module-HR-Payroll.md`) بيتأجّل لحد أول مستهلك فعلي ليه (يُوثَّق كـ Follow-up صريح، مش بيتنسى). |
| **الملفات المتأثرة** | جديد: `src/Habbak.ERP.Domain/Common/DataScope.cs` (Enum) · `src/Habbak.ERP.Domain/Common/IEmployeeScopedEntity.cs` (مرآة لـ `IBranchScopedEntity`، الموجودة في `src/Habbak.ERP.Domain/Common/ICompanyScopedEntity.cs:17-20` — نفس الملف فيه `ICompanyScopedEntity` و`IBranchScopedEntity` معًا). تعديل: `src/Habbak.ERP.Infrastructure/Persistence/AppDbContext.cs` (إضافة فرع فلترة `IEmployeeScopedEntity` جنب `BuildBranchFilters` الموجودة، سطر 599-640؛ الآلية Reflection/Expression Tree بالكامل — كيان جديد بيلتزم `IEmployeeScopedEntity` بياخد الفلترة "ببلاش") · `src/Habbak.ERP.API/Auth/JwtAccessTokenIssuer.cs` (`AuthClaims` سطر 10-17: إضافة `EmployeeId` جنب `CompanyId`/`BranchId`/`UserId`؛ بناء الـ Claims سطر 25-44) · `src/Habbak.ERP.Application/Settings/Auth/SessionIssuer.cs` (`IssueAsync` سطر 80: تمرير `EmployeeId` لـ `AccessTokenSubject` — الـ Record ده معرّف في `Application/Common/Interfaces`، يتأكد مكانه بالظبط وقت التنفيذ) · `AccessTokenSubject` (إضافة `EmployeeId` اختياري). |
| **Migration** | لأ — تعديل Interfaces وClaims بس، مفيش عمود جديد في المرحلة دي (`EmployeeId` بيفضل `null` لحد ما `Employee` يتبني في 1.1، وقتها `SessionIssuer` بيتربط بيه فعليًا عن طريق `Employee.UserId`). |
| **الاختبارات المطلوبة** | Integration: كيان تجريبي بـ`IEmployeeScopedEntity` بيتفلتر صح بنفس نمط اختبار `IBranchScopedEntity` الموجود · API: الـ JWT الصادر فيه `EmployeeId` claim (فاضي لحد ما فيه موظف مربوط) · اختبار سلبي: مستخدم بدون موظف مربوط ما بيتأثرش بأي فلتر `Self`. |
| **التقدير** | 2 يوم |
| **الترتيب** | 2 (بالتوازي مع 0.5 و0.6، مستقل عن الباقي) |

### 0.2 إخفاء جزئي + إظهار مدقَّق — ⏸️ منقول لـ 1.1

> **قرار محسوم 2026-09-26**: بند 0.2 اتأجل بالكامل لـ **1.1** (تفصيله الكامل هناك بعد ما `Employee`/`EmployeePersonalData` يتوجدوا). السبب: الـ Endpoint مالوش معنى ولا كيان حقيقي يشتغل عليه قبل كده، والاختبار صعب من غيره، واليجن (Authorization) والحقول هتكون معروفة أصلًا وقت 1.1 (تفصيل القرار في قسم 8).

### 0.3 تشفير عمود + HMAC — ✅ منفّذ

| | |
|---|---|
| **الوصف** | مفيش أي نمط `EF Core ValueConverter` للتشفير في الكود دلوقتي (اتفحص بالبحث، صفر نتائج) — النمط الوحيد الموجود هو `ISecretProtector` (`src/Habbak.ERP.API/Auth/DataProtectionSecretProtector.cs:19-44`) بيتستخدم يدويًا في الـ Command Handlers لتشفير سر 2FA بس، مش عن طريق EF تلقائيًا. المطلوب هنا: (أ) `EncryptedString` Value Converter عام يتستخدم في `HasConversion` وقت الإعداد، بيستخدم بروتكتور مخصص (`"Habbak.ERP.HR.PII.v1"` بدل `"Habbak.ERP.Secrets.v1"` الحالي — مفتاح تشفير منفصل عن مفاتيح الـ 2FA)، و(ب) Helper منفصل لحساب HMAC (`NationalIdHash`) — **ده مش Value Converter**، لازم يتحسب صراحة في الـ Command Handler وقت الحفظ عشان يتقارن ويتفهرس (فهرس فريد على `CompanyId + NationalIdHash`)، مش يتشفّر تلقائيًا. الـ HMAC Key **مختلف عن مفتاح التشفير**: لازم يفضل ثابت طول عمر الشركة (تغييره يكسر كل الفهرسة القديمة)، فمصدره المفروض يكون Vault مباشرة (0.6) مش `DataProtection`. |
| **الملفات المتأثرة** | جديد: `src/Habbak.ERP.Infrastructure/Persistence/Converters/EncryptedStringConverter.cs` (يستقبل `ISecretProtector` في الـ Constructor، `AppDbContext` بيبنيه مرة واحدة ويمرره لكل `HasConversion` محتاجه) · `src/Habbak.ERP.Application/Common/Interfaces/IPiiHasher.cs` (واجهة جديدة: `string ComputeHash(string value)`) · `src/Habbak.ERP.Infrastructure/Services/HmacPiiHasher.cs` (تنفيذ HMAC-SHA256، المفتاح من الإعداد/Vault) · تعديل: `src/Habbak.ERP.Infrastructure/DependencyInjection.cs` (تسجيل `IPiiHasher`). الاستخدام الفعلي (`NationalIdEncrypted`/`BankIbanEncrypted` على `EmployeePersonalData`) بيحصل في المرحلة 1 (1.1) — دي بس البنية التحتية العامة القابلة لإعادة الاستخدام. |
| **Migration** | لأ (مفيش كيان لسه). |
| **الاختبارات المطلوبة** | Unit: `EncryptedStringConverter` — قيمة بتتشفّر وترجع زي ما هي (Round-trip)، والقيمة المخزّنة فعليًا في الـ DB **مش** نص صريح (يتفحص مباشرة بـ SQL في اختبار التكامل) · Unit: `HmacPiiHasher` — نفس المدخل بيدّي نفس الـ Hash دايمًا (Deterministic)، ومدخلين مختلفين بيدّوا Hash مختلف (تصادم منخفض الاحتمال يكفي هنا). |
| **التقدير** | 1.5 يوم |
| **الترتيب** | 4 (محتاج 0.6 يخلص الأول — مصدر المفتاح) |

### 0.4 تسجيل PII في `FieldPermissionCatalog` — ✅ منفّذ

| | |
|---|---|
| **الوصف** | `FieldPermissionCatalog` (`src/Habbak.ERP.Domain/Settings/Permissions/FieldPermission.cs:33-65`) **مش جدول قاعدة بيانات ولا مسير من إعداد** — هو `Dictionary` ثابت (`SensitiveFields`, سطر 37-51) بيربط `ScreenCode → EntityType → أسماء حقول`. تسجيل حقل PII معناه فعليًا **تعديل هذا الملف بالذات** وقت بناء كل شاشة HR، مش عمل منفصل. اللي المرحلة 0 تقدر تبنيه دلوقتي (من غير ما `Employee`/`EmployeePersonalData` يتبنوا لسه) هو **الاختبار الآلي بس**: اختبار بيفحص (Reflection على الـ Assembly، أو Convention صريحة زي "أي Property اسمها بينتهي بـ`Encrypted` أو `Hash` أو مُعلَّمة بـ `[PiiField]` جديد") إن كل حقل من دول **لازم** يكون مسجّل في `FieldPermissionCatalog`، وإلا الاختبار بيفشل — التسجيل الفعلي لحقول `EmployeePersonalData` نفسها بيحصل في 1.1 لما الكيان يتبني، لكن الاختبار اللي بيضمن عدم النسيان بيتكتب دلوقتي. |
| **الملفات المتأثرة** | جديد: `[PiiField]` Attribute بسيط (`src/Habbak.ERP.Domain/Common/PiiFieldAttribute.cs`) يتحط على أي Property حساس بغض النظر عن اسمها · اختبار جديد في `Habbak.ERP.ApiTests` (بجوار `FieldButtonPermissionsTests.cs` الموجود، نفس المشروع) بيسكان كل الـ Entities في `Habbak.ERP.Domain` عن `[PiiField]`، ويتأكد كل واحد فيهم موجود في `FieldPermissionCatalog.IsListed(entityType, fieldName)` (`FieldPermission.cs:59-60`). |
| **Migration** | لأ. |
| **الاختبارات المطلوبة** | الاختبار نفسه هو المطلوب — لازم يفشل عمدًا لو حد ضاف `[PiiField]` جديد ونسي التسجيل (يتجرب بكيان وهمي في اختبار الوحدة قبل ما يتوصل لأي كيان HR حقيقي). |
| **التقدير** | 1 يوم |
| **الترتيب** | 4 (بالتوازي مع 0.3 و0.5 — مستقل، لكن مالوش قيمة حقيقية غير كتوثيق واختبار جاهز لحد ما 1.1 يستخدمه) |

### 0.5 تعقيم الـ Logging — ✅ منفّذ

| | |
|---|---|
| **الوصف** | **`Serilog` مش موجود في المشروع خالص** — لا Package Reference ولا أي إعداد، بس `Microsoft.Extensions.Logging` الافتراضي (`appsettings.json`/`appsettings.Development.json`، قسم `Logging` بس). البند ده معناه فعليًا **إدخال `Serilog` من الصفر**، مش تمديد Enricher موجود — ده أكبر من "تعقيم PII"، وهيلمس تهيئة الـ Host كله (`Program.cs`). عشان النطاق يفضل صغير: الهدف هنا هو **تسجيل Serilog + Destructuring Policy واحدة بس** بتستبعد أي Property مُعلَّمة `[PiiField]` (من 0.4) من أي Log تلقائيًا — مش مراجعة كل سطر Logging موجود في المشروع (ده برّه النطاق). |
| **الملفات المتأثرة** | تعديل: `src/Habbak.ERP.API/Program.cs` (`builder.Host.UseSerilog(...)`) · `src/Habbak.ERP.API/Habbak.ERP.API.csproj` (Package References: `Serilog.AspNetCore`, `Serilog.Sinks.Console` أو المكافئ) · `appsettings.json`/`appsettings.Development.json` (قسم `Serilog` بدل `Logging`، أو الاتنين معًا بالـ Bridge حسب القرار وقت التنفيذ) · جديد: `src/Habbak.ERP.API/Logging/PiiDestructuringPolicy.cs` (بيفحص أي Property اسمها `[PiiField]` ويستبدلها بـ `"[REDACTED]"` — نفس نص `AuditLog.Redacted` بالظبط، `AuditLog.cs:10`، عشان الاتساق). |
| **Migration** | لأ. |
| **الاختبارات المطلوبة** | Unit: `PiiDestructuringPolicy` — كائن فيه Property معلّمة `[PiiField]`، الناتج المُسجَّل (Log Output فعلي في اختبار، مش مجرد استدعاء الدالة) مافيهوش القيمة الخام. |
| **التقدير** | 1.5 يوم |
| **الترتيب** | 2 (بالتوازي مع 0.1، مستقل تمامًا عن باقي بنود المرحلة) |

### 0.6 مفاتيح التشفير — ✅ منفّذ (Vault كامل)

> **قرار محسوم 2026-09-26 — Vault مع Fallback مؤقت** (تفصيل كامل في قسم 8، سؤال 1).

| | |
|---|---|
| **الوصف** | الوضع الحالي: مفاتيح `DataProtection` (بتاعة الـ 2FA فقط دلوقتي) متخزّنة كملفات على القرص جوه الـ API نفسه (`src/Habbak.ERP.API/App_Data/DataProtection-Keys/`، ملف مفتاح حقيقي موجود فعلًا هناك من التشغيل التطويري) — مفيش `DataProtection:KeysPath` صريح في أي `appsettings`، فبيقع على الافتراضي (`Program.cs:60-69`). التوصية في `10-Module-HR-Payroll.md` (ملحق د.1): **HashiCorp Vault** مناسب لـ Contabo VPS، **بشرط نسخة احتياطية لمفاتيح فك الختم (Unseal Keys) برّه نفس الـ VPS**. |
| **المسار المحسوم** | **Day 1 — Spike (نص يوم)**: محاولة تنصيب HashiCorp Vault على بيئة Dev فعليًا. **نجح** → استكمال 0.6 الكامل (2-3 يوم) عادي، و0.3 تستنى عليه زي ما هو مخطط. **فشل** → 0.6 يتحول مؤقتًا لمفتاح في **Environment Variable محمي** (موثّق صراحة كحل انتقالي، **بشرط Backup Strategy له من نفس اليوم** — مش اختياري)؛ 0.3 بتكمل عليه فورًا من غير ما تستنى؛ وفي نفس الوقت Vault بيتبنى **بالتوازي** مع 0.1 و0.5 و0.4 (نفس المطور أو مطور تاني، حسب التوفر). |
| **✅ نتيجة الـ Spike (2026-09-26، محليًا على جهاز التطوير)** | **نجح بالكامل.** Vault 2.1.1 (أحدث إصدار مستقر رسمي، `vault_2.1.1_windows_amd64.zip` من `releases.hashicorp.com`، الـ SHA256 اتطابق مع `SHA256SUMS` الرسمي) نزل واشتغل في وضع `-dev` محليًا. اتعمل KV secrets engine (`hr-spike/`) واختبار Round-trip كامل: كتابة/قراءة عن طريق `vault.exe kv put/get` (CLI) نجحت، وكتابة/قراءة عن طريق `VaultSharp 1.17.5.1` (.NET، نفس المكتبة المقترحة في "الملفات المتأثرة") نجحت كمان بنفس النتيجة. **⚠️ ملحوظة مهمة**: ده وضع `-dev` بس (تخزين في الذاكرة، بدون TLS، Root Token صريح) — مناسب لإثبات مسار التكامل البرمجي بس، **مش تمثيل لبيئة الإنتاج على Contabo VPS** (اللي محتاجة تنصيب حقيقي، Storage Backend دائم، تفعيل TLS، وسياسات وصول — ده جزء من استكمال 0.6 الكامل، مش الـ Spike). **القرار**: نكمّل بمسار Vault الكامل (مش الـ Fallback)، لأن الـ Spike نجح من أول محاولة. |
| **الشرط الصارم (Gate بين المرحلة 0 والمرحلة 1)** | **المرحلة 1 (1.1 Core HR entities) مايبدأش إلا بعد ما Vault يشتغل فعليًا** ومفتاح التشفير/HMAC يترحّل من الـ Env Var المؤقت لـ Vault. لو Vault لسه مش جاهز لما باقي بنود المرحلة 0 تخلص → **توقّف والقرار يطلع للإدارة** (مش قرار هندسي يتاخد منفرد) — يُسجَّل في قسم 3 (Dependencies) كـ Gate صريح، وفي قسم 7 (Timeline) كخطر جدولة محتمل. |
| **الملفات المتأثرة** | مفيش كود تطبيقي مباشر لتنصيب Vault نفسه — ده تجهيز بيئة (تنصيب Vault Server، سياسات وصول، تفعيل Secrets Engine). تعديل: `src/Habbak.ERP.Infrastructure/DependencyInjection.cs` وربما `Program.cs` لإضافة عميل Vault (`VaultSharp` أو مكافئ) كمصدر لمفتاح HMAC (0.3) ولاحقًا مفتاح تشفير الأعمدة نفسه؛ لو الفشل حصل، نفس الملفات تتعدّل مبدئيًا لقراءة المفتاح من `IConfiguration`/Environment Variable بدل Vault، وراها Migration كود بسيطة (تبديل مصدر المفتاح بس، من غير تغيير في شكل البيانات المشفّرة نفسها لو المفتاح اتولّد بنفس الطريقة) لما Vault يجهز. |
| **Migration** | لأ (على مستوى قاعدة البيانات). |
| **الاختبارات المطلوبة** | اختبار استعادة فعلي (Restore Test) لمفتاح تجريبي من النسخة الاحتياطية — مش مجرد التأكد من وجود الملف (نفس مبدأ `00-Project-Overview.md §24`). لو الفشل حصل والمسار Env Var اتفعّل: اختبار إضافي إن الـ Backup الخاص بالـ Env Var فعليًا موجود ومُختبَر قبل ما 0.3 يبدأ يشتغل عليه. |
| **التقدير** | نص يوم (Spike) + 2-3 يوم (Vault الكامل، لو نجح) — أو نص يوم (Spike) + Env Var فوري + Vault بالتوازي مع باقي بنود المرحلة 0 (لو فشل، المدة تتحدد بمدى تعقيد الإعداد الفعلي، تُتابع يوميًا) |
| **الترتيب** | 1 (بادئ) — لكن دلوقتي بواقعين: (أ) 0.3 يبدأ من نص اليوم الأول تقريبًا (Env Var أو Vault، أيهما جاهز)، (ب) **Gate جديد** قبل 1.1 يتطلب Vault شغّال فعليًا بغض النظر عن باقي تقدم المرحلة 0 |

---

## 2. تفصيل المرحلة 1

### Phase 1.1 — Final Status (كل الـ 6 Batches خلصوا — 2026-09-27)

> **1.1 و1.1b قفلوا بالكامل.** التنفيذ الفعلي اتقسّم لـ 6 Batches (B1-B6)، كل واحد فيهم Research/Plan → Execute → Test → Regression → تقرير، بموافقة صريحة قبل كل واحد. الجدول تحت ملخص كل Batch؛ الأرقام الإجمالية والـ Bugs المكتشفة موثّقة بالكامل في `docs/Implementation/Phase-1.1-Final-Report.md`.

| Batch | المحتوى | الحالة |
|---|---|---|
| **B1** | 13 كيان مرجعي (Lookup) — 4 Organization (`Country`/`City`/`Nationality`/`Bank`) + 9 HR (`JobGrade`/`JobPosition`/`OrgUnit`/`EmployeeDocumentType` + 5 جداد) + Migration شاملة (22 جدول مع الـ 9 الأصليين) + `MenuItemSeedData` | ✅ خلص |
| **B2** | `Employee` + `EmployeePersonalData` (تشفير عمود + HMAC، أول استخدام فعلي لـ `EncryptedStringConverter`/`IPiiHasher`) | ✅ خلص |
| **B3** | الـ 3 "Followers" (`EmploymentContract`/`EmployeeDocument`/`EmployeeCertification`) — أول استخدام فعلي لـ `IEmployeeScopedEntity` | ✅ خلص |
| **B4** | Full Vertical (Commands/Queries/Controllers) للـ 8 Lookups اللي أخدت شاشة كاملة (قرار Hybrid) | ✅ خلص |
| **B5** | Full Vertical لـ `Employee`/`EmployeePersonalData` — Activate/Terminate/AssignUser/SetManager + `HR_REVEAL_PII` (1.1b) | ✅ خلص |
| **B6** | Full Vertical للـ 3 Followers — Renew/Terminate على مستوى العقد، Nested Routes | ✅ خلص |
| **B7** | Full Regression + `xunit.runner.json` (أداء) + توثيق نهائي (هذا التحديث) | ✅ خلص |

**الإحصائيات النهائية** (تفصيل التحقق في `Phase-1.1-Final-Report.md`): **18 جدول** جديد (13 Lookup + `Employee` + `EmployeePersonalData` + 3 Followers) · **45 Command** (36 HR + 9 Organization) · **26 Query** · **14 Controller**. تفاصيل الـ Bugs الخمسة المكتشفة والمصلّحة أثناء التنفيذ (Migration Timeout، وEF Core Model Cache، وSQL Server NULL-uniqueness، وSystemDataSeeder Silent Bypass، وButtonPermissionCatalog FallbackAction) موثّقة في `Docs/Reports-UPDATED/Known-Issues.md`.

**الباقي من 1.1/1.1b برّه نطاق B1-B7 عمدًا**: Frontend (1.5) — الشاشات كلها لسه من غير واجهة. أي Batch لاحق (1.2-1.5) بياخد نفس الأسلوب (Research → Plan → Approval → Execute → Test).

---

### 1.1 Core HR entities

> **محدّث 2026-09-26 بعد Research Pass** (`Docs/Implementation/Phase-1.1-Research.md`) — نمط الملفات وترتيب البناء اتغيّروا عن النص الأصلي بناءً على فحص الكود الفعلي.

| | |
|---|---|
| **الوصف** | 9 كيانات جديدة: `Employee` (+`ILookupEntity`)، `EmployeePersonalData` (1:1، جدول منفصل)، `OrgUnit` (+`ILookupEntity`، `ParentId`)، `JobPosition`، `JobGrade` (كلاهما `ILookupEntity`)، `EmploymentContract`، `EmployeeDocumentType` (`ILookupEntity`)، `EmployeeDocument`، `EmployeeCertification`. **+ 13 كيان مرجعي (Lookup) جديد — قرار محسوم 2026-09-26** (تفصيل كامل في "كيانات مرجعية جديدة (13 Lookup)" تحت): 4 من موديول التنظيم (`Country`، و`City`، و`Nationality`، و`Bank`) و9 من HR (`JobGrade`، و`JobPosition`، و`OrgUnit`، و`EmployeeDocumentType` الأربعة الأصليين + `RelationshipType`، و`MilitaryStatus`، و`QualificationType`، و`InsuranceOffice`، و`TerminationReason` الخمسة الجدد). **مفيش جدول Lookup عام** — كل كيان جدول مستقل بيلتزم `ILookupEntity`. كل التفاصيل الحقلية في `10-Module-HR-Payroll.md §2.1` (مُعتمدة، منقولة حرفيًا من 5.1 في التحليل الأولي). **ملاحظة تصميم**: `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` هيتم تفعيل `IEmployeeScopedEntity` (موجودة فعليًا من Phase 0) عليها من الأول — كده أي شاشة Self-service مستقبلية (خارج نطاق هذه الخطة) بتاخد الفلترة "ببلاش" من غير كود إضافي. |
| **نمط ملفات الـ Application (محسوم 2026-09-26)** | **فولدر منفصل لكل Command/Query** — `src/Habbak.ERP.Application/HR/{Employees,OrgUnits,JobPositions,JobGrades,EmploymentContracts,EmployeeDocumentTypes,EmployeeDocuments,EmployeeCertifications}/Commands/{CreateX,UpdateX,DeleteX}/XCommand.cs` + `Queries/{GetXList,GetXById}/XQuery.cs` — **نفس نمط `Purchasing/PurchaseOrders`/`Accounting/Vouchers`/`Accounting/TreasuryTransfers`** (كل كود اتكتب فعليًا في آخر يومين قبل هذا التحديث)، **مش** نمط `UserCommands.cs`/`CategoryCommands.cs` المجمّع (أقدم، مش الاتجاه الحالي — تفصيل المقارنة في `Phase-1.1-Research.md §2.5`). |
| **الملفات المتأثرة** | جديد بالكامل: `src/Habbak.ERP.Domain/Organization/{Country,City,Nationality,Bank}.cs` (4 Lookups جديدة) · `src/Habbak.ERP.Domain/HR/{Employee,EmployeePersonalData,OrgUnit,JobPosition,JobGrade,EmploymentContract,EmployeeDocumentType,EmployeeDocument,EmployeeCertification,RelationshipType,MilitaryStatus,QualificationType,InsuranceOffice,TerminationReason}.cs` (9 الأصليين + 5 Lookups جديدة) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Organization/OrganizationLookupsConfigurations.cs` (ملف واحد للأربعة Lookups، نفس نمط `HrConfigurations.cs`) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/HR/HrConfigurations.cs` (ملف واحد متعدد الكلاسات للـ EF Configurations بس — لا علاقة بنمط الـ Commands أعلاه؛ نفس نمط `FixedAssetsConfigurations.cs`) — فيه: `NationalIdEncrypted`/`BankIbanEncrypted` عن طريق `EncryptedStringConverter` (0.3، أول استخدام فعلي ليه)، وفهرس فريد مفلتر `(CompanyId, NationalIdHash)`، وفهرس فريد مفلتر `(CompanyId, UserId)` على `Employee.UserId` (قاعدة 1) · `EmployeePersonalData` بنمط `BranchPOSSettings` (FK + فهرس فريد مفلتر، **مش** Shared Primary Key — مفيش سابقة لده في المشروع، `Phase-1.1-Research.md §2.3`) · تعديل: `src/Habbak.ERP.Infrastructure/Persistence/AppDbContext.cs` (9 `DbSet` جديدة) · `src/Habbak.ERP.Application/HR/{...}/Commands/`+`Queries/` (فولدر لكل Command/Query، أعلاه) · `src/Habbak.ERP.API/Controllers/HR/HrControllers.cs` (`[Screen("HR_EMPLOYEES")]` إلخ) · **تعديل مؤكد الآن (كان معلّق كخطر 8)**: `src/Habbak.ERP.Infrastructure/Persistence/Seeding/MenuItemSeedData.cs` — تسجيل الشاشات الجديدة **يدوي بالكامل** (`Leaf(group, "CODE", ...)` — ملف Seed ثابت، مش Reflection ولا DB Table)؛ مجموعة `HR` مستقلة جديدة تُضاف بكل الـ 7 شاشات (قرار محسوم — قسم 8). |
| **Migration** | نعم — إضافية بالكامل (**22 جدول جديد**: 9 كيانات HR Core الأصليين + 13 Lookup جديد، مفيش تعديل على جدول موجود). `dotnet ef migrations add HrCoreEntities`. |
| **فحص حلقة `OrgUnit.ParentId` (محسوم 2026-09-26)** | **Walk كامل للأعلى بـ `HashSet<long>`** (مش فحص سطحي "الأب = نفسي" زي `ItemGroup` الوحيد الموجود حاليًا — مفيش سابقة حقيقية في الكود لنسخها، `Phase-1.1-Research.md §2.2`): كل مرة نضيف/نعدّل `ParentId`، امشي لأعلى من الأب المقترح لحد ما توصل لـ `null` (الجذر) أو تلاقي `Id` الحالي تاني في المسار (حلقة). |
| **الاختبارات المطلوبة** | Integration: `OrgUnit.ParentId` — حلقة مباشرة (Self-loop: `A.ParentId = A.Id`)، وحلقة غير مباشرة (`A→B→A`)، وسلسلة عمق 2 وعمق 3 صحيحة (لازم تتقبل)، وحلقة في عمق 3 (لازم تترفض) · `Employee.ManagerId` نفس الفحوصات (قاعدة 3) · فريد `(CompanyId, UserId)` على `Employee.UserId` (محاولتين متزامنتين لربط نفس المستخدم بموظفين مختلفين) · فريد `(CompanyId, NationalIdHash)` (رقمين قوميين متطابقين، الرقم مشفّر مختلف بس الـ Hash واحد) · `NationalIdEncrypted` مخزّن فعليًا كنص غير مقروء **على `EmployeePersonalData` نفسها** (فحص SQL مباشر — مش بس الكيان التجريبي اللي اتفحص بيه الـ Converter في 0.3) · الموظف ما يتفعّلش (`Active`) إلا بعد عقد ساري + كل المستندات الإلزامية + هيكل راتب (قاعدة 2 — نطاق مبسّط هنا: التحقق موجود من غير هيكل راتب حقيقي لسه، الرواتب مرحلة لاحقة، فالشرط ده بيتأجل جزئيًا وبيتوثّق كـ Open Question، قسم 8). |
| **التقدير** | **15 يوم** (كان 10 — زيادة 5 أيام لـ 13 Lookup إضافي، كل واحد بسيط (`ILookupEntity` قياسي) بس بمتوسط ~0.4 يوم لكل واحد: كيان + Configuration + Commands/Queries بنمط الفولدر المنفصل + تسجيل شاشة في `MenuItemSeedData` — راجع قسم 8 لسؤال مفتوح عن هل كل الـ 13 محتاجين شاشة List/Edit مستقلة فعلًا) |
| **ترتيب البناء (محسوم 2026-09-26 — 4 Batches، محدّث بالـ 13 Lookup)** | **B1** (3 خطوات فرعية بالتسلسل، وبالتوازي جوه كل خطوة): **(أ)** موديول التنظيم — `Country` أولًا (مفيش تبعية)، بعدين `City`/`Nationality`/`Bank` بالتوازي (كلهم `CountryId?`/`CountryId` بيعتمدوا على `Country` بس). **(ب)** HR المستقلة تمامًا (بالتوازي، مفيش تبعية بينهم): `RelationshipType`، و`MilitaryStatus`، و`QualificationType`، و`InsuranceOffice`، و`TerminationReason`. **(جـ)** HR الأصلية (بالترتيب المتفق عليه سابقًا): `JobGrade`→`JobPosition`→`OrgUnit`→`EmployeeDocumentType`. ثم Migration شاملة واحدة لكل الـ 13 + الـ 9 الأساسيين (22 جدول)، وتحديث `MenuItemSeedData.cs`. **B2**: `Employee` + `EmployeePersonalData` (يعتمدوا على B1 كامل — `EmployeePersonalData` بتاخد `NationalityId`/`CityId`/`BankId`/`MilitaryStatusId`/`QualificationTypeId`/`EmergencyContactRelationshipTypeId` كـ FKs جديدة، تفصيل تحت). **B3**: التابعين (`EmploymentContract`، `EmployeeDocument`، `EmployeeCertification` — بالتوازي مع بعض، كلهم بيعتمدوا على `Employee` بس مش على بعض). **B4**: اختبارات شاملة (API + تشغيل Regression كامل) — **مش كل الاختبارات مؤجلة لآخر حاجة**: كل Batch ياخد اختباراته الخاصة (Integration) فور ما يتبني، B4 هو الـ API Tests الشاملة + Regression بس. |
| **الترتيب** | 5 (الأساس اللي كل حاجة تانية في المرحلة 1 بتعتمد عليه) |

#### كيانات مرجعية جديدة (13 Lookup) — قرار محسوم 2026-09-26، جزء من B1

> **التصنيف**: 4 من موديول **التنظيم (Organization)** — بيانات جغرافية/مصرفية عامة ممكن تتستخدم برّه HR مستقبلًا (فواتير، عناوين شركات...)، فمكانها الطبيعي `src/Habbak.ERP.Domain/Organization/` مش HR، حتى لو HR أول مستهلك ليها فعليًا. 9 من موديول **HR** نفسه (الأربعة الأصليين من `10-Module-HR-Payroll.md §2.1` + خمسة جداد بيغذّوا حقول `EmployeePersonalData`/مراحل لاحقة) — مكانهم `src/Habbak.ERP.Domain/HR/`. **مفيش جدول Lookup عام في الحالتين** — كل كيان جدول مستقل بيلتزم `ILookupEntity` (`Code`/`NameAr`/`NameEn`/`IsActive`).
>
> **نطاق البيانات (قرار تصميم وقت التنفيذ، مش في الطلب الأصلي حرفيًا)**: `Country`/`City`/`Nationality`/`Bank`/`RelationshipType`/`MilitaryStatus`/`QualificationType`/`TerminationReason`/`InsuranceOffice` **نظام-عام (System-Wide)** — بدون `CompanyId`، فريد على `Code` بس، **نفس نمط `Currency` بالحرف** (`CurrencyConfiguration.cs`) — دول حقائق موضوعية/قانونية، مش اختيار كل شركة لوحدها. أما `JobGrade`/`JobPosition`/`OrgUnit`/`EmployeeDocumentType` فضلوا **Company-Scoped** (`ICompanyScopedEntity`, فريد على `CompanyId+Code`) — الهيكل التنظيمي وسلم الدرجات بيختلف شركة عن التانية فعلًا.
>
> **قرار Hybrid على الشاشات (محسوم 2026-09-26)**: 8 من الـ 13 بياخدوا شاشة List/Edit كاملة (عمود "الشاشة" تحت)، والـ 5 الباقيين **Seed Only** — بيانات مزروعة من `HrCoreLookupSeedData.cs` عن طريق `SystemDataSeeder`، بدون أي `Screen.Code` في هذه المرحلة.

**Organization (4) — `src/Habbak.ERP.Domain/Organization/`، نظام-عام**:

| # | الكيان | الحقول الإضافية | العلاقات | الشاشة |
|---|---|---|---|---|
| 1 | `Country` | `IsoCode?` | — | `SETTINGS_COUNTRIES` (تحت `SETTINGS`، مش `HR` — زي `Companies`/`Currencies`) |
| 2 | `City` | — | `CountryId` → `Country` (إلزامي) | `SETTINGS_CITIES` |
| 3 | `Nationality` | — | `CountryId?` → `Country` (اختياري) | **Seed Only** — بدون شاشة |
| 4 | `Bank` | `SwiftCode?`، و`Address?` | `CountryId?` → `Country` | `SETTINGS_BANKS` |

**HR — أساسية، من `10-Module-HR-Payroll.md §2.1` الأصلي (4)، Company-Scoped — `src/Habbak.ERP.Domain/HR/`**:

| # | الكيان | الحقول الإضافية | العلاقات | الشاشة |
|---|---|---|---|---|
| 5 | `JobGrade` | `Level (int)`، و`MinSalary?`، و`MaxSalary?` | — | `HR_JOB_GRADES` |
| 6 | `JobPosition` | `OrgUnitId?`، و`DefaultJobGradeId?`، و`DefaultSalaryStructureId?` | → `OrgUnit`، → `JobGrade` | `HR_JOB_POSITIONS` |
| 7 | `OrgUnit` | `ParentId` (شجرة)، و`BranchId?`، و`ManagerEmployeeId?`، و`CostCenterDimensionValueId?` | Self-ref (فحص حلقة، أعلاه) | `HR_ORG_UNITS` |
| 8 | `EmployeeDocumentType` | `RequiresExpiry`، و`IsMandatory`، و`ExpiryAlertDays` | — | `HR_DOCUMENT_TYPES` |

**HR — إضافية، جديدة كليًا (5)، نظام-عام — `src/Habbak.ERP.Domain/HR/`**:

| # | الكيان | الحقول الإضافية | العلاقات | بيتستخدم في | الشاشة |
|---|---|---|---|---|---|
| 9 | `RelationshipType` | — | — | `EmployeePersonalData.EmergencyContactRelationshipTypeId` (B2) | **Seed Only** |
| 10 | `MilitaryStatus` | — | — | `EmployeePersonalData.MilitaryStatusId` (B2) | **Seed Only** |
| 11 | `QualificationType` | — | — | `EmployeePersonalData.QualificationTypeId` (B2) | **Seed Only** |
| 12 | `InsuranceOffice` | `OfficialCode?`، و`Address?` | — | `EmployeeSocialInsurance` (موديول الرواتب، **مرحلة لاحقة برّه هذه الخطة** — الكيان بيتبني دلوقتي كـ Lookup بس، الاستهلاك الفعلي لاحقًا) | `HR_INSURANCE_OFFICES` — استثناء Hybrid: نظام-عام لكن **باخد شاشة** (مكتب تأمينات حقيقي ممكن يتفتح جديد) |
| 13 | `TerminationReason` | — | — | `EmployeeEndOfService` (موديول الرواتب، **مرحلة لاحقة برّه هذه الخطة** — نفس الملاحظة) | **Seed Only** |

**ربط `Employee`/`EmployeePersonalData` بالـ Lookups الجديدة (B2)**: الحقول الخمسة (`NationalityId`، و`CityId`، و`BankId`، و`MilitaryStatusId`، و`QualificationTypeId`) **اتحطت على `EmployeePersonalData` مش `Employee`** — قرار تصميم (مش في الطلب الأصلي حرفيًا، لكن أنسب معماريًا): كل الحقول دي بيانات شخصية ديموغرافية/بنكية، بنفس فئة `BirthDate`/`Gender`/`MaritalStatus`/`Address` الموجودين فعلًا في `EmployeePersonalData`، عكس `Employee` اللي حقوله تشغيلية (فرع، ووظيفة، ومدير). `EmergencyContactRelationshipTypeId` اتضاف كمان لنفس السبب (بيوصف نوع صلة جهة الاتصال الطارئ، حقل موجود بالفعل في `EmployeePersonalData`). تفصيل الحقول الكامل في `10-Module-HR-Payroll.md §2.1` (اتحدّث).

### 1.1b إظهار مدقَّق PII (منقول من 0.2 — قرار محسوم 2026-09-26)

> **السبب في التأجيل**: الـ Endpoint مالوش معنى حقيقي قبل ما `Employee`/`EmployeePersonalData` يتوجدوا (1.1)، والاختبار عليه مستحيل من غير كيان حقيقي، وYAGNI — مفيش داعي نبني آلية عامة بدون مستهلك فعلي. الربط بـ 1.1 مباشرة أنضف: الـ Authorization والحقول المشفّرة (`NationalIdEncrypted`/`BankIbanEncrypted`) هيكونوا موجودين فعليًا وقت البناء، مش مفترضين.

| | |
|---|---|
| **الوصف** | `MaskFieldsAttribute` الحالي (`src/Habbak.ERP.API/Auth/MaskFieldsAttribute.cs:19-78`) بيعمل حاجة واحدة بس: أي حقل حساس المستخدم مالوش صلاحية `View` عليه بيتحوّل لـ `null` بالكامل في استجابة الـ API (سطر 63، `Mask()`) — ده مطابق لسياسة الإخفاء العام في `00-Project-Overview.md §6.1`، **مش** الإخفاء الجزئي المطلوب لـ PII (`••••1234`). الإخفاء الجزئي الفعلي **مش هيمر بالـ Attribute ده أصلًا**: `NationalIdLast4`/`BankIbanLast4` بيتخزنوا صراحة كأعمدة منفصلة (قسم 5.5 و2.1 من `10-Module-HR-Payroll.md`)، والـ DTO العادي بيرجّع الـ`Last4` بس ومايرجّعش `NationalIdEncrypted`/`NationalIdHash` خالص — يعني **دفاع مزدوج**: الحقل الحساس مش موجود في الاستجابة العادية من الأساس (مش بيتبعت ويتقفل بعدين). الإظهار الكامل مطلوب Endpoint منفصل تمامًا يفك التشفير عند الطلب بس (عن طريق `EncryptedStringConverter`/`PiiSecretProtector` من 0.3)، بصلاحية، وبتسجيل تدقيق إلزامي. |
| **الملفات المتأثرة** | جديد: `src/Habbak.ERP.API/Controllers/HR/HrPiiController.cs` (`POST /api/v1/hr/pii/reveal` — يستقبل `EntityType` + `EntityId` + `FieldName`، يتحقق من صلاحية `HR_REVEAL_PII`، يفك التشفير عن طريق الـ`ISecretProtector` الـ Keyed `"HR.PII"` (`PiiSecretProtector`، 0.3)، ويكتب `AuditLog` **إلزاميًا بغض النظر عن إعداد التدقيق العام للكيان** — قاعدة 5 في `10-Module-HR-Payroll.md`) · تعديل: `src/Habbak.ERP.Domain/Settings/Permissions/FieldPermission.cs` (تسجيل `HR_REVEAL_PII` في نفس نمط `ButtonPermissionCatalog` الموصوف في `Field-Button-Permissions.md`) · لا تعديل مطلوب على `MaskFieldsAttribute` نفسه — `NationalIdEncrypted`/`BankIbanEncrypted` أصلًا مش بترجع في الـ DTO العادي (بيرجع `Last4` بس)، فمفيش حاجة يحجبها الـ Attribute ده. |
| **Migration** | لأ. |
| **الاختبارات المطلوبة** | API: استدعاء `reveal` من غير صلاحية `HR_REVEAL_PII` = 403، ومفيش `AuditLog` بيتكتب · استدعاء ناجح على موظف حقيقي (من 1.1) = القيمة الكاملة بترجع (بعد فك التشفير) + سطر `AuditLog` واحد بالظبط بيتكتب فيه `EntityType`/`EntityId`/`FieldName`/`UserId` · استدعاء متكرر لنفس الحقل = سطر تدقيق جديد كل مرة (مفيش تجميع أو تخفيف) · فشل فك التشفير (مفتاح ضايع/بيانات تالفة) = خطأ واضح للمستخدم، مش 500 عام. |
| **التقدير** | 1.5 يوم |
| **الترتيب** | يتنفذ بعد ما `EmployeePersonalData` (1.1) يتبني، بالتوازي مع باقي شاشات 1.5 |

### 1.2 Migrations الحقول الحرة

> **3 أنماط مختلفة تمامًا**، مش نمط واحد متكرر — الفحص المباشر للكود كشف فروق جوهرية بين الأربعة. **`CustodyRegister.EmployeeId` قرارها محسوم 2026-09-26 (Option A)** — تفصيل كامل بعد الجدول.

| الحقل | الوضع الحالي | نمط المطابقة الممكن | الخطورة |
|---|---|---|---|
| `User.EmployeeId` (`User.cs:31`, `long?`) | عمود موجود، **مش مربوط بأي شيء فعليًا** حسب التعليق نفسه، مفيش Mapping في `UserConfiguration.cs` | مفيش اسم يتقارن بيه — الربط الصحيح الوحيد فعليًا هو العكسي: `Employee.UserId` (قاعدة 1)، و`User.EmployeeId` يفضل **للقراءة بس** ومهجور تدريجيًا | منخفضة (القيم الحالية على الأغلب فاضية أو بدون معنى حقيقي) |
| `CustodyRegister.EmployeeId` (`CustodyRegister.cs:11`, `long` **إلزامي**) | بيانات حقيقية موجودة دلوقتي، **مفيش أي حقل اسم مرافق** يتقارن بيه | **Option A المحسومة**: عمود جديد منفصل `EmployeeIdLinked` (`long?`)، **من غير ما نلمس العمود القديم خالص** — تفصيل تحت | **الأعلى في المرحلتين كلها** — راجع قسم 4 |
| `CustodyOfficer` (`CustodyOfficer.cs`, كامل الملف) | **مفيش عمود `EmployeeId` خالص دلوقتي** — كيان `ILookupEntity` مستقل (`Code`/`NameAr`/`NameEn`) | عمود جديد `EmployeeId?` + مطابقة بالاسم (`NameAr`/`NameEn` مقابل `Employee.NameAr`/`NameEn`) — نفس منطق التوثيق في المواصفة: "الاسم مش مضمون، فجدول مطابقة يدوي" | متوسطة (فيه اسم يتقارن بيه، بس التطابق مش مضمون 100%) |
| `MaintenanceRequest.TechnicianId` + `MaintenanceSchedule.TechnicianId` (`Maintenance.cs:68-69,139-140`, `long?` + `TechnicianName string?`) | عمودين موجودين ومعاهم اسم نصي مرافق فعلي | مطابقة بالاسم (`TechnicianName` مقابل `Employee.NameAr`/`NameEn`) — تطابق تام يترابط تلقائي، غموض/عدم تطابق يتعلّم للمراجعة | منخفضة-متوسطة |

**`CustodyRegister.EmployeeId` — Option A (محسوم 2026-09-26)**:

| الخطوة | التوقيت | التفصيل |
|---|---|---|
| Migration 1.2 (ضمن هذه الخطة) | فورًا | عمود جديد `CustodyRegister.EmployeeIdLinked` (`long?`, Nullable) — **لا يُلمس `EmployeeId` القديم إطلاقًا** ولا يتحط عليه أي FK دلوقتي. |
| شاشة العهد الحالية | فورًا | تفضل شغّالة زي ما هي بالظبط، من غير أي تعطيل — بس بإضافة Banner تحذيري: "**X سجل عهد محتاج ربط بموظف (HR)**" (X = عدد الصفوف اللي `EmployeeIdLinked` لسه `null` فيها). |
| `HR_CUSTODY_MIGRATION` (شاشة جديدة) | فورًا | تقرير تفاعلي (Screen.Code جديد) بيعرض كل صفوف `CustodyRegister` مع قيمة `EmployeeId` الخام القديمة، ويسمح للـ HR/المحاسب بالربط اليدوي بـ`Employee` حقيقي (Dropdown بحث)، وبيكتب في `EmployeeIdLinked`. **مفيش مطابقة تلقائية** (زي ما اتقرر — مفيش إشارة تكفي). |
| Migration تانية — Cleanup (**Pending، برّه نطاق هذه الخطة**) | متوقعة بعد **أسبوعين** من نهاية المرحلة 1 (بعد ما المراجعة اليدوية تكتمل) | نقل البيانات نهائيًا من `EmployeeId` لـ`EmployeeIdLinked` (أو العكس)، حذف العمود القديم، وتفعيل FK Enforced. **مش جزء من جدول/تقدير هذه الخطة** — تتجدول كمهمة منفصلة لما نسبة الربط تقارب 100%. |

| | |
|---|---|
| **الوصف** | لكل حقل من الأربعة: تقرير تشخيصي قبل أي FK حقيقي (قاعدة 50) — بيوضح كام صف "تطابق بيقين" (هيترّبط تلقائي، بينطبق على `CustodyOfficer` و`Maintenance.TechnicianId` بس)، وكام صف "غامض/من غير تطابق" (هيتعلّم لمراجعة يدوية)، **ومفيش صف بيتمسح تحت أي ظرف**. لـ`CustodyRegister` تحديدًا: **Option A** أعلاه — عمود جديد جنب القديم بدل تعديل/فرض FK على الموجود. |
| **الملفات المتأثرة** | جديد: `src/Habbak.ERP.Application/HR/Migration/FreeFieldDiagnosticReport.cs` (Query بيرجّع 3 مجموعات لـ`CustodyOfficer`/`MaintenanceRequest`/`MaintenanceSchedule`، وقائمة خام لـ`CustodyRegister` من غير تصنيف مطابقة) · جديد: `frontend/src/features/hr/custodyMigration/` (شاشة `HR_CUSTODY_MIGRATION` + الـ Banner على شاشة العهد الحالية) · تعديل: `src/Habbak.ERP.Domain/Accounting/CustodyRegister.cs` (إضافة `EmployeeIdLinked` — `EmployeeId` القديم **يفضل زي ما هو حرفيًا**) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Accounting/CustodyRegisterConfiguration.cs` (Mapping للعمود الجديد بس، Nullable، من غير FK Enforced) · `src/Habbak.ERP.Domain/Inventory/CustodyOfficer.cs` (إضافة `EmployeeId?`) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Inventory/CustodyOfficerConfiguration.cs` (FK جديد) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Settings/UserConfiguration.cs` (FK جديد لـ `EmployeeId`، اختياري) · `src/Habbak.ERP.Infrastructure/Persistence/Configurations/FixedAssets/FixedAssetsConfigurations.cs` (FK لـ `MaintenanceRequest.TechnicianId`/`MaintenanceSchedule.TechnicianId`). |
| **Migration** | نعم — بس **أقل خطورة بكتير بعد Option A**: `CustodyRegister` بتاخد عمود جديد إضافي بس (مفيش لمس للعمود القديم أو البيانات الموجودة)، فالمخاطرة الوحيدة الباقية على بيانات الإنتاج الحقيقية هي `CustodyOfficer`/`Maintenance` (إضافة عمود Nullable بس، منخفضة المخاطر). تتنفذ برضو بنفس أسلوب Remarks7: نسخة احتياطية كاملة → تجربة على `HabbakErp_Trial_HrFreeFieldMigration` → تطبيق على `HabbakErp` بـ`--connection` صريح. |
| **الاختبارات المطلوبة** | Integration: التقرير التشخيصي بيصنّف صفوف تجريبية صح لـ`CustodyOfficer`/`Maintenance` · اختبار **صريح**: بعد Migration 1.2، `CustodyRegister.EmployeeId` القديم **بقيمته الأصلية بالظبط** لكل صف (لا تعديل، لا حذف)، و`EmployeeIdLinked` كله `null` في أول نشر (لسه محدش ربط حاجة) · API: `HR_CUSTODY_MIGRATION` بيربط صف واحد صح، والـ Banner على شاشة العهد بيعرض العدد الصحيح المتبقي. |
| **التقدير** | 5 يوم (نفس التقدير — Option A قلّلت مخاطرة الـ Migration نفسها، لكن ضافت شاشة `HR_CUSTODY_MIGRATION` كاملة كتعويض في الجهد) |
| **الترتيب** | 6 (بالتوازي مع 1.3 — الاتنين محتاجين 1.1 بس) |

**الحالة الفعلية بعد التنفيذ (2026-09-27 — منفّذة بالكامل، تفصيل كامل في `Docs/Implementation/Phase-1.2-Final.md`)**: نفّذت بانحراف مقصود واحد عن الجدول أعلاه، بعد Research Pass (`Phase-1.2-Research.md`) أكّد إن **مفيش شاشة Frontend لـ `CustodyRegister` خالص حاليًا** (فجوة في موديول الحسابات نفسه، مسجّلة في `Known-Issues.md`) — فبدل شاشة `HR_CUSTODY_MIGRATION` التفاعلية الكاملة، اتبنى: (1) الـ Backend كامل (تقرير تشخيصي + Auto-match) في `FreeFieldDiagnosticReport.cs` زي المخطط بالظبط، مكشوف عن طريق `HrFreeFieldMigrationController` (يعيد استخدام صلاحية `HR_EMPLOYEES` الموجودة بدل تسجيل Screen Code جديد بلا شاشة تستخدمه)، و(2) `CustodyMigrationBanner.tsx` كـ Component Frontend معزول جاهز (TODO صريح للربط لما شاشة `CustodyRegister` تُبنى)، بدل الشاشة التفاعلية الكاملة. `UserConfiguration.cs` اتوثّق بـ XML Comment بس (بدون FK) بدل "FK جديد اختياري" — العمود Deprecated فمفيش داعي لفرض علاقة على حاجة بتتجاهل.

| | |
|---|---|
| **الوصف** | `CounterpartyType.Employee = 3` موجود فعلًا (`Enums.cs:68-74`) — مفيش تعديل Enum مطلوب. النقطة الوحيدة المعطّلة فعليًا: `CounterpartyAccountResolver.cs:47-50` بيرمي `BusinessRuleException("ACC-COUNTERPARTY-RESOLUTION-PENDING", ...)` لأي `CounterpartyType` غير `Customer`/`Supplier` — يعني أي سند بطرف `Employee` بيفشل حاليًا برسالة صريحة بتقول "محتاج ربط الحساب الفرعي من HR". **قرار تصميم**: خلافًا لـ`Customer.ReceivableAccountId`/`Supplier.PayableAccountId` (حساب مستقل لكل عميل/مورد)، `Employee` **مالوش حساب فرعي خاص بيه** — الحل المعتمد في `10-Module-HR-Payroll.md §6.2` هو حساب تحكم واحد على مستوى الشركة (`CompanyAccountRole.EmployeeReceivable`، موجود فعلًا) لكل الموظفين، والتفصيل لكل موظف بيطلع من سطور المعاملة نفسها (`EmployeeAdvance`/`PayrollLine` لاحقًا) مش من حساب GL منفصل. ده **مش نفس نمط Customer/Supplier**، فرق متعمَّد لازم يتوثق (زي ما `Posting-Engine-Implementation-Plan.md` بيوثّق قرارات مشابهة). |
| **الملفات المتأثرة** | تعديل: `src/Habbak.ERP.Infrastructure/Services/CounterpartyAccountResolver.cs` (إضافة فرع `CounterpartyType.Employee` سطر ~46، بيرجّع `CompanyAccountMapping` حيث `Role == CompanyAccountRole.EmployeeReceivable` للشركة الحالية، بدل الرمي المباشر) · لو الحساب مش متسجّل في `CompanyAccountMapping` بعد (لسه المدير المالي ما ربطهوش)، الرسالة تتغيّر لخطأ واضح مشابه (`ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED` مثلًا) بدل `ACC-COUNTERPARTY-RESOLUTION-PENDING` العام. |
| **Migration** | لأ (الحساب نفسه بيتسجّل عن طريق شاشة `CompanyAccountMapping` الموجودة فعلًا، مش عمود جديد). |
| **الاختبارات المطلوبة** | Integration: سند بطرف `CounterpartyType.Employee` وموظف حقيقي (من 1.1) + `CompanyAccountMapping` فيه `EmployeeReceivable` مربوط = القيد بيترحّل صح عن طريق `IPostingService.PostAsync` (بدون تعديل على الواجهة نفسها) · اختبار سلبي: نفس السيناريو بدون `EmployeeReceivable` مربوط = خطأ واضح (مش استثناء عام). |
| **التقدير** | 2 يوم |
| **الترتيب** | 6 (بالتوازي مع 1.2) |

**الحالة الفعلية بعد التنفيذ (2026-09-27 — منفّذة بالكامل، تفصيل كامل في `Docs/Implementation/Phase-1.3-Final.md`)**: نفّذت مطابقة للخطة 100% بدون أي انحراف — الـ Research Pass (`Phase-1.3-Research.md`) أكّد إن الكود جاهز بالكامل (Enum + Validation) ومفيش غير فرع واحد ناقص في `CounterpartyAccountResolver.cs`. اتضاف فرع `CounterpartyType.Employee` (يرجّع `CompanyAccountMapping` حيث `Role == EmployeeReceivable`، أو `ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED` لو مش مربوط) بنفس نمط `Supplier`/`Customer` بالحرف — صفر تعديل على `PostVoucherCommand.cs` أو أي واجهة تانية. اختبارين Integration جداد (`EmployeeCounterpartyPostingTests.cs`) بيغطوا المسارين. **ملاحظة**: طلب اختبار "Audit Log بيتكتب صح" اتفحص ولوحظ إن `PostVoucherCommand.cs` مفيهوش أي كتابة لـ`AuditLog` أصلًا (لا لـ Employee ولا لأي نوع طرف تاني) — مش سلوك موجود يتاخد كأساس، فاتسجّل كملاحظة مفتوحة برّه نطاق 1.3 بدل ما يتخترع سلوك جديد من غير Research مخصّص.

### 1.4 إنهاء خدمة إداري بسيط

| | |
|---|---|
| **الوصف** | **مبسّط عمدًا** — الحساب المالي الكامل (بدل الإجازات، والتعويض، والتسوية) في المرحلة 5 من الموديول (برّه نطاق هذه الخطة تمامًا). المطلوب هنا بس: `Employee.Status → Terminated` بيعمل 3 حاجات في نفس الـ Transaction (قاعدة 44، نطاق مبسّط بدون سلسلة اعتماد لأن محرك الموافقات لسه مش موجود): (1) إيقاف `User` المرتبط لو موجود، (2) إلغاء كل `UserScope` بتاعته في الشركة دي، (3) تنبيه/منع لو فيه عهدة مفتوحة. **نقطة كويسة اتلقيت في الكود**: إيقاف مستخدم موجود فعلًا وبيسحب الجلسات تلقائيًا — `UpdateUserCommandHandler` (`UserCommands.cs:228-231`) لما الحالة الجديدة `Suspended` بينادي `UserRules.RevokeSessionsAsync(db, user.Id, "UserSuspended", ct)` — دي بالظبط النقطة اللي إنهاء الخدمة هيستدعيها، مش هيعيد بناءها. **نقطة مش موجودة خالص**: مفيش أي فحص "عهدة مفتوحة قبل حذف/تعطيل" في أي مكان في الكود (`DeleteCustodyOfficerCommand.cs:14-21` بيمسح مباشرة من غير أي تحقق) — الفحص ده جديد بالكامل. |
| **الملفات المتأثرة** | جديد: `src/Habbak.ERP.Application/HR/Employees/Commands/TerminateEmployee/TerminateEmployeeCommand.cs` — بيستدعي منطق تعطيل مشابه لـ`UpdateUserCommandHandler` (قد يُستخرَج جزء مشترك كـ Helper بدل تكرار الكود) + `SetUserScopesCommand`-style تعطيل للـ Scopes (لاحظ: `SetUserScopesCommand` الحالي Full-Replace، مش Deactivate مستهدف — إما إعادة استخدامه بقايمة فاضية، أو دالة `DeactivateScopesForEmployeeTermination` أبسط) + فحص جديد: `CustodyRegister` حيث `EmployeeId == employee.Id` و`Status == CustodyStatus.Open` (وبعد ما 1.2 يخلص: `CustodyOfficer.EmployeeId` → `FixedAsset.CustodyOfficerId`، قاعدة 43). |
| **Migration** | لأ. |
| **الاختبارات المطلوبة** | Integration: إنهاء خدمة موظف بمستخدم مرتبط = `User.Status = Suspended` + الجلسات النشطة اتلغت + كل `UserScope` بتاعته في الشركة دي `IsActive = false` (كله في نفس الـ Transaction، فشل أي خطوة = تراجع الكل) · اختبار عهدة مفتوحة: موظف عليه `CustodyRegister` بحالة `Open` = الإنهاء **يُرفض** برسالة واضحة (`HR-EMPLOYEE-OPEN-CUSTODY`) لحد ما يتسوّى — أو يعدّي بتأكيد صريح (`confirm=true`) حسب القرار في قسم 8 · موظف من غير مستخدم مرتبط (`UserId = null`) = الإنهاء يكمل عادي من غير أي تعطيل مستخدم. |
| **التقدير** | 2 يوم |
| **الترتيب** | 7 (محتاج 1.1 كامل، ويستفيد لو 1.2 خلصت الأول عشان فحص العهدة يبقى شامل `CustodyOfficer` كمان مش `CustodyRegister` بس) |

**الحالة الفعلية بعد التنفيذ (2026-09-27 — منفّذة بالكامل ضمن نطاقها المبسّط، تفصيل كامل في `Docs/Implementation/Phase-1.4-Final.md`)**: `TerminateEmployeeCommand` كان موجود بالفعل من Batch B5 (مش جديد كما افترضت الخطة الأصلية) وبيعمل كل حاجة ماعدا فحص عهدة الأصول — بما فيها سحب الجلسات النشطة (اتأكد إنه شغّال فعليًا، عكس ما كان موثّق في `Known-Issues.md`). Phase 1.4 وسّعت الفحص الموجود (نفس كود الخطأ `HR-EMPLOYEE-OPEN-CUSTODY`) ليشمل `CustodyOfficer.EmployeeId` → `FixedAsset.CustodyOfficerId` (قاعدة 43)، مستثنية أصول `Draft`/`Disposed`/`WrittenOff` (مفيش مسؤولية حقيقية باقية عليها). الرسالة بتسمّي بالتحديد العهدة النقدية (`CustodyRegister #`) و/أو أرقام الأصول (`AssetNumber`) المعلّقة، أول 5 كحد أقصى. **مفيش `confirm=true` تجاوز** — نفس القرار المحسوم أصلًا لعهدة الـ`CustodyRegister` (قسم 8، سؤال 4).

### 1.5 شاشات HR Core

> **محدّث 2026-09-26 — قرار Hybrid على الـ 13 Lookup الجديدة** (تفصيل كامل في 1.1): 8 منهم بياخدوا شاشة List/Edit كاملة (منهم 5 بالفعل جزء من قائمة الشاشات هنا، و3 جداد تحت Settings — Countries/Cities/Banks — راجع 1.1.4)، والـ 5 التانيين (`Nationality`، و`RelationshipType`، و`MilitaryStatus`، و`QualificationType`، و`TerminationReason`) **Seed Only بدون شاشة في هذه المرحلة**. القائمة تحت لسه 7 شاشات HR الأصلية (زي ما كانت) — الجداد (`Countries`/`Cities`/`Banks`/`HR_INSURANCE_OFFICES`) موثّقين في 1.1 مش هنا لأنهم مسجّلين في `MenuItemSeedData.cs` بالفعل من B1 كشاشات مستقلة عن الـ 7 دول.

| الشاشة | `Screen.Code` | القالب المرجعي في الكود | ملاحظة |
|---|---|---|---|
| الموظفين | `HR_EMPLOYEES` | `frontend/src/features/purchasing/suppliers/` (List/Edit + تبويبات) | تبويب "شخصية" بيعرض `Last4` بس + زرار "إظهار" (1.1b) |
| الهيكل التنظيمي | `HR_ORG_UNITS` | `frontend/src/features/accounting/chartOfAccounts/ChartOfAccountsPage.tsx` — نمط شجرة كامل وجاهز لإعادة الاستخدام (`useAccountTree`، تجميع بـ`parentId` في `Map`، توسيع/طي بـ`Set`، إنشاء ابن Inline) | أقرب نمط موجود لشجرة `OrgUnit.ParentId` — نفس الهيكل بالحرف. **الكيان + الـ Migration جاهزين من B1** |
| الوظائف / الدرجات | `HR_JOB_POSITIONS` / `HR_JOB_GRADES` | أي `Lookup` بسيط موجود (`AssetCategoryPages.tsx` مثلًا) | شاشتين منفصلتين بسيطتين، أقل الشاشات مخاطرة. **الكيانات + الـ Migration جاهزين من B1** |
| أنواع المستندات | `HR_DOCUMENT_TYPES` | نفس نمط Lookup | **الكيان + الـ Migration جاهزين من B1** |
| مكاتب التأمينات | `HR_INSURANCE_OFFICES` | نفس نمط Lookup | **جديد 2026-09-26** (Hybrid) — **الكيان + الـ Migration جاهزين من B1** |
| التعيين | `HR_HIRING` | **مفيش قالب Wizard موجود في المشروع خالص** (اتفحص بالبحث الشامل، صفر نتائج حقيقية غير Positives زائفة) | **أول شاشة Wizard في المشروع كله** — مخاطرة تصميم حقيقية، محتاجة Design Spike قصير قبل البناء الكامل (قسم 4) |
| إعدادات HR | `HR_SETTINGS` | `frontend/src/features/purchasing/settings/PurchaseCycleSettingsPage.tsx` (+`api.ts`/`types.ts` المجاورين) — نمط `DEFAULTS` + `DOCUMENT_FIELDS`/`CHECKBOX_FIELDS` معلنة كمصفوفات Typed | **محسوم 2026-09-26** — تفصيل الحقول تحت |

**الشاشات الثلاثة الجداد تحت `SETTINGS` مش `HR`** (`SETTINGS_COUNTRIES`/`SETTINGS_CITIES`/`SETTINGS_BANKS`) — قرار تنفيذي وقت B1: نفس المكان اللي `Companies`/`Currencies` (كيانات Organization برضو) مسجلين فيه بالفعل، مش مجموعة `HR` ولا مجموعة "تنظيم" جديدة. **الـ 5 Seed Only مفيهمش أي `Screen.Code` أو شاشة خالص في هذه المرحلة** — بيانات مزروعة بس عن طريق `SystemDataSeeder`/`HrCoreLookupSeedData.cs`.

**محتوى `HrSettings` — محدّث 2026-09-27 (`Phase-1.5-Research.md`)**: ⚠️ الكيان **لسه مش موجود خالص** في الكود وقت كتابة هذا القسم — الجملة الأصلية هنا كانت بتفترض إنه "مبني من الأول من Phase 1.1"، والفحص الفعلي أكّد إن ده غلط (صفر Migration، صفر Controller). بُني فعليًا في **Sub-Batch 1.5.0** (`HR-MASTER-PLAN.md §Phase 1.5`)، بنفس فكرة `PurchaseCycleSettings`/`BranchPOSSettings` — صف واحد لكل شركة، يتوسّع بعدين من غير Migration جديدة لكل حقل. شاشة `HR_SETTINGS` **بتعرض وتعدّل حقول المرحلة 1 بس** — باقي الحقول موجودة في الكيان (Nullable/بقيمة افتراضية) لحد ما مرحلتها تفتحها في الشاشة. **`EmployeeCodePrefix`/`EmployeeCodeSequence` اتشالوا نهائيًا** (كانوا في الجدول الأصلي هنا) — آلية `ICodeGenerator`/`CodingRule` العامة الموجودة بالفعل بتغطي نفس الوظيفة تحت `Screen.Code = HR_EMPLOYEES`، وحقلين مخصصين لنفس الغرض تكرار مصدر حقيقية غير مطلوب:

| الحقل | الاستخدام | الحالة |
|---|---|---|
| `DefaultProbationDays` | مدة فترة الاختبار الافتراضية | **مرحلة 1 — معروض** |
| `DefaultBranchId` | الفرع الافتراضي لموظف جديد | **مرحلة 1 — معروض** |
| `RequireNationalIdForActivation` | هل الرقم القومي إلزامي قبل تفعيل الموظف؟ | **مرحلة 1 — معروض** |
| `MonthBasis` | أساس حساب الشهر (أيام فعلية/30) | مؤجل — مرحلة الرواتب |
| `DefaultCutoffDay` | يوم قفل الفترة الشهرية (`10-Module-HR-Payroll.md:130`، لم يكن مسجّلًا في هذا الجدول أصلًا) | مؤجل — مرحلة الرواتب |
| `KioskSessionSeconds` | مهلة جلسة الكشك | مؤجل — مرحلة الخدمة الذاتية |
| `SelfServiceClockInRequiresLocation` | تسجيل حضور بالموقع | مؤجل — مرحلة الخدمة الذاتية |
| `CompanyDefaultApproverUserId` | المعتمد الاحتياطي | مؤجل — مرحلة محرك الاعتمادات |
| `MaxAdvanceInstallmentPercent` | حد قسط السلفة | مؤجل — مرحلة السلف |

| | |
|---|---|
| **الملفات المتأثرة** | `frontend/src/features/hr/{employees,orgUnits,jobPositions,jobGrades,documentTypes,hiring,settings}/` — كل مجلد بنفس نمط `purchasing/settings/` (`api.ts` + `types.ts` + `*Page.tsx`)، عدا `orgUnits` اللي بيتبني فوق نمط `ChartOfAccountsPage.tsx` مباشرة. |
| **Migration** | لأ (كل الكيانات المطلوبة اتبنت في 1.1). |
| **الاختبارات المطلوبة** | API: كل Endpoint جديد محكوم بـ`[Screen(...)]` صح (الاختبار الموجود اللي بيرفض أي Controller من غير `[Screen]` هيغطي دول تلقائيًا لو الالتزام بالنمط صح) · صلاحيات الحقول على `HR_EMPLOYEES` (تبويب شخصية مخفي لمين مالوش صلاحية) · Manual: التعيين كاملًا من الـ Wizard لحد ما الموظف يبقى `Draft` (مش `Active` — الشرط الكامل للتفعيل مؤجل جزئيًا، قسم 8) · شجرة `HR_ORG_UNITS` توسيع/طي وإنشاء ابن. |
| **التقدير** | 10-12 يوم (الوزن الأكبر على `HR_HIRING` كأول Wizard) |
| **الترتيب** | 8 — لكن الشاشات البسيطة (`HR_JOB_POSITIONS`/`HR_JOB_GRADES`/`HR_DOCUMENT_TYPES`) ممكن تتبني بالتوازي بمجرد ما الـ API الخاص بيها في 1.1 يخلص، من غير ما تستنى باقي المرحلة 1 (فرصة توازي حقيقية، قسم 3) |

---

## 3. Dependencies & Order

```
0.6 Spike (نص يوم) ──┬── نجح ──► 0.6 Vault كامل (2-3 يوم) ──────────────┐
                     └── فشل ──► 0.6 Env Var مؤقت (فوري) ──► 0.3 يبدأ فورًا  │
                                      │                                    │
                                      └──► Vault الكامل (بالتوازي مع 0.1/0.5/0.4) ──┤
                                                                                     │
0.1 نطاق Self/Team ────────────────────────────────────────────────────────────────┤
0.5 تعقيم Logging ─────────────────────────────────────────────────────────────────┤
0.4 اختبار FieldPermissionCatalog ─────────────────────────────────────────────────┤
0.2 إظهار مدقَّق (endpoint عام) ────────────────────────────────────────────────────┤
                                                                                     ▼
                                                            ⛔ GATE: Vault لازم يكون شغّال فعليًا هنا ⛔
                                                                                     │
                                                                                     ▼
                                                                    1.1 Core HR entities ──┬──► 1.2 Migrations الحقول الحرة ──┐
                                                                                            │                                  ├──► 1.4 إنهاء خدمة إداري بسيط
                                                                                            ├──► 1.3 الموظف كطرف في السندات ───┘
                                                                                            └──► 1.5 شاشات HR Core (البسيطة منها توازي مع 1.2/1.3/1.4؛ HR_EMPLOYEES/HR_ORG_UNITS/HR_HIRING بعد ما 1.1 يكتمل بالكامل)
```

**Critical Path**: `0.6 Spike → [Vault أو Env Var] → GATE (Vault شغّال) → 1.1 → 1.2 → 1.4` (أو `1.1 → 1.5` لو الشاشات الثقيلة هي عنق الزجاجة الفعلي — يتحدد بعدد المطورين المتاحين). **الـ GATE ده مختلف عن باقي الاعتماديات** — لو Vault اتأخر، 1.1 بتستنى بغض النظر عن اكتمال 0.1/0.2/0.4/0.5 (اللي ممكن تكون خلصت من زمان)، والقرار وقتها بيطلع للإدارة زي ما اتفق في قسم 8 سؤال 1.

**فرص التوازي الحقيقية**:
- `0.1`، و`0.5`، و`0.4` تلاتتهم مستقلين تمامًا عن `0.6`/`0.3` — مطور تاني يقدر يشتغل عليهم من اليوم الأول.
- `1.2` و`1.3` الاتنين محتاجين `1.1` بس، ومحتاجين مطورين مختلفين (الأول Migration/بيانات، التاني محاسبة) — يشتغلوا بالتوازي.
- شاشات `HR_JOB_POSITIONS`/`HR_JOB_GRADES`/`HR_DOCUMENT_TYPES` (فرونت) ما تحتاجش تستنى `1.2`/`1.3`/`1.4` — تبدأ بمجرد ما الـ API بتاعها في `1.1` يخلص.

**تبعية عابرة للمرحلتين (مهمة)**: الـ`EmployeeId` claim في الـ JWT (0.1) بيتبني كـ"مكان فاضي" في المرحلة 0، ومابيتلمش فعليًا إلا بعد ما `Employee.UserId` يتوجد في 1.1 — يعني `SessionIssuer.IssueAsync` (`SessionIssuer.cs:80`) محتاج تعديل تاني صغير في 1.1 (مش بس 0.1) عشان يجيب `EmployeeId` من `Employee` table الفعلي. ده مذكور في خانة "الملفات المتأثرة" بتاعة 0.1، بس التنفيذ الفعلي بيتأجل لحد 1.1.

---

## 4. Risks & Mitigation

| # | الخطر | الأثر | التخفيف |
|---|---|---|---|
| 1 | **Vault (0.6) أول مرة الفريق بيشغّلها** — بنية تحتية جديدة كليًا، مش تعديل كود | لو Vault اتأخر أكتر من مدة باقي بنود المرحلة 0، **المرحلة 1 كلها بتقف** (Gate صارم، قسم 3) — ده أخطر من مجرد تأخير 0.3 | **محسوم**: Spike نص يوم أولًا؛ فشل → Env Var مؤقت (بشرط Backup فوري) يفك القفل عن 0.3 بس مش عن 1.1؛ Vault يتبنى بالتوازي مع باقي المرحلة 0. لو لسه مش جاهز عند نهاية المرحلة 0 → توقّف والقرار يطلع للإدارة (مش قرار هندسي منفرد) |
| 2 | **`CustodyRegister.EmployeeId` بدون أي إشارة مطابقة** (لا اسم ولا مرجع) | التقرير التشخيصي على الأرجح هيسيب **كل الصفوف الحالية** "محتاجة مراجعة يدوية" — مش نجاح جزئي، فشل شبه كامل للمطابقة التلقائية | **محسوم (Option A)**: عمود جديد `EmployeeIdLinked` جنب القديم من غير أي لمس له، شاشة العهد تفضل شغّالة، وربط يدوي عن طريق `HR_CUSTODY_MIGRATION` — التنظيف النهائي (حذف العمود القديم + FK) مؤجّل كمهمة منفصلة بعد أسبوعين من نهاية المرحلة 1 |
| 3 | **مفيش نمط Wizard في المشروع خالص** | `HR_HIRING` أول شاشة من نوعها — ممكن تاخد وقت أكتر من المقدّر، أو تأسس نمط غير متسق لباقي النظام | Design Spike قصير (نص يوم) قبل البناء الكامل؛ أبسط تصميم ممكن (State محلي + مصفوفة خطوات)، من غير مكتبة خارجية جديدة |
| 4 | **`FieldPermissionCatalog` قاموس ثابت في الكود، مش جدول** | أي حقل PII جديد مستقبلًا (مش HR بس) محتاج تعديل كود + Deploy، مش شاشة إعداد | مقبول للنطاق الحالي (نفس النمط المتبع في باقي المشروع) — يتسجل كقيد معماري معروف، مش يتحل دلوقتي |
| 5 | **إدخال Serilog من الصفر (0.5)** | ممكن يجرّ مراجعة شاملة لكل الـ Logging الموجود، وده مش مطلوب | النطاق يقفل صراحة على: تسجيل Serilog + Policy واحدة لـ PII بس — مراجعة كل سطر Log موجود **برّه النطاق** توثيقًا |
| 6 | **محرك الموافقات مش موجود** (`NullApprovalWorkflowService`) | كيانات المرحلة 1 (`Employee`, `EmploymentContract`...) مالهاش سلسلة اعتماد حقيقية دلوقتي — التعيين هيبقى حفظ مباشر (`Draft`) من غير الخطوات الموصوفة في `10-Module-HR-Payroll.md §4.1` | يتوضّح صراحة للعميل إن "اعتماد التعيين" مش مفعّل في المرحلة دي — بيتفعّل مع محرك الموافقات (مرحلة تانية، برّه هذه الخطة) |
| 7 | **بيانات إنتاج حقيقية على `CustodyRegister`** | أي خطأ في Migration 1.2 بيأثر على بيانات عميل حقيقية | نسخة احتياطية كاملة + تجربة على قاعدة Trial أولًا (قسم 5) — إلزامي، مش اختياري |
| 8 | **تسجيل الـ ScreenCode الجديدة** | ✅ **محسوم بعد Research Pass**: الآلية اتأكدت — `MenuItemSeedData.cs` (`Infrastructure/Persistence/Seeding/`) ملف Seed يدوي ثابت (`Leaf(group, "CODE", ...)`)، مش Reflection ولا DB Table | تُضاف مجموعة `HR` + الـ 7 شاشات يدويًا في B1 (مع الـ Migration، مش آخر المرحلة) |
| 9 | **فحص حلقة `OrgUnit.ParentId` بدون سابقة حقيقية في الكود** | `Account` مايسمحش بتغيير الأب أصلًا (مفيش فحص محتاج)، و`ItemGroup` عنده فحص سطحي بس ("الأب = نفسي") — مفيش Walk عميق موجود للنسخ منه، فالكود والاختبارات (5 حالات: مباشر، غير مباشر، عمق 2، عمق 3، Self-loop) هيتبنوا من الصفر | `HashSet<long>` Walk للأعلى، مُختبَر بكل الحالات الخمسة صراحة (تفصيل في 1.1) |
| 10 | **`MenuItemSeedData.cs` ملف واحد كبير مشترك بين كل الموديولات** | Merge Conflict مستقبلي لو أكتر من مطور بيضيف شاشات مختلفة بنفس الوقت | **مقبول دلوقتي** (نفس النمط المتبع لكل الموديولات التانية) — إضافة الشاشات السبعة كـ Batch واحد في B1 يقلل الاحتكاك؛ **توصية مستقبلية** (مش الآن): لو الملف كبر كتير، يتقسّم لملفات لكل موديول (`MenuItemSeedData.HR.cs`، إلخ) بدل ملف واحد ضخم |

---

## 5. Rollout Plan

**Dev**: على `HabbakErp` (قاعدة التطوير، فيها بيانات عميل حقيقية بالفعل — `Docs/E2E-Cycle-Execution-Log.md:4`). كل Migration بتتبع نفس تسلسل Remarks7: نسخة احتياطية → `HabbakErp_Trial_<Name>` (`dotnet ef database update --connection "..."`) → تحقق → مسح القاعدة التجريبية → تطبيق حقيقي على `HabbakErp`.

**UAT**: بيانات **صناعية بالكامل** لموظفين وهميين — **ممنوع** نسخ بيانات موظفين حقيقيين (أرقام قومية، إلخ) لبيئة UAT قبل ما التشفير (0.3) والتدقيق (0.2) يتفحصوا فعليًا فيها، حتى لو UAT بيئة "داخلية". هذا القرار غير قابل للتفاوض نظرًا لحساسية البيانات.

**Production**: نافذة صيانة معلنة، نسخة احتياطية كاملة (قاعدة + مفاتيح Vault/DataProtection معًا — قسم 24 من الـ Overview)، تطبيق الـ Migrations بالترتيب (1.1 قبل 1.2 دايمًا — 1.2 محتاجة `Employee` موجود)، اختبار دخان (Smoke Test) عاجل: تسجيل دخول عادي لمستخدم موجود (يتأكد إن الـ`EmployeeId` claim الجديد ما بيكسرش تسجيل الدخول لمستخدمين من غير موظف مربوط)، ثم فتح شاشة HR واحدة بسيطة للتأكد.

**Backup Strategy**: يتوسّع نطاق النسخ الاحتياطي الحالي (قسم 24 من الـ Overview) ليشمل صراحة مفاتيح Vault/Unseal Keys (0.6) — بدونها، فقدان السيرفر = فقدان كل الأرقام القومية والحسابات البنكية المشفّرة نهائيًا (نفس التحذير الحرفي في `10-Module-HR-Payroll.md` قاعدة 8).

**Rollback Plan**: كل Migrations المرحلة 0/1 **إضافية بالكامل** (جداول/أعمدة جديدة) عدا 1.2 اللي بتلمس جداول موجودة — لهذا السبب 1.2 لازم تتصمم كخطوتين منفصلتين قابلتين للتراجع كل واحدة لوحدها: (أ) إضافة عمود FK كـNullable/Unenforced أولًا، (ب) تفعيل الـ Constraint بصرامة في Migration منفصلة لاحقة بعد التأكد من نظافة البيانات. التراجع عن (أ) أو (ب) بمفردهم أرخص وأأمن من استرجاع نسخة احتياطية كاملة. **ممنوع أي حذف أو تعديل على بيانات موجودة** في أي من الـ Migrations دي (قاعدة 50).

---

## 6. Test Plan

### Unit Tests
- `EncryptedStringConverter` Round-trip (0.3) · `HmacPiiHasher` Deterministic + تصادم منخفض (0.3) · `PiiDestructuringPolicy` بتستبعد `[PiiField]` من الـ Log (0.5) · `Employee.ManagerId`/`OrgUnit.ParentId` كشف الحلقات (1.1، منطق Domain بمعزل عن قاعدة البيانات).

### Integration Tests (`Habbak.ERP.IntegrationTests`, نمط `PostingServiceFixture.cs` — LocalDB حقيقي، مش In-Memory، لأن الفهارس الفريدة المفلترة و`RowVersion` محتاجين محرك SQL Server فعلي)
- فلترة `IEmployeeScopedEntity` (Self scope) — كيان تجريبي (0.1).
- فريد `(CompanyId, UserId)` على `Employee.UserId`، وفريد `(CompanyId, NationalIdHash)` — محاولتين متزامنتين (1.1، نمط `DepreciationRunConfiguration` الفهرس المفلتر، `FixedAssetsConfigurations.cs:105`).
- `NationalIdEncrypted` مخزّن كنص غير مقروء فعليًا في العمود (فحص SQL خام داخل الاختبار، مش بس عن طريق الـ Repository).
- التقرير التشخيصي (1.2) بيصنّف بيانات تجريبية صح (تطابق أكيد/غامض/بدون تطابق) لكل الأربعة حقول، وعدد الصفوف قبل وبعد الـ Migration **متطابق تمامًا**.
- `CounterpartyAccountResolver` مع `CounterpartyType.Employee` (1.3) — نجاح مع Mapping موجود، فشل واضح من غيره.
- `TerminateEmployeeCommand` (1.4) — تعطيل المستخدم + سحب الجلسات + إلغاء الـ Scopes في نفس الـ Transaction؛ الرفض عند وجود عهدة مفتوحة.

### API Tests (`Habbak.ERP.ApiTests`, نمط `AccountingApiFactory.cs` — `WebApplicationFactory<Program>` + LocalDB خاص + `TestAuthHandler`)
- `POST /api/v1/hr/pii/reveal`: 403 من غير `HR_REVEAL_PII`، سطر `AuditLog` واحد بالظبط عند النجاح (0.2) — يتبنى بجانب `FieldButtonPermissionsTests.cs`/`SecurityTests.cs` الموجودين فعلًا كمرجع نمط.
- كل Endpoint جديد تحت `[Screen("HR_...")]` صح، وصلاحيات الحقول على `HR_EMPLOYEES` (تبويب شخصية) بتتحجب صح لمين مالوش صلاحية.
- JWT الصادر بعد تسجيل الدخول فيه `EmployeeId` claim (فاضي أو معبّى حسب وجود ربط).

### Manual Tests
- رحلة `HR_HIRING` كاملة من البداية للنهاية (أول Wizard في النظام — لازم يتجرب يدوي بعناية).
- شجرة `HR_ORG_UNITS`: توسيع/طي، إنشاء ابن Inline.
- زرار "إظهار" الرقم القومي: يظهر كامل، ويترصد في `AuditLog` فورًا (يتفحص في قاعدة البيانات مباشرة بعد الضغط).
- تسجيل دخول مستخدم قديم (من غير موظف مربوط) بعد نشر كل التغييرات — يتأكد إنه ما تأثرش.
- إنهاء خدمة موظف مربوط بمستخدم نشط فعليًا (من متصفح تاني) — الجلسة بتتقفل فورًا.

---

## 7. Timeline

### Gantt نصي (يوم عمل واحد = عمود؛ مطور واحد بدوام كامل ما لم يُذكر غير ذلك؛ **السيناريو الأسوأ: Vault Spike فشل يوم 1**)

```
اليوم:        1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19  20
0.6 Spike    [█]
0.6 Env Var    [█]
0.6 Vault الكامل   [██████████████]  ← بالتوازي، لحد ما يشتغل فعليًا
0.1 Scope        [████]
0.5 Serilog      [████]
0.4 Catalog Test         [██]
0.3 Encrypt (Env Var)  [████]
0.2 Reveal               [████]
                                └─ باقي بنود 0 خلصت (~11 يوم) ─┘
                                                       ⛔ GATE: مستني Vault يشتغل فعليًا ⛔
1.1 Core HR                                                  [████████████████████]
                                                                                    └─ نهاية 1.1 (يوم ~35، لو Vault خلص في الوقت) ─┘
```

> لو الـ Spike يوم 1 **نجح** (السيناريو المتفائل)، `0.6 Vault الكامل` بتاخد 2-3 يوم بس (مش موازية طول المدة)، والـ Gate بيتحل بدري، وباقي الجدول بيرجع لنفس التسلسل الأصلي (نهاية 1.1 ~يوم 31 زي المخطط الأول).
اليوم:       21  22  23  24  25  26  27  28  29  30  31  32  33  34  35  36  37  38  39  40  41  42  43
1.2 Free-fields                                          [██████████]
1.3 Counterparty                                         [████]
1.4 Termination                                                      [████]
1.5 Screens (بسيطة)                                       [████████████████]
1.5 Screens (Employees/OrgUnits/Hiring)                              [██████████████████████]
                                                                                              └─ نهاية المرحلة 1 (يوم ~64) ─┘
```

> ملحوظة: الجدول أعلاه بمطور واحد بدوام كامل، تسلسلي بالكامل تقريبًا — **بمطورين اتنين** (Backend + Frontend بالتوازي من يوم 1.1) الإجمالي بينزل لحوالي **40-45 يوم تقويمي** بدل 64، مطابق لتقدير `10-Module-HR-Payroll.md` (~32-47 يوم) بافتراض التوازي الموضّح في قسم 3.

### Milestones

| المعلم | بعد يوم عمل رقم | الشرط |
|---|---|---|
| M0 — أرضية PII جاهزة | ~11 | Vault شغّال، التشفير/HMAC معمول لهما اختبار Round-trip، `DataScope` enum موجود |
| M1 — Core HR منشور | ~31 | 9 كيانات + APIs أساسية، Migration مطبّقة على Dev |
| M2 — تكامل مالي/إداري | ~40 | Migrations الحقول الحرة + الموظف كطرف في السندات + إنهاء خدمة بسيط، كلهم بـ Integration Tests خضراء |
| M3 — جاهز لـ UAT | ~64 (أو ~40-45 بمطورين اتنين) | كل الشاشات السبعة شغّالة، Test Plan (قسم 6) كامل، بيانات صناعية جاهزة لـ UAT |

---

## 8. Open Questions

| # | السؤال | الحالة | القرار |
|---|---|---|---|
| 1 | **مفاتيح التشفير (0.6)**: HashiCorp Vault كامل ولا حل انتقالي أبسط؟ | ✅ **تم الحسم (2026-09-26)** | Spike يوم واحد (نص يوم فعليًا) لتنصيب Vault على Dev. نجح → Vault كامل عادي. فشل → Env Var محمي مؤقت (بشرط Backup من نفس اليوم) لـ0.3 تكمل عليه فورًا، وVault يتبنى بالتوازي مع 0.1/0.5/0.4. **Gate صارم**: 1.1 (المرحلة 1) مايبدأش إلا بعد ما Vault يشتغل فعليًا والمفتاح يترحّل ليه. لو Vault فشل نهائيًا وقت ما باقي المرحلة 0 يخلص → توقّف وقرار إداري (تفصيل كامل في 0.6). |
| 2 | **`CustodyRegister.EmployeeId`**: الـ FK يفضل غير مفروض لحد مراجعة يدوية، ولا نقفل شاشة العهد مؤقتًا؟ | ✅ **تم الحسم (2026-09-26)** | **Option A**: عمود جديد `EmployeeIdLinked` جنب القديم، من غير أي لمس أو FK على `EmployeeId` الأصلي. شاشة العهد تفضل شغّالة زي ما هي + Banner تحذيري بعدد السجلات المحتاجة ربط. شاشة جديدة `HR_CUSTODY_MIGRATION` للربط اليدوي. Migration تنظيف نهائية (حذف العمود القديم + FK Enforced) **مؤجّلة لأسبوعين بعد نهاية المرحلة 1**، وموسومة `Pending Cleanup` — برّه نطاق هذه الخطة (تفصيل كامل في 1.2). |
| 3 | **محتوى `HR_SETTINGS`**: نبني الكيان بحقول قليلة دلوقتي ونوسّعه بعدين، ولا نأجّل الشاشة كاملة؟ | ✅ **تم الحسم (2026-09-26)** | الكيان `HrSettings` يتبنى **بكل الحقول من الأول** (نمط `BranchPOSSettings`)، والشاشة `HR_SETTINGS` تعرض **حقول المرحلة 1 بس** (`EmployeeCodePrefix`، و`EmployeeCodeSequence`، و`DefaultProbationDays`، و`DefaultBranchId`، و`RequireNationalIdForActivation`) — باقي الحقول (`MonthBasis`، و`KioskSessionSeconds`، وغيرها) موجودة في الكيان بس مؤجلة العرض لمرحلتها (تفصيل كامل في 1.5). |
| 4 | **إنهاء الخدمة الإداري البسيط (1.4)**: يمنع (Block) بالكامل لو فيه عهدة مفتوحة (زي روح قاعدة 43 الكاملة)، ولا يدّي تحذير بس مع تأكيد صريح (`confirm=true`) من الـ HR؟ | 🔲 **لسه مفتوح** | بيحدد سلوك الـ Endpoint وتصميم الـ UI — **أهم قرار متبقي قبل تنفيذ 1.4**. |
| 5 | **شرط تفعيل الموظف (قاعدة 2)**: "عقد ساري + مستندات إلزامية + هيكل راتب" — هيكل الراتب مش موجود لسه (مرحلة الرواتب لاحقة). نفعّل الشرط جزئيًا (عقد + مستندات بس) في هذه المرحلة، ونضيف شرط الراتب لاحقًا كتحديث على نفس المنطق، ولا الموظف يفضل `Draft` دايمًا لحد ما كل الشروط الثلاثة تتوفر؟ | 🔲 لسه مفتوح | بيحدد هل شاشة `HR_EMPLOYEES` ممكن تعرض موظف `Active` فعلي في هذه المرحلة أصلًا. |
| 6 | **التعيين بدون سلسلة اعتماد (خطر 6)**: موافق إن `HR_HIRING` في هذه المرحلة بيحفظ مباشرة (`Draft`) من غير اعتماد HR/مالية الموصوف في المواصفة، لحد ما محرك الموافقات (مرحلة تانية) يتبني؟ | 🔲 لسه مفتوح | تواصل توقعات مع العميل قبل التسليم (قرار سلوك متوقع أصلًا من بنية المشروع). |
| 7 | **0.2 (إظهار مدقَّق PII)**: يتبني دلوقتي كآلية عامة بدون كيان حقيقي، ولا يتأجل لحد ما `Employee`/`EmployeePersonalData` يتوجدوا في 1.1؟ | ✅ **تم الحسم (2026-09-26): Defer to 1.1** | مفيش كيان HR حقيقي لحد الآن — الـ Endpoint مالوش معنى من غيره، والاختبار عليه مستحيل، وYAGNI (مفيش داعي نبني آلية بدون مستهلك). التفصيل الكامل اتنقل لقسم **1.1b** — الربط بـ 1.1 أنضف لأن الـ Authorization والحقول المشفّرة هيكونوا موجودين فعليًا. |
| 8 | **نمط ملفات الـ Commands/Queries لـ 1.1**: نتبع نص الخطة الأصلي (ملف مجمّع، نمط `UserCommands.cs`) ولا الاتجاه الفعلي الحالي في الكود (فولدر منفصل لكل Command)؟ | ✅ **تم الحسم (2026-09-26): فولدر لكل Command** | Research Pass (`Phase-1.1-Research.md §2.5`) أكّد إن كل كود اتكتب فعليًا في آخر يومين (`Purchasing/PurchaseOrders`، `Accounting/Vouchers`، `Accounting/TreasuryTransfers`) بيستخدم فولدر منفصل لكل Command — ده الاتجاه الحالي، مش النمط المجمّع القديم. تفصيل كامل في 1.1. |
| 9 | **عمق فحص حلقة `OrgUnit.ParentId`**: فحص سطحي ("الأب = نفسي" زي `ItemGroup`) ولا Walk عميق كامل؟ | ✅ **تم الحسم (2026-09-26): Walk كامل** | مفيش سابقة حقيقية في الكود لأي فحص عميق (`Account` مايتغيّرش أصلًا، و`ItemGroup` سطحي بس) — `HashSet<long>` Walk للأعلى، مُختبَر بـ 5 حالات (مباشر/غير مباشر/عمق 2/عمق 3/Self-loop). تفصيل كامل في 1.1. |
| 10 | **ترتيب بناء الكيانات التسعة**: كل كيان لوحده، ولا Batches؟ | ✅ **تم الحسم (2026-09-26): 4 Batches** | B1 (Lookups + Migration شاملة + MenuItemSeedData) → B2 (Employee + EmployeePersonalData) → B3 (الثلاثة التابعين، بالتوازي) → B4 (اختبارات API شاملة + Regression). تفصيل كامل في 1.1. |
| 11 | **متى تُضاف مجموعة `HR` في `MenuItemSeedData.cs`؟** | ✅ **تم الحسم (2026-09-26): B1** | مع الـ Migration مباشرة، مش مع الـ Controllers لاحقًا — أي Controller يتبني بعدين محتاجها موجودة أصلًا عشان `ScreenPermissionFilter` ما يرفضش الطلبات. مجموعة `HR` مستقلة (مش موزّعة على مجموعات موجودة) — 7 شاشات. |
| 12 | **الـ 13 Lookup الجديد (2026-09-26) محتاجين شاشة List/Edit مستقلة لكل واحد فيهم دلوقتي، ولا يتداروا بشاشة/آلية عامة أبسط (أو حتى Seed بس من غير شاشة تعديل في المرحلة دي)؟** | ✅ **تم الحسم (2026-09-26): Hybrid** | **8 شاشات كاملة**: `Country`/`City`/`Bank` (تحت `SETTINGS`، مش `HR`) + `JobGrade`/`JobPosition`/`OrgUnit`/`EmployeeDocumentType`/`InsuranceOffice` (تحت `HR`). **5 Seed Only بدون شاشة**: `Nationality`، و`RelationshipType`، و`MilitaryStatus`، و`QualificationType`، و`TerminationReason` — بيانات مزروعة عن طريق `SystemDataSeeder` بس (`HrCoreLookupSeedData.cs`)، تتضاف شاشة ليهم لاحقًا لو احتاجوا. تفصيل كامل في 1.1 و1.5. |

---

**متبقي قبل التنفيذ**: القرارات التسعة الأساسية (1، 2، 3، 7، 8، 9، 10، 11 + خطر 8 المحسوم) اتحسمت 2026-09-26. لسه فاضل سؤال واحد صغير (4 — سياسة الحظر عند إنهاء الخدمة) قبل تنفيذ 1.4 تحديدًا، وسؤالين توثيقيين (5، 6) بلا أثر جدولة حقيقي.

**حالة التنفيذ الفعلي (2026-09-26)**: المرحلة 0 خلصت 5/6 (0.1، و0.3، و0.4، و0.5، و0.6 — كلهم كود حقيقي مبني ومُختبَر، مش خطة بس). 0.2 اتأجل رسميًا لـ 1.1b. Research Pass لـ 1.1 خلص (`Phase-1.1-Research.md`) وقراراته الأربعة اتحسمت، وبعدها قرارات الـ 13 Lookup الإضافية والـ Hybrid على الشاشات اتحسمت كمان. **✅ B1 خلص فعليًا (13 جدول Lookup + Migration + Seed + MenuItemSeedData + فحص حلقة `OrgUnit`)** — 133/133 IntegrationTests و250/250 ApiTests، مطبّق على `HabbakErp` الحقيقية بنجاح. **الخطوة الجاية: B2 (`Employee` + `EmployeePersonalData`).**
