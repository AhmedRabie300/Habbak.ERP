# معمارية محرك الترحيل المحاسبي (Posting Engine Architecture)

> هذا الملف يتبع القالب الموحّد المحدد في `00-Project-Overview.md` (قسم 27)، ويطبّق كل الدروس المستفادة من موديولات الحسابات والمخازن والمشتريات والمبيعات ونقاط البيع. **هذا الملف يمثل التنفيذ الفعلي للقسم 11 من `00-Project-Overview.md`** (محرك الترحيل المحاسبي المركزي).

> **ملاحظة تسمية مقصودة**: هذا الملف **ليس موديول عمل برقم تسلسلي** (زي المبيعات أو المخازن) — هو **بنية تحتية عابرة لكل الموديولات**، بنفس طبيعة `00-Project-Overview.md` نفسه. لا يحمل رقم موديول، ولا يدخل في ترتيب أولوية تنفيذ الموديولات (`00-Project-Overview.md` قسم 28) كموديول مستقل — هو جزء من البنية التحتية اللي أي موديول (Sales, Purchasing, POS, Inventory) بيبنى فوقها من اليوم الأول.

> **حد الملف**: يغطي **كيفية إنشاء القيود المحاسبية آليًا من الموديولات الأخرى** — ولا يغطي **إدارة القيود اليدوية نفسها** (مسؤولية `01-Module-Accounting.md`).

---

## 1. الهدف

توفير **محرك مركزي موحّد** لإنشاء القيود المحاسبية الآلية من كل الموديولات، بحيث:
- **لا يوجد أي موديول ينشئ قيدًا بنفسه مباشرة** — كلهم يمرون عبر `IPostingService`.
- **القوالب قابلة للتخصيص** من شاشة إعدادات الشاشات بدون تدخل المطور — لكن **ضمن مجموعة صيغ ثابتة ومُختبَرة مسبقًا** (قسم 4)، مش تعبيرات نصية حرة يتم تفسيرها وقت التشغيل.
- **أي فشل في حل حساب أو مبلغ يوقف الترحيل بالكامل بخطأ صريح** — لا يوجد تجاهل صامت لأي سطر لأي سبب غير شرط موثّق صراحة (قسم 5).
- **الأبعاد التحليلية** تُحدَّد تلقائيًا حسب القالب، وتُلتزم بحد الـ5 أبعاد لكل حساب (`01-Module-Accounting.md` قاعدة 24).

---

## 2. المعمارية العامة (Architecture)

### 2.1 نظرة عامة

```
[موديول المصدر] ← المستند بالفعل خضع لسلسلة موافقاته الخاصة إن وُجدت (Sales/Purchasing/POS)
      ↓ ينشئ PostingRequest (يحمل IdempotencyKey)
[IPostingService.PostAsync(request)]
      ↓ يقرأ PostingTemplate المناسب (النسخة الحالية النشطة، قسم 5.4)
[PostingEvaluator]
      ↓ لكل سطر: تقييم الشرط (ثابت من Enum) → حل الحساب (فشل = توقف فوري) → حل المبلغ (فشل = توقف فوري)
      ↓ التحقق من التوازن الكامل
      ↓ التحقق من الحسابات القابلة للترحيل والأبعاد الإلزامية (نفس فحوصات 01-Module-Accounting.md)
      ↓ التحقق من الفترة المالية المفتوحة
      ↓ إنشاء JournalEntry (Status = Posted مباشرة — قاعدة 1) + JournalEntryLine + JournalEntryLineDimensionValue
      ↓ حفظ Snapshot القالب المُستخدَم (قسم 5.4)
[PostingResult] → يرجع لموديول المصدر (نجاح مع رقم القيد / فشل مع رسالة خطأ صريحة)
```

> **ملاحظة معمارية مهمة**: القيد الناتج هنا **لا يمر أبدًا بمرحلة اعتماد منفصلة على مستوى `IPostingService` نفسه** — راجع قسم 6.9 لتفصيل السبب والعلاقة بسلسلة موافقات المستند المصدر.

### 2.2 الـ Interface الأساسي

```csharp
public interface IPostingService
{
    Task<PostingResult> PostAsync(PostingRequest request, CancellationToken ct = default);
    Task<PostingResult> ReverseAsync(long journalEntryId, string reversalReason, CancellationToken ct = default);
}
```

### 2.3 `PostingRequest` — الطلب

```csharp
public class PostingRequest
{
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }

    // المصدر
    public string ScreenCode { get; set; }
    public string SourceDocumentType { get; set; }
    public long SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }

    // التاريخ
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; }

    // اختيار القالب
    public string? TemplateSelectorField { get; set; }
    public string? TemplateSelectorValue { get; set; }

    // السياق — يُستخدم من PostingEvaluator، لازم يحمل كل الحقول اللي أي سطر في القالب ممكن يحتاجها
    public Dictionary<string, object> Context { get; set; } = new();

    // Idempotency — إلزامي بدون استثناء (00-Project-Overview.md قسم 14.1)
    public Guid IdempotencyKey { get; set; }
}
```

### 2.4 `PostingResult` — النتيجة

```csharp
public class PostingResult
{
    public bool Success { get; set; }
    public long? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public string? ErrorMessage { get; set; }          // رسالة واضحة عند أي فشل حل حساب/مبلغ (قسم 5)
    public List<string> ValidationErrors { get; set; } = new();
}
```

---

## 3. الكيانات (Entities)

### 3.1 `PostingTemplate` — قالب الترحيل

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `ScreenCode` | `string` | ✅ | كود الشاشة المصدر (`SalesInvoice`, `POSInvoice`...) |
| `NameAr` / `NameEn` | `string` | ✅ | اسم القالب |
| `Description` | `string?` | ❌ | — |
| `TemplateSelectorField` / `TemplateSelectorValue` | `string?` | ❌ | لاختيار القالب الصحيح لو في أكتر من قالب لنفس الشاشة |
| `PostingMode` | `enum` | ✅ | `PerDocument` / `PerShift` / `DailySummary` (مؤجَّلة، قسم 12) |
| `VersionNumber` / `PreviousVersionId` / `IsCurrentVersion` | `int` / `long?` / `bool` | ✅ | **إلزامي وليس TBD** — نفس نمط إصدارات الوصفة في `02-Module-Inventory-Manufacturing.md` (قاعدة 31)؛ تعديل قالب مُستخدَم فعليًا ينشئ إصدارًا جديدًا، والقديم يفضل موجودًا لتفسير القيود التاريخية (قسم 5.4) |
| `IsActive` | `bool` | ✅ | — |
| `IsSystemTemplate` | `bool` | ✅ | `true` = Seed تلقائي، لا يُحذف |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

### 3.2 `PostingTemplateLine` — سطر القالب

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PostingTemplateId`, `LineNumber` | — | ✅ | — |
| `Direction` | `enum` | ✅ | `Debit` / `Credit` |
| `AccountSourceType` | `enum` | ✅ | `Fixed` / `FromDocument` / `FromCounterparty` / `FromItem` / `FromCategory` / `FromCompany` / `Dynamic` |
| `FixedAccountId` | `long?` | ⚠️ | إلزامي لو `AccountSourceType = Fixed` |
| `AccountFieldName` | `string?` | ⚠️ | إلزامي لو `AccountSourceType = FromDocument` |
| `AccountResolverKey` | `string?` | ⚠️ | إلزامي لو `AccountSourceType ∈ {FromCounterparty, FromItem, FromCategory, Dynamic}` — **قيمة من قائمة Resolvers مسجَّلة ومعروفة مسبقًا في الكود** (قسم 4.3)، مش نص حر يُفسَّر |
| `AmountFormulaType` | `enum` | ✅ | **بديل `AmountSourceType`/`AmountFormula` الحر — قائمة ثابتة من الصيغ المُختبَرة (قسم 4.1)** |
| `AmountFieldName` | `string?` | ⚠️ | إلزامي لو `AmountFormulaType = DirectField` |
| `ConditionType` | `enum` | ✅ (افتراضي `None`) | **بديل `Condition` الحر النصي — قائمة ثابتة من الشروط (قسم 4.2)** |
| `ConditionFieldName` / `ConditionFieldValue` | `string?` | ⚠️ | إلزاميان حسب `ConditionType` المُختار |
| `LineDescription` | `string?` | ❌ | — |

### 3.3 `PostingTemplateLineCostCenter` — بُعد على سطر القالب

**قيود من `01-Module-Accounting.md`**: الحد الأقصى 5 أبعاد لكل حساب (قاعدة 24)، والبُعد لازم يكون مربوطًا بالحساب عبر `AccountDimensionLink` مسبقًا — **القالب لا يمكنه إضافة بُعد غير مربوط بالحساب المُستهدَف، ويُرفض حفظ القالب لو حاول ذلك**.

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PostingTemplateLineId`, `CostCenterDimensionId` | — | ✅ | — |
| `SourceType` | `enum` | ✅ | `Fixed` / `FromDocument` / `Dynamic` — **`FromUser` أُزيل من الإصدار الأول** (كان مقترحًا سابقًا "من إعدادات المستخدم الحالي" بدون تعريف واضح — النظام لسه مفيهوش كيان `UserSettings` يحدد أي إعداد بالظبط؛ يُعاد النظر فيه لما `UserSettings` يتعرّف رسميًا في موديول مستقبلي) |
| `FixedValueId` | `long?` | ⚠️ | إلزامي لو `SourceType = Fixed`. **يُتحقَّق إلزاميًا إن `CostCenterDimensionValue.CostCenterDimensionId` بتاعه مطابق لنفس `CostCenterDimensionId` على السطر** — قيمة من بُعد مختلف تُرفض عند الحفظ (مثال: منع اختيار قيمة تخص بُعد "نوع المبيعات" في سطر بُعده الفعلي "الفرع") |
| `ValueFieldName` | `string?` | ⚠️ | إلزامي لو `SourceType = FromDocument` |
| `ValueResolverKey` | `string?` | ⚠️ | إلزامي لو `SourceType = Dynamic` — من قائمة Resolvers مسجَّلة (قسم 4.3) |
| `DisplayOrder` | `int` | ✅ | — |

> **قاعدة تحقق إضافية**: `CostCenterDimensionValue` المُختارة كـ`FixedValueId` **لازم تكون `IsActive = true` وقت إنشاء أو تعديل القالب** — قيمة مُعطَّلة لا تُستخدم في قوالب جديدة أو مُعدَّلة، لكنها **تفضل موجودة بدون حذف** لتفسير القيود التاريخية اللي استخدمتها قبل التعطيل (نفس فلسفة Soft Delete العامة، `00-Project-Overview.md` قسم 16).

### 3.4 `JournalEntryTemplateSnapshot` (جديد — إلزامي)

**يُحفَظ تلقائيًا مع كل `JournalEntry` آلي وقت الترحيل** — نسخة مجمَّدة (JSON) من `PostingTemplate` بإصداره المُستخدَم فعليًا وقت إنشاء القيد.

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `JournalEntryId` | — | ✅ | — |
| `PostingTemplateId` / `TemplateVersionNumber` | `long` / `int` | ✅ | مرجع للإصدار المحدد المُستخدَم |
| `TemplateSnapshotJson` | `string` | ✅ | نسخة كاملة من تعريف القالب وسطوره وقت الترحيل |

> **السبب**: لو القالب اتعدّل لاحقًا (إصدار جديد)، القيد القديم لازم يفضل قابلًا للتفسير والمراجعة بنفس المنطق اللي أنشأه بالضبط — نفس فلسفة `RecipeVersion` في المخازن، وليست ميزة اختيارية.

---

## 4. الصيغ والشروط الثابتة (بديل التعبيرات النصية الحرة)

> **قرار معماري حاسم**: الإصدار الأول من المحرك **لا يدعم أي تعبير نصي حر يُفسَّر وقت التشغيل** (لا لحساب المبلغ ولا للشرط) — لأن أي خطأ إملائي أو منطقي في نص حر مش هيتكشف إلا وقت الترحيل الفعلي على بيانات حقيقية، وده غير مقبول لمحرك بيولّد قيودًا مالية. بدلًا من ده، **مجموعة ثابتة من الصيغ والشروط المُختبَرة مسبقًا (Type-Safe Enums)**.

### 4.1 `AmountFormulaType`

```csharp
public enum AmountFormulaType
{
    DirectField = 1,                      // قيمة من حقل مباشر في السياق (AmountFieldName يحدد الاسم)
    SumLineQuantityTimesUnitPrice = 2,     // SUM(Lines.Quantity * Lines.UnitPrice)
    SumLineQuantityTimesUnitCost = 3,      // SUM(Lines.Quantity * Lines.UnitCost) — تكلفة البضاعة المباعة
    SubtotalMinusDiscount = 4,             // Subtotal - DiscountAmount
    SumShiftVarianceLiability = 5,         // ShiftVarianceLiabilitySplit.MinorShortage + MajorShortage — بديل صريح لأي مفهوم "موازنة تلقائية"
}
```

> **قرار حرج — لا يوجد `BalancingAmount` في هذا المحرك إطلاقًا (بعد مراجعة معمارية).** كان مقترحًا في مسودة سابقة كصيغة "تاخد أي فرق يوازن القيد تلقائيًا"، لكن ده بالظبط النمط اللي بيتعارض مع فلسفة الفشل الصريح (قاعدة 12): لو فيه خطأ في مبلغ سطر سابق، صيغة "موازنة تلقائية" **هتخفي الخطأ بصمت** بدل ما تفشل بوضوح — القيد هيتزن رياضيًا لكنه غلط اقتصاديًا، وده بالظبط الفخ اللي المحرك ده مصمَّم يمنعه من الأساس. **كل سيناريو "متغيّر" (زي فروق الوردية) لازم يتحسب بصيغة صريحة معروفة مسبقًا** (`SumShiftVarianceLiability` مثال) بدل أي مفهوم عام لموازنة تلقائية، بغض النظر عن وجود شرط (`ConditionType`) على القالب أو عدمه.
كل قيمة من دول ليها **تنفيذ ثابت ومُختبَر مسبقًا** في `IAmountFormulaEvaluator` — إضافة صيغة جديدة تتطلب تعديل كود (Pull Request ومراجعة واختبار)، مش إدخال نص جديد من شاشة الإعدادات.

### 4.2 `ConditionType`

```csharp
public enum ConditionType
{
    None = 1,                    // بدون شرط — السطر يُنفَّذ دايمًا
    FieldEquals = 2,             // Context[ConditionFieldName] == ConditionFieldValue
    FieldGreaterThanZero = 3,    // Context[ConditionFieldName] > 0
    FieldNotNull = 4,            // Context[ConditionFieldName] != null
}
```

### 4.3 قائمة الـ Resolvers المسجَّلة (`AccountResolverKey` / `ValueResolverKey`)

قيم هذه الحقول **لازم تكون من قائمة مغلقة معروفة مسبقًا في الكود**، مش أي نص:
- `Customer.ReceivableAccountId`, `Supplier.PayableAccountId` (حسابات فرعية للأطراف)
- `Item.InventoryAccountId`, `Item.Category.ToCostCenterValue` (حسابات/أبعاد الأصناف)
- `Branch.ToCostCenterValue` (بُعد الفرع المرتبط بـ`LinkedEntityType = Branch`، `01-Module-Accounting.md` قسم 2.1)
- `Company.CashAccountId`, `Company.SalesRevenueAccountId`, `Company.VatPayableAccountId`, `Company.CogsAccountId`, `Company.InventoryAccountId`, وباقي حسابات إعدادات الشركة الثابتة

أي `AccountResolverKey`/`ValueResolverKey` غير مسجَّل في هذه القائمة **يُرفض عند حفظ القالب نفسه**، قبل ما يوصل لمرحلة الترحيل الفعلي أصلًا.

### 4.4 آلية تسجيل الـ Resolvers تقنيًا

- **Static Registry**: كل Resolver مُنفَّذ في ملف مستقل تحت `Domain/Posting/Resolvers/`، ومسجَّل في قاموس ثابت (`IReadOnlyDictionary<string, IPostingResolver>`) وقت بدء تشغيل التطبيق.
- **التسجيل التلقائي عبر DI**: استخدام Assembly Scanning وقت تسجيل الخدمات (`Program.cs`) لاكتشاف كل الكلاسات المُنفِّذة لـ`IPostingResolver` تلقائيًا وربطها بمفتاحها — لا حاجة لتسجيل يدوي لكل Resolver جديد في أكتر من مكان.
- **إضافة Resolver جديد = تعديل كود حصريًا**: ملف جديد + تنفيذ الواجهة + Pull Request + مراجعة + اختبار وحدة مستقل — **لا يوجد أي مسار لإضافة Resolver من واجهة المستخدم أو الإعدادات**، بما يتسق مع مبدأ رفض التعبيرات الحرة (قسم 4).
- **التحقق وقت حفظ القالب**: أي `AccountResolverKey`/`ValueResolverKey` يُدخَل في شاشة محرر القالب (قسم 9.2) يُتحقَّق فورًا من وجوده في الـ Registry — القيمة غير المسجَّلة تُرفض في نفس لحظة الحفظ، مش وقت أول محاولة ترحيل فعلية بيها.

---

## 5. محرك التقييم (`PostingEvaluator`)

### 5.1 دورة العمل — مع الفصل الصارم بين "شرط = false" و"فشل حل"

```csharp
public async Task<JournalEntry> EvaluateAsync(PostingRequest request, PostingTemplate template, CancellationToken ct)
{
    var entry = new JournalEntry
    {
        CompanyId = request.CompanyId,
        BranchId = request.BranchId,
        EntryDate = request.EntryDate,
        Description = request.Description,
        SourceModule = MapScreenCodeToSourceModule(request.ScreenCode),
        SourceDocumentType = request.SourceDocumentType,
        SourceDocumentId = request.SourceDocumentId,
        Status = JournalEntryStatus.Posted,   // مباشرة — لا يمر بـ Draft إطلاقًا (01-Module-Accounting.md قاعدة 4)
        IsAutoGenerated = true,
        PostedAtUtc = DateTime.UtcNow,
    };

    foreach (var line in template.Lines.OrderBy(l => l.LineNumber))
    {
        // 1) تقييم الشرط — "false" هنا قرار تصميم متعمَّد وصحيح، السطر يُتجاهَل بهدوء
        if (!EvaluateCondition(line.ConditionType, line, request.Context))
            continue;

        // 2) حل الحساب — الفشل هنا خطأ صارم يوقف الترحيل بالكامل فورًا، وليس تجاهلًا صامتًا للسطر
        var accountId = ResolveAccount(line, request.Context)
            ?? throw new PostingAccountResolutionException(
                $"تعذّر تحديد الحساب للسطر {line.LineNumber} في القالب '{template.NameAr}' (إصدار {template.VersionNumber}) — راجع إعداد {line.AccountSourceType} لهذا السطر.");

        // 3) حل المبلغ — نفس مبدأ الفشل الصارم؛ القيمة صفر بعد اجتياز الشرط بنجاح تُعامَل كخطأ يستحق المراجعة لا التجاهل،
        //    فيما عدا صيغ موثّقة صراحة بترجع صفر كنتيجة سليمة (لا يوجد منها حاليًا في قائمة 4.1)
        var amount = ResolveAmount(line, request.Context);

        var entryLine = new JournalEntryLine
        {
            AccountId = accountId,
            LineNumber = entry.Lines.Count + 1,
            DebitAmount = line.Direction == PostingDirection.Debit ? amount : 0,
            CreditAmount = line.Direction == PostingDirection.Credit ? amount : 0,
            Description = line.LineDescription ?? request.Description,
        };

        foreach (var cc in line.CostCenters.OrderBy(c => c.DisplayOrder))
        {
            var valueId = ResolveCostCenterValue(cc, request.Context)
                ?? throw new PostingCostCenterResolutionException(
                    $"تعذّر تحديد قيمة البُعد {cc.CostCenterDimensionId} للسطر {line.LineNumber}.");

            entryLine.DimensionValues.Add(new JournalEntryLineDimensionValue
            {
                CostCenterDimensionId = cc.CostCenterDimensionId,
                CostCenterDimensionValueId = valueId
            });
        }

        entry.Lines.Add(entryLine);
    }

    entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
    entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

    if (entry.TotalDebit != entry.TotalCredit)
        throw new PostingBalanceException(
            $"القيد غير متزن: مدين {entry.TotalDebit} ≠ دائن {entry.TotalCredit} — القالب '{template.NameAr}' إصدار {template.VersionNumber}.");

    return entry;
}
```

### 5.2 حل الحساب (`ResolveAccount`)

```csharp
private long? ResolveAccount(PostingTemplateLine line, Dictionary<string, object> context) =>
    line.AccountSourceType switch
    {
        AccountSourceType.Fixed => line.FixedAccountId,
        AccountSourceType.FromDocument => GetLongFromContext(context, line.AccountFieldName),
        AccountSourceType.FromCounterparty => CallRegisteredResolver(line.AccountResolverKey, context),
        AccountSourceType.FromItem => CallRegisteredResolver(line.AccountResolverKey, context),
        AccountSourceType.FromCategory => CallRegisteredResolver(line.AccountResolverKey, context),
        AccountSourceType.FromCompany => GetCompanyAccount(line.AccountResolverKey),
        AccountSourceType.Dynamic => CallRegisteredResolver(line.AccountResolverKey, context),
        _ => null
    };
```

### 5.3 حل المبلغ (`ResolveAmount`) — عبر `AmountFormulaType` الثابت فقط

```csharp
private decimal ResolveAmount(PostingTemplateLine line, Dictionary<string, object> context) =>
    line.AmountFormulaType switch
    {
        AmountFormulaType.DirectField => GetDecimalFromContext(context, line.AmountFieldName),
        AmountFormulaType.SumLineQuantityTimesUnitPrice => SumLines(context, l => l.Quantity * l.UnitPrice),
        AmountFormulaType.SumLineQuantityTimesUnitCost => SumLines(context, l => l.Quantity * l.UnitCost),
        AmountFormulaType.SubtotalMinusDiscount => GetDecimalFromContext(context, "Subtotal") - GetDecimalFromContext(context, "DiscountAmount"),
        AmountFormulaType.SumShiftVarianceLiability => SumShiftVarianceLiability(context),
        _ => throw new PostingConfigurationException($"AmountFormulaType غير مدعوم: {line.AmountFormulaType}")
    };
```

### 5.4 استخدام الإصدار الحالي من القالب

`IPostingTemplateResolver` **يجيب دايمًا `PostingTemplate` بـ`IsCurrentVersion = true`** المطابق لـ`ScreenCode` و`TemplateSelectorField/Value` — القيد الناتج يحفظ `JournalEntryTemplateSnapshot` (قسم 3.4) لحظة الترحيل، بحيث تعديل القالب لاحقًا (إصدار جديد) **لا يغيّر تفسير القيود القديمة أبدًا**.

---

## 6. قوالب الترحيل الأساسية (System Templates)

### 6.1 Seed تلقائي

| # | `ScreenCode` | `TemplateSelectorField` | `TemplateSelectorValue` | `PostingMode` |
|---|---|---|---|---|
| 1 | `SalesInvoice` | `PaymentType` | `Cash` | `PerDocument` |
| 2 | `SalesInvoice` | `PaymentType` | `Credit` | `PerDocument` |
| 3 | `PurchaseInvoice` | `PaymentType` | `Cash` | `PerDocument` |
| 4 | `PurchaseInvoice` | `PaymentType` | `Credit` | `PerDocument` |
| 5 | `POSInvoice` | — | — | `PerShift` |
| 6 | `SalesReturn` | — | — | `PerDocument` |
| 7 | `PurchaseReturn` | — | — | `PerDocument` |
| 8 | `Voucher` | `VoucherType` | `Receipt` | `PerDocument` |
| 9 | `Voucher` | `VoucherType` | `Payment` | `PerDocument` |
| 10 | `TreasuryTransfer` | — | — | `PerDocument` |
| 11 | `CustodyIssue` | — | — | `PerDocument` |
| 12 | `CustodySettlement` | — | — | `PerDocument` |
| 13 | `ShiftVariance` | — | — | `PerShift` |

### 6.2 القالب 1: فاتورة بيع نقدي

| # | Direction | AccountSourceType | AmountFormulaType | ConditionType |
|---|---|---|---|---|
| 1 | Debit | Fixed: `Company.CashAccountId` | DirectField (`Total`) | None |
| 2 | Credit | Fixed: `Company.SalesRevenueAccountId` | DirectField (`Subtotal`) | None |
| 3 | Credit | Fixed: `Company.VatPayableAccountId` | DirectField (`TaxAmount`) | None |
| 4 | Debit | Fixed: `Company.CogsAccountId` | `SumLineQuantityTimesUnitCost` | None |
| 5 | Credit | Fixed: `Company.InventoryAccountId` | `SumLineQuantityTimesUnitCost` | None |

الأبعاد: الفرع (`FromDocument: BranchId`) على كل السطور، نوع المبيعات (`FromDocument: OrderType`) على 2-5، قسم المنتج (`Dynamic: Item.Category.ToCostCenterValue`) على 4-5.

### 6.3 القالب 2: فاتورة بيع آجل

نفس القالب 1، فيما عدا السطر 1: `Direction=Debit`, `AccountSourceType=FromCounterparty` (`Customer.ReceivableAccountId`), `AmountFormulaType=DirectField` (`Total`).

### 6.4 القالب 3: فاتورة شراء نقدي

| # | Direction | AccountSourceType | AmountFormulaType |
|---|---|---|---|
| 1 | Debit | Fixed: `Company.InventoryAccountId` | DirectField (`Subtotal`) |
| 2 | Debit | Fixed: `Company.VatReceivableAccountId` | DirectField (`TaxAmount`) |
| 3 | Credit | Fixed: `Company.CashAccountId` | DirectField (`Total`) |

### 6.5 القالب 4: فاتورة شراء آجل

نفس القالب 3، فيما عدا السطر 3: `AccountSourceType=FromCounterparty` (`Supplier.PayableAccountId`).

### 6.6 القالب 5: POS — `PerShift` 🌟

> **تسمية الحدود المالية هنا مُوضَّحة صراحة (قسم 7.3) لتفادي التباس مع حد اعتماد إغلاق الوردية في `05-Module-POS-Shifts.md` (قاعدة 5) — دول إعدادان منفصلان تمامًا بأسماء مختلفة.**

| # | Direction | AccountSourceType | AmountFormulaType/قيمة | ConditionType |
|---|---|---|---|---|
| 1 | Debit | Fixed: `Company.CashAccountId` | مجموع فواتير الوردية بطريقة دفع Cash | None |
| 2 | Debit | Fixed: `Company.BankAccountId` | مجموع فواتير الوردية بطريقة دفع Card | None |
| 3 | Debit | Fixed: `Company.WalletAccountId` | مجموع فواتير الوردية بطريقة دفع EWallet | None |
| 4 | Credit | Fixed: `Company.SalesRevenueAccountId` | مجموع `Subtotal` لكل فواتير الوردية | None |
| 5 | Credit | Fixed: `Company.VatPayableAccountId` | مجموع `TaxAmount` لكل فواتير الوردية | None |
| 6 | Debit | Fixed: `Company.CogsAccountId` | `SumLineQuantityTimesUnitCost` على كل بنود كل الفواتير | None |
| 7 | Credit | Fixed: `Company.InventoryAccountId` | `SumLineQuantityTimesUnitCost` على كل بنود كل الفواتير | None |
| 8 | Debit | Fixed: `Company.BankCommissionAccountId` | مجموع عمولة ماكينة الدفع | `FieldGreaterThanZero` |
| 9 | Credit | Fixed: `Company.BankAccountId` | مجموع عمولة ماكينة الدفع | `FieldGreaterThanZero` |
| 10 | Debit | Fixed: `Company.CashShortageAccountId` | `ShiftVarianceLiabilitySplit.MinorShortage` | `FieldGreaterThanZero` |
| 11 | Debit | Fixed: `Company.EmployeeReceivableAccountId` | `ShiftVarianceLiabilitySplit.MajorShortage` | `FieldGreaterThanZero` |
| 12 | Credit | Fixed: `Company.CashAccountId` | `SumShiftVarianceLiability` (= `MinorShortage + MajorShortage`، صيغة صريحة وليست موازنة تلقائية) | `FieldGreaterThanZero` (على إجمالي النقص) |
| 13 | Debit | Fixed: `Company.CashAccountId` | `ShiftVarianceLiabilitySplit.SurplusAmount` | `FieldGreaterThanZero` |
| 14 | Credit | Fixed: `Company.SalesVarianceAccountId` | `ShiftVarianceLiabilitySplit.SurplusAmount` | `FieldGreaterThanZero` |

الأبعاد: الفرع + الكاشير (`FromDocument: BranchId` / `CashierUserId`) على كل السطور.

### 6.7 القالب 6: مردود بيع

| # | Direction | AccountSourceType | AmountFormulaType |
|---|---|---|---|
| 1 | Debit | Fixed: `Company.SalesReturnAccountId` | DirectField (`Subtotal`) |
| 2 | Debit | Fixed: `Company.VatPayableAccountId` | DirectField (`TaxAmount`) |
| 3 | Credit | `FromCounterparty` (`Customer.ReceivableAccountId`) | DirectField (`Total`) |
| 4 | Debit | Fixed: `Company.InventoryAccountId` | `SumLineQuantityTimesUnitCost` |
| 5 | Credit | Fixed: `Company.CogsAccountId` | `SumLineQuantityTimesUnitCost` |

### 6.8 القالب 7: فروق الوردية (Standalone)

نفس منطق سطور 10-14 من القالب 5 (قسم 6.6)، كقيد مستقل لو تم اكتشاف الفرق بعد ترحيل قيد الوردية الأصلي.

---

## 7. قواعد العمل (Business Rules)

### 7.1 قواعد ترحيل عامة (موروثة من `01-Module-Accounting.md`)
1. لا يُقبل ترحيل أي قيد غير متزن (قاعدة 1).
2. الحسابات التجميعية (`IsPostable = false`) لا تقبل قيودًا مباشرة (قاعدة 2).
3. أي حساب مرتبط ببُعد إلزامي لا يُقبل ترحيل سطر بدون قيمة له (قاعدة 3).
4. **القيود الآلية (`IsAutoGenerated = true`) تُنشأ `Posted` مباشرة، بدون المرور بحالة `Draft` إطلاقًا ولو للحظة واحدة** — منفَّذ حرفيًا في الكود (قسم 5.1).
5. أي محاولة ترحيل لقيد بتاريخ في فترة مقفلة تُرفض صراحةً (قاعدة 9).
6. الحد الأقصى 5 أبعاد لكل حساب — القالب يُرفض حفظه لو حاول تجاوز الحد (قاعدة 24).
7. بُعد "مركز التكلفة" يُميَّز بـ `Code = 'COST_CENTER'` الثابت (قاعدة 29).

### 7.2 قواعد خاصة بمحرك الترحيل
8. أي موديول ينشئ قيدًا لازم يمر عبر `IPostingService` — لا يوجد `new JournalEntry()` مباشر في أي موديول آخر.
9. `PostingTemplate` لا يُحذف لو استُخدم في قيد واحد على الأقل — يُعطَّل (`IsActive = false`) بدلًا من الحذف.
10. `PostingTemplate` مربوط بـ `ScreenCode` و`CompanyId` — نفس الشاشة يكون ليها قوالب متعددة حسب الشركة.
11. **`AmountFormulaType` و`ConditionType` قيم Enum ثابتة فقط — لا يوجد تعبير نصي حر يُفسَّر وقت التشغيل في أي حقل من حقول القالب** (قسم 4) — إضافة صيغة/شرط جديد تتطلب تعديل كود ومراجعة واختبار، وليست عملية إعداد من الواجهة.
12. **أي فشل في حل حساب (`ResolveAccount`) أو قيمة بُعد (`ResolveCostCenterValue`) يوقف الترحيل بالكامل فورًا بخطأ صريح** — لا يُتجاهَل السطر بصمت مهما كان السبب (`00-System-Wide-Corrections-03.md` قسم 3). التجاهل المتعمَّد للسطر مسموح **فقط** عبر `ConditionType` يُقيَّم صراحة بـ `false`.
13. `PostingMode = PerShift` يعني أن الموديول المصدر (`05-Module-POS-Shifts.md`) يجمّع كل فواتير الوردية في السياق (`Context`) قبل نداء `IPostingService` — المحرك نفسه لا "يجمّع" شيئًا، هو بيقيّم القالب على البيانات المُجمَّعة اللي وصلته جاهزة.
14. `PostingMode = DailySummary` **مؤجَّل لمرحلة لاحقة** — غير مدعوم في هذا الإصدار (قسم 12).
15. **`IdempotencyKey` إلزامي على كل `PostingRequest` بدون استثناء** — تكرار نفس المفتاح يرجّع نفس النتيجة المخزّنة دون إعادة التنفيذ (`00-Project-Overview.md` قسم 14.1).
16. `SourceModule` على `JournalEntry` يُحسب تلقائيًا من `ScreenCode` عبر Mapping ثابت معروف مسبقًا (مش تخمين ديناميكي).
17. **`JournalEntryTemplateSnapshot` يُحفَظ إلزاميًا مع كل قيد آلي** (قسم 3.4) — ليست ميزة مؤجَّلة.
18. **تحويل العملة (`00-Project-Overview.md` قسم 10) يُطبَّق تلقائيًا على مستوى `IPostingService` نفسه لكل سطر ناتج من أي قالب**، بغض النظر عن تعقيد الصيغة — هذا سلوك أساسي في المحرك، وليس ميزة إضافية تخص القوالب نفسها أو تحتاج دعمًا خاصًا فيها.

### 7.3 قواعد فروق الوردية — تسميات صريحة منفصلة (تصحيح لبس حرج)

> **تنبيه**: كان فيه لبس بين حدّين ماليين مختلفين تمامًا في المسودة الأولى، اتصلّح هنا بتسميتين منفصلتين تمامًا:

19. **`ShiftCloseApprovalThreshold`** (الموثَّق في `05-Module-POS-Shifts.md` قاعدة 5، افتراضي 100 ج.م) يحكم **هل إغلاق الوردية يحتاج سلسلة موافقات قبل اعتماده النهائي** — ده حد إداري/تشغيلي، ومفيش علاقة له بمحرك الترحيل مباشرة.
20. **`ShiftVarianceEmployeeLiabilityThreshold`** (افتراضي 10 ج.م، إعداد مستقل تمامًا في `PostingTemplate` الخاص بفروق الوردية) يحكم **مين الحساب المحاسبي اللي الفرق يترحّل عليه**: أقل من الحد أو يساويه → `Company.CashShortageAccountId` ("فروق خزينة" — مصروف عام)، أكتر منه → `Company.EmployeeReceivableAccountId` ("ذمم كاشير" — مديونية شخصية على الموظف). **الفرق الموجب (زيادة)** يترحّل دايمًا على `Company.SalesVarianceAccountId` بغض النظر عن قيمته.
21. الاعتماد الإداري على تجاوز `ShiftCloseApprovalThreshold` **لا يمنع أو يؤجّل** ترحيل قيد الفرق نفسه حسب `ShiftVarianceEmployeeLiabilityThreshold` — الاثنان مستقلان تمامًا؛ ممكن فرق يتطلب اعتماد إداري لإغلاق الوردية (لأنه كبير) ويترحّل في نفس الوقت على "ذمم الكاشير" تلقائيًا بمجرد اكتمال الاعتماد.

### 7.4 علاقة القيود الآلية بسلاسل الموافقات (توضيح يحل تعارضًا ظاهريًا)

22. **قيود `IPostingService` الآلية لا تخضع لفحص حد الموافقات المذكور في `01-Module-Accounting.md` (قاعدة 11) على الإطلاق** — هذا الفحص يخص **القيود اليدوية فقط** التي يُنشئها محاسب مباشرة من شاشة القيود (`01-Module-Accounting.md`). **السبب**: أي مستند مصدر (فاتورة بيع، فاتورة شراء...) خضع بالفعل لسلسلة موافقاته الخاصة به إن وُجدت (موثَّقة في موديوله: `04-Module-Sales.md`, `04-Module-Purchasing.md`...) **قبل** ما يوصل لحالة تسمح بالترحيل أصلًا — فرض فحص موافقة إضافي على القيد الناتج تكرار غير مبرر لنفس البوابة. **نتيجة مباشرة**: `JournalEntry.ApprovalInstanceId` **يبقى دايمًا `null` لأي قيد `IsAutoGenerated = true`**، ولا يُستخدَم إلا للقيود اليدوية.

### 7.5 قواعد الـ COGS

23. **Weighted Average** هي طريقة حساب تكلفة البضاعة المباعة المعتمدة — متسقة مع `02-Module-Inventory-Manufacturing.md`.
24. **`Item.AverageCost` تمت إضافته رسميًا في `02-Module-Inventory-Manufacturing.md`** (قواعد 32-35، مضافة كنتيجة مباشرة لمراجعة هذا الملف) — معادلة التحديث، نقل التكلفة بين المخازن عند التحويل، وإلزامية `StockTransaction.UnitCost` موثَّقة بالتفصيل الكامل هناك. **`SumLineQuantityTimesUnitCost` هنا (قسم 4.1) يعتمد على قيمة `UnitCost` المحفوظة على كل `SalesInvoiceLine`/`POSInvoiceLine` وقت الإنشاء (Snapshot من `Item.AverageCost` في تلك اللحظة، قاعدة 25) — لا يُعاد حساب `AverageCost` من محرك الترحيل نفسه.**
25. `SalesInvoiceLine.UnitCost` و`POSInvoiceLine.UnitCost` يُحفَظان كـ Snapshot من `Item.AverageCost` **لحظة إنشاء السطر**، وليس لحظة الترحيل — قيمة تاريخية ثابتة لا تتغيّر لو `AverageCost` اتغيّر لاحقًا.

---

## 8. التكامل مع الموديولات (أمثلة الاستخدام)

### 8.1 Sales Module

```csharp
public async Task<PostingResult> PostInvoiceAsync(long invoiceId, CancellationToken ct)
{
    var invoice = await _db.SalesInvoices.Include(i => i.Lines)
        .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    var request = new PostingRequest
    {
        CompanyId = invoice.CompanyId,
        BranchId = invoice.BranchId,
        ScreenCode = "SalesInvoice",
        SourceDocumentType = "Invoice",
        SourceDocumentId = invoice.Id,
        SourceDocumentNumber = invoice.InvoiceNumber,
        EntryDate = invoice.InvoiceDate,
        Description = $"قيد فاتورة بيع — {invoice.InvoiceNumber}",
        TemplateSelectorField = "PaymentType",
        TemplateSelectorValue = invoice.PaymentType.ToString(),
        Context = new Dictionary<string, object>
        {
            ["Subtotal"] = invoice.Subtotal,
            ["TaxAmount"] = invoice.TaxAmount,
            ["DiscountAmount"] = invoice.DiscountAmount,
            ["Total"] = invoice.Total,
            ["OrderType"] = invoice.OrderType.ToString(),
            ["Customer"] = invoice.Customer,
            ["BranchId"] = invoice.BranchId,
            ["Lines"] = invoice.Lines.Select(l => new { l.ItemId, l.Quantity, l.UnitPrice, l.UnitCost }).ToList(),
        },
        IdempotencyKey = invoice.IdempotencyKey,
    };

    return await _postingService.PostAsync(request, ct);
    // ملاحظة: مفيش أي فحص لحد موافقات هنا — الفاتورة خضعت لسلسلتها الخاصة قبل الوصول لحالة تسمح بالترحيل (قسم 7.4)
}
```

### 8.2 POS Module — `PerShift`

```csharp
public async Task<PostingResult> PostShiftAsync(long shiftId, CancellationToken ct)
{
    var shift = await _db.Shifts.Include(s => s.Invoices).ThenInclude(i => i.Lines)
        .FirstOrDefaultAsync(s => s.Id == shiftId, ct);

    var (minorShortage, majorShortage, surplus) = SplitShiftVariance(
        shift.DifferenceAmount,
        _settings.ShiftVarianceEmployeeLiabilityThreshold); // إعداد مستقل — قسم 7.3، وليس نفس حد اعتماد الإغلاق

    var request = new PostingRequest
    {
        CompanyId = shift.CompanyId,
        BranchId = shift.BranchId,
        ScreenCode = "POSInvoice",
        SourceDocumentType = "Shift",
        SourceDocumentId = shift.Id,
        SourceDocumentNumber = shift.Code,
        EntryDate = DateOnly.FromDateTime(shift.ClosedAtUtc!.Value),
        Description = $"قيد وردية POS — {shift.Code}",
        Context = new Dictionary<string, object>
        {
            ["Invoices"] = shift.Invoices,
            ["CashierUserId"] = shift.CashierUserId,
            ["BranchId"] = shift.BranchId,
            ["ShiftVarianceLiabilitySplit"] = new { MinorShortage = minorShortage, MajorShortage = majorShortage, SurplusAmount = surplus },
        },
        IdempotencyKey = shift.IdempotencyKey,
    };

    return await _postingService.PostAsync(request, ct);
}
```

### 8.3 Purchasing Module
مشابه لـ Sales Module — `PaymentType` (Cash/Credit) يحدد القالب.

### 8.4 Inventory Module
قيود تسوية الجرد، الهالك، وأوامر الإنتاج — تُنشأ عبر `IPostingService` عند الاعتماد/الاكتمال، بنفس النمط، ومفيش فحص موافقة إضافي هنا كمان (قسم 7.4).

---

## 9. إعدادات الشاشات (Screen Settings Integration)

### 9.1 شاشة "إعدادات الشاشات"
لكل `ScreenCode`: تفعيل الترحيل الآلي، اختيار `PostingMode`، وقائمة `PostingTemplates` المرتبطة (إضافة/تعديل/عرض إصدارات سابقة).

### 9.2 محرر القالب
Grid لسطور القالب: `Direction`, `AccountSourceType` + القيمة، `AmountFormulaType` + الحقل (لو `DirectField`)، `ConditionType` + الحقول المرتبطة، الأبعاد. **كل الاختيارات من قوائم منسدلة ثابتة (قسم 4) — لا يوجد حقل نص حر لأي منها**. + معاينة (Preview) لشكل القيد المتوقع قبل الحفظ.

---

## 10. التقارير

نفس التصنيف العشري المقترح في المسودة الأولى (10 مجموعات، 47 تقريرًا) — **مقبول كما هو** كخريطة طريق شاملة، مع البدء بالمجموعة المختصرة التالية:

| # | التقرير | الأولوية |
|---|---|---|
| 1 | سجل القيود الآلية | 🔴 عاجل |
| 2 | القيود الفاشلة (رسائل الخطأ الصريحة من قسم 5.1) | 🔴 عاجل |
| 3 | تحليل القيود حسب المصدر (Sales/Purchasing/POS/Inventory) | 🟡 مهم |
| 4 | قيود POS (`PerShift`) | 🟡 مهم |
| 5 | تحليل فروق الورديات (فصل الحدين — قسم 7.3) | 🟡 مهم |
| 6 | حالة إقفال الفترات | 🟢 مفيد |
| 7 | تحقق توازن القيود (Database Integrity) | 🟢 مفيد |
| 8 | سجل استخدام القوالب وإصداراتها | 🟢 مفيد |

> **ملاحظة**: "القيود المعلّقة" (Draft/PendingApproval) من قائمة التقارير الأصلية **غير منطبقة على القيود الآلية** (قاعدة 4 — تُنشأ `Posted` مباشرة) — التقرير ده لو لزم يفضل خاص بالقيود اليدوية بس في `01-Module-Accounting.md`.

---

## 11. الصلاحيات

| الصلاحية | ملاحظة |
|---|---|
| عرض القوالب وإصداراتها | — |
| **إنشاء/تعديل قالب** | صلاحية عالية — أي تعديل ينشئ إصدارًا جديدًا (قسم 3.1)، لا يُعدِّل القديم |
| تعطيل قالب | صلاحية أعلى — لا يوجد حذف فعلي لقالب مُستخدَم |
| عرض تقارير القيود الآلية | — |
| مراجعة القيود الفاشلة | صلاحية إدارية |
| الاطلاع على سجل إصدارات القوالب | صلاحية تدقيق |

---

## 12. بنود مؤجَّلة أو محسومة

### محسومة (لم تعد TBD)
- [x] **Snapshot القالب وقت الترحيل**: إلزامي (`JournalEntryTemplateSnapshot`، قسم 3.4)، وليس اختياريًا.
- [x] **تعدد العملات**: سلوك أساسي تلقائي في `IPostingService` نفسه (قاعدة 18)، وليس ميزة قالب.
- [x] **علاقة القيود الآلية بسلاسل الموافقات**: القيود الآلية لا تخضع لفحص حد الموافقات إطلاقًا (قسم 7.4).
- [x] **`AmountFormula`/`Condition`**: قيم Enum ثابتة فقط، لا تعبيرات نصية حرة (قسم 4).
- [x] **`BalancingAmount`**: أُزيل نهائيًا من المحرك — أي سيناريو "متغيّر" (فروق الوردية) يُحسب بصيغة صريحة معروفة مسبقًا (`SumShiftVarianceLiability`)، لا مفهوم عام لموازنة تلقائية قد يخفي أخطاء (قسم 4.1).
- [x] **`FromUser` Source Type**: أُزيل من الإصدار الأول لعدم وجود `UserSettings` مُعرَّف بعد — يُعاد النظر فيه لاحقًا (قسم 3.3).
- [x] **التحقق من `CostCenterDimensionValue`**: قاعدتا تحقق إضافيتان (مطابقة البُعد، وإلزامية `IsActive` وقت إنشاء/تعديل القالب) — قسم 3.3.
- [x] **آلية تسجيل الـ Resolvers**: موثَّقة بالتفصيل التقني الكامل (قسم 4.4).
- [x] **معاينة القيد (Preview)**: منقولة للمرحلة 1 كأداة أساسية، وليست تحسينًا مؤجَّلًا (قسم 13).

### لسه مؤجَّلة فعليًا
- [ ] `PostingTemplate` مرتبط بـ `CompanyId` فقط حاليًا — دعم وراثة قوالب من شركة أم للشركات التابعة (لو النظام يدعم هيكل شركات أم/تابعة مستقبلًا) مؤجَّل.
- [ ] `PostingMode = DailySummary` مؤجَّل بالكامل لمرحلة لاحقة (قاعدة 14).
- [ ] محرر قوالب متقدم (Drag & Drop) — تحسين تجربة استخدام لاحق، غير حرج للإصدار الأول (بخلاف المعاينة البسيطة، منقولة للمرحلة 1).

---

## 13. خارطة الطريق

**المرحلة 1 (الحالية)**: الكيانات (بما فيها `JournalEntryTemplateSnapshot` والإصدارات)، `IPostingService`/`PostingEvaluator` بالصيغ والشروط الثابتة (قسم 4)، آلية تسجيل الـResolvers (قسم 4.4)، Seed للقوالب الأساسية، شاشتا إعدادات الشاشات ومحرر القالب، **و"معاينة القيد" (Preview) البسيطة** — توليد شكل القيد المتوقع بدون حفظ فعلي، عشان المستخدم يكتشف أخطاء إعداد القالب قبل أول ترحيل حقيقي عليه، مش أداة تحسين مؤجَّلة.

**المرحلة 2**: التقارير الثمانية الأساسية (قسم 10)، ثم باقي مجموعات التقارير حسب الحاجة الفعلية.

**المرحلة 3**: `PostingMode = DailySummary`، محرر قوالب متقدم (Drag & Drop)، ودراسة توسعة `AmountFormulaType`/`ConditionType` بصيغ جديدة **عبر تعديل كود ومراجعة**، وليس فتح تعبيرات حرة.

---

## 14. ملاحظات للتنفيذ

- نفس نمط موديولات المشتريات والمخازن: Domain/Application/Infrastructure/API، `PublicId`/`RowVersion`/`IsDeleted`، CQRS عبر MediatR، FluentValidation.
- **الكيانات المشتركة**: `JournalEntry` من `01-Module-Accounting.md`، `CostCenterDimension`/`CostCenterDimensionValue`/`AccountDimensionLink`، `Account`/`Company`/`Branch`، و**`Item.AverageCost` بعد إضافته لملف المخازن (قاعدة 24)**.
- **خدمات جديدة**: `IPostingService`, `IPostingTemplateResolver` (يجيب دايمًا الإصدار الحالي)، `IPostingEvaluator`, `IAmountFormulaEvaluator` (بالصيغ الثابتة فقط)، `IConditionEvaluator` (بالشروط الثابتة فقط).
- **الاختبارات**: Integration Test مستقل لكل قالب أساسي (قسم 6)، بما فيها **اختبار مسار الفشل الصارم** (حساب غير قابل للحل يوقف الترحيل فعليًا ولا يمرّ بصمت) — هذا الاختبار تحديدًا إلزامي وليس اختياريًا نظرًا لحساسية الإصلاح رقم 2.
- **الأداء**: Cache لقوالب `IsCurrentVersion = true` النشطة، `AsNoTracking` للقراءات.
- **الأمان**: `IdempotencyKey` إلزامي، صلاحيات عالية لتعديل القوالب، Audit Log لكل إصدار جديد من أي قالب.
