# نظام الحبّاك ERP — نظرة عامة محدّثة

> كل رقم في الملف ده متقروء من الكود الفعلي (`src/` و`frontend/`) بتاريخ **2026-09-25** — مش من ملفات المواصفات.

---

## 1. الإحصائيات الحالية

| البند | العدد | المصدر في الكود |
|---|---|---|
| الموديولات (مجموعات القائمة) | **7 مجموعات / 102 شاشة** | `Persistence/Seeding/MenuItemSeedData.cs` |
| الكيانات المُعرَّفة في الـ DbContext | **148 DbSet** | `Persistence/AppDbContext.cs` |
| كلاسات الـ Domain | 148 (منها 103 `AuditableEntity`) | `Habbak.ERP.Domain/**` |
| إعدادات EF (Configurations) | **92 ملف** | `Persistence/Configurations/**` |
| Commands | **308** | `record …Command` في `Habbak.ERP.Application` |
| Queries | **192** | `record …Query` في `Habbak.ERP.Application` |
| Controllers | **105 كلاس في 86 ملف** | `Habbak.ERP.API/Controllers/**` |
| Migrations | **68** | `Persistence/Migrations/*.cs` |
| اختبارات API | **241 ناجحة** (240 `[Fact]`/`[Theory]`) | `Habbak.ERP.ApiTests` |
| اختبارات Integration | **75 ناجحة** | `Habbak.ERP.IntegrationTests` |
| مسارات الواجهة | **173 Route** | `frontend/src/app/routeTable.tsx` |

**Commands / Queries لكل موديول:**

| الموديول | Commands | Queries | Controllers |
|---|---|---|---|
| المخزون والتصنيع | 54 | 25 | 23 |
| المشتريات | 47 | 34 | 14 |
| نقاط البيع | 45 | 23 | 15 |
| الأصول والصيانة | 42 | 19 | 2 (فيهم 13 كلاس) |
| الحسابات | 41 | 33 | 17 |
| المبيعات | 38 | 18 | 10 |
| الصلاحيات والإعدادات | 24 | 15 | 2 |
| محرك الترحيل | 5 | 15 | (ضمن Accounting) |
| التنظيم (شركات/فروع/عملات) | 9 | 4 | 3 |

---

## 2. الموديولات الثمانية — كلهم مبنيين ✅

| # | الموديول | مجموعة القائمة | الشاشات | أبرز الكيانات |
|---|---|---|---|---|
| 1 | الحسابات العامة والحركات المالية | `ACCOUNTING` | 16 | `JournalEntry` · `Account` · `Voucher` · `CostCenterDimension` · `AccountingPeriod` · `CustodyRegister` · `BankReconciliation` |
| 2 | المخازن والتصنيع | `INVENTORY` | 23 | `Item` · `Warehouse` · `StockBalance` · `StockTransaction` · `Recipe` · `ProductionOrder` · `InventoryCount` · `WasteRecord` |
| 3 | المشتريات | `PURCHASING` | 14 | `PurchaseRequest` · `RequestForQuotation` · `PurchaseOrder` · `GoodsReceipt` · `PurchaseInvoice` · `PurchaseReturn` · `SupplierContract` · `SupplierPaymentAllocation` |
| 4 | المبيعات | `SALES` | 11 | `SalesQuote` · `SalesOrder` · `SalesInvoice` · `DeliveryOrder` · `SalesReturn` · `Customer` · `PriceList` · `LoyaltyTier` |
| 5 | نقاط البيع | `POS` | 16 | `POSTerminal` · `Shift` · `POSCheck` · `POSInvoice` · `POSReturn` · `CashDrawerMovement` · `QRTicket` · `DeliveryPlatformOrder` |
| 6 | الأصول الثابتة والصيانة | `ASSETS_MAINTENANCE` | 13 | `FixedAsset` · `DepreciationSchedule` · `DepreciationRun` · `AssetTransfer` · `AssetDisposal` · `MaintenanceRequest` · `MaintenanceSchedule` |
| 7 | الصلاحيات والمستخدمين | `SETTINGS` (جزء) | ضمن 9 | `User` · `Role` · `ScreenPermission` · `FieldPermission` · `ButtonPermission` · `UserScope` · `RefreshToken` · `LoginAttempt` · `UserRecoveryCode` |
| 8 | الإعدادات والتنظيم | `SETTINGS` (جزء) | ضمن 9 | `Company` · `Branch` · `Currency` · `MenuItem` · `CodingRule` · `SystemSettings` · `AuditLog` + `AuditLogArchive` |

> الموديولان 7 و8 بيشتركوا في مجموعة قائمة واحدة (`SETTINGS`، 9 شاشة) لكن كودهم منفصل: `Domain/Settings/**` للصلاحيات و`Domain/Organization/**` للتنظيم.

---

## 3. محرك الترحيل ومحرر القيود

### 3.1 الكيانات الفعلية — `src/Habbak.ERP.Domain/Posting/`

| الكيان | الدور |
|---|---|
| `PostingTemplate` | قالب لشاشة (`ScreenCode`) بنسخة (`VersionNumber`) و`TemplateFamilyId` و`TriggerType` |
| `PostingTemplateLine` | سطر: `Direction` (مدين/دائن) · `AccountSourceType` · `AmountFormulaType` · `ConditionType` |
| `PostingTemplateLineCostCenter` | مركز التكلفة للسطر (`CostCenterSourceType`) |
| `JournalEntryTemplateSnapshot` | **لقطة القالب وقت الترحيل** — القيد بيفضل مفهوم حتى لو القالب اتغيّر بعدها |
| `PostingFailure` | سجل محاولات الترحيل الفاشلة (تقرير "اللي مارحّلش") |

**Enums:** `PostingDirection` · `PostingTriggerType` · `AccountSourceType` · `AmountFormulaType` · `ConditionType` · `CostCenterSourceType`.

### 3.2 المحرك — `src/Habbak.ERP.Application/Posting/`

| الملف | الدور |
|---|---|
| `PostingTemplateEngine.cs` | `PostIfConfiguredAsync` (اللي كل مستند بينادي عليه) · `IsConfiguredAsync` · `ReverseAsync` · `BuildAsync` |
| `PostingFormulas.cs` | **هو الـ Evaluator فعليًا**: `EvaluateAmount` و`EvaluateCondition` (مافيش كلاس اسمه `PostingEvaluator` في الكود) |
| `PostingContext.cs` | حقول المستند + السطور + المجموعات + `Dimensions` (مراكز تكلفة بيفرضها الموديول) |
| `PostingKeys.cs` | مفاتيح الـ Idempotency لكل ترحيل |
| `PostingEntities.cs` | `PostingEntityValueMapper` — بيحوّل كيان (فرع، كاشير…) لقيمة بُعد تكلفة |
| `PostingFailureRecorder.cs` | بيسجّل الفشل بدل ما يبلعه |
| `Resolvers/PostingResolvers.cs` | **4 Resolvers**: `Supplier.PayableAccountId` · `Customer.ReceivableAccountId` · `Branch.ToCostCenterValue` · `POSTerminal.CashTreasuryAccount` |
| `Screens/PostingScreenCatalog.cs` | **20 شاشة ترحيل** + **23 قالب قياسي** جاهز |
| `Templates/` | CRUD القوالب + `PreviewPostingTemplateQuery` (معاينة قبل التفعيل) |
| `Reports/PostingReportsQueries.cs` | تقارير: اتّرحل / فشل / لسه من غير قيد |

### 3.3 الشاشات المربوطة (20)

`SalesInvoice` · `SalesReturn` · `PurchaseInvoice` · `PurchaseReturn` · `PosInvoice` · `PosReturn` · `PosShiftVariance` · `PosDrawerExpense` · `DeliveryOrder` · `StockIn` · `StockOut` · `InventoryAdjustment` · `InventoryCount` · `Waste` · `OpeningBalance` · `FixedAssetAcquisition` · `Depreciation` · `AssetDisposal` · `MaintenanceExternalCost` · `MaintenanceSpareParts`.

**مبدأ ثابت:** المدير المالي هو اللي بيربط الحسابات — مافيش حسابات متزرعة. الشاشة من غير قالب فعّال بتشتغل عادي وبترجع `JournalEntryId = null`.

---

## 4. الـ Tech Stack

**Backend** — .NET **10.0** (`net10.0`)
- EF Core **10.0.11** (SQL Server) · MediatR **14.2** · FluentValidation **12.1.1** · Mapster **10.0.12**
- JWT: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 + `System.IdentityModel.Tokens.Jwt` 8.22
- BCrypt.Net-Next 4.2 (كلمات المرور) · Swashbuckle 10.2.3 (Swagger)
- `Microsoft.AspNetCore.DataProtection` لتشفير أسرار الـ TOTP (المفاتيح في `API/App_Data/DataProtection-Keys`)

**Frontend** — React **19.2** + TypeScript + Vite
- TanStack Query 5.102 · React Hook Form 7.87 + Zod 4.5 · Zustand 5.0 · React Router 7.18
- i18next 26.4 (عربي/إنجليزي، RTL) · axios 1.20
- التصدير: `xlsx` 0.20.3 · `jspdf` 4.2 + `html2canvas` 1.4 · `qrcode` 1.5
- الفحص: `tsc -b` + `oxlint` (البناء `tsc -b && vite build` مع `noUnusedLocals`)

**Database** — SQL Server (LocalDB للتطوير والاختبارات، وSQL Server كامل للتشغيل)

---

## 5. الـ Local + Cloud Setup — الحالة الفعلية

> **مهم:** الكود النهارده **مش فيه مزامنة**. النظام بيشتغل على **قاعدة بيانات واحدة** عن طريق `ConnectionStrings:Default` (`appsettings.Development.json`). أي كلام عن "SQL Server على الكاشير" لسه **مش مبني**.

| العنصر | الحالة في الكود |
|---|---|
| SQL Server مركزي | ✅ ده اللي شغال — connection string واحد لكل التطبيق |
| SQL Server على الكاشير | ❌ مش مبني |
| Local Agent / Sync Service | ❌ مش مبني |
| `POSOperationMode` (`CloudOnly` / `OfflineOnly` / `HybridAutoSync`) | ⚠️ محفوظ في `BranchPOSSettings` و**مفيش كود بيقراه** — تعليق الكيان نفسه بيقول كده |

**اللي موجود فعلاً وبيخدم المزامنة لما تتبني:**
- **Idempotency على مستوى النظام**: `ProcessedIdempotencyKey` + `IdempotencyBehavior` (pipeline behavior) + `IIdempotentRequest` — أي أمر بيتبعت مرتين بيرجّع نفس النتيجة بدل ما يعمل مستند تاني. مستخدم في POS (فتح وردية، إتمام دفع، طلبات المنصات) وفي الإهلاك والصيانة الدورية.
- **مفاتيح ترحيل ثابتة** (`PostingKeys.For`) — إعادة الترحيل مابتعملش قيد تاني.
- **`ProcessedIdempotencyKey` بيتنضّف** بعد 7 أيام في الـ job اليومي (`MaintenanceJobs`).

---

## 6. السلوك بدون إنترنت (Offline) لكل موديول

النظام كله **SPA + REST**: أي شاشة محتاجة السيرفر. مافيش Service Worker ولا تخزين محلي للمستندات. يعني:

| الموديول | Offline |
|---|---|
| الحسابات · المخزون · المشتريات · المبيعات · الأصول والصيانة · الصلاحيات · الإعدادات | ❌ بيقف تمامًا — كل عملية REST call |
| نقاط البيع | ❌ بيقف برضه عمليًا، **لكنه الوحيد المجهّز**: كل أوامره الحرجة `IIdempotentRequest`، و`QRTicket` مصمّم يتقرا من غير نت وقت المسح، و`POSOperationMode` مستنية الـ Local Agent |

**الخلاصة:** التشغيل بدون إنترنت لسه مرحلة قادمة؛ الأساس (Idempotency + مفاتيح الترحيل) اتبنى عشان لما الـ Local Agent يتعمل، إعادة الإرسال ماتعملش ازدواج.

---

## 7. مشاريع الـ Solution والمسارات

`src/Habbak.ERP.slnx` — 7 مشاريع:

| المشروع | المسار | الدور |
|---|---|---|
| `Habbak.ERP.Domain` | `src/Habbak.ERP.Domain/` | الكيانات والـ enums — مالوش أي مرجع خارجي |
| `Habbak.ERP.Application` | `src/Habbak.ERP.Application/` | CQRS (MediatR) + Validators + الواجهات (`IApplicationDbContext`…) + محرك الترحيل |
| `Habbak.ERP.Infrastructure` | `src/Habbak.ERP.Infrastructure/` | `AppDbContext` + Configurations + Migrations + Seeding + الخدمات (ترقيم، حركة مخزون، jobs) |
| `Habbak.ERP.API` | `src/Habbak.ERP.API/` | Controllers + المصادقة + فلتر صلاحيات الشاشات + معالجة الأخطاء |
| `Habbak.ERP.Shared` | `src/Habbak.ERP.Shared/` | Enums مشتركة بين الطبقات |
| `Habbak.ERP.ApiTests` | `src/Habbak.ERP.ApiTests/` | اختبارات end-to-end على `WebApplicationFactory` بقاعدة LocalDB لكل كلاس |
| `Habbak.ERP.IntegrationTests` | `src/Habbak.ERP.IntegrationTests/` | اختبارات محرك الترحيل والخدمات |

**الواجهة:** `frontend/src/` — `app/` (التخطيط والراوتر والـ axios) · `features/<module>/<screen>/` · `ui-kit/` (DataGrid · ActionBar · SearchableSelect · ExportMenu…) · `i18n/ar.json` + `en.json` · `store/` (Zustand).

---

## 8. الـ Conventions

**المعمار**
- Clean Architecture: `Domain ← Application ← Infrastructure ← API`، والـ Application مابتعرفش EF غير عن طريق `IApplicationDbContext`.
- CQRS بـ MediatR: كل عملية `record …Command`/`…Query` + Handler، والتحقق `AbstractValidator` في pipeline behavior (الفشل = 400).

**قواعد البيانات**
- كل كيان `AuditableEntity`: `Id` · `RowVersion` (تزامن متفائل) · `IsDeleted` (حذف ناعم) · أعمدة تدقيق بمفاتيح للمستخدمين (المستخدم 0 = النظام).
- **Global Query Filters**: حذف ناعم + `CompanyId` للشركة الحالية + نطاق الفرع (`IBranchScopedEntity`، والسطر التابع بياخد فرع أبوه). `GuardBranchScope` بيرجّع 403 `BRANCH-OUT-OF-SCOPE`.
- الفهارس الفريدة بفلتر `[IsDeleted] = 0`؛ الحذف `Restrict` إلا سطور المستند فـ`Cascade`.
- **الترقيم** مركزي: `ICodeGenerator.ResolveCodeAsync(screenCode, manualCode)` + `ScreenCodeCatalog` + شاشة "قواعد الترقيم".

**الأمان والصلاحيات**
- كل كنترولر لازم `[Screen("CODE")]` أو `[AnySignedInUser]` — اللي مالوش يترفض (`SCREEN-NOT-MAPPED`).
- الصلاحية بتتحدد من الفعل: GET→`View` · POST على الجذر→`Add` · PUT→`Edit` · DELETE→`Delete` · POST على `post/approve/reject/cancel/reverse…`→`Approve`. و7 أفعال: View/Add/Edit/Delete/Print/Export/Approve، وفوقهم صلاحيات **حقول** و**أزرار** لكل شاشة.
- الدخول JWT + Refresh Token + تحقق بخطوتين (TOTP) + أكواد استرداد.
- `AuditLog` **append-only** (تعديله أو حذفه استثناء)، والأرشفة بتنقل مش بتمسح.

**الأخطاء**
- `ValidationException` → 400 · `ForbiddenException` → 403 · `NotFoundException` → 404 · `BusinessRuleException` → **409** مع `errorCode` نصي (مثل `PUR-INVOICE-QTY-EXCEEDS-ORDER`) ورسالة بالعامية المصرية.

**الوحدات والتكلفة**
- كل سطر مستند بياخد **لقطة**: `UnitId` + `UnitFactor` + `BaseQuantity` + `BaseUnitCost` — المخزون بيتحرك بالوحدة الأساسية دايمًا، وتغيير تحويلات الصنف بعدين مابيعيدش كتابة المستندات القديمة.

**الواجهة**
- كل شاشة: List (DataGrid + بحث + تصدير) ← Edit بمسار خاص (مش Modal)، و`ActionBar` واحد فوق بيتحكم في الأزرار وتأكيد الحذف.
- عربي أولاً (RTL) مع إنجليزي كامل؛ أي نص جديد لازم يتضاف للملفين.
- `tsc -b` لازم يفضل نضيف — البناء بيقع مع أول خطأ أو متغير غير مستخدم.

**الوظائف المجدولة** — `MaintenanceHostedService` (يومي): تنظيف مفاتيح الـ Idempotency · أرشفة سجل التدقيق · تشغيل الإهلاك الشهري · توليد طلبات الصيانة الدورية (كلها لكل شركة في نطاق مستقل).
