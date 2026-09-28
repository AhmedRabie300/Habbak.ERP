# Phase 1.2 — Research Pass (Migration الحقول الحرة)

**التاريخ**: 2026-09-27
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md` § Phase 1.2 · `Docs/Implementation/HR-Core-Plan.md §1.2`

---

## 1. فحص الكود الفعلي — الأربعة حقول

| الحقل | الملف | الحالة الفعلية المتحقق منها |
|---|---|---|
| `User.EmployeeId` | `src/Habbak.ERP.Domain/Settings/Users/User.cs:31` | `long?`، بتعليق صريح فوقه: *"Employee record, once HR exists — no FK until then"*. **مفيش أي Mapping/FK/Navigation** في `UserConfiguration.cs` (بحث كامل عن `EmployeeId` رجّع صفر نتائج) — عمود بسيط اتعمل بالـ Convention بس، بدون علاقة. |
| `CustodyRegister.EmployeeId` | `src/Habbak.ERP.Domain/Accounting/CustodyRegister.cs:11` | `long` **إلزامي** (مش Nullable). `CustodyRegisterConfiguration.cs` فيها تعليق صريح: *"EmployeeId references the future HR module's aggregate — plain column, no FK"*. مفيش أي Navigation Property لـ `Employee` في الكيان. |
| `CustodyOfficer` | `src/Habbak.ERP.Domain/Inventory/CustodyOfficer.cs` | **مفيش عمود `EmployeeId` خالص دلوقتي** — الكيان كامل مبني كـ `ILookupEntity` قياسي (`Code`/`NameAr`/`NameEn`/`IsActive`)، ومعاه تعليق طويل بيوثّق القرار: "الحقل الرسمي هو EmployeeId... لسه مش مبني... لما HR يتبنى، ده يتحول لـ EmployeeId حقيقي بعملية migration منفصلة" — يعني **هذه المرحلة بالظبط هي المقصودة بالتعليق ده**. |
| `MaintenanceRequest.TechnicianId`/`TechnicianName` + `MaintenanceSchedule.TechnicianId`/`TechnicianName` | `src/Habbak.ERP.Domain/FixedAssets/Maintenance.cs:68-69,139-140` | عمودين `long?` + `string?` في كل كيان، بتعليق: *"No FK — technicians wait for HR (section 6.2, item 2). The name is kept as it was"*. مفيش FK في `FixedAssetsConfigurations.cs` (`MaintenanceRequestConfiguration`/`MaintenanceScheduleConfiguration` — كل الـ `HasOne(...)` الموجودة لسطور تانية زي `Issue`/`FixedAsset`/`Supplier`، ومفيش سطر لـ `TechnicianId`). |

**نتيجة**: التوصيف في `HR-Core-Plan.md §1.2` (والمنقول في Master Plan) **لسه مطابق للكود بالحرف** — مفيش أي تغيير حصل على الأربعة حقول دول من وقت كتابة الخطة.

**الكيان اللي هيتربط بيه (`FixedAsset.CustodyOfficerId`)**: `src/Habbak.ERP.Domain/FixedAssets/FixedAsset.cs:87` — `long? CustodyOfficerId` **موجود فعلًا ومربوط بـ FK** لـ `CustodyOfficer` (الموديول 08 بالفعل). يعني لما `CustodyOfficer.EmployeeId` يتضاف في هذه المرحلة، سلسلة `FixedAsset → CustodyOfficer → Employee` بتكتمل من غير أي تعديل على `FixedAsset` نفسه — مهم لـ Phase 1.4 (فحص عهدة الأصول).

**`DeleteCustodyOfficerCommand.cs`**: تأكيد الملاحظة الموجودة في الخطة — **مسح مباشر بدون أي تحقق** (`db.CustodyOfficers.Remove(entity); await db.SaveChangesAsync();`، سطر 19-20)، مفيش فحص لو فيه `FixedAsset` مرتبط أو استخدام تاريخي. الفحص ده **خارج نطاق Phase 1.2** (مفيش طلب بتعديله)، بس يتسجل كملاحظة لأي مرحلة مستقبلية تلمس دورة حياة `CustodyOfficer`.

**Registry الشاشات**: `HR_CUSTODY_MIGRATION` (الشاشة الجديدة المطلوبة في 1.2.4) **مسجّلة في مكانين بس حاليًا: صفر** — بحث في `ScreenCodeCatalog.cs` و`MenuItemSeedData.cs` رجّع صفر نتائج لـ `HR_CUSTODY_MIGRATION`. الشاشات الأخرى من نفس المجموعة (`HR_INSURANCE_OFFICES`, `HR_DOCUMENT_TYPES`) مسجّلة في نفس الملفين، فده النمط الجاهز للاتباع في 1.2.4.

---

## 2. فحص بيانات الإنتاج الفعلية (قاعدة البيانات الحقيقية `HabbakErp`)

> استُعلم مباشرة من `HabbakErp` (مش قاعدة تجريبية) عن طريق `sqlcmd`.

| الجدول | إجمالي الصفوف | تفصيل |
|---|---|---|
| `Employees` | **0** | **الجدول فاضي تمامًا.** رغم إن Phase 1.1 اتنفذت وأُختبرت بالكامل، محدش أدخل موظف حقيقي في النظام الحي لسه — منطقي، لأن شاشة الإدخال (`HR_HIRING`/`HR_EMPLOYEES`) لسه Phase 1.5 (Frontend) ومحصلتش. |
| `Users` | 2 (`system`, `admin`) | **كلاهما `EmployeeId = NULL`.** يؤكد وصف الخطة: "القيم الحالية على الأغلب فاضية". |
| `CustodyRegisters` | 4 | **الأربعة صفوف `EmployeeId = 1` بالظبط** (نفس القيمة مكررة) — قيمة placeholder واضحة من قبل ما HR يوجد، **مالهاش أي معنى حقيقي** دلوقتي لأن مفيش موظف رقمه 1 (الجدول فاضي). `BranchId` فاضي (`NULL`) في 3 من الـ 4 صفوف، متسق مع تعليق الكيان "direct nullable column". |
| `CustodyOfficers` | 2 | الكودين `CO-01` و`E2E-CO-MAADI` — **الكود التاني (`E2E-CO-MAADI`) يبدو بوضوح بيانات باقية من اختبار E2E** (Naming Convention مطابق لأسلوب Test Fixtures)، مش بيانات تشغيل حقيقية. الأسماء (`Ahmed Transfer Officer`, `Mahmoud - Maadi Custody Officer`) نصوص عامة/تجريبية مش أسماء موظفين حقيقيين قابلين للمطابقة. |
| `MaintenanceRequests` / `MaintenanceSchedules` | 0 / 0 | **فاضيين تمامًا** — موديول الصيانة (08) لسه مالوش استخدام تشغيلي حقيقي، فحقول `TechnicianId`/`TechnicianName` مفيش عليها أي بيانات تتفحص أصلًا. |

### الأثر على خطة Phase 1.2 كما هي مكتوبة

1. **تقرير التطابق التلقائي (`FreeFieldDiagnosticReport`) هيرجّع صفر تطابقات بيقين لكل الحقول الأربعة دلوقتي** — مش لأن المنطق غلط، لكن لأن **مفيش موظفين حقيقيين في `Employees` أصلًا يتقارن بيهم أي حاجة**. هذا متوقّع ومتسق مع كون Phase 1.1 (Backend) خلصت قبل Phase 1.5 (Frontend/إدخال بيانات).
2. **شاشة `HR_CUSTODY_MIGRATION` (1.2.4) هتفتح على 4 صفوف من غير أي مرشح ربط** — الوظيفة (الربط اليدوي) صحيحة وهتشتغل بمجرد ما أول موظف حقيقي يتسجل، بس مفيش سيناريو حقيقي لاختبارها بمعطيات إنتاج فعلية النهاردة — الاختبار هيكون Integration بموظف Seed مش بيانات حقيقية.
3. **الـ 2 صف في `CustodyOfficers` أحدهم (`E2E-CO-MAADI`) مرشّح قوي إنه بيانات اختبار متسربة للإنتاج** — ده مش جزء من نطاق Phase 1.2 (مفيش طلب لتنظيف بيانات)، لكن يستأهل يتسجل كملاحظة منفصلة في `Known-Issues.md` مش يتصلح ضمن هذا الـ Batch (خارج نطاق "الحقول الحرة" تحديدًا).
4. **لا داعي لأي مخاوف على فقدان بيانات حقيقية** أثناء الـ Migration — القيم الوحيدة الموجودة (`CustodyRegister.EmployeeId = 1` × 4) هي بيانات ما قبل-HR بلا معنى، والـ Migration (Option A) أصلًا مصمم على أساس عدم لمسها.

---

## 3. تأكيد قرار Option A

القرار المحسوم في `HR-Core-Plan.md §1.2` (2026-09-26) **لسه صالح وموصى بيه بقوة أكتر بعد الفحص**: بما إن القيم الحالية في `CustodyRegister.EmployeeId` بلا معنى (placeholder `1` في الأربعة صفوف)، تحويلها المباشر لـ FK حقيقي (Option B الضمنية المرفوضة) كان هيربط الأربعة صفوف غلط بموظف عشوائي لو حد اسمه صدفة برقم 1. Option A (عمود جديد `EmployeeIdLinked` بجانب القديم، بدون لمس الأصلي، وربط يدوي فقط) **يتفادى هذا الخطر تمامًا**.

---

## 4. الملفات المتأثرة (مؤكدة من الكود، مش افتراض)

| الغرض | الملف | نوع التغيير |
|---|---|---|
| تقرير تشخيصي | `src/Habbak.ERP.Application/HR/Migration/FreeFieldDiagnosticReport.cs` | جديد |
| `CustodyOfficer` + FK | `src/Habbak.ERP.Domain/Inventory/CustodyOfficer.cs` | تعديل (إضافة `EmployeeId?`) |
| Config لـ `CustodyOfficer` | `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Inventory/CustodyOfficerConfiguration.cs` | تعديل (FK جديد) |
| `CustodyRegister` عمود جديد | `src/Habbak.ERP.Domain/Accounting/CustodyRegister.cs` | تعديل (إضافة `EmployeeIdLinked`، **القديم يفضل زي ما هو حرفيًا**) |
| Config لـ `CustodyRegister` | `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Accounting/CustodyRegisterConfiguration.cs` | تعديل (Mapping للعمود الجديد بس، Nullable، بدون FK Enforced) |
| `Maintenance` FKs | `src/Habbak.ERP.Infrastructure/Persistence/Configurations/FixedAssets/FixedAssetsConfigurations.cs` | تعديل (`MaintenanceRequestConfiguration`/`MaintenanceScheduleConfiguration` — إضافة `HasOne(...).HasForeignKey(r => r.TechnicianId)`) |
| `User` FK (اختياري) | `src/Habbak.ERP.Infrastructure/Persistence/Configurations/Settings/UserConfiguration.cs` | تعديل محتمل (توثيق بس فعليًا — راجع 1.2.5) |
| تسجيل الشاشة الجديدة | `src/Habbak.ERP.Application/Common/Coding/ScreenCodeCatalog.cs` + `src/Habbak.ERP.Infrastructure/Persistence/Seeding/MenuItemSeedData.cs` | تعديل (إضافة `HR_CUSTODY_MIGRATION`، نفس نمط `HR_INSURANCE_OFFICES`) |
| Frontend شاشة الربط | `frontend/src/features/hr/custodyMigration/` (جديد) + Banner على `frontend/src/features/inventory/custodyOfficers/CustodyOfficersListPage.tsx` أو شاشة العهد المحاسبية المقابلة (يتحدد بدقة وقت 1.2.4 — شاشة العهد الحالية المقصودة في الخطة هي عهد `CustodyRegister` وليست `CustodyOfficer`؛ الملف الفعلي لشاشة `CustodyRegister` لسه محتاج تحديد دقيق وقت التنفيذ) | جديد/تعديل |

> ⚠️ ملاحظة دقة صغيرة اكتشفتها أثناء الفحص: `Docs/Implementation/HR-Core-Plan.md §1.2` بيقول الـ Banner يتحط على "شاشة العهد الحالية" في سياق `CustodyRegister` (عهد نقدية، موديول الحسابات) — الملف الموجود فعليًا اللي فحصته (`CustodyOfficersListPage.tsx`) هو شاشة **`CustodyOfficer`** (مسؤول عهدة نقل مخزني، موديول المخازن) — كيان مختلف تمامًا حسب تعليق الكيان نفسه ("مفهوم مختلف تمامًا عن CustodyRegister في موديول الحسابات"). لازم أوضح إن شاشة `CustodyRegister` الفعلية (المحاسبية) هي اللي هتاخد الـ Banner، مش `CustodyOfficersListPage.tsx` — هحدد المسار الدقيق وقت تنفيذ 1.2.4 بعد ما أدوّر على شاشة `CustodyRegister` في الـ Frontend تحديدًا.

---

## 5. Migration

**المخاطرة**: منخفضة. `CustodyRegister` بتاخد عمود جديد إضافي بس (بدون لمس العمود القديم أو بياناته الأربعة). `CustodyOfficer`/`Maintenance` بتاخد أعمدة `Nullable` جديدة (منخفضة المخاطر، لا بيانات حالية تتأثر لأن `MaintenanceRequests`/`MaintenanceSchedules` فاضيين، و`CustodyOfficers` مفيهاش عمود قديم يتلمس).

**البروتوكول**: نفس أسلوب كل الـ Batches السابقة — Backup كامل → تجربة على `HabbakErp_Trial_HrFreeFieldMigration` → تطبيق على `HabbakErp` بـ`--connection` صريح.

---

## 6. الأسئلة المفتوحة قبل الموافقة على التنفيذ

1. **شاشة `CustodyRegister` في الـ Frontend مش موجودة خالص دلوقتي** — تأكدت بالفحص: فيه Backend كامل (`src/Habbak.ERP.API/Controllers/Accounting/CustodyRegistersController.cs`) لكن **صفر نتائج** لأي ملف Frontend باسم `CustodyRegister` في `frontend/src`. يعني الـ Banner التحذيري المطلوب في 1.2.3 **مالوش شاشة موجودة يتحط عليها دلوقتي** — ده خارج نطاق HR أصلًا (فجوة في موديول الحسابات نفسه، مش حاجة Phase 1.2 مسؤولة عنها). **الخيار المقترح**: تنفيذ الـ Banner كـ Component جاهز/معزول (يقبل العدد كـ Prop) بدون ربطه فعليًا بشاشة، ويتفعّل تلقائيًا أول ما شاشة `CustodyRegister` تتبنى مستقبلًا — يحتاج قرارك: نكمل بالخيار ده، ولا نأجل بند الـ Banner بالكامل ونكتفي بشاشة `HR_CUSTODY_MIGRATION` المستقلة؟
2. **بيانات `E2E-CO-MAADI` في `CustodyOfficers` الحقيقية** — نظّفها دلوقتي (خارج نطاق Phase 1.2) ولا نسجّلها في `Known-Issues.md` بس ونكمل؟ (التوصية: تسجيل بس، بدون لمس — نفس مبدأ "مفيش صف بيتمسح").
3. **User.EmployeeId (1.2.5)** — الخطة بتقول "توثيق بس... إلا لو الـ Research كشف كتابة فعلية عليه". الفحص أكّد **صفر كتابة فعلية** (لا في الكود ولا في البيانات — كلا الصفين `NULL`). هل نكتفي بتوثيق فقط بدون أي تعديل كود، ولا نضيف تعليق XML أوضح على الخاصية نفسها كجزء من "التوثيق"؟

---

## الخلاصة

الخطة كما هي مكتوبة في `HR-MASTER-PLAN.md §Phase 1.2` **صحيحة ومطابقة للكود بنسبة كاملة تقريبًا**، والانحراف الوحيد المكتشف هو تحديد شاشة الـ Frontend الصحيحة للـ Banner (سؤال 1). البيانات الحقيقية الحالية بلا مخاطرة (Employees فاضي، والقيم الموجودة placeholder بلا معنى) — يعني **الـ Migration نفسها آمنة جدًا تقنيًا**، لكن **الفائدة العملية للربط اليدوي (1.2.4) هتفضل صفر لحد ما موظف حقيقي أول يتسجل** (بعد Phase 1.5 أو إدخال يدوي مباشر).

**جاهز للتنفيذ بعد الرد على الأسئلة الثلاثة في قسم 6، أو "موافق" لو مفيش اعتراض والقرارات الافتراضية المذكورة مقبولة.**
