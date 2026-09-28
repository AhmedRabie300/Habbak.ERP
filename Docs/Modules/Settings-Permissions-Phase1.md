# موديول الإعدادات والصلاحيات — المرحلة 1 (Domain + قاعدة البيانات)

> **التاريخ:** 19 سبتمبر 2026
> **النطاق:** الكيانات، والـ EF Configurations، وmigration واحدة، والبيانات المبدئية، والاختبارات.
> **مش في المرحلة دي:** تسجيل الدخول، وJWT، وفحص الصلاحيات في الـ handlers، والـ Audit Interceptor، والشاشات، وربط حقول المستخدمين في الموديولات القديمة.

> ⚠️ **الملف المرجعي `07-Module-Settings-Permissions.md` مش موجود في المشروع.** اتبنى من نص الطلب نفسه (الأقسام 2–5).

---

## 1. الملخص

| البند | النتيجة |
|---|---|
| الكيانات | **10 كيانات + ملف Enums** (11 ملف) في `src/Habbak.ERP.Domain/Settings/` |
| Configurations | **10** في `Infrastructure/Persistence/Configurations/Settings/` |
| Migration | **`20260918230908_AddSettingsPermissionsPhase1`**: 10 جداول جديدة، ومفيش ولا جدول قديم اتلمس |
| التطبيق | ✅ LocalDB · ✅ SQL Server الحقيقي |
| البيانات المبدئية | 8 أدوار + SystemSettings لكل شركة · مستخدم `admin` مدير نظام على كل الشركات |
| الاختبارات | **22 اختبار جديد**. الإجمالي **171/171** ناجحين (69 Integration + 102 API) |

---

## 2. الكيانات

| الكيان | الجدول | مقيّد بالشركة؟ | ملاحظات |
|---|---|---|---|
| `User` | `Users` | ❌ على مستوى النظام | `Username` و`Email` فريدين. فيه دوال القفل: `RegisterFailedLogin` · `ReleaseExpiredLock` · `RegisterSuccessfulLogin` |
| `RefreshToken` | `RefreshTokens` | ❌ | `Token` بيتخزّن **SHA-256 hash** مش التوكن نفسه. `IsActive` بيتحسب ومش متخزّن |
| `LoginAttempt` | `LoginAttempts` | ❌ | `UserId` ممكن يكون null (اسم مستخدم مش موجود). مالوش FK عشان السجل يفضل لو المستخدم اتمسح |
| `Role` | `Roles` | ✅ | `(CompanyId, Code)` فريد. فيه `SystemRoles` بالأدوار الـ8 |
| `UserRole` | `UserRoles` | ❌ (بيتبع الدور) | `(UserId, RoleId, BranchId)` فريد |
| `UserScope` | `UserScopes` | ✅ | `(UserId, CompanyId, BranchId)` فريد. `RoleInScope` كود الدور، مش FK |
| `ScreenPermission` | `ScreenPermissions` | ✅ | `(RoleId, ScreenCode)` فريد. `CanApprove` منفصل عن `CanEdit` |
| `FieldPermission` | `FieldPermissions` | ✅ | `(RoleId, EntityType, FieldName)` فريد. `RequiresAuditLog = true` افتراضيًا. فيه `FieldPermissionCatalog` بالحقول الحساسة |
| `AuditLog` | `AuditLogs` | ❌ مفيش أي فلتر | **مش `AuditableEntity`**. الإضافة بس مسموحة. فيه `AuditLog.FieldChange(...)` بيحط `[REDACTED]` مكان قيمة أي حقل حساس |
| `SystemSettings` | `SystemSettings` | ✅ | صف واحد لكل شركة (فهرس فريد). فيه `CheckPassword(...)` بترجّع القواعد اللي كلمة المرور مخالفاها بالعربي |
| `Enums` | — | — | `UserStatus` · `PreferredLanguage` · `AuditActionType` زي ما هم في الطلب |

**الفلاتر (Global Query Filters):** بتشتغل أوتوماتيك بالنمط الموجود في `AppDbContext`:
- أي كيان `AuditableEntity` عليه `!IsDeleted`.
- أي كيان `ICompanyScopedEntity` عليه كمان `CompanyId == CurrentCompanyId`.
- النتيجة زي الطلب بالظبط:
  - `Role` / `UserScope` / `ScreenPermission` / `FieldPermission` / `SystemSettings`: فلتر الشركة والحذف.
  - `User` / `RefreshToken` / `LoginAttempt`: فلتر الحذف بس.
  - `AuditLog`: مفيش فلتر خالص.

**الفهارس الفريدة** كلها `WHERE [IsDeleted] = 0` زي باقي النظام. يعني اسم مستخدم محذوف ينفع يتاخد تاني (فيه اختبار لده). و`BranchId = NULL` بيتحسب قيمة واحدة في الفهرس الفريد، فالمستخدم ليه نطاق واحد بس "كل الفروع" في كل شركة.

---

## 3. حاجات اتنفذت مختلف عن الطلب — ولازم تعرفها

| # | الطلب | اللي اتعمل | السبب |
|---|---|---|---|
| 1 | `long CompanyId` | `long? CompanyId` (والعمود نفسه `NOT NULL` في القاعدة) | الـ interface الموجود `ICompanyScopedEntity` بيعرّفه `long?`، وفلتر الشركة التلقائي معتمد عليه |
| 2 | `PublicId` | **مش موجود** | مفيش ولا كيان في المشروع فيه `PublicId`؛ القاعدة دي اتقررت قبل كده في المبيعات. `RowVersion` و`IsDeleted` موجودين من `AuditableEntity` |
| 3 | "`AuditLog` لا يُحذف" | `AppDbContext` **بيرفض** أي تعديل أو حذف لسطر في السجل (`AUDIT-LOG-APPEND-ONLY`) | الرفض في `SaveChanges` نفسه، فبيشتغل مع أي context حتى من غير interceptors |
| 4 | "Seed تلقائي لكل شركة جديدة" | `CreateCompanyCommand` بقى بيضيف الأدوار الـ8 وSystemSettings، وبيدّي كل مدير نظام دور ونطاق في الشركة الجديدة، **في نفس الـ transaction** | عشان مدير النظام يوصل لكل الشركات، حتى اللي هتتعمل بعده. **ده التعديل الوحيد على كود قديم** (handler، مش كيان) |
| 5 | Seed للشركة الحالية | الـ seed **جوه الـ migration نفسها** (SQL) | عشان يتطبق على القاعدتين مع `database update` من غير ما نشغّل البرنامج. الـ hash بتاع كلمة المرور بيتحسب وقت تشغيل الـ migration، فكل قاعدة ليها salt مختلف |
| 6 | "Unit Tests" | في مشروع `IntegrationTests` (أول 8 اختبارات من غير قاعدة بيانات) | مفيش مشروع Unit Tests في الـ solution |
| 7 | `FieldPermission.RequiresAuditLog` default | القيمة الافتراضية في الـ C# (`= true`) **مش** في قاعدة البيانات | default في القاعدة على `bool` بيخلّي EF يعتبر `false` "مش متحدد" فمفيش حد يقدر يحفظ `false` أبدًا. اتكشف قبل التطبيق واتصلّح |
| 8 | Enums كاملة في المرحلة 2 | الـ Enums الـ3 اتعملوا دلوقتي | الكيانات مش هتعمل compile من غيرهم |
| 9 | كلمة المرور | **BCrypt بـ work factor 12** (`BCrypt.Net-Next 4.2.0`) عن طريق `IPasswordHasher` | الـ interface في Application والتنفيذ في Infrastructure |

**حاجات زيادة ماكانتش في الطلب بس محتاجينها:**
- فهرس فريد على `UserRole (UserId, RoleId, BranchId)`: نفس الدور مرتين لنفس المستخدم في نفس الفرع ملوش معنى.
- فهرس فريد على `SystemSettings.CompanyId`: صف واحد لكل شركة.
- فهارس للقراءة على `LoginAttempts` و`AuditLogs` و`RefreshTokens`.

---

## 4. البيانات المبدئية (على القاعدتين)

| البند | LocalDB | SQL Server الحقيقي |
|---|---|---|
| شركات | 1 | 1 |
| أدوار | 8 (كلها `IsSystemRole`) | 8 |
| SystemSettings | 1 (8 حروف، 5 محاولات، 15 دقيقة، 7 سنين) | 1 |
| مستخدمين | `admin` (Id 1، Active، hash يبدأ بـ `$2a$12$`) | نفس الكلام |
| أدوار المستخدم | admin ← SUPER_ADMIN | نفس الكلام |
| النطاقات | admin على الشركة، كل الفروع، افتراضي | نفس الكلام |

- اتأكدت إن العربي متخزّن صح (أول حرف من "مدير النظام" = U+0645).
- `admin` واخد **Id = 1**، وده نفس الرقم اللي كل `CreatedBy` في البيانات القديمة شايله (dev login كان بيستخدم user 1). فالبيانات القديمة هتتربط بيه طبيعي لما نضيف الـ FK في المرحلة 3.

> ⚠️ **أمان:** كلمة مرور `admin` معروفة (`Admin@123`)، و`MustChangePassword = false` زي ما الطلب قال. أول ما تسجيل الدخول يشتغل (المرحلة 2)، **لازم تتغيّر**. أو نخلّي `MustChangePassword = true`، وده تغيير سطر واحد في الـ migration أو update.

---

## 5. الاختبارات — `SettingsPermissionsTests.cs` (22)

**قواعد الـ Domain (من غير قاعدة بيانات):**

| # | الاختبار |
|---|---|
| 1 | كلمة المرور بتتخزّن BCrypt hash، بيتحقق منها، وبياخد salt مختلف كل مرة، والـ hash البايظ بيرجع false |
| 2 | كلمة المرور القصيرة بتترفض |
| 3 | السياسة بتقول كل قاعدة ناقصة، بتقبل كلمة المرور القوية، وبتحترم الإعدادات المخففة |
| 4 | الحساب بيتقفل في المحاولة الخامسة الغلط (مش قبلها) |
| 5 | القفل بيتفك بعد 15 دقيقة، ومش بيتفك في الدقيقة 14 |
| 6 | الدخول الصح بيصفّر العدّاد، والحساب الموقوف (Suspended) عمره ما بيتفك لوحده |
| 7 | الحقول الحساسة بتتسجّل `[REDACTED]` في السجل، والحقول العادية بتتسجّل بقيمتها |
| 8 | `RefreshToken.IsActive` بيبقى false بعد الانتهاء أو الإلغاء |

**قاعدة البيانات (LocalDB جديدة، والـ migration بتتطبق عليها):**

| # | الاختبار |
|---|---|
| 9 | إنشاء مستخدم ← حفظ ← قراءة |
| 10 | اسم مستخدم مكرر بيترفض (`IX_Users_Username`) |
| 11 | إيميل مكرر بيترفض (`IX_Users_Email`) |
| 12 | اسم مستخدم محذوف ينفع يتاخد تاني |
| 13 | ربط دور بمستخدم (للشركة كلها + لفرع)، والتكرار بيترفض |
| 14 | منح نطاق، ونطاق "كل الفروع" التاني لنفس الشركة بيترفض |
| 15 | صلاحية شاشة، وصف واحد بس لكل دور وشاشة |
| 16 | صلاحية حقل: التسجيل في السجل مفعّل افتراضيًا، **وينفع يتقفل** |
| 17 | سجل المراجعة بيتكتب ويتقري، ومحاولة تعديله أو حذفه بترمي خطأ |
| 18 | فلتر الشركة على الأدوار بيرجّع أدوار الشركة الحالية بس، ونفس الكود مسموح في شركة تانية |
| 19 | محاولات الدخول (بمستخدم ومن غير) والـ refresh tokens، والتوكن المكرر بيترفض |
| 20 | SystemSettings بقيمها الافتراضية، وصف واحد لكل شركة |
| 21 | الـ migration بتعمل `admin` شغّال وكلمة مروره `Admin@123` |
| 22 | الشركة الجديدة بتاخد الأدوار الـ8 وSystemSettings، ومدير النظام بياخد نطاق ودور فيها |

---

## 6. مشاكل واجهتها

| # | المشكلة | الحل |
|---|---|---|
| 1 | الملف المرجعي `07-Module-Settings-Permissions.md` مش موجود | الشغل اتعمل من الطلب نفسه |
| 2 | default في القاعدة على `RequiresAuditLog` كان هيمنع حفظ `false` | شلته، والـ migration اتعملت من جديد قبل ما تتطبق |
| 3 | Visual Studio شغّال على الـ API وقافل ملفات `bin/Debug` | الـ build بـ `--artifacts-path`، والـ EF بـ `--configuration Release` |
| 4 | `sqlcmd` بيعرض العربي غلط | متخزّن صح، وده اتأكد بـ `UNICODE()` |

---

## 7. المرحلة 2 — الجاية

1. `AuthController`: login، وrefresh، وlogout. بيستخدم `ReleaseExpiredLock` قبل الفحص، و`RegisterFailedLogin` / `RegisterSuccessfulLogin` بعده، ويسجّل `LoginAttempt`.
2. JWT من `User` + `UserScope` (بدل dev login).
3. Audit Interceptor بيكتب `AuditLog` عن طريق `AuditLog.FieldChange`، فالحقول الحساسة بتتسجّل `[REDACTED]`.
4. فحص صلاحيات الشاشات والحقول في الـ handlers والـ API.
5. الشاشات العشرة.
6. **المرحلة 3:** FK من `CreatedBy` / `CashierUserId` / … على `Users`.
