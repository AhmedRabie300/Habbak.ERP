# تقرير شامل — كل ما تم إنجازه في نظام الحبّاك ERP

تقرير تراكمي لكل المحاور التي تم العمل عليها في هذه الجلسة، بترتيب زمني، مع الحالة النهائية لكل محور.

---

## 1. نظام القوائم المرجعية — بُني ثم أُلغي بقرار معماري

### 1.1 ما تم بناؤه أولًا
نظام عام مشترك `LookupType`/`LookupValue` (جدولين + شاشة إدارة واحدة `LookupsSettingsPage`) لإدارة أي تصنيف بسيط (طريقة دفع، سبب رفض...) بدون ما كل نوع ياخد جدول مستقل — تنفيذًا لمواصفة كانت موجودة في `00-System-Wide-Corrections-02.md`.

### 1.2 القرار النهائي: إلغاء النظام العام بالكامل
بعد المراجعة، تقرر إن **كل شاشة Lookup لازم يكون ليها جدول مستقل خاص بيها** — لا يوجد جدول عام مشترك في النظام. تم:
- حذف `LookupType.cs`، `LookupValue.cs`، كل أوامر/استعلامات `Common/Lookups`، الـ Controller، شاشة الفرونت-إند بالكامل.
- Migration جديدة (`RemoveGenericLookupSystem`) تمسح الجدولين، طُبّقت على **كل قواعد البيانات المستخدمة** خلال الجلسة.
- تحديث `00-System-Wide-Corrections-02.md` ليعكس السياسة الجديدة رسميًا (قسم 2.2 بقى ينص صراحة: "لا يوجد جدول مرجعي عام مشترك في هذا النظام").
- الإبقاء على `ILookupEntity` كواجهة عامة (Code/NameAr/NameEn/IsActive) — كل كيان مرجعي جديد ينفّذها بجدوله الخاص.

### 1.3 أول تطبيق للسياسة الجديدة: طرق الدفع (Payment Methods)
بُني كمثال مرجعي كامل للنمط الجديد: كيان `PaymentMethod` مستقل، جدول `PaymentMethods` خاص بفلترة soft-delete، Commands/Queries/Controller خاصة بيه، شاشة `PaymentMethodsListPage`، مسار `/accounting/payment-methods`، مسجَّل في نظام الترقيم (Coding Rules) بكود `ACCOUNTING_PAYMENT_METHODS`. تم اختباره حيًا (إنشاء، تعديل، حفظ) والتحقق من التخزين الصحيح.

---

## 2. تصحيح CostCenterDimension / CostCenterDimensionValue

حقل `Name` الواحد اتقسم لـ `NameAr`/`NameEn` منفصلين (تطبيقًا لمبدأ عرض الاسم حسب لغة الواجهة). تعديل شمل: Domain، Infrastructure، 7 ملفات Application، الـ API، الاختبارات، و6 شاشات فرونت-إند. الـ Migration اتصلحت يدويًا بعد ما اكتشفنا إن EF كانت هتقلب بيانات الأسماء العربية غلط.

---

## 3. تحويل كل الـ Select في النظام لقوابل بحث

مكوّنين جدد في `ui-kit`: `SearchableSelect` و`SearchableMultiSelect`، وإزالة الـ `<select>` الأصلي نهائيًا. **10 شاشات** تم تحويلها بالكامل: Custody، Bank/Cash Reconciliations، Coding Rules، Chart of Accounts، Account Cost Centers، Dimensions، Reports، Voucher Edit، Journal Entry Edit. كل شاشة اتفحصت حيًا في المتصفح (فلترة، اختيار، وللـ MultiSelect: إضافة/حذف شرائح).

---

## 4. إصلاح الأخطاء الصامتة (Silent Catch)

**المشكلة الجذرية**: الـ Axios interceptor المركزي يستثني عمدًا أخطاء الـ validation من الـ toast العام على افتراض إن كل شاشة تعرضها بنفسها — لكن ولا شاشة كانت بتعمل كده. النتيجة: بيانات ناقصة كانت بتفشل بصمت تام (بلاغ المستخدم الأصلي: "New Manual Entry — مش بيعمل save as draft").

**الإصلاح**: دالة مشتركة `getFieldErrorMessage()` طُبّقت على **14 ملف/شاشة** (JournalEntryEditPage، VoucherEditPage، CashReconciliations، BankReconciliations، Custody، Dimensions، ChartOfAccounts + استيراد الإكسل، AccountCostCentersEditor، LookupsSettingsPage، CodingRules، Periods، BranchesListPage).

**اكتشاف إضافي**: تواريخ فاضية كانت بترجّع شكل خطأ خام من ASP.NET (ProblemDetails) مختلف تمامًا عن عقد الأخطاء المعتاد، فمكانش بيتعرض هو كمان. تم إصلاحه مركزيًا في الـ interceptor نفسه — إصلاح واحد يحمي كل شاشة في النظام.

---

## 5. توثيق API التفاعلي (Swagger)

- جُرِّب Scalar UI أولًا، ثم استُبدل بـ **Swagger UI التقليدي (Swashbuckle)** بناءً على طلب صريح.
- `launchBrowser` اتفعّل مع `launchUrl: "swagger"` عشان يفتح تلقائيًا عند التشغيل من فيجوال ستوديو.
- **مشكلة الـ Authorize**: زر التفويض في Swagger UI كان موجود بس بلا تأثير فعلي — تبيّن إن `AddSecurityRequirement` في Swashbuckle 10 / Microsoft.OpenApi v2 بقى محتاج صيغة جديدة تمامًا (`document => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }`). بعد الإصلاح، تم التحقق حيًا: توكن حقيقي من `/api/v1/dev/auth/login` → Authorize → طلب محمي رجّع 200 بدل 401.
- **مشكلة الـ HTTPS Redirect**: `UseHttpsRedirection()` كانت بتحوّل كل طلب HTTP لـ HTTPS تلقائيًا لما بروفايل "https" في فيجوال ستوديو يشتغل (بورتين HTTP+HTTPS مع بعض) — ده كان بيكسر تسجيل الدخول من الفرونت-إند (بيتصل بـ HTTP بس). تم تعطيلها في بيئة Development فقط (تبقى فعّالة للنشر الحقيقي).

---

## 6. الانتقال من LocalDB لسيرفر SQL Server حقيقي

- ربط المشروع بـ `DESKTOP-DBL395G\MSSQLSERVER01` (SQL Server 2022 Developer) عبر SQL Authentication، وتحديث `appsettings.Development.json`.
- إنشاء قاعدة `HabbakErp` بالكامل على السيرفر الجديد عبر تطبيق كل الـ Migrations (8 وقتها، ثم أصبحت 10 بعد إضافة Payment Methods وإلغاء الـ Lookup).
- **نقل كل بيانات الاختبار** من LocalDB للسيرفر الجديد: 17 جدول بـ `bcp` (مع تعطيل مؤقت للـ FK constraints، والتعامل مع مشكلة `QUOTED_IDENTIFIER` للجداول ذات الـ filtered indexes عبر `bcp -q`)، مع الحفاظ التام على الـ identity values. تم التحقق من تطابق عدد الصفوف 100% في كل الـ 25 جدول، وتفعيل الـ constraints مرة أخرى بدون أي انتهاك.

---

## 7. إصلاحات تشغيلية متكررة (Infrastructure)

- **قفل ملفات البناء**: عدة مرات اكتشفنا إن فيجوال ستوديو (أو عملية API سابقة لم تُغلق بشكل نظيف) كانت قافلة ملفات الـ `bin` فيمنع أي `dotnet build`/`dotnet ef` من عندي — الحل الثابت: طلب وقف الـ Debug (Shift+F5) قبل أي عملية بناء من طرفي.
- **تعارض البورتات**: البورت الافتراضي في `launchSettings.json` (5231) كان مختلف عن البورت اللي الفرونت-إند بيتوقعه (5299 في `.env.development`) — تم تثبيت `applicationUrl` على 5299 في كل البروفايلات.
- **تعطّل LocalDB**: اكتُشِف 3 عمليات `sqlservr.exe` شغّالة في نفس الوقت على نفس الـ instance (بقايا عمليات لم تُغلق بشكل نظيف)، مما كان يمنع أي تشغيل جديد. تم إيقافها بأمان (`sqllocaldb stop -k` + إنهاء العملية اليتيمة في نفس جلسة المستخدم فقط، مع ترك عمليتين تابعتين لخدمة SQL Server منفصلة تمامًا دون المساس بيهم).

---

## 8. الاختبار والتحقق (منهج ثابت طوال الجلسة)

- **الباك-إند**: `dotnet build` نظيف و**20 اختبار ناجح** (14 Integration + 6 API) بعد كل تعديل جوهري.
- **الفرونت-إند**: `npx tsc --noEmit` نظيف بلا استثناء طوال الجلسة.
- **الفحص الحي**: كل ميزة جديدة أو إصلاح تم اختباره فعليًا في المتصفح (وليس فقط بالاعتماد على نجاح الـ build)، مع تنظيف أي بيانات اختبار من قاعدة البيانات بعد كل تحقق.

---

*تقرير مبني على وقائع هذه الجلسة فقط.*
