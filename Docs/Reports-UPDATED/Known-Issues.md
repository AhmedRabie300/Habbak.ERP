# مشاكل معروفة — اتصلحت أثناء التطوير

> سجل للمشاكل اللي اتكتشفت واتصلحت في نفس الجلسة اللي بنيت فيها الميزة — مش Bug tracker مستقل، الهدف إن حد يرجع يفهم "إيه اللي كان غلط وليه" لو نفس النمط ظهر تاني في شاشة تانية.

---

## 🟢 ملاحظة تجميلية (مش Bug) — بانل "تفاصيل طلب الشراء" على أمر ملغي بيعرض كميته القديمة

**التاريخ**: 2026-09-25 (لوحظ أثناء الاختبار اليدوي على الـ Real DB بعد الـ Migration)
**المكان**: `GetPurchaseOrderSourceRequestQuery.cs`
**الخطورة**: 🟢 تجميلية بس — البيانات في الداتابيز صح 100%، العرض بس ممكن يلخبط.

### الوصف

جرّبت السيناريو الكامل (إنشاء أمر شراء من طلب شراء بكمية 1000 → تعديل لـ 600 → إلغاء) على `HabbakErp` الحقيقية بعد الـ Migration. بعد الإلغاء:
- **الداتابيز صح 100%**: `PurchaseRequestLine.OrderedQuantity = 0`, `PurchaseRequest.Status = Approved` (رجعت من `PartiallyConverted`).
- **بانل "تفاصيل طلب الشراء" على الأمر الملغي نفسه** فضل عارض "في الأمر ده = 600" (بدل ما يرجع 0) — رغم إن "لسه مامتطلبش = 1,000" ظهرت صح.

### السبب

`GetPurchaseOrderSourceRequestQuery` (بعد تعديل Remarks7 بند 7) بيحسب `OnThisOrderBaseQuantity` من سطور **نفس الأمر** (`PurchaseOrderId == order.Id`) من غير فلترة بحالة الأمر — يعني بيعرض "إيه اللي السطر بيقوله" بغض النظر عن كون الأمر اتلغى. الحساب القديم (قبل Remarks7) كان بيستبعد الأوامر الملغية/المرفوضة من "في الأمر ده" برضه، مش بس من "في أوامر تانية".

### هل محتاج إصلاح؟

قرار مفتوح — مش Bug في البيانات، مجرد سؤال عرض: هل "في الأمر ده" المفروض تفضل تعرض "الأمر طلب كام قبل ما يتلغي" (العرض الحالي) ولا "0 لأنه اتلغى ومابقاش له قيمة" (العرض القديم)؟ الحالتين منطقيتين. لو القرار إنها ترجع للسلوك القديم، الإصلاح بسيط: إضافة فلتر `order.Status != Cancelled` على استعلام `onThisOrder` في `GetPurchaseOrderSourceRequestQuery.cs`.

---

## 🔴 لسه مفتوحة — `DesignTimeDbContextFactory` بيتجاهل أي Connection String تاني

**التاريخ**: 2026-09-25 (اتكتشف أثناء اختبار Migration بتاع Remarks7 على قاعدة بيانات حقيقية)
**المكان**: `src/Habbak.ERP.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
**الخطورة**: 🔴 خطر مستقبلي — ممكن يخلي حد يظن إنه طبّق Migration على قاعدة، وهي فعليًا اتطبّقت على قاعدة تانية.

### الوصف

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=HabbakErp;Trusted_Connection=True;");
        return new AppDbContext(optionsBuilder.Options);
    }
}
```

الـ Connection String هنا **Hardcoded** ومربوط بـ `(localdb)\mssqllocaldb` — قاعدة منفصلة تمامًا عن الـ Real Dev DB الفعلية (`DESKTOP-DBL395G\MSSQLSERVER01` → `HabbakErp`, من `appsettings.Development.json`). `dotnet ef` بيفضّل `IDesignTimeDbContextFactory` (لو موجود) على أي حاجة تانية — يعني:

- `dotnet ef database update` من غير `--connection` صريح → بيروح لـ `(localdb)\mssqllocaldb`، مش الـ DB الحقيقية.
- تعيين `ConnectionStrings__Default` كـ Environment Variable **مالوش أي تأثير** — بيتجاهله تمامًا.
- الحل الوحيد المتاح دلوقتي: تمرير `--connection "..."` صريح مع كل أمر `dotnet ef database update`.

### إزاي اتكشفت

أثناء اختبار Migration بتاع Remarks7: شغّلت `dotnet ef database update` مع `ConnectionStrings__Default` كـ Env Var مستهدف نسخة تجريبية (`HabbakErp_Trial_Remarks7`) — الأمر قال "Applying migration... Done" بنجاح ظاهري، لكن لما اتأكدت من القاعدة التجريبية **مالقتش أي تغيير**. تتبعت المشكلة لغاية ما لقيت إن الـ Migration فعليًا اتطبّقت على `(localdb)\mssqllocaldb` → `HabbakErp` — قاعدة تالتة معزولة محدش قاصدها. لولا التحقق اليدوي بعد كل خطوة (Verification Queries)، كان ممكن حد يفتكر إن Migration اتطبّقت صح على القاعدة المطلوبة وهي فعليًا ماتلمستش.

### التأثير الفعلي (المرة دي)

**مفيش ضرر حقيقي حصل** — القاعدة الحقيقية (`DESKTOP-DBL395G\MSSQLSERVER01` → `HabbakErp`) اتأكد إنها مالمسهاش حاجة قبل ما نجرّب تاني بـ `--connection` صريح، ونجحت صح. القاعدة المعزولة (`(localdb)\mssqllocaldb → HabbakErp`) اتلغت (`DROP DATABASE`) بعد التأكد إنها مش مستخدمة من حد.

### الحل المطلوب (Follow-up — لسه ماتعملش)

- يا إما يتشال الـ `DesignTimeDbContextFactory` خالص لو مش ضروري (EF ممكن يبني الـ Host من `Program.cs` مباشرة لو مفيش Factory، ويقرا الإعدادات الحقيقية تلقائيًا).
- يا إما — لو فيه سبب حقيقي للـ Factory ده (مثلاً تشغيل `dotnet ef migrations add` من غير سيرفر شغال) — يتقرا الـ Connection String بتاعه من `appsettings.Development.json` أو من Environment Variable بدل ما يكون مكتوب جوه الكود.
- في الحالتين: **أي حد يشغّل `dotnet ef database update` من غير `--connection` صريح دلوقتي بيخاطر إنه يطبّق على القاعدة الغلط من غير ما ياخد أي رسالة خطأ.**

---

## Bug Fixed in Remarks7 — `reset()` effect كان بيفصل سطور أمر الشراء عن طلب الشراء بصمت عند التعديل

**التاريخ**: 2026-09-25
**المكان**: `frontend/src/features/purchasing/purchaseOrders/PurchaseOrderEditPage.tsx`
**الخطورة**: 🔴 عالية (لو ماتصلحتش، كانت هتفشل بصمت — من غير أي رسالة خطأ للمستخدم)

### الوصف

جزء من تنفيذ **Remarks7** (ربط أمر الشراء بطلب الشراء) كان إضافة `purchaseRequestLineId` على كل سطر أمر شراء، عشان الـ Backend (`PurchaseOrderRequestLinking.cs`) يعرف يربط كل سطر بسطر طلب الشراء اللي جاي منه، ويحدّث `PurchaseRequestLine.OrderedQuantity` صح.

الـ `useEffect` اللي بيعمل `reset()` على الـ Form لما بيانات الأمر (`order`) توصل من الـ API كان بيبني قيم السطور من غير `purchaseRequestLineId`:

```tsx
lines: order.lines.map((l) => ({
  itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discountAmount,
  unitId: l.unitId, expectedDeliveryDate: l.expectedDeliveryDate, weight: l.weight
  // purchaseRequestLineId ناقص هنا
}))
```

### الأثر لو ماتصلحش

1. المستخدم يفتح أمر شراء موجود ومربوط بطلب شراء (مثلاً اتعمل بـ 400 من أصل 1000).
2. يعدّل أي حاجة تانية في الأمر (تاريخ، سعر، ملاحظات...) ويحفظ.
3. الـ Form بيبعت للـ Backend سطور من غير `purchaseRequestLineId` (لأنها اتبنيت من `reset()` الناقص).
4. الـ Backend (`UpdatePurchaseOrderCommand`) بيرجّع الكمية القديمة (400) لطلب الشراء بشكل صحيح، **لكن السطور الجديدة اللي بيحفظها مالهاش ربط بالطلب خالص** — يعني الكمية **مابترجعش تتطبّق تاني**.
5. **النتيجة**: طلب الشراء يفضل شايف إن مفيش حاجة اتحوّلت منه (`OrderedQuantity` = 0)، رغم إن فيه أمر شراء فعلي شغال عليه — **من غير أي رسالة خطأ**. المستخدم مش هيلاحظ غير لو راجع شاشة تانية.

### الحل

إضافة `purchaseRequestLineId: l.purchaseRequestLineId` في الـ `reset()` mapping، عشان أي تعديل لاحق يحافظ على الربط الأصلي:

```tsx
lines: order.lines.map((l) => ({
  itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discountAmount,
  unitId: l.unitId, expectedDeliveryDate: l.expectedDeliveryDate, weight: l.weight,
  purchaseRequestLineId: l.purchaseRequestLineId
}))
```

### الدرس العام

**أي شاشة `EditPage` بتحمّل بيانات موجودة عن طريق `reset()`، وعندها حقل "ربط بمستند مصدر" على مستوى السطر (زي `purchaseOrderLineId` في فاتورة الشراء، أو `purchaseRequestLineId` في أمر الشراء) — لازم الـ `reset()` mapping يشمل الحقل ده صراحةً.** الاختبارات Backend وحدها (Integration/API) مابتكتشفش المشكلة دي لأنها بتبعت الـ Payload يدوي وبتضمّن الحقل — المشكلة كانت في الـ Frontend بس، ومحتاجة اختبار يدوي أو E2E لشاشة التعديل عشان تتكشف.

**تحقق مشابه مطلوب** (لسه ماتعملش، مجرد ملاحظة): مراجعة سريعة لباقي شاشات `EditPage` اللي فيها حقل ربط بمستند مصدر على مستوى السطر، للتأكد إن `reset()` بتاعتهم شايلة الحقل ده.

---

## Behavior Change in Remarks7 — أمر شراء مرتبط بطلب شراء لازم كل سطر فيه يحدد `purchaseRequestLineId` صراحةً

**التاريخ**: 2026-09-25
**المكان**: `CreatePurchaseOrderCommand` / `GetPurchaseOrderSourceRequestQuery` (Backend) — اكتُشف عن طريق فشل اختبار قديم `PurchasingQaBatch20260921Tests.The_order_screen_shows_what_its_request_asked_for_and_what_is_left`.
**النوع**: تغيير سلوك متعمّد (مش Bug) — نتيجة مباشرة لتصميم Remarks7.

### قبل Remarks7

أمر الشراء كان بيتربط بطلب الشراء على مستوى **الهيدر بس** (`PurchaseOrder.PurchaseRequestId`). البانل (`GetPurchaseOrderSourceRequestQuery`) كان بيحسب "قد إيه اتحوّل من كل صنف" **ديناميكيًا** بمطابقة `ItemId` عبر كل أوامر الشراء المرتبطة بنفس الطلب — يعني أي سطر في أي أمر شراء عليه نفس `PurchaseRequestId` كان بيتحسب تلقائيًا ضد الطلب، حتى من غير ما حد يربط السطر نفسه بسطر الطلب صراحةً.

### بعد Remarks7

الحساب بقى بيعتمد على عمود محفوظ (`PurchaseRequestLine.OrderedQuantity`) بيتحدّث بس لما سطر أمر الشراء يحمل `PurchaseOrderLine.PurchaseRequestLineId` صريح. أي سطر من غير الربط ده بيتعامل معاه كـ **"سطر يدوي"** (زي بالظبط `purchaseOrderLineId` في فاتورة الشراء من Remarks6) — بيتضاف للأمر لكن **مابيحسبش** ضد الطلب.

### ليه اتغيّر؟

ده بالظبط جوهر Remarks7: لازم نمنع تجاوز الكمية المطلوبة (`PUR-ORDER-QTY-EXCEEDS-REQUEST`) ونحدد حالة `PartiallyConverted`/`Converted` بدقة — وده مستحيل يتعمل بمطابقة الصنف وحدها (لو الطلب فيه صنف واحد بسطرين مختلفين مثلاً، أو لو فيه أكتر من أمر شراء بيطلب نفس الصنف بكميات مختلفة). لازم ربط صريح لكل سطر عشان يبقى فيه مصدر حقيقة واحد يقدر يتحقق منه الـ Validation والبانل مع بعض.

### الأثر على الكود الموجود

**اختبار واحد قديم اتأثر**: `PurchasingQaBatch20260921Tests.The_order_screen_shows_what_its_request_asked_for_and_what_is_left` (من Remarks4) كان بيبني أمر شراء من طلب شراء من غير ما يحدد `purchaseRequestLineId` على السطر — بما إن المفهوم ده مكانش موجود وقت ما الاختبار اتكتب. اتصلح بإضافة `purchaseRequestLineId` لـ `OrderBody` helper وتمريره في الاستدعاء، بنفس الطريقة اللي الـ Frontend الجديد (`PurchaseOrderEditPage.tsx` عن طريق `loadRequestLines`) بيبعته فعليًا.

**أي كود/استدعاء تاني** (لو موجود) بيعمل أمر شراء من طلب شراء من غير ما يحدد `purchaseRequestLineId` لكل سطر → هيفضل يعمل الأمر بنجاح، بس **مش هيتحسب ضد الطلب** (زي سطر يدوي). مفيش رفض أو Exception — سلوك صامت لازم أي Integration تانية تاخده بالحسبان.

---

## Pre-existing: تزاحم LocalDB بيسبب فشل عشوائي في السويت الكامل

**التاريخ**: 2026-09-25 (لوحظ أثناء اختبار Remarks7، مش ناتج عنه)
**النوع**: مشكلة بيئة/تزامن (Test Infrastructure) — **مش Bug في الكود**.

### الوصف

أول تشغيل كامل لـ `Habbak.ERP.ApiTests` (243 اختبار) بعد تعديلات Remarks7 طلّع **9 فشل عشوائي** في اختبارات مالهاش أي علاقة بالمشتريات (`TwoFactorTests`, `PostingReportsTests`, `POSTerminalSaleFlowTests`, `POSPostingTests`, `StockDeductionOnSaleTests`, `FieldButtonPermissionsTests`) — كل الأخطاء كانت `500 Internal Server Error` أو رسائل خطأ غير متوقعة (`INTERNAL_ERROR` بدل كود واضح).

**إعادة التشغيل فورًا من غير أي تعديل** طلّعت **243/243 ناجحة بالكامل**.

### السبب المرجّح

كل كلاس اختبار في `Habbak.ERP.ApiTests` بيعمل قاعدة بيانات LocalDB خاصة بيه (`HabbakErpApiTests_{Guid}`) ويشغّل عليها كل الـ Migrations من الصفر. تشغيل عشرات الكلاسات دي بالتوازي (xUnit بيشغّل كلاسات مختلفة بالتوازي افتراضيًا) بيحمّل السيرفر لحد ما بعض الطلبات بتفشل بـ Timeout أو تزاحم قفل — وده **موثّق بالفعل** كتعليق داخل `AccountingApiFactory.cs` نفسه:

> "Every test class builds its own database and runs the whole migration chain; a dozen classes doing that at once on one LocalDB instance goes well past the 30-second default, so the tests wait rather than fail on a timeout that says nothing about the code."

### الأثر

**مفيش أثر على صحة الكود** — أي فشل من النوع ده لازم يتأكد بإعادة التشغيل قبل ما يتاخد كدليل على مشكلة حقيقية. لو الفشل اتكرر في نفس الاختبار بالظبط في تشغيلتين متتاليتين، وقتها يبقى Bug حقيقي يستاهل تحقيق.

---

## 🟡 Pre-existing — `SettingsPermissionsPhase3` بتفشل بـ Timeout على قاعدة بيانات جديدة فاضية

**التاريخ**: 2026-09-26 (اتكتشف أثناء تطبيق Migration بتاع HR Core Batch B1، مش ناتج عنها)
**المكان**: Migration `20260919080215_SettingsPermissionsPhase3.cs`
**الخطورة**: 🟡 متوسطة — بتفشل، لكن ليها حل بديل بسيط (Config)، ومحدش هيصطدم بيها غير أول مرة يطبّق فيها كل تاريخ الـ Migrations من الصفر على قاعدة فاضية تمامًا (بيئة جديدة، أو قاعدة تجريبية).

### الوصف

تطبيق سلسلة الـ Migrations كاملة (من البداية) على أي قاعدة بيانات فاضية جديدة — زي `HabbakErp_Trial_HrCoreLookupsB1` أثناء التحضير لتطبيق Migration الـ HR Core الجديدة — بيفشل عند `SettingsPermissionsPhase3` بالضبط برسالة:

```
Failed executing DbCommand (35,018ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
...
Cannot release the application lock (Database Principal: 'public', Resource: '__EFMigrationsLock')
because it is not currently held.
```

الأمر بياخد **35 ثانية**، وأكتر من الـ `CommandTimeout` الافتراضي (30 ثانية) بشوية بس — فبيفشل بالظبط عند الحد.

### السبب

`SettingsPermissionsPhase3` فيها خطوة تنظيف بيانات (تصفير أي `CreatedBy`/`UpdatedBy`/`DeletedBy` بيشاور على `User` مش موجود) بتُنفَّذ عن طريق SQL ديناميكي بيُبنى وقت التشغيل بمسح `sys.columns`/`sys.tables` **لكل جداول قاعدة البيانات**:

```sql
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'UPDATE [' + t.name + N'] SET [' + c.name + N'] = 0 WHERE ...'
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
WHERE (c.name IN (N'CreatedBy', N'UpdatedBy', N'DeletedBy') AND ...)
   OR (t.name = N'AccountingPeriods' AND c.name = N'ClosedByUserId')
   -- ... إلخ
EXEC sp_executesql @sql;
```

الوقت اللي بياخده الاستعلام ده **بيكبر مع عدد جداول/أعمدة قاعدة البيانات نفسها** (O(n) على `sys.columns`/`sys.tables`)، مش على حجم البيانات — يعني كل ما المشروع يكبر (جداول جديدة زي HR Core الأخيرة)، الوقت المطلوب بيزيد أكتر، والمشكلة هتتكرر حتى لو حُلّت دلوقتي بمجرد Config، لحد ما تتحل بشكل دائم.

### التأثير الفعلي

**بس على تطبيق كامل سلسلة الـ Migrations على قاعدة فاضية من الصفر** (بيئة جديدة تمامًا، أو قاعدة تجريبية بتتعمل خصيصًا لاختبار Migration جديدة قبل تطبيقها على `HabbakErp` — نفس أسلوب Remarks7). **مش بيأثر على `HabbakErp` نفسها** (فيها كل الـ Migrations القديمة مطبّقة بالفعل، فمش بتعيد تشغيل `SettingsPermissionsPhase3` تاني).

### الحل المستخدم دلوقتي (Mitigation)

تمرير `Command Timeout=180` صريح في الـ Connection String وقت `dotnet ef database update`:
```
Server=...;Database=...;Command Timeout=180;
```
نجح تمامًا بعدها — الأمر أخد حوالي 35-40 ثانية زي المتوقع، وباقي السلسلة كملت من غير مشاكل.

### الحل الدائم (مؤجّل — لسه ماتعملش)

Migration جديدة بتستبدل الـ SQL الديناميكي بحل ثابت (مثلًا: قايمة الجداول/الأعمدة المطلوبة مكتوبة صراحةً في الكود بدل ما تتبنى من `sys.columns` وقت التشغيل)، أو نقل الخطوة دي لـ Job منفصل بره سلسلة الـ Migrations العادية. **برّه نطاق أي خطة حالية** — يتسجل هنا كـ Follow-up لما حد يقرر يتعامل معاه.

---

## 🔴 اتصلح — EF Core بيتجاهل الـ Protector المختلف لكل `AppDbContext` ويشارك موديل واحد بين الكل (Batch B2)

**التاريخ**: 2026-09-26 (اتكتشف أثناء الـ Regression الكامل بعد B2، مش أثناء الاختبار المعزول)
**المكان**: `src/Habbak.ERP.Infrastructure/Persistence/AppDbContext.cs`
**الخطورة**: 🔴 عالية وقت الاكتشاف — كانت ممكن تسيب تشفير `EmployeePersonalData.NationalIdEncrypted`/`BankIbanEncrypted` معطّل بصمت في أي بيئة فيها أكتر من `AppDbContext` واحد شغال بـ Protector مختلف في نفس الـ Process (كل بيئة اختبار، فعليًا).

### الوصف

`OnModelCreating` بيبني الـ `EncryptedStringConverter` بتاع `EmployeePersonalData` باستخدام `ISecretProtector` المحقون في الـ Constructor — لكن **EF Core بيعمل Cache للموديل المُجمَّع حسب نوع الـ `DbContext` بس افتراضيًا** (`ModelCacheKeyFactory`)، من غير أي اعتبار لأي Constructor Parameter. يعني: أول `AppDbContext` يتبني في الـ Process — سواء كان معاه Protector حقيقي، أو الـ `NoOpSecretProtector` الاحتياطي (اللي بيستخدمه أغلب الـ 380 اختبار قديم اللي بتبني `AppDbContext` مباشرة من غير DI) — بيفوز بالموديل المخزّن Cache لكل الـ Instances التانية بعد كده، **بغض النظر عن الـ Protector الفعلي بتاعهم هم**.

أول تشغيل كامل لـ `IntegrationTests` بعد B2 طلّع **138/139** (فشل واحد بس): اختبار التشفير نفسه (اللي عدّى لوحده) فشل لما اتشغّل جوه السويت الكامل، لأن مئات الاختبارات القديمة بنت `AppDbContext` من غير Protector قبل اختبار B2 — فالموديل المخزّن كان بالـ `NoOpSecretProtector`، والرقم القومي اتخزّن نص صريح غير مشفّر.

### التأثير الفعلي

**الإنتاج مش متأثر** — فيه Instance واحد Singleton بس للـ `"HR.PII"` Protector طول عمر التطبيق (`DependencyInjection.cs`)، فمفيش تضارب. لكن التصميم كان هش لأي سيناريو فيه أكتر من `AppDbContext` بـ Protector مختلف في نفس الـ Process — وده بالظبط شكل أي بيئة اختبار.

### الحل

`IModelCacheKeyFactory` مخصص (`PiiProtectorModelCacheKeyFactory`، Nested Class جوه `AppDbContext`) بيضيف الـ Protector Instance نفسه كجزء من مفتاح الـ Cache — كل Protector مختلف (حتى لو Real واتنين Real) بياخد Model منفصل. اتوصّل عن طريق `OnConfiguring` باستخدام `optionsBuilder.ReplaceService<IModelCacheKeyFactory, ...>()`. ضيف اختبار Regression مخصص (`Encryption_still_applies_even_when_a_no_protector_context_built_the_model_first`) بيبني Context من غير Protector عمدًا الأول، وبعدين يتأكد إن التشفير لسه شغّال.

### الدرس العام

**أي `DbContext` بيبني جزء من الـ Model بناءً على قيمة من الـ Constructor (Converter، Filter، إلخ) لازم `IModelCacheKeyFactory` مخصص يعكس نفس الاختلاف ده** — الافتراضي بيكاش حسب النوع بس، وده بيفترض إن `OnModelCreating` نفس النتيجة دايمًا لأي Instance من نفس النوع.

---

## 🟡 اتصلح — `SystemDataSeeder` بيزرع `MenuItems` مرة واحدة بس، فشاشة جديدة بعد أول Seed ما توصلش للـ Real DB أبدًا (Batch B5)

**التاريخ**: 2026-09-26 (اتكتشف أثناء اختبار Reveal PII في B5 — محاولة منح صلاحية HR_EMPLOYEES لـ Role فشلت بـ `SET-SCREEN-UNKNOWN`)
**المكان**: `src/Habbak.ERP.Infrastructure/Persistence/Seeding/SystemDataSeeder.cs`
**الخطورة**: 🟡 كانت متوسطة وقت الاكتشاف، طلعت خطيرة أكتر بعد الفحص — أثّرت على **9 شاشات** عبر Batches B1 لحد B5، مش شاشة واحدة.

### الوصف

الزرع القديم كان: `if (!await db.MenuItems.AnyAsync()) { db.MenuItems.AddRange(MenuItemSeedData.Build()); ... }` — يعني **يزرع مرة واحدة بس لو الجدول فاضي بالكامل**. بما إن `HabbakErp` الحقيقية كان فيها صفوف `MenuItems` من زمان قبل Batch B1 (شاشات Settings/Security)، أي شاشة جديدة اتضافت لـ `MenuItemSeedData.Build()` من B1 لحد B5 **ما وصلتش للـ Real DB خالص** — اتأكد بالفحص المباشر: مجموعة "HR" نفسها + 6 شاشات تحتها + `SETTINGS_COUNTRIES`/`SETTINGS_CITIES`/`SETTINGS_BANKS` (9 شاشات في المجموع) كانوا **مش موجودين خالص** في `HabbakErp`، بس موجودين في قواعد الاختبار (اللي بتتعمل فاضية تمامًا فبتاخد الزرع الكامل من أول مرة).

### التأثير الفعلي

أي محاولة لمنح Role صلاحية على أي من الـ 9 شاشات دي عن طريق `PUT /api/v1/settings/roles/{id}/screens` كانت هتفشل بـ `BusinessRuleException("SET-SCREEN-UNKNOWN", ...)` — يعني **مفيش Role غير الـ SuperAdmin (Full Access Bypass) يقدر يشوف/يعدّل بيانات الشاشات دي فعليًا على الإنتاج**، طول ما الفحص ده ما اتعملش.

### الحل

1. `SystemDataSeeder` بقى بيزرع **كل عنصر ناقص لوحده** (مقارنة بالـ Code، مش بس "الجدول فاضي ولا لأ") — Pass اتنين: المجموعات (Parent) الأول، بعدين العناصر الفرعية بمرجع Parent ID حقيقي (سواء كان موجود قبل كده أو اتزرع لسه في Pass 1.
2. زرع الـ 9 صفوف الناقصة يدويًا في `HabbakErp` الحقيقية (Script مؤقت اتشال بعد التنفيذ)، بنفس القيم من `MenuItemSeedData.Build()` بالظبط.

### الدرس العام

**"يزرع مرة واحدة لو الجدول فاضي" غلط لأي Seed بيتوسّع بمرور الوقت** — لو قاعدة حقيقية بدأت بصفوف من زمان، أي إضافة لاحقة للـ Seed Data مش هتوصلها أبدًا من غير فحص لكل عنصر لوحده.

---

## 🟡 اتصلح — `ButtonPermissionCatalog.FallbackAction` من غير `[ScreenAction]` صريح مبيتفحصش فعليًا (Batch B5)

**التاريخ**: 2026-09-26 (اتكتشف أثناء اختبار Reveal PII في B5)
**المكان**: `src/Habbak.ERP.API/Auth/ScreenPermissionFilter.cs` (منطق `Required()`) + `src/Habbak.ERP.API/Controllers/HR/HrPiiController.cs`
**الخطورة**: 🟡 متوسطة — سلوك مش متوقع، مش ثغرة أمنية (كان بيرفض بصلاحية أوسع مش أضيق).

### الوصف

`ButtonDefinition.FallbackAction` (في `ButtonPermissionCatalog`) بيوثّق "الصلاحية اللي الزرار محتاجها لو الـ Role مالوش صف `ButtonPermission` صريح ليه" — لكن `ScreenPermissionFilter.Required()` **مبيقراش القيمة دي خالص وقت التشغيل**. الصلاحية المطلوبة بتتحسب بس من الـ HTTP Verb + آخر جزء في الـ Route (`ApproveSegments` Set ثابتة زي "approve"، "reject"، "cancel"...). لو الـ Action مالوش `[ScreenAction(...)]` صريح، والـ Route Segment بتاعه مش من ضمن `ApproveSegments`، الفحص بيرجع لـ Default حسب الـ HTTP Verb (POST لغير الـ Root = `Edit` مثلًا) — **مش الـ `FallbackAction` المكتوب في الـ Catalog**.

اتكشف لما `HrPiiController.Reveal` (Route: `POST .../pii/reveal`، `FallbackAction: Approve`) اتفحص: الـ Segment "reveal" مش في `ApproveSegments`، فالفحص الفعلي كان بيطلب `Edit` بدل `Approve` — يعني أي Role عنده `canEdit` على `HR_EMPLOYEES` كان هيقدر يكشف PII من غير أي صلاحية إضافية صريحة، عكس القصد الأصلي.

### الحل

إضافة `[ScreenAction(ScreenAction.Approve)]` صريح على الـ Action نفسه، عشان يتطابق فعليًا مع `FallbackAction` المكتوب في الـ Catalog.

### الدرس العام

**`FallbackAction` في `ButtonPermissionCatalog` توثيق بس، مش قيمة بتتفحص تلقائيًا** — أي `[ScreenButton]` جديد بصلاحية أعلى من افتراضي الـ Route/Verb لازم `[ScreenAction(...)]` صريح يتحط بجانبه، وإلا الفحص الفعلي هيرجع لأضعف صلاحية (حسب الـ Verb) من غير أي تحذير.

---

## 🟢 اتصلح — `TerminateEmployeeCommand` (B5) كان بيوقف الـ `User` من غير ما يسحب جلساته

**التاريخ**: 2026-09-27 (اتكتشف أثناء مراجعة B7 — مقارنة `HR-Core-Plan.md §1.4` بالكود الفعلي؛ **اتصلح وتأكّد أثناء Phase 1.4 Research Pass، `Phase-1.4-Research.md §1`** — الكود بينادي `RevokeSessionsAsync` بالفعل دلوقتي، ومعاه اختبار مخصص `EmployeeBatch5Tests.TerminateEmployee_revokes_the_linked_user_active_refresh_token_session`)
**المكان**: `src/Habbak.ERP.Application/HR/Employees/Commands/TerminateEmployee/TerminateEmployeeCommand.cs`
**الخطورة (وقت الاكتشاف)**: 🔴 عالية أمنيًا — موظف اتعمله إنهاء خدمة ممكن يفضل جلسته (Refresh Token) شغّالة لحد ما تنتهي طبيعيًا، رغم إن حسابه بقى `Suspended`.

### الوصف

`HR-Core-Plan.md §1.4` كان واضح إن إنهاء الخدمة المفروض "يستدعي منطق تعطيل مشابه لـ `UpdateUserCommandHandler`... دي بالظبط النقطة اللي إنهاء الخدمة هيستدعيها، مش هيعيد بناءها" — وبالتحديد `UserRules.RevokeSessionsAsync(db, user.Id, "UserSuspended", ct)` (`UserCommands.cs:228-231`)، اللي بينفَّذ **كل مرة** الحالة بتتغيّر لـ `Suspended` عن طريق `UpdateUserCommandHandler`.

`TerminateEmployeeCommandHandler` (Batch B5) بيغيّر `user.Status = UserStatus.Suspended` **مباشرة على الـ Entity**، من غير ما يمر بـ `UpdateUserCommandHandler` ولا ينادي `RevokeSessionsAsync` بنفسه — يعني السطر ده بالذات من الخطة الأصلية اتفات أثناء تنفيذ B5.

### التأثير الفعلي

أي `RefreshToken` نشط للموظف وقت إنهاء الخدمة يفضل صالح لحد ما ينتهي بمدته الطبيعية أو حد يسحبه يدويًا — الحساب بقى `Suspended` (فمينفعش يعمل Login جديد)، لكن أي جلسة **موجودة بالفعل** ممكن تفضل شغّالة.

### الحل

`await UserRules.RevokeSessionsAsync(db, user.Id, "EmployeeTerminated", cancellationToken);` موجود بالفعل جوه `TerminateEmployeeCommandHandler` (نفس الشرط `employee.UserId is not null` اللي بيغيّر فيه حالة الـ `User`). **مش معروف بالضبط إمتى انصلح** (بين توثيق B7 وبداية Phase 1.4) — المهم إنه اتأكد إنه شغّال دلوقتي بفحص مباشر للكود + اختبار ناجح، مش افتراض.

---

## 🟢 اتصلح — الـ Full ApiTests Suite بياخد 31+ دقيقة بدل 15-20 (Batch B6 → B7)

**التاريخ**: 2026-09-27 (لوحظ أثناء الـ Regression الكامل بعد B6)
**المكان**: `Habbak.ERP.ApiTests/` (توازي xUnit) + `Habbak.ERP.IntegrationTests/`
**الخطورة**: 🟢 أداء بس، مش صحة — كل الاختبارات كانت بتعدّي، بس الوقت والـ Memory (وصلت 97% + Swap) كانوا بيكبروا مع كل Batch جديد بيضيف اختبارات.

### الوصف

كل اختبار في `Habbak.ERP.ApiTests` (260 اختبار وقت B7) بيعمل قاعدة LocalDB منفصلة بالكامل وبيشغّل عليها كل الـ Migrations من الصفر (`AccountingApiFactory`). xUnit بشكل افتراضي بيوازي تشغيل الكلاسات المختلفة **من غير حد أقصى واضح على عدد الـ Threads** — يعني مع كل Batch جديد بيضيف اختبارات (زي B1-B6 كلهم)، عدد قواعد الـ LocalDB الشغّالة في نفس اللحظة بيكبر، لحد ما الـ Memory وصلت 97% + Swap، وده زوّد وقت التنفيذ الكلي (31 د 35 ث) رغم إن كل اختبار لوحده سريع.

### الحل

`xunit.runner.json` في كل من `Habbak.ERP.ApiTests/` و`Habbak.ERP.IntegrationTests/` (مربوط بالـ`.csproj` عن طريق `<None Update="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />`):
```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4,
  "parallelizeAssembly": false
}
```

### النتيجة

| Suite | قبل | بعد | التغيّر |
|---|---|---|---|
| ApiTests (260 اختبار) | 31 د 35 ث | **11 د 9 ث** | **-65%** |
| IntegrationTests (171 اختبار) | ~8-9 دقيقة | 8 د 52 ث | تقريبًا زي ما هو — مكانتش عندها نفس المشكلة أصلًا |

مفيش تراجع في النتائج — كل الـ 431 اختبار (171+260) عدّوا بنجاح كامل قبل وبعد.

### الدرس العام

**أي Test Project بيعمل موارد خارجية فعلية لكل اختبار (قاعدة بيانات، اتصال شبكة، إلخ) محتاج حد أقصى صريح على التوازي** — الافتراضي (بدون حد) بيفترض إن الاختبارات "خفيفة"، وده مش صحيح لما كل اختبار بيعمل LocalDB Instance كامل بمهاجرة Migrations كاملة. الحد الأمثل (4 هنا) اتحدد تجريبيًا حسب موارد الجهاز — مش رقم عام يتنسخ لأي بيئة تانية من غير قياس.

---

## 🟢 ملاحظة جودة بيانات — بيانات اختبار E2E متسربة لجدول `CustodyOfficers` الحقيقي

**التاريخ**: 2026-09-27 (اتكتشف أثناء Phase 1.2، Research Pass — فحص مباشر لبيانات `HabbakErp`)
**المكان**: جدول `CustodyOfficers` (`HabbakErp`)
**الخطورة**: 🟢 منخفضة — صفر تأثير وظيفي، مجرد بيانات وصفية غير حقيقية في صف واحد.

### الوصف

صف `CustodyOfficers.Code = 'E2E-CO-MAADI'` (الاسم: "Mahmoud - Maadi Custody Officer") موجود في `HabbakErp` الحقيقية — نمط التسمية مطابق لأسلوب Test Fixtures، مش بيانات تشغيل حقيقية أدخلها مستخدم. رجّح الفحص إنه بقي من تشغيل اختبار E2E وصل بالخطأ للقاعدة الحقيقية بدل قاعدة اختبار معزولة.

### التأثير

صفر — الصف موجود بس، مفيش أي كيان تاني (مثل `FixedAsset.CustodyOfficerId`) مربوط بيه فعليًا في البيانات الحالية.

### الحل

قرار مقصود (مش Bug): **توثيق بس، بدون أي حذف** — نفس مبدأ "مفيش صف بيتمسح" الحاكم لكل التعامل مع `HabbakErp`. التنظيف (لو تقرر لاحقًا) يبقى Batch منفصل خارج نطاق Phase 1.2 تمامًا.

---

## 🟡 فجوة Frontend — لا توجد شاشة `CustodyRegister` (عهد الحسابات) في الـ Frontend خالص

**التاريخ**: 2026-09-27 (اتكتشف أثناء Phase 1.2، Research Pass)
**المكان**: `frontend/src/` — صفر نتائج بحث عن أي ملف باسم `CustodyRegister`
**الخطورة**: 🟡 متوسطة — فجوة في موديول الحسابات نفسه، سابقة على HR ومش ناتجة عنه.

### الوصف

الـ Backend كامل وموجود (`src/Habbak.ERP.API/Controllers/Accounting/CustodyRegistersController.cs` + الـ Application/Domain layers)، لكن **مفيش أي شاشة Frontend مقابلة** — الشاشة الوحيدة الموجودة فعليًا باسم مشابه (`CustodyOfficersListPage.tsx`) هي لكيان مختلف تمامًا (`CustodyOfficer`، مسؤول عهدة نقل مخزني في موديول المخازن)، مش `CustodyRegister` (عهدة نقدية، موديول الحسابات).

### التأثير على HR

Phase 1.2 (HR-MASTER-PLAN.md §Phase 1.2, sub-batch 1.2.4) كان المفروض يحط Banner تحذيري على "شاشة العهد الحالية" — بما إنها مش موجودة، القرار كان بناء [`CustodyMigrationBanner.tsx`](../../frontend/src/features/hr/components/CustodyMigrationBanner.tsx) كـ Component معزول جاهز (بياخد `count`/`onMigrate` كـ Props) بدون ربطه بأي شاشة دلوقتي — TODO صريح جوه الملف نفسه يوضح إنه لازم يترص فوق شاشة `CustodyRegister` أول ما تُبنى.

### الحل

خارج نطاق Phase 1.2 — يحتاج Batch منفصل لبناء شاشة `CustodyRegister` الكاملة في موديول الحسابات (مش HR)، وقتها الـ Banner الجاهز يتربط في دقيقتين.

---

## 🟡 لسه مفتوحة — `PostVoucherCommand` مفيهوش أي كتابة لـ`AuditLog`

**التاريخ**: 2026-09-27 (اتكتشف أثناء Phase 1.3، Sub-Batch 1.3.3 — كتابة اختبارات ترحيل سند بطرف Employee)
**المكان**: `src/Habbak.ERP.Application/Accounting/Vouchers/Commands/PostVoucher/PostVoucherCommand.cs`
**الخطورة**: 🟡 متوسطة — فقدان تدقيق، مش خطأ في صحة البيانات المحاسبية نفسها.

### الوصف

`PostVoucherCommandHandler` مفيهوش أي كتابة لجدول `AuditLog` خالص — لا لسند بطرف `Employee` (المضاف في Phase 1.3)، ولا لـ`Customer`، ولا لـ`Supplier`، ولا لأي نوع طرف تاني. الفحص أثناء كتابة `EmployeeCounterpartyPostingTests.cs` أكّد إن الملف كامل ماعندوش أي استدعاء لـ`AuditLog`/`IAuditLogger` أو ما شابه.

### السبب

`AuditLog` في المشروع ده بيتكتب **صراحة بس في أماكن محددة** (زي `HrPiiController.Reveal` وقت كشف PII) — **مش تلقائي لكل عملية حساسة**. ترحيل السندات (Receipt/Payment) لسه ما اتضافلوش تسجيل تدقيق صريح من الأساس، من قبل Phase 1.3 بكتير.

### التأثير الفعلي

فقدان أثر تدقيق (Who/When) لعمليات ترحيل السندات المحاسبية — القيد نفسه واتزانه وحالته كلهم صح 100%، بس مفيش سجل منفصل يوثّق "مين رحّل السند ده وإمتى" غير `UpdatedBy`/`UpdatedAtUtc` العامين على الكيان نفسه (`AuditSaveChangesInterceptor`).

### هل محتاج إصلاح؟

قرار مؤجّل — **مش من نطاق Phase 1.3** (إضافته دلوقتي لطرف Employee بس هيكون تغيير سلوك جزئي/غير متسق، لأن باقي الأطراف هتفضل من غيره). **الحل المقترح**: Research Pass منفصل بعد محرك الاعتمادات المركزي (Phase 2) — إضافة عامة تغطي كل أنواع الأطراف مرة واحدة، مش لـ Employee بس.

**المرحلة المقترحة**: Phase 2+ (بعد محرك الاعتمادات).

---

## 🟡 لسه مفتوحة — Bug-005: API DateTime بدون UTC Marker

**التاريخ**: 2026-09-27 (اتسجّل قبل بداية Phase 3)
**الخطورة**: 🟡 متوسطة

### الوصف

API يرجّع حقول `*AtUtc` من غير `Z` (UTC Marker) → الـ Frontend بيفهمها Local Time بدل UTC.

### التأثير

تواريخ نسبية (relative time) غلط في كل الشاشات اللي بتعرض أي حقل `*AtUtc`.

### الحل

- **Local Fix (اتعمل)**: `relativeTime.ts` (Phase 2.5).
- **Root Fix (لسه لأ)**: توحيد Json/EF Options على مستوى الـ API كله (Task منفصل).

### الأولوية

🟡 — يتحل قبل Phase 6 (ESS).

---

## 🟡 لسه مفتوحة — مفيش Endpoint لحذف Attachment

**التاريخ**: 2026-09-28 (اتسجّل أثناء Local Verification لـ Phase 3C)
**المكان**: `src/Habbak.ERP.API/Controllers/Common/AttachmentsController.cs` (أو المكافئ) — الآلية العامة للمرفقات
**الخطورة**: 🟡 متوسطة — صفوف يتيمة بس، صفر تأثير وظيفي أو أمني.

### الوصف

الآلية العامة للمرفقات (`POST /attachments` للرفع، `GET /attachments/{id}/content` للتنزيل) مفيهاش أي `DELETE /attachments/{id}`. لما شاشة تفك ارتباط مرفق موجود بسجل (مثلًا `SetEmploymentContractAttachmentCommand`/`SetEmployeeCertificationAttachmentCommand` من Phase 3C بقيمة `AttachmentId = null`)، صف `Attachment` نفسه (والملف المخزَّن جوه `Content` بصيغة `varbinary(max)`) **بيفضل موجود في الجدول كصف يتيم (Orphaned)** — مفيش أي كيان تاني بيشاور عليه، ومفيش طريقة نظيفة لحذفه غير SQL مباشر.

### إزاي اتكشفت

أثناء التحقق اليدوي لـ Phase 3C (رفع/فك ارتباط مرفقات تجريبية على عقد وشهادة حقيقيين لموظف E1) — بعد فك الارتباط عن طريق `SetAttachment(..., null)`، صفوف `Attachments` الأصلية اتسابت عمدًا في الجدول (Orphaned) لأنه مفيش Endpoint رسمي لحذفها، وحذفها بـ SQL مباشر كان هيحتاج معرفة كل تبعياتها بدون ضمان — أخطر من إبقائها كصفوف يتيمة غير مؤذية.

### التأثير الفعلي

صفر تأثير وظيفي أو أمني — الصفوف اليتيمة مش مربوطة بأي سجل، ومش بتظهر لأي مستخدم. التأثير الوحيد هو تراكم بيانات غير مستخدَمة (Storage) بمرور الوقت مع أي استخدام متكرر لميزات "استبدال مرفق" (زي `AttachmentCell.tsx` الجديدة في Phase 3C) أو حذف سجلات بتاعة مرفقات.

### الحل المطلوب (Follow-up — لسه ماتعملش)

`DELETE /attachments/{id}` Endpoint جديد — يحتاج قرار تصميم: حذف فعلي فوري، أو فحص عدم وجود أي مرجع (Scalar FK زي `EmploymentContract.AttachmentId` أو Polymorphic `EntityType`/`EntityId`) قبل السماح بالحذف، لتفادي كسر أي سجل لسه بيستخدم المرفق.

### الأولوية

🟡 — يتحل في Phase 6 (ESS) أو Phase 7، مش عاجل.

---

## 🟡 لسه مفتوحة — `ApprovalWorkflow` مايدعمش خطوات شرطية (Multi-step Conditional)

**التاريخ**: 2026-09-28 (اتكشفت أثناء بناء Phase 4.4 — محرك الرواتب)
**المكان**: `ApprovalWorkflowAssignment`/`ApprovalWorkflowService` (Phase 2، `src/Habbak.ERP.Application/Approvals/`)
**الخطورة**: 🟡 متوسطة — بديل عملي شغّال دلوقتي، بس مش الحل المعماري الصحيح.

### الوصف

محرك الاعتمادات (Phase 2) بيدعم **سلسلة `ApprovalWorkflow` واحدة بس لكل `Screen`** (فهرس فريد مفلتر بيمنع أكتر من `ApprovalWorkflowAssignment` نشط لكل شاشة، `AssignWorkflowToScreenCommand.cs`). `ApprovalWorkflowAssignment.MinAmount` بيدعم عتبة مبلغ، بس بشكل ثنائي بس: تحت العتبة = مفيش سلسلة خالص (اعتماد مباشر)، فوق العتبة = تشتغل السلسلة المُسكَّنة (بعدد خطواتها الثابت). **مفيش آلية لـ"عدد خطوات مختلف حسب المبلغ لنفس الشاشة"** (زي "سلفة تحت 5000 = خطوتين، وفوقها = 3 خطوات" أو "إضافي عادي = خطوة، وفوق الحد الشهري = خطوتين").

### إزاي اتكشفت

Phase 4.4 (قاعدة 20: خطوة اعتماد إضافية على `OvertimeRequest` لو تجاوز الحد اليومي/الشهري) حاولت تستخدم نفس الآلية، واتأكد إن الفهرس الفريد بيمنع تسكين سلسلتين (واحدة قصيرة وواحدة طويلة) لنفس الشاشة `HR_OVERTIME` في نفس الوقت.

### الحل المؤقت المطبَّق (Phase 4.4)

`OvertimeRequest.ExceedsLimit` + `HrOverrideApprovedByUserId` — علامة على الكيان نفسه، مش خطوة `ApprovalWorkflow` حقيقية. تشغيل الرواتب (`PayrollCalculationService`) بيتجاهل أي طلب إضافي متجاوز من غير Override صريح من HR. شغّال وظيفيًا، بس مش نفس نمط باقي المحرك (تسكين إداري من شاشة، مش كود مخصص لكل حالة).

### التأثير على مراحل قادمة

Phase 5 (سلفة، مكافأة) محتاجة نفس الآلية بالظبط حسب مصفوفة الاعتماد في `10-Module-HR-Payroll.md §4.8` (صفوف 2 و7: "+خطوة لو ≥ حد"). لو الحل المؤقت اتكرر لكل حالة (سلفة، مكافأة، إضافي)، هيبقى 3 حلول مخصصة منفصلة بدل حل معماري واحد.

### الحل المطلوب (Follow-up — لسه ماتعملش)

توسيع `ApprovalWorkflowAssignment` عشان يدعم أكتر من سلسلة لنفس الشاشة بعتبات متدرجة (Tiered)، أو إضافة مفهوم "خطوة شرطية" (`ApprovalWorkflowStep.MinAmount` بدل `ApprovalWorkflowAssignment.MinAmount` بس) — قرار معماري يحتاج مراجعة قبل التنفيذ، ومؤثر على أكتر من شاشة.

### الأولوية

🟡 — يتحل في Phase 5 أو Phase 6، قبل ما عدد الحلول المؤقتة يزيد.
