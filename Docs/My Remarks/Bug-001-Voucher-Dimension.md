# Bug-001 — السندات مش بتترحّل لو الحساب عليه بُعد إلزامي

> **التاريخ**: 2026-09-25
> **المصدر**: اختبار يدوي — دورة الحسابات الشاملة
> **الخطورة**: 🔴 عالية
> **الحالة**: ✅ تم الإصلاح (Phase 1 — 2026-09-25)

---

## ✅ ملخص الإصلاح (Phase 1)

**الحل المُنفّذ**: الحل 1 (Auto-Resolve من الفرع)، **مركزي بالكامل داخل `PostingService`**
(`src\Habbak.ERP.Infrastructure\Services\PostingService.cs`) — بدون أي تعديل في أي Command.

- `ValidateAndThrowAsync` بقى ياخد `branchId`، وقبل ما يرفض بُعد إلزامي ناقص، بيتأكد الأول لو
  البُعد ده `LinkedEntityType = Branch` — لو كده، بيحله من فرع المستند (`Code` matching، نفس منطق
  `BranchDimensionSync`) ويحقنه في السطر قبل الترحيل الفعلي (مش بس قبل الـ Validation).
- تمت تغطية **Voucher، TreasuryTransfer، CustodyRegister، JournalEntry (اليدوية)** في ضربة واحدة،
  لأن الأربعة بيعدّوا من نفس الميثود (`PostAsync`/`PostDraftEntryAsync`) واللي بالفعل بتوصلها
  `BranchId` — مفيش حاجة اتغيّرت في `PostVoucherCommand`، `PostTreasuryTransferCommand`،
  `CreateCustodyRegisterCommand`/`CreateCustodySettlementCommand`، أو `PostJournalEntryCommand`.
- **أكواد أخطاء جديدة** واضحة بدل الرفض العام:
  - `ACC-R3-NO-BRANCH-ON-DOCUMENT` — المستند مش مربوط بفرع.
  - `ACC-R3-BRANCH-DIMENSION-UNRESOLVED` — الفرع نفسه مالوش قيمة في بُعد الفروع بعد (مفيش
    Auto-mirror هنا عمدًا — بيتسجل خطأ واضح بدل ما يتخلق قيمة تلقائيًا بدون علم حد).
  - `ACC-R3-MISSING-DIMENSION` باقي زي ما هو لأي بُعد إلزامي **مش** مربوط بالفروع.
- **الاختبارات**: 7 اختبارات Integration جديدة (4 في `PostingServiceTests.cs` على مستوى المحرك،
  2 في `VoucherAndJournalEntryCommandTests.cs`، 1 جديد في `TreasuryTransferCommandTests.cs`) —
  الاختبار القديم `PostAsync_MissingMandatoryDimension_ThrowsRule3` اتأكد إنه لسه شغال زي ما هو.
  مجموعة الـ IntegrationTests كلها (82 اختبار) شغالة.

**خارج نطاق Phase 1 (بقرار المستخدم)**:
- **CashReconciliation**: مالوش أي posting path دلوقتي (`CreateCashReconciliationCommand`/
  `ApproveCashReconciliationCommand` بيحسبوا الرصيد بس، مفيش قيد بيتعمل) — مفيش حاجة تتصلح فيه
  حاليًا؛ هيستفيد تلقائيًا من الإصلاح لو حد ضاف له posting في المستقبل.
- **BankReconciliationRun** و **AccountOpeningBalanceBatch**: مالهمش `BranchId` أصلاً (مش
  `IBranchScopedEntity`) — محتاجين قرار منفصل (Bug منفصل) قبل ما يتصلحوا، مفيش Migration
  اتضافت في الإصلاح ده.

---

## 1. الوصف

**السيناريو**:
1. المدير المالي ربط حساب "خزينة رئيسية" ببُعد "الفروع" (`CostCenterDimension`) وخلّاه **إلزامي** (`IsMandatory = true`).
2. المستخدم فتح شاشة **سند صرف**.
3. اختار "خزينة رئيسية" كخزينة.
4. عند **الترحيل** → **فشل**:
   > "الحساب 'الخزينة الرئيسية' يتطلب تحديد قيمة للبُعد الإلزامي رقم 10002."

**نفس المشكلة في**:
- **سند القبض**.
- **القيود اليدوية** (على الأغلب).

---

## 2. السبب الجذري

| # | السبب | المكان |
|---|---|---|
| 1 | `PostingService.ValidateAndThrowAsync` بيرفض أي بُعد `IsMandatory` بدون قيمة | `ACC-R3-MISSING-DIMENSION` |
| 2 | شاشة السندات **مالهاش حقل** لمركز التكلفة | `VoucherEditPage.tsx` |
| 3 | **مفيش Auto-Resolve** من الفرع رغم وجود `BranchDimensionSync` | `Application/Organization/Branches/` |
| 4 | `Voucher` عنده `BranchId` (مفروض) — بس **مش بيستخدم** لحل البُعد | `Voucher.cs` |

---

## 3. الأثر

| البُعد | التفصيل |
|---|---|
| **الأداء** | 🔴 السندات + القيود اليدوية **مش بتترحّل** |
| **التكرار** | 🔴 **دايمًا** لو فيه بُعد إلزامي |
| **التأثير المالي** | 🔴 **حرج** — كل السندات بتفشل |
| **Workaround** | ⚠️ شيل `IsMandatory` من الحساب |

---

## 4. الأماكن المتأثرة

### Backend
- `IPostingService.ValidateAndThrowAsync` — `01-Accounting-Settings.md` قسم 4.
- `PostingService.ValidateAndThrowAsync` — `Infrastructure/Services/PostingService.cs`.
- `CounterpartyAccountResolver` — قسم 4.
- `Voucher` + `VoucherEdit` — قسم 5.
- `JournalEntry` + `JournalEntryEdit` — قسم 5.

### Frontend
- `VoucherEditPage.tsx`
- `JournalEntryEditPage.tsx`

---

## 5. الحلول المقترحة

| # | الحل | الأولوية | التعقيد |
|---|---|---|---|
| 1 | **Auto-Resolve من الفرع** (`BranchDimensionSync`) | 🔴 الأفضل | 🟡 متوسط |
| 2 | إضافة حقل بُعد في السند + القيد | 🟡 مؤقت | 🟢 بسيط |
| 3 | بُعد تلقائي من `Voucher.BranchId` | 🟡 جيد | 🟢 بسيط |
| 4 | منع ربط الخزائن ببُعد إلزامي | 🟢 مؤقت | 🟢 بسيط |

### الاقتراح النهائي — الحل 1

**التفاصيل**:
1. `PostingService` يشوف المستند.
2. ياخد `BranchId` منه.
3. يحوّله لـ `CostCenterDimensionValue` عن طريق `BranchDimensionSync.UpsertAsync`.
4. **يحقنه** في القيد قبل `ValidateAndThrowAsync`.
5. لو الفرع مفيش له `CostCenterDimensionValue` → يرفض بـ error واضح.

**ليه الأفضل؟**
- ✅ بيستخدم `BranchDimensionSync` **الموجود**.
- ✅ مش بيضيف خطوات للمستخدم.
- ✅ متسق مع `FromContext` في `08A`.
- ✅ حل مركزي — يغطي كل الشاشات.

---

## 6. الحل المؤقت (Workaround)

**الخطوات**:
1. افتح `دليل الحسابات → خزينة رئيسية`.
2. **في "مراكز التكلفة"** → شيل **`IsMandatory`** من البُعد.
3. احفظ.
4. جرّب ترحيل السند تاني.

**⚠️ تحذير**: ده **حل مؤقت** — لأن البُعد **مش هيتسجل** على السند.

---

## 7. الاختبارات المطلوبة

| # | الاختبار |
|---|---|
| 1 | سند صرف بحساب عليه بُعد إلزامي + فرع محدد → يترحّل تلقائي |
| 2 | قيد يدوي بنفس الحالة → يترحّل تلقائي |
| 3 | سند قبض بحساب عليه بُعد إلزامي → يترحّل تلقائي |
| 4 | لو الفرع نفسه مالهوش `CostCenterDimensionValue` → رفض بـ error واضح |
| 5 | حسابين مختلفين بأبعاد مختلفة على نفس السند → كل واحد ياخد القيمة الصح |

---

## 8. الحلول البديلة (لو الحل 1 معقد)

### الحل 2: حقل بُعد في السند

- إضافة `CostCenterDimensionValueId` على `Voucher`.
- المستخدم يختار البُعد يدويًا.
- **العيب**: خطوة إضافية + عرضة للخطأ.

### الحل 3: بُعد تلقائي من `Voucher.BranchId`

- نفس الحل 1 بس أبسط.
- `Voucher.BranchId` → `CostCenterDimensionValue`.
- **العيب**: أقل مرونة.

### الحل 4: منع البُعد الإلزامي على الخزائن

- المدير المالي **ميلزّمش** البُعد على حسابات الخزينة.
- **العيب**: حل "هروب" — مش حل معماري.

---

## 9. مقارنة مع ملاحظات تانية

**نمط متكرر** في النظام:
- حقول/قواعد **إلزامية** من غير **آلية Auto-Resolve**.
- المستخدم بيتفاجئ بـ **رسالة خطأ وقت الحفظ**.

**اقتراح عام**:
- أي **قاعدة إلزامية** لازم يكون معاها **آلية Auto-Resolve** أو **حقل UI**.
- مراجعة كل `IsMandatory` في النظام.

---

## 10. القرار

| السؤال | الإجابة |
|---|---|
| **الخطورة** | 🔴 عالية |
| **الحل المقترح** | Auto-Resolve من الفرع |
| **الأولوية** | 🔴 عاجل |
| **العلاقة** | مستقل — مش مرتبط بـ Remarks5/6/7 |

---

**ملاحظة**: الحل ده **لازم يتنفذ قبل Offline Database** — لأن الـ Offline هيورّث نفس المشكلة.