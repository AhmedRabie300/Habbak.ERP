# تشغيل الاختبارات (IntegrationTests / ApiTests)

> مرجعي: `00-Project-Overview.md §25` ("xUnit + WebApplicationFactory"، اختبارات ضد قاعدة بيانات
> حقيقية مش In-Memory). الملف ده بيوثّق **إزاي تشغّل الاختبارات دي فعليًا** حسب نظام التشغيل.

---

## 1. الفكرة

`Habbak.ERP.IntegrationTests` و`Habbak.ERP.ApiTests` محتاجين SQL Server حقيقي — مش
In-Memory Database — لأن كود المشروع بيعتمد على مزايا خاصة بـ SQL Server نفسه (RowVersion
Concurrency Tokens، Filtered Unique Indexes، Check Constraints...). كل كلاس اختبار بيعمل قاعدة
بيانات خاصة بيه (اسم عشوائي فريد)، يشغّل عليها كل الـMigrations من الصفر، يشغّل اختباراته، وبعدين
يمسحها.

السؤال العملي: **السيرفر نفسه بييجي منين؟** الإجابة اتحسمت في `TestSqlServer.cs` (نسخة في كل
مشروع اختبارات — `Habbak.ERP.IntegrationTests`/`Habbak.ERP.ApiTests` — نفس المنطق بالظبط،
مش مشتركين لأن مفيش مشروع مشترك بينهم):

| نظام التشغيل | المصدر | ليه |
|---|---|---|
| **Windows** (جهاز تطوير محلي عادي) | SQL Server **LocalDB** — زي ما كان دايمًا | أسرع، مثبَّت بالفعل مع Visual Studio/`dotnet` SDK Workloads، مفيش Docker لازم |
| **أي حاجة تانية** (Linux CI، أو جلسة Cloud/Container، أو Mac) | **Docker container حقيقي** عن طريق مكتبة Testcontainers | LocalDB نفسها تقنية Windows بس — `System.PlatformNotSupportedException` فورًا لو حاولت تستخدمها على Linux، مفيش أي طريقة تانية |

الاختيار بيحصل تلقائيًا (`OperatingSystem.IsWindows()`) — **مفيش أي إعداد يدوي أو متغيّر بيئة
لازم تظبطه** عشان تشغّل الاختبارات، على أي نظام تشغيل.

---

## 2. المتطلبات المسبقة

### على Windows
نفس اللي كان دايمًا: SQL Server LocalDB مثبَّت (بييجي جاهز مع أغلب تثبيتات Visual Studio/.NET SDK).
مفيش حاجة إضافية.

### على Linux (أو أي بيئة من غير LocalDB)
**Docker لازم يكون متاح وشغّال** (`docker info` يرجع بيانات سيرفر حقيقية، مش خطأ اتصال). أول
تشغيلة هتنزّل صورة `mcr.microsoft.com/mssql/server:2022-latest` (~600 ميجا) لو مش موجودة محليًا —
بعد كده بتتكرّر من الـCache.

لو مفيش Docker Daemon شغّال، الاختبارات هتفشل بخطأ واضح من مكتبة Testcontainers نفسها (بيقولّك
مفيش Docker Endpoint متاح) — مش خطأ غامض.

---

## 3. تشغيل الاختبارات

```bash
# كل اختبارات Phase/موديول معيّن
dotnet test src/Habbak.ERP.IntegrationTests/Habbak.ERP.IntegrationTests.csproj --filter "FullyQualifiedName~AttendanceDevicesTests"

# Regression كامل (كل الاختبارات، المشروعين)
dotnet test src/Habbak.ERP.IntegrationTests/Habbak.ERP.IntegrationTests.csproj
dotnet test src/Habbak.ERP.ApiTests/Habbak.ERP.ApiTests.csproj
```

على Linux، أول تشغيلة في كل عملية `dotnet test` بتاخد وقت إضافي (تنزيل الصورة لو أول مرة + بدء
تشغيل الـContainer) — بعد كده كل كلاسات الاختبار في نفس العملية بتشارك نفس السيرفر (Container
واحد لكل عملية اختبار، مش واحد لكل كلاس — بالظبط زي LocalDB على Windows، سيرفر واحد مشترك لكل
جهاز مش واحد لكل كلاس).

**مفيش تنظيف يدوي لازم بعد كده** — مكتبة Testcontainers بتشغّل Container مرافق اسمه Ryuk بيراقب
عملية `dotnet test` نفسها وبيمسح أي Container متروك تلقائيًا لما العملية تقفل، حتى لو حصل Crash.

---

## 4. Vault (لازم لـApiTests كاملة، مش بس Testing.md ده)

بعض اختبارات `Habbak.ERP.ApiTests` (مثلًا `TwoFactorTests`) بتحتاج `IVaultClient` شغّال فعليًا.
افتراضيًا (`appsettings.json`) الإعداد بيرجع فاضي عن قصد (`Vault:Token: ""`) — في `Development`
ده بيرجع لـDefault محلي (`http://127.0.0.1:8200` + `dev-root-token`، اتصلح في
`VaultServiceCollectionExtensions.cs` — كان فيه Bug هنا بيخلي القيمة الفاضية "" تفوّت من غير
Fallback، اتصلح). يعني لازم يكون فيه Vault Dev Server شغّال فعليًا على نفس الجهاز:

```bash
vault server -dev -dev-root-token-id="dev-root-token" -dev-listen-address="127.0.0.1:8200"
```

تفاصيل كاملة (تفعيل KV، كتابة مفتاح الـHMAC) في `Docs/Setup/Vault-Setup.md`, قسم "وضع التطوير
المحلي". من غير Vault شغّال، الاختبارات اللي مش محتاجاه (الأغلبية) بتشتغل عادي — بس أي اختبار
بيلمس تشفير PII حقيقي أو الـ2FA هيفشل بخطأ اتصال واضح (مش الـBug القديم بتاع الـToken الفاضي).

---

## 5. استكشاف الأخطاء

| العرض | السبب الأرجح | الحل |
|---|---|---|
| `PlatformNotSupportedException: LocalDB is not supported on this platform` | بتحاول تشغّل الاختبارات على Linux بنسخة قديمة من الكود (قبل `TestSqlServer.cs`) | تأكد إنك على آخر إصدار من الفرع؛ لو المشكلة استمرت، تأكد إن `OperatingSystem.IsWindows()` بترجع `false` فعليًا (بديهي على Linux، بس تأكد إن مفيش `RuntimeInformation` Override غريب في الكود) |
| رسالة من Testcontainers بتقول مفيش Docker Endpoint | Docker Daemon مش شغّال | شغّل `dockerd`/Docker Desktop، تأكد بـ`docker info` |
| `ArgumentException: 'vaultToken' value is empty` | نسخة قديمة من `VaultServiceCollectionExtensions.cs` (الـBug القديم) | تأكد إنك على آخر إصدار؛ لو استمر، اضبط `Vault__Token` كمتغيّر بيئة يدويًا كحل مؤقت |
| أول تشغيلة بطيئة جدًا على Linux | تنزيل صورة SQL Server لأول مرة | طبيعي، مرة واحدة بس — `docker images` هتوريك الصورة بعد كده |
