# Bug-004 — `DesignTimeDbContextFactory` بيتجاهل أي Connection String تاني (Migration ممكن تروح لقاعدة غلط بصمت)

> **التاريخ**: 2026-09-25
> **المصدر**: اختبار Migration بتاعة Remarks7 على قاعدة بيانات حقيقية
> **الخطورة**: 🔴 عالية
> **الحالة**: مفتوح

---

## 1. الوصف

**السيناريو اللي كشف المشكلة**:
1. أثناء تجربة Migration Remarks7 على نسخة تجريبية (`HabbakErp_Trial_Remarks7`)، اتعمل:
   ```bash
   ConnectionStrings__Default="Server=...;Database=HabbakErp_Trial_Remarks7;..." \
     dotnet ef database update --project ... --startup-project ...
   ```
2. الأمر طلّع `Applying migration '20260925141556_PurchaseOrderRequestLinking'. Done.` — نجاح ظاهري كامل.
3. **لما اتأكدت من `HabbakErp_Trial_Remarks7` مالقتش أي تغيير خالص** — الأعمدة الجديدة مش موجودة.
4. تتبّعت المشكلة: الـ Migration فعليًا اتطبّقت على `(localdb)\mssqllocaldb` → `HabbakErp` — **قاعدة تالتة معزولة تمامًا**، مش الـ Trial ومش الـ Real Dev DB.

---

## 2. السبب الجذري

`src/Habbak.ERP.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`:

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

Connection String **مكتوب جوه الكود نفسه** (Hardcoded). لما `IDesignTimeDbContextFactory` موجود في الـ Assembly، `dotnet ef` **بيفضّله دايمًا** على أي طريقة تانية لبناء الـ DbContext (زي بناء الـ Host الحقيقي من `Program.cs` اللي بيقرا `appsettings.json`/Environment Variables بشكل طبيعي). النتيجة:

- `ConnectionStrings__Default` كـ Environment Variable → **بيتجاهله تمامًا**.
- تعديل `appsettings.Development.json` → **بيتجاهله برضه** (الـ Factory مش بيقرا منه أصلًا).
- الطريقة الوحيدة الشغالة: تمرير `--connection "..."` صريح مع كل أمر `dotnet ef database update` أو `dotnet ef migrations add`.

---

## 3. الأثر

| البُعد | التفصيل |
|---|---|
| **الأداء** | 🔴 خطر مباشر — Migration ممكن "تنجح" وهي فعليًا ماجتش القاعدة المقصودة |
| **التكرار** | أي حد يشغّل `dotnet ef database update` من غير `--connection` صريح |
| **التأثير المالي** | ممكن يبقى حرج — لو حد فكّر إنه طبّق Migration على قاعدة Staging/Production وهي فعليًا اتطبّقت على `(localdb)` بس، هيكتشف متأخر إن القاعدة الحقيقية لسه على السكيما القديمة |
| **Workaround** | تمرير `--connection "<real connection string>"` صريح مع كل أمر `database update` |

---

## 4. الأثر الفعلي في جلسة Remarks7

**مفيش ضرر حقيقي حصل** — بفضل التحقق اليدوي بعد كل خطوة (Verification Queries):
1. اتأكد إن `HabbakErp` الحقيقية (`DESKTOP-DBL395G\MSSQLSERVER01`) مالمسهاش حاجة قبل أي محاولة تانية.
2. اتصلح باستخدام `--connection` صريح، ونجح صح على `HabbakErp_Trial_Remarks7` الأول، وبعدين على `HabbakErp` الحقيقية.
3. القاعدة المعزولة العرضية (`(localdb)\mssqllocaldb → HabbakErp`) اتمسحت بعد التأكد إنها مش مستخدمة من حد.

**لولا التحقق اليدوي، كان سهل جدًا حد يفتكر إن الـ Migration خلصت صح وهي فعليًا ماجتش القاعدة المقصودة.**

---

## 5. الحلول المقترحة

| # | الحل | الأولوية | التعقيد |
|---|---|---|---|
| 1 | شيل `DesignTimeDbContextFactory` خالص — سيب `dotnet ef` يبني الـ Host من `Program.cs` مباشرة (بيقرا الإعدادات الحقيقية تلقائيًا، وده السلوك الافتراضي في مشاريع ASP.NET Core العادية) | 🔴 الأفضل | 🟢 بسيط |
| 2 | لو فيه سبب حقيقي للـ Factory (مثلاً تشغيل `migrations add` من بيئة معندهاش Connection حقيقي) — يقرا الـ Connection String من `appsettings.Development.json` أو Environment Variable بدل ما يكون مكتوب في الكود | 🟡 بديل | 🟡 متوسط |

### الاقتراح النهائي

الحل 1 — شيل الـ Factory. الـ Comment الموجود فوقه بيقول إنه "Lets `dotnet ef migrations add` build the model without a running API host or a real connection" — بس ده مش صحيح عمليًا لـ `database update` (اللي فعلًا محتاج Connection حقيقي)، وأي مشروع فيه `IDesignTimeDbContextFactory` بيقرا Connection ثابت بيبقى مصدر لبس دايم. لو فيه سبب تاريخي (مثلاً `migrations add` كان بيفشل من غير Factory)، الأفضل نفهم السبب ده الأول قبل الشيل.

---

## 6. الاختبارات المطلوبة (بعد الحل)

1. `dotnet ef migrations add <name>` من غير `--connection` → لازم يشتغل زي ما هو دلوقتي (من غير Factory، هيحتاج Host حقيقي — لازم نتأكد إنه لسه شغال في بيئة التطوير العادية).
2. `dotnet ef database update` من غير `--connection` → لازم يستخدم `ConnectionStrings:Default` من `appsettings.Development.json`/Environment، مش أي Hardcoded value.
3. توثيق في `README`/دليل المطورين: إزاي تشغّل Migration على قاعدة مختلفة عن الافتراضية (لو الحل المختار هو 2).
