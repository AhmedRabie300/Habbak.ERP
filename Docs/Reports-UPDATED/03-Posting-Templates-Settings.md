# شاشة إعدادات الشاشات + محرر قوالب الترحيل — تقرير

> كل حقيقة هنا متقروءة من الكود الفعلي بتاريخ **2026-09-25**. المصادر: `Habbak.ERP.Application/Posting/**`،
> `Habbak.ERP.API/Controllers/Accounting/PostingTemplatesController.cs` +`PostingReportsController.cs`،
> `frontend/src/features/settings/codingRules/**` + `frontend/src/features/accounting/postingTemplates/**`
> + `frontend/src/features/accounting/postingReports/**`.

---

## 1. نظرة عامة

**المسار الفعلي مش شاشة واحدة، شاشتان منفصلتان:**

| الشاشة | المسار | كود الشاشة (`ScreenCode` صلاحيات) | القائمة |
|---|---|---|---|
| إعدادات الشاشات (فيها محرر القوالب) | `/settings/coding-rules` | `SETTINGS_CODING_RULES` | "إعدادات الشاشات" |
| تقارير الترحيل الآلي | `/accounting/posting-reports` | `ACCOUNTING_POSTING_REPORTS` | "تقارير الترحيل الآلي" |

**الملف الفعلي**: [`CodingRulesSettingsPage.tsx`](../../frontend/src/features/settings/codingRules/CodingRulesSettingsPage.tsx)
— اسمها الداخلي "قواعد الترقيم" لسه في اسم الملف والـ hooks (`useCodingRules`)، لكن الشاشة بقت
مُعاد تسميتها فعليًا لـ"إعدادات الشاشات" (تعليق الكود بيقول ده صراحة، مأخوذ من "My Remarks/Remarks2.md
remark 4.1"): **Dropdown واحد** يجمع كل شاشات الترقيم + كل شاشات الترحيل، تختار شاشة وحدة فيفتح تحتها
لوحتين: `ScreenSettingsPanel` (قواعد الترقيم/الحقول الإلزامية) و`PostingSettingsPanel` (إعدادات
الترحيل) — **مش شبكة/جدول فيه العشرين شاشة سوا**، ده اختيار واحدة بواحدة.

**مين له صلاحية؟**
- محرر القوالب (`PostingTemplatesController`) كله محمي بـ `[Screen("SETTINGS_CODING_RULES")]` — نفس
  صلاحية شاشة الترقيم. مفيش كود صلاحية منفصل لمحرر القوالب عن باقي "إعدادات الشاشات".
- التقارير (`PostingReportsController`) محمية بـ `[Screen("ACCOUNTING_POSTING_REPORTS")]` — صلاحية
  مستقلة. `JournalEntriesController` قابل لأي واحدة من الصلاحيتين (`[Screen("ACCOUNTING_JOURNAL_ENTRIES",
  "ACCOUNTING_POSTING_REPORTS")]`) عشان لينك "افتح القيد" من التقارير يشتغل حتى لمستخدم مالوش صلاحية
  شاشة القيود نفسها.
- زرار الإضافة يتقيّد بـ`usePermission('add')`، والتعديل/التفعيل بـ`usePermission('edit')` — نفس نموذج
  صلاحيات الحقول/الأزرار العام في النظام (مفيش صلاحية Approve/Delete هنا: مفيش حذف للقوالب أصلًا).

**إزاي بتشتغل عمومًا:** الشاشة مبنية حول كتالوج ثابت في الكود (`PostingScreenCatalog.All`, **20 شاشة
مُبرمَجة**) بيوصف كل شاشة ممكن ترحّل: حقولها المتاحة للقوالب، مجموعاتها (POS payments)، وقوالبها
القياسية الجاهزة. المدير المالي بيختار شاشة، يشغّل السويتش، يضيف/يعدّل قوالب مبنية بالكامل من قوائم
مقفولة (Dropdowns) — **مفيش خانة كتابة كود أو Expression حرة في أي مكان بالمحرر**، وده أساس التصميم
(القالب يتحفظ ويتفحص وقت الحفظ، مش وقت أول مستند حقيقي).

---

## 2. الجزء الأول: قائمة الشاشات (20 شاشة) — من `PostingScreenCatalog.All`

| الشاشة | `ScreenCode` | بتحرّك مخزون؟ | القوالب القياسية الجاهزة |
|---|---|:---:|:---:|
| فاتورة شراء | `PURCHASING_PURCHASE_INVOICE` | لا | 1 |
| مرتجع مشتريات | `PURCHASING_PURCHASE_RETURN` | نعم | 1 |
| فاتورة مبيعات | `SALES_INVOICE` | لا | 1 |
| أمر تسليم | `SALES_DELIVERY_ORDER` | نعم | 1 |
| مرتجع مبيعات | `SALES_RETURN` | نعم | 2 |
| فواتير نقطة البيع | `POS_INVOICES` | نعم | 2 |
| مرتجع نقطة البيع | `POS_RETURNS` | نعم | 2 |
| فروق الوردية | `POS_SHIFT_VARIANCE` | لا | 1 |
| مصروفات درج الكاشير | `POS_DRAWER_EXPENSE` | لا | 1 |
| إذن إضافة (مخزن) | `INVENTORY_STOCK_IN` | نعم | 1 |
| إذن صرف (مخزن) | `INVENTORY_STOCK_OUT` | نعم | 1 |
| أرصدة افتتاحية (مخزن) | `INVENTORY_OPENING_BALANCES` | نعم | **0 — لا يوجد قالب قياسي** |
| تسوية مخزون مستقلة | `INVENTORY_ADJUSTMENT` | نعم | 1 |
| تسوية الجرد | `INVENTORY_COUNT` | نعم | 1 |
| الهالك | `INVENTORY_WASTE` | نعم | 1 |
| اقتناء أصل ثابت | `FIXED_ASSETS` | لا | 1 |
| تشغيل الإهلاك الشهري | `FIXED_ASSETS_DEPRECIATION_RUNS` | لا | 1 |
| استبعاد أصل | `FIXED_ASSETS_DISPOSALS` | لا | 1 |
| صيانة — تكلفة خارجية | `MAINTENANCE_REQUESTS` | لا | 1 |
| صيانة — قطع غيار من المخزون | `MAINTENANCE_SPARE_PARTS` | نعم | 1 |

**"السويتش" و"عدد/حالة القوالب" مش بيانات ثابتة في الكتالوج — دي حالة حية بترجع من
`GetPostingScreensQuery`** (`Habbak.ERP.Application/Posting/Screens/GetPostingScreensQuery.cs`):
- `isPosting` = `true` لو فيه **قالب واحد على الأقل فعّال** من قوالب الشاشة الحالية (`IsCurrentVersion`)
  — مش عمود منفصل في قاعدة البيانات، بل مُشتق كل مرة.
- `templates[].lineCount` = عدد سطور كل قالب، و`versionNumber`/`isActive` مباشرة من الصف.
- **حالة القالب فعليًا بوضعين بس: فعّال / متوقف** (`PostingTemplate.IsActive` — `bool`). **مفيش
  حالة "مسودة" منفصلة في الكود** رغم إن العنوان المتوقع في التصميم بيقترح 3 حالات — قالب جديد
  (يدوي أو من القياسي) بيتحفظ `IsActive = false` (زي المسودة فعليًا) وده اللي بيبان "متوقف" في
  الواجهة، مفيش تفرقة تانية.

---

## 3. الجزء الثاني: إعدادات الشاشة الواحدة

### 3.1 السويتش الرئيسي — "الشاشة بترحّل قيود؟"

Endpoint: `POST /api/v1/accounting/posting-templates/screens/{screenCode}/active` →
`SetPostingScreenActiveCommand` (`Templates/PostingTemplateCommands.cs:248-271`).

- **مش عمود مستقل** — التفعيل بيشغّل/يوقف **كل القوالب الحالية للشاشة دفعة واحدة** (`IsActive` لكل
  صف). لو فيه قالبين، واحد شغال وواحد متوقف، وبعدين حد ضغط "شغّل الشاشة"، **الاتنين هيبقوا شغالين**
  — مفيش حفظ لحالة كل قالب لوحده عبر تبديل السويتش (نقطة UX مذكورة في قسم 9).
- **On بدون قوالب مرفوض**: `POST-SCREEN-NO-TEMPLATES` — "الشاشة مالهاش قوالب — ضيف قالب الأول".
- **Off** يوقف كل القوالب لكن **مايمسحش حاجة** — القوالب تفضل موجودة بحالتها وتاريخها.
- الواجهة (`PostingSettingsPanel.tsx:79-95`): checkbox في هيدر الكارد، `disabled` لو مفيش صلاحية
  تعديل أو مفيش قوالب أصلًا (`title` بيشرح السبب).

### 3.2 قائمة القوالب

كل صف من `PostingScreenTemplateDto` (`Screens/GetPostingScreensQuery.cs:19-21`):

| الحقل | المصدر |
|---|---|
| الاسم | `NameAr` |
| الترتيب (`ExecutionOrder`) | رقم دائري بيظهر يمين كل صف — ده ترتيب تنفيذ القوالب لما أكتر من قالب على نفس الشاشة (إيراد ثم تكلفة بيع مثلًا) |
| الشرط (`TriggerType`) | نص وصفي مُولَّد (`describeTrigger` في الفرونت): "بيشتغل دايمًا" / "بيشتغل لما المستند يحرّك مخزون بتكلفة" / "بيشتغل لما {حقل} = {قيمة}" |
| الحالة | Badge أخضر "فعّال" / رمادي "متوقف"، + checkbox مستقل بجنبها لتبديل حالة **هذا القالب بمفرده** (`SetPostingTemplateActiveCommand`) |
| الإصدار (`VersionNumber`) | رقم فقط، بدون رابط لتاريخ الإصدارات القديمة في الواجهة (قسم 9) |
| عدد السطور | `LineCount` |

### 3.3 إضافة قالب جديد

زرارين فوق القائمة (`PostingSettingsPanel.tsx:119-130`):
1. **"ابدأ من القياسي (N)"** — يظهر بس لو `defaultTemplateCount > 0` **و**مفيش قوالب على الشاشة
   خالص. بينادي `CreateDefaultPostingTemplatesCommand` اللي بيضيف *كل* القوالب القياسية للشاشة
   دفعة واحدة (مش قالب واحد يتختار) — مرفوض لو الشاشة فيها أي قالب بالفعل (`POST-TEMPLATE-DEFAULTS-EXIST`)
   عشان الضغط مرتين ميضاعفش القيود. كل قالب قياسي بيتحفظ **متوقف** إجباريًا.
2. **"إضافة قالب"** — يفتح `TemplateEditor` فاضي (قسم 4)، بيبدأ بسطرين (مدين + دائن فاضيين).

مفيش نسخ (Copy/Duplicate) من قالب موجود، ومفيش استيراد من شركة/فرع تاني.

---

## 4. الجزء الثالث: محرر القالب — `TemplateEditor` في `PostingSettingsPanel.tsx:279-427`

### 4.1 المعلومات الأساسية
اسم عربي/إنجليزي (`NameAr`/`NameEn` إلزاميين)، وصف اختياري (`Description`, حتى 1000 حرف)، ترتيب
تنفيذ (`ExecutionOrder`, 1–99). **`LineDescription` على مستوى السطر موجود في الـDTO والـDomain لكن
مفيهوش أي حقل إدخال في الواجهة** — بيتحفظ `null` دايمًا من الفرونت (قسم 9).

### 4.2 متى يشتغل — `TriggerType` (مش `ConditionType`؛ ده اسم مختلف على مستوى السطر، قسم 4.3)

3 قيم فقط، Dropdown مقفول:
| القيمة | الوصف | شرط ظهورها |
|---|---|---|
| `Always` | يشتغل مع كل مستند من الشاشة | دايمًا متاحة |
| `HasStockMovement` | يشتغل بس لو المستند حرّك مخزون بتكلفة | متاحة بس لو `screen.canMoveStock` — لو مش كده وحاولت تحفظه بيترفض `POST-TEMPLATE-TRIGGER-NOT-APPLICABLE` |
| `FieldCondition` | يشتغل لو حقل نصي معيّن (`PaymentType` مثلًا) بيساوي قيمة من قايمة قيمه المقفولة | لازم تختار حقل من حقول الشاشة (`Text`/`Id`) وقيمة من `Choices` بتاعته |

**مفيش قيمة "Manual"** رغم إنها موجودة في المواصفة الأصلية — اتشالت عمدًا لأن مفيش أي كود بينادي
عليها (نفس مشكلة "الإعداد الميت" اللي اتحلت في G-6، حسب تعليق `Domain/Posting/PostingTemplate.cs:157-159`).

### 4.3 السطور — `PostingTemplateLine`

كل سطر Debit أو Credit (قسمين منفصلين في الواجهة، كل واحد له زرار "إضافة سطر" خاص):

**`AccountSourceType`** (5 قيم، الخيارات بتتصفّى حسب الشاشة):
| القيمة | معناها | مصدر الرقم وقت الترحيل |
|---|---|---|
| `Fixed` | حساب ثابت مُختار من شجرة الحسابات | `FixedAccountId` — لازم يكون Postable ونشط |
| `FromCompany` | دور محاسبي عام (`CompanyAccountRole`, مثل `Inventory`/`SalesRevenue`) | بيتحل من شاشة "حسابات الترحيل الافتراضية" |
| `Resolver` | Resolver مسجّل بالكود (`Supplier.PayableAccountId`، `Customer.ReceivableAccountId`، `POSTerminal.CashTreasuryAccount`) | بيظهر بس لو الشاشة فيها الحقل اللي الـResolver محتاجه |
| `FromDocument` | حقل من نوع `Account` على المستند نفسه (مثل `ExpenseAccountId` في مصروف الدرج) | بيظهر بس لو فيه حقول `Account` في الشاشة |
| `FromGroup` | حساب لكل عنصر في مجموعة (مثال: خزينة كل جهاز/طريقة دفع في POS) — مربوط دايمًا بـ`AmountFormulaType.GroupItemAmount` | بيظهر بس لو الشاشة فيها Groups (POS بس حاليًا) |

**`AmountFormulaType`** — **11 قيمة في الـDomain enum، لكن المحرر بيعرض 6 بس + `GroupItemAmount`
ضمنيًا**: `DirectField`، `PercentageOf` (×نسبة%)، `Multiply` (×معامل)، `AddFields` (جمع 2–5 حقول)،
`SubtractFields`/`DivideFields` (حقلين بالظبط). الـ4 الباقيين (`SumLineQuantityTimesUnitPrice`،
`SumLineQuantityTimesUnitCost`، `SubtotalMinusDiscount`، `SumShiftVarianceLiability`) **مُنفَّذين
فعليًا في `PostingFormulas.cs` لكن مالهومش أي Dropdown في الواجهة** — قالب يستخدمهم لازم يتحفظ عبر
نداء API مباشر، مش من المحرر (قسم 9).

**`ConditionType`** (على مستوى السطر، مختلف عن `TriggerType` بتاع القالب كله): `None` /
`FieldGreaterThanZero` / `FieldNotNull` / `FieldEquals` (بقيمة نصية). بيستخدم غالبًا لسطور اختيارية
زي الضريبة أو الخصم (`optional: true` في تعريفات القوالب القياسية).

**`CostCenterSourceType`** (حتى 5 أبعاد للسطر الواحد، قاعدة 01-Module-Accounting §24): `Fixed`
(قيمة بُعد ثابتة)، `FromDocument` (من حقل Id في المستند نفسه)، `FromContext` (قيمة بيوفّرها الموديول
مش حقل فعلي — زي كاشير الوردية)، `FromRelatedEntity` (حقل من سجل مرتبط، زي كاشير الشيفت)، `Dynamic`
(بس لـ`Branch.ToCostCenterValue` — فرع المستند تلقائيًا). الواجهة بتصفّي الأبعاد المتاحة حسب
`LinkedEntityType` بتاع البُعد عشان تمنع ربط بُعد فرع بحقل مورد غلط.

### 4.4 المعاينة (Preview)

مستويين، من غير حفظ حاجة (`Templates/PreviewPostingTemplateQuery.cs`):
- **معاينة قالب واحد** (وأنت بتحرره، قبل الحفظ) — `PreviewPostingTemplateQuery`.
- **معاينة كل قيود الشاشة سوا** — `PreviewPostingScreenQuery`، بيجيب كل القوالب الحالية للشاشة
  بترتيب `ExecutionOrder` ويحسبهم كلهم على نفس المستند التجريبي.

المعاينة بتاخد "مستند مثال" (`PostingPreviewSample` — قيم مبلغية لكل حقل Amount، نص لكل حقل
Text/Choices، و`hasStockMovement` boolean) وبترجع لكل سطر: الحساب (أو وصف مصدره لو مش معروف دلوقتي
زي "حساب المورد نفسه")، المبلغ المحسوب، **نص الصيغة بالأرقام** ("الإجمالي × 14% = 1,000.00 × 0.14")،
مراكز التكلفة، وهل السطر اتخطّى لأن شرطه مايتحققش. وفي الآخر: مدين/دائن الإجمالي، متزن ولا لأ، وقائمة
"مشاكل" (`Problems`) زي دور محاسبي مش مربوط أو مبلغ ≤ صفر بدون شرط حماية — **دي بتظهر قبل أي مستند
حقيقي يتأثر**.

### 4.5 الحفظ والتفعيل

- **قالب جديد**: بيتحفظ **متوقف دايمًا** بغض النظر عن أي حاجة تانية — الفرونت بيبعت `isActive: false`
  صراحةً مهما كانت قيمة افتراضية الـCommand. المدير المالي لازم يراجعه ويفعّله يدويًا (checkbox
  منفصل في قائمة القوالب، قسم 3.2).
- **تعديل قالب**: قسم 5.

---

## 5. الجزء الرابع: إصدارات القوالب

**إمتى بيتعمل إصدار جديد؟** `UpdatePostingTemplateCommand`
(`Templates/PostingTemplateCommands.cs:149-217`):
- لو القالب **معملش قيد أبدًا** (مفيش `JournalEntryTemplateSnapshot` بيشاور عليه) → **تعديل في
  نفس الصف** (نفس `Id`، `VersionNumber` زي ما هو).
- لو القالب **عمل قيد ولو مرة واحدة** → صف جديد بالكامل: `VersionNumber + 1`،
  `PreviousVersionId = القديم`، `FamilyId` بنفس القيمة، `IsCurrentVersion = true` على الجديد و
  `false` على القديم. الـResponse (`UpdatePostingTemplateResult`) بيرجّع `isNewVersion: true` والـ
  `Id` الجديد — الفرونت بيغيّر رسالة التوست تبعًا لده (`postingSettings.savedNewVersion`).
- نفس فلسفة `Recipe.CreateNewRecipeVersionCommand` في المخزون بالظبط.

**الـSnapshots**: `JournalEntryTemplateSnapshot` (`Domain/Posting/PostingTemplate.cs:134-146`) —
لقطة كاملة (`TemplateSnapshotJson`) + `TemplateVersionNumber` + `PostingGroupId` (يربط كل قيود
مستند واحد سوا لو أكتر من قالب اشتغل) + `IdempotencyKey` خاص بالقيد ده تحديدًا + `RequestIdempotencyKey`
خاص بالمستند كله. بتتكتب إجباريًا مع أي قيد آلي.

**القيود القديمة بتربط بإصدارها** — `PostingTemplateEngine.ReverseAsync`
(`Posting/PostingTemplateEngine.cs:328-390`) بيعكس **سطور القيد الأصلي نفسه** (مدين↔دائن) مش بيعيد
تشغيل القالب الحالي، فتعديل القالب بعدين **مايأثّرش على عكس قيد قديم**. العكس الجماعي: عكس أي قيد في
`PostingGroupId` بيعكس **كل** قيود المجموعة سوا (إيراد + تكلفة بيع مع بعض) مش قيد لوحده.

**ملاحظة**: مفيش شاشة/تبويب في الواجهة لعرض الإصدارات القديمة لقالب — الـQuery اللي بيدعم كده
(`GetPostingTemplatesListQuery(IncludeHistory: true)`) موجود ومكشوف على `GET
/accounting/posting-templates?includeHistory=true` لكن **مفيهوش أي نداء له من الفرونت** (قسم 9).

---

## 6. الجزء الخامس: تقارير الترحيل — 9 تبويبات

شاشة منفصلة `/accounting/posting-reports` (`PostingReportsPage.tsx`)، تبويبات Pills فوق (`role="tablist"`)
بترتيب ثابت مش قابل للتخصيص، كل تبويب بيناديه `GET /api/v1/accounting/posting-reports/{path}`:

| # | التبويب | الـEndpoint | المصدر |
|---|---|---|---|
| 1 | القيود الفاشلة | `GET failures?includeResolved=` | `GetPostingFailuresReportQuery` — من `PostingFailure` (جدول مستقل، بيتكتب برّه ترانزاكشن المستند نفسه عشان مايتمسحش مع الـRollback) |
| 2 | المستندات اللي مترحّلتش | `GET unposted-documents` | `GetUnpostedDocumentsReportQuery` — لكل شاشة: هل مفعّلة؟ وعدد المستندات بدون قيد (كل التاريخ + بعد لحظة التفعيل بس) |
| 3 | القيود الآلية | `GET auto-entries?from=&to=&sourceModule=&branchId=` | `GetAutoEntriesReportQuery` |
| 4 | القيود حسب المصدر | `GET by-source?from=&to=` | `GetEntriesBySourceReportQuery` — مجمّعة بالموديول/نوع المستند |
| 5 | قيود نقطة البيع | `GET pos-entries?from=&to=&branchId=` | `GetPOSEntriesReportQuery` |
| 6 | فروق الورديات | `GET shift-variances?from=&to=&branchId=` | `GetShiftVarianceReportQuery` |
| 7 | استخدام القوالب | `GET template-usage` | `GetTemplateUsageReportQuery` — كل الإصدارات (مش الحالي بس)، عدد قيود كل إصدار وآخر استخدام |
| 8 | حالة الفترات المحاسبية | `GET periods` | `GetPeriodStatusReportQuery` |
| 9 | تكامل القيود | `GET integrity` | `GetEntryIntegrityReportQuery` — قيود مش متزنة أو الهيدر مش متفق مع السطور (لازم تكون فاضية دايمًا؛ `IPostingService` بيرفض إنشاء أي حاجة كده أصلًا) |

كل تبويب بجدول عام (`Table<T>` مكوّن مشترك). تبويبات `auto`/`bySource`/`pos`/`variances` بس هي
اللي بتظهر لها فلتر تاريخ (من/إلى) — الباقي بيرجع كل شيء دفعة واحدة.

---

## 7. الجزء السادس: الـ API

### 7.1 Endpoints — `PostingTemplatesController` (`api/v1/accounting/posting-templates`)

| Method | المسار | Command/Query |
|---|---|---|
| GET | `/screens` | `GetPostingScreensQuery` |
| POST | `/screens/{screenCode}/active` | `SetPostingScreenActiveCommand` |
| POST | `/defaults` | `CreateDefaultPostingTemplatesCommand` |
| POST | `/preview` | `PreviewPostingTemplateQuery` |
| POST | `/preview-screen` | `PreviewPostingScreenQuery` |
| GET | `/` (`?screenCode=&includeHistory=`) | `GetPostingTemplatesListQuery` |
| GET | `/catalog` | `GetPostingEngineCatalogQuery` |
| GET | `/{id}` | `GetPostingTemplateByIdQuery` |
| POST | `/` | `CreatePostingTemplateCommand` |
| PUT | `/{id}` | `UpdatePostingTemplateCommand` |
| POST | `/{id}/activate` | `SetPostingTemplateActiveCommand(true)` |
| POST | `/{id}/deactivate` | `SetPostingTemplateActiveCommand(false)` |

**+ `PostingReportsController`** (`api/v1/accounting/posting-reports`): 9 مسارات GET، واحد لكل
تبويب في قسم 6.

### 7.2 Permission Model

- `[Screen("SETTINGS_CODING_RULES")]` على `PostingTemplatesController` بالكامل — **مفيش تفريق بين
  "قدرة على المشاهدة" و"قدرة على التعديل" على مستوى الـController**؛ التفريق بيحصل في الفرونت بس
  (`usePermission('add')`/`('edit')` تتحكم في ظهور الأزرار، مش في السيرفر مباشرة لمعظم الأفعال —
  الحماية الحقيقية هي إن GET محتاج `View` وPOST/PUT محتاجين `Add`/`Edit` تلقائيًا حسب نمط الفعل من الـHTTP verb، زي كل كنترولر في النظام).
- `[Screen("ACCOUNTING_POSTING_REPORTS")]` منفصلة تمامًا للتقارير.
- مفيش صلاحية `Approve` أو `Delete` مُعرَّفة لمحرر القوالب: مفيش اعتماد منفصل لتفعيل قالب (التفعيل
  نفسه هو فعل `Edit`)، ومفيش حذف للقوالب خالص (قسم 9).

### 7.3 الـ Validators

`PostingTemplateDefinitionValidator` (`Templates/PostingTemplateQueries.cs:60-166`, FluentValidation)
— أهم القواعد:
- اسم عربي/إنجليزي إلزاميين، وصف حتى 1000 حرف، ترتيب تنفيذ 1–99.
- القالب لازم فيه **سطر مدين واحد على الأقل وسطر دائن واحد على الأقل**، وأرقام السطور لازم تكون فريدة.
- حسب `AccountSourceType`/`AmountFormulaType`/`CostCenterSourceType` لكل سطر: كل مصدر بيتطلب
  حقوله المرتبطة بيه (مثال: `Multiply` محتاج معامل، `AddFields` محتاج 2–5 حقول).
- **`AccountResolverKey` بيتفحص بالاسم الحرفي مش `Enum.TryParse`** — تعليق الكود بيوضّح السبب: TryParse
  هيقبل "999" كمان وهتفشل بس أول مرة تترحّل فعليًا، مش وقت الحفظ.
- `PostingTemplateDefinitionChecks.CheckAgainstScreen` (قبل أي حفظ) — كل اسم حقل/مجموعة/Resolver
  مستخدم في القالب لازم يكون من حقول الشاشة نفسها فعليًا (مش أي نص)، وحد أقصى 5 أبعاد تكلفة للسطر.
- `PostingTemplateDefinitionChecks.CheckAsync` (بيحتاج قاعدة البيانات) — الحساب الثابت لازم يكون
  Postable ونشط، البُعد لازم يكون نشط ومربوط فعليًا بالحساب لو الحساب ثابت، وقيمة البُعد الثابتة
  لازم تتبع نفس البُعد وتكون نشطة.

---

## 8. الـ Frontend

### 8.1 الملفات

| الملف | السطور | الدور |
|---|---|---|
| [`CodingRulesSettingsPage.tsx`](../../frontend/src/features/settings/codingRules/CodingRulesSettingsPage.tsx) | 197 | الصفحة الحاضنة: Dropdown اختيار شاشة + `ScreenSettingsPanel` (ترقيم) + `PostingSettingsPanel` (ترحيل) |
| [`postingTemplates/PostingSettingsPanel.tsx`](../../frontend/src/features/accounting/postingTemplates/PostingSettingsPanel.tsx) | 802 | لوحة الترحيل + محرر القالب + المعاينة بالكامل — أكبر ملف في هذا الجزء |
| [`postingTemplates/api.ts`](../../frontend/src/features/accounting/postingTemplates/api.ts) | 237 | كل الـTypes + React Query hooks |
| [`postingReports/PostingReportsPage.tsx`](../../frontend/src/features/accounting/postingReports/PostingReportsPage.tsx) | 291 | شاشة التقارير التسعة |

### 8.2 Components

- `PostingSettingsPanel` → `TemplateRow` (صف في القائمة) → `TemplateEditor` (المحرر) →
  `LineEditor` (سطر) → `CostCenterRow` (بُعد تكلفة على السطر). تسلسل تعشيش واضح، كله جوه ملف واحد
  (مفيش تقسيم لملفات منفصلة لكل مكوّن فرعي).
- `SampleEditor` مشترك بين معاينة القالب الواحد ومعاينة الشاشة كلها.
- `PreviewTable` مكوّن جدول القيد التجريبي، مستخدم في الحالتين.
- `NotWiredPostingPanel` — بديل بيظهر لو شاشة عندها قاعدة ترقيم بس **مش من الـ20 شاشة اللي بترحّل**
  (نفس فكرة "السويتش ظاهر بس معطّل" اللي مذكورة في تعليق الكود: "every screen shows its posting
  settings ... it cannot be turned on").
- تقارير: مكوّن `Table<T>` عام واحد بيُستخدم في كل الـ9 تبويبات.
- **مفيش استخدام لمكوّنات `ui-kit` القياسية زي `DataGrid`/`ActionBar` هنا** — الشاشتين مبنيتين
  بـ`Card`/`Button`/`SearchableSelect`/`Input`/`Badge` مباشرة + جداول HTML يدوية، مختلف عن نمط
  شاشات List/Edit القياسي في باقي النظام.

### 8.3 الـ State Management

- **مفيش Store مخصص (Zustand)** لأي جزء من الشاشتين — كل الحالة `useState` محلي + TanStack Query
  (`useQuery`/`useMutation`) من `postingTemplates/api.ts`. الـInvalidation بعد أي تعديل بسيط:
  `invalidateQueries(['posting-screens'])` + `['posting-template']` (`useInvalidateScreens`,
  `api.ts:173-179`) — يعني أي حفظ/تفعيل بيعيد تحميل **كل** شاشات الترحيل مش الشاشة الحالية بس.
- التوست (`useToastStore`) هو القناة الوحيدة لرسائل النجاح/الفشل — مفيش Modal تأكيد لأي فعل هنا
  (لا للتفعيل، لا للحفظ، لا لإنشاء القوالب القياسية).

---

## 9. الـ Risks / Gaps

| # | المشكلة | النوع | التفاصيل |
|---|---|---|---|
| 1 | السويتش الرئيسي بيشغّل/يوقف **كل** قوالب الشاشة سوا | UX | مفيش حفظ لحالة كل قالب لوحده عبر تبديل السويتش (قسم 3.1) — لو كان عندك قالب متوقف عمدًا وقالب شغال، وقفت الشاشة وشغّلتها تاني، الاتنين هيرجعوا شغالين |
| 2 | `LineDescription` بلا حقل إدخال في الواجهة | UX/Gap | موجود في الـDTO والـDomain بالكامل، لكن `emptyLine()` في الفرونت بيحطه `null` دايمًا ومفيش أي `<Input>` ليه في `LineEditor` |
| 3 | 4 من الـ11 قيمة لـ`AmountFormulaType` مش متاحة من المحرر | Gap | `SumLineQuantityTimesUnitPrice`/`SumLineQuantityTimesUnitCost`/`SubtotalMinusDiscount`/`SumShiftVarianceLiability` مُنفَّذين في `PostingFormulas.cs` بس محدش يقدر يستخدمهم غير بنداء API مباشر برّه الواجهة |
| 4 | مفيش شاشة لعرض تاريخ إصدارات القالب | Gap | `GetPostingTemplatesListQuery(includeHistory: true)` موجود ومكشوف على الـAPI، صفر استخدام في الفرونت |
| 5 | مفيش حذف للقوالب، ومفيش تأكيد Modal لأي فعل | تصميم متعمد (مش خطأ) | القوالب بتتوقف مش بتتمسح (قصدًا — إصدار قديم لازم يفضل مقروء)؛ لكن غياب أي تأكيد قبل "إيقاف شاشة بترحّل فعليًا الآن" نقطة UX تستاهل مراجعة |
| 6 | صلاحية واحدة (`SETTINGS_CODING_RULES`) لكل عمليات محرر القوالب | Security/UX | مفيش صلاحية Approve منفصلة لتفعيل قالب حساس (بيغيّر سلوك محاسبي فوري) عن مجرد تعديل مسودة — أي حد معاه Edit يقدر يفعّل قالب مباشرة |
| 7 | `PostingSettingsPanel.tsx` ملف واحد 802 سطر | Maintainability | كل المحرر + المعاينة + السطور + الأبعاد في ملف واحد بلا تقسيم |
| 8 | Invalidation عام لكل الشاشات بعد أي حفظ | أداء بسيط | `['posting-screens']` بيتبطّل بالكامل حتى لو غيّرت شاشة واحدة — مفيش استهداف دقيق |

---

## 10. الـ Tests

| الملف | العدد | التغطية |
|---|---|---|
| `Habbak.ERP.ApiTests/PostingTemplatesTests.cs` | 5 `[Fact]` | كتالوج الـResolvers، إنشاء/قراءة قالب، رفض قالب بلا سطر دائن، تعطيل بيحافظ على القالب، عزل بين الشركات |
| `Habbak.ERP.IntegrationTests/PostingTemplateEngineTests.cs` | 22 `[Fact]` | أشمل ملف: بناء القيد (شروط/تخطي/عدم اتزان)، Idempotency، تخطي الاعتماد للقيود الآلية، **العكس الجماعي (`SecondTemplate_...AndCancellingReversesBoth`)**، **الفهرسة/الإصدارات (`Update_BeforeAnyPosting_EditsInPlace_AfterPosting_CreatesNextVersion`)**، الشروط (`FieldCondition`/`HasStockMovement`)، الصيغ الحسابية بالجمع/الطرح/القسمة (والقسمة على صفر بتوقف الترحيل)، مراكز التكلفة من كيان مرتبط، رفض بُعد غير مربوط بحساب ثابت وقت الحفظ |
| `Habbak.ERP.IntegrationTests/PostingServiceTests.cs` | — | خدمة الترحيل الأساسية (`IPostingService`) اللي المحرك بينادي عليها |
| `Habbak.ERP.ApiTests/OperationalCycle/DocumentPostingTests.cs` | 11 `[Fact]` | تكامل مستندات حقيقية (فاتورة شراء/مخزون...) مع المحرك من طرف لطرف |
| `Habbak.ERP.ApiTests/OperationalCycle/PostingReportsTests.cs` | 1 `[Fact]` | تغطية خفيفة جدًا لتقارير الترحيل — تبويب واحد بس مُختبَر صراحة رغم وجود 9 |
| `Habbak.ERP.ApiTests/OperationalCycle/PostingIdempotencyTests.cs` | 4 `[Fact]` | إعادة إرسال نفس المفتاح ما بيكررش القيد |
| `Habbak.ERP.ApiTests/AccountMappingsTests.cs` | — | تعيينات حسابات الشركة (`CompanyAccountRole`) اللي القوالب بتعتمد عليها |

**فجوة اختبار واضحة**: تقارير الترحيل (9 تبويبات كاملة في الواجهة) مُغطاة باختبار واحد فقط على
مستوى الـAPI — لا يوجد اختبار مخصص لكل تقرير من الـ9 على حدة.

---

خلصت التقرير.
