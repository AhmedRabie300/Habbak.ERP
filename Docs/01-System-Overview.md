# نظرة عامة على النظام — Habbak ERP

> تقرير مبني بالكامل على الكود الفعلي في `D:\Projects\ALHabbak` (لا افتراضات). كل بند غير مؤكد 100% موسوم صراحة بـ **TBD** أو **⚠️**.

---

## 1. نبذة عامة عن النظام

**الهدف**: نظام ERP متكامل لسلسلة محلات قهوة (Habbak ERP)، مبني على **Clean Architecture** (Domain / Application / Infrastructure / API)، بـ**Backend** .NET 10 (MediatR CQRS، FluentValidation، EF Core Code-First، SQL Server) و**Frontend** React + TypeScript بدعم كامل عربي/إنجليزي (RTL).

**المشاريع الفعلية** (`src/*.csproj`)، كلها `net10.0`:

| المشروع | المسار | الغرض |
|---|---|---|
| `Habbak.ERP.Shared` | `src/Habbak.ERP.Shared` | أدنى طبقة، بدون اعتماديات (enums/constants مشتركة) |
| `Habbak.ERP.Domain` | `src/Habbak.ERP.Domain` | الكيانات وقواعد العمل — يعتمد على Shared فقط |
| `Habbak.ERP.Application` | `src/Habbak.ERP.Application` | حالات الاستخدام (CQRS عبر MediatR 14.2.0، FluentValidation 12.1.1، Mapster) |
| `Habbak.ERP.Infrastructure` | `src/Habbak.ERP.Infrastructure` | EF Core SqlServer، الـ Migrations، الـ Seeding، الـ Interceptors |
| `Habbak.ERP.API` | `src/Habbak.ERP.API` | ASP.NET Core، JWT Bearer، Swashbuckle، الـ Controllers |
| `Habbak.ERP.IntegrationTests` | `src/Habbak.ERP.IntegrationTests` | اختبارات تكامل (xUnit) |
| `Habbak.ERP.ApiTests` | `src/Habbak.ERP.ApiTests` | اختبارات API (xUnit + `WebApplicationFactory`) |

**⚠️ ملاحظة توثيقية (محدَّثة 2026-09-26)**: النسخة المعتمدة الوحيدة من مستند النظرة العامة هي **`Docs/Modules/00-Project-Overview.md`**. النسختين `Docs/00-Project-Overview.md` و`Docs/Analysis/00-Project-Overview.md` متعلّمين **OUTDATED**. النقاط تحت كانت الفروق بين النسختين القديمتين وقت كتابة التقرير ده، والقرارات المحسومة فيها (الاستضافة، والجلسات، والمرفقات، وتأجيل الموافقات) **اتدمجت في النسخة المعتمدة يوم 2026-09-26**:
- النسخة الأحدث تضيف موديول عاشر: **شئون العاملين والمرتبات (HR/Payroll)**، وتوسّع "الصيانة" لتشمل **الأصول الثابتة**.
- تحسم استضافة النظام على **Contabo VPS** (ومن ثم المراقبة عبر **Prometheus + Grafana** بدل Application Insights)، ومدة الاحتفاظ بسجل التدقيق **7 سنوات موحّدة**، والدخول من أكثر من جهاز **مسموح دائمًا بدون حد لعدد الجلسات**.
- تضيف قسم حماية بيانات حساسة (PII) — تشفير عمودي وإخفاء جزئي للرقم القومي/الحساب البنكي.
- تُرجئ بناء محرك سير الموافقات (Approval Workflow) رسميًا لحين اكتمال الموديولات الأساسية.

كلا المستندين متفقان على البنية المعمارية العامة (Clean Architecture، MediatR/FluentValidation، Table-per-Company، JWT، RowVersion).

---

## 2. الهيكل التنظيمي للسيستم

| الكيان | الملف | الوصف |
|---|---|---|
| **Company** | `src/Habbak.ERP.Domain/Organization/Company.cs` | جذر الشركة (Tenant) — `Code`, `NameAr/En`, `CommercialRegister`, `TaxCard`, `BaseCurrencyId`, `IsActive`. غير مرتبط بشركة أخرى (هو الجذر نفسه). |
| **Branch** | `src/Habbak.ERP.Domain/Organization/Branch.cs` | ينفّذ `ICompanyScopedEntity` — `Code`, `NameAr/En`, `IsActive`. يُستخدم أيضًا كمصدر قيم حيّة لبُعد تحليلي (Dimension) من نوع "الفرع". |

**المستخدمون والمصادقة (User/Auth) — ⚠️ Stub تطويري فقط، لا يوجد نظام دخول حقيقي:**
- لا يوجد كيان `User` في قاعدة البيانات إطلاقًا (لا جدول، لا Identity).
- `ICurrentCompanyContext` (`src/Habbak.ERP.Application/Common/Interfaces/ICurrentCompanyContext.cs`) يوفّر `CompanyId`/`BranchId`/`UserId` من الـ JWT Claims مباشرة — لا يوجد `ICurrentUserContext` منفصل.
- الدخول الوحيد المتاح: `DevAuthController` (`api/v1/dev/auth/login`) — يقبل `{CompanyId, UserId, BranchId}` خام **بدون أي تحقق من كلمة مرور**، ويصدر JWT موقّع. موسوم صراحة في الكود كـ"مؤقت، وضع تطوير فقط"، ومحجوب تمامًا خارج بيئة Development (404).
- **الخلاصة**: البنية التحتية للـ JWT حقيقية وجاهزة، لكن لا يوجد جدول مستخدمين ولا شاشة تسجيل دخول فعلية بعد — هذا موديول مستقبلي (`07-Module-Settings-Permissions.md`).

**الصلاحيات (Permissions) — ⚠️ Stub، دائمًا "مسموح":**
- لا يوجد كيان `Permission` في الـ Domain.
- `frontend/src/ui-kit/usePermission.ts` كامل تنفيذه:
  ```ts
  export function usePermission(_permissionKey: string): boolean {
    return true; // مؤقت لحين بناء موديول الصلاحيات الحقيقي
  }
  ```
- التحكم الحالي في الأزرار/الإجراءات يعتمد فقط على **حالة المستند (Status)**، وليس على صلاحية مستخدم فعلية.

---

## 3. البنية التحتية المشتركة

### MenuItem + FieldLabel (القائمة والحقول الديناميكية)
- `MenuItem` (`src/Habbak.ERP.Domain/Common/MenuItem.cs`): `Code`, `NameAr/En`, `ParentId` (شجرة ذاتية)، `DisplayOrder`, `RouteKey` (فارغ = عنوان مجموعة بدون رابط)، `IconKey`.
- `FieldLabel` (`src/Habbak.ERP.Domain/Common/FieldLabel.cs`): مفتاحه `ScreenCode` + `FieldCode` — يقود عناوين أعمدة الجداول وتسميات الحقول من قاعدة البيانات بدل النصوص الثابتة.
- التغذية الأولية: `SystemDataSeeder.cs` يعمل عند كل إقلاع للـ API، ويضيف من `MenuItemSeedData.Build()` و`FieldLabelSeedData.Build()` **فقط لو الجدول فاضي** — لا يطغى أبدًا على تعديلات لاحقة من شاشة إعدادات.
- الاستهلاك: `NavigationController` (`api/v1/navigation`)، `FieldLabelsController` (`api/v1/field-labels`)، ويستهلكهم الفرونت-إند عبر `useMenuTree` في `AppLayout.tsx`.

### CodingRule (قواعد الترقيم → أصبحت "إعدادات الشاشات")
- `src/Habbak.ERP.Domain/Common/CodingRule.cs`: `CompanyId`, `ScreenCode`, `IsAutomatic`, `Format` (أرقام/حروف/مختلط)، `Prefix`, `SequenceLength`, `LastSequence`، بالإضافة لحقلين أُضيفا لاحقًا: **`IsAttachmentMandatory`** و**`IsDescriptionMandatory`** — ما حوّل هذا الكيان من "قواعد ترقيم" فقط إلى إعدادات شاشة عامة.
- الافتراضات لكل شاشة مُعرّفة في `ScreenCodeCatalog.cs` — **34 شاشة** مسجّلة حاليًا (9 حسابات، 15 مخازن، 10 مشتريات).
- الواجهة: `CodingRulesController` (`api/v1/settings/coding-rules`).

### سجل التدقيق (Audit Trail)
- `AuditableEntity` (`src/Habbak.ERP.Domain/Common/AuditableEntity.cs`): `Id`, `RowVersion`, `CreatedAtUtc/By`, `UpdatedAtUtc/By`, `IsDeleted`, `DeletedAtUtc/By`.
- `AuditSaveChangesInterceptor` (`src/Habbak.ERP.Infrastructure/Persistence/Interceptors/`): يعمل في كل `SaveChanges` — يختم تلقائيًا حقول الإنشاء/التعديل، وعند أي `Remove()` **يحوّل الحالة من `Deleted` إلى `Modified`** ويضبط `IsDeleted = true` بدل الحذف الفعلي.
- **⚠️ فجوة موثّقة**: لا يوجد جدول `AuditLog` منفصل يسجّل تاريخ كل تغيير — التتبع الحالي هو ختم حقول على الكيان نفسه فقط، وليس سجل تدقيق كامل كما يذكره مستند النظرة العامة الأحدث.

### RowVersion (التزامن التفاؤلي)
- كل كيان ينفّذ `IAuditableEntity` يُهيَّأ تلقائيًا بـ `.IsRowVersion()` عبر Reflection في `AppDbContext.OnModelCreating` — أي **كل الكيانات في النظام تقريبًا** مرشحة للتزامن التفاؤلي، وليس مجموعة مختارة يدويًا.
- عند التعارض: `DbUpdateConcurrencyException` يُلتقط مركزيًا في `ApiExceptionHandler.cs` ويُحوَّل إلى **HTTP 409** برسالة عربية واضحة.

### IsDeleted (الحذف الناعم)
- Global Query Filter عام (`e => e.IsDeleted == false`) يُبنى تلقائيًا بـ Reflection لكل كيان `IAuditableEntity` — مدمج مع فلتر الشركة حيث ينطبق.

### دعم تعدد الشركات (Multi-Company)
- `ICompanyScopedEntity` (`CompanyId` nullable) + `IBranchScopedEntity`.
- الإنفاذ عبر **EF Core Global Query Filters** مبنية بـ Reflection (وليس فحصًا يدويًا في كل Repository): `e => e.CompanyId == CurrentCompanyId`، مع استثناء خاص لكيان `Account` يسمح بصفوف الشركة الحالية **أو** المُعلّمة `IsSharedAcrossCompanies`.
- **⚠️ فجوة موثّقة**: "خط الدفاع الثاني" الموثّق (Middleware/Action Filter على مستوى الـ API يعيد التحقق من تطابق `CompanyId`) **غير موجود فعليًا** في `Habbak.ERP.API` — الاعتماد الحالي على فلتر EF فقط.

### نظام المرفقات (Attachments)
- `Attachment.cs` (`src/Habbak.ERP.Domain/Common/`): `EntityType`/`EntityId` (متعدد الأشكال)، `FileName`, `ContentType`, `FileSizeBytes`, و`Content` (`byte[]`) — **مخزَّن مباشرة في SQL Server (`varbinary(max)`)**.
- القائمة المغلقة للشاشات الموصولة حاليًا (`AttachmentEntityTypes.cs`) — **10 أنواع**: JournalEntry، Voucher، TreasuryTransfer، PurchaseInvoice، PurchaseOrder، GoodsReceipt، SupplierPayment، PurchaseReturn، SupplierContract، RFQ (كلها حسابات/مشتريات — لا شاشة مخازن موصولة بعد).
- **⚠️ ملاحظة**: النسخة القديمة من مستند النظرة العامة (`Docs/Analysis/00-Project-Overview.md`، قسم 8.5) كانت بتنص على تخزين الملفات على الـ VPS، والنسخة المعتمدة (`Docs/Modules/00-Project-Overview.md`، قسم 8.5) بتعتبره قرار معلّق. التوثيق بينص على تخزين الملفات على نظام ملفات الخادم (VPS) لا في قاعدة البيانات — هذا تباين بين التوثيق والتنفيذ الفعلي يستحق المراجعة.
- الواجهة: `AttachmentsController` (`api/v1/attachments`)، يستهلكه مكوّن `AttachmentPanel.tsx` العام في الفرونت-إند.

---

## 4. الموديولات والحالة الحالية

| الموديول | الحالة | الدليل |
|---|---|---|
| ✅ **الحسابات العامة** | مكتمل ومُختبر | `Domain/Accounting` (16 ملف جذر) + 13 Controller |
| ✅ **المخازن والتصنيع** | مكتمل ومُختبر | `Domain/Inventory` (21 ملف جذر) + 22 Controller |
| ⚠️ **المشتريات** | مكتمل برمجيًا، بانتظار اختبار QA | `Domain/Purchasing` (12 ملف جذر) + 14 Controller |
| 📅 **المبيعات** | مخطط له فقط | لا يوجد `Domain/Sales` ولا Controllers |
| 📅 **نقاط البيع (POS)** | مخطط له فقط | يوجد `POSCategory.cs` فقط (تصنيف أصناف، ليس موديول POS فعلي) |
| 📅 **ETA (المنظومة الضريبية)** | مخطط له فقط | لا يوجد أي كيان/كود متعلق |
| 📅 **تقارير عابرة للموديولات** | مخطط له فقط | التقارير الحالية كلها داخل موديولها (لا موديول تقارير مستقل) |
| 📅 **الصيانة / الأصول الثابتة** | مخطط له فقط | لا يوجد أي كيان/كود متعلق |
| 📅 **شئون العاملين والمرتبات (HR)** | مخطط له فقط (في النسخة الأحدث من المستند فقط) | لا يوجد أي كيان/كود متعلق |

يتطابق هذا تمامًا مع مجلدات الفرونت-إند الفعلية: `frontend/src/features/` يحتوي فقط على `accounting`, `inventory`, `purchasing`, `organization`, `settings`, `navigation`, `common`, `dev`.

---

## 5. الـ Options العامة على مستوى السيستم

| الميزة | الملفات الرئيسية | الوصف |
|---|---|---|
| **فتح أكثر من شاشة في تاب** | `frontend/src/store/tabsStore.ts`, `frontend/src/app/AppLayout.tsx` | كل تاب مُركّب مستقل يبقى في الذاكرة (مخفي بـ `display:none` وليس مُزالًا) — الضغط من الشريط الجانبي يفتح تابًا جديدًا أو يُنشّط الموجود، أما التنقل الداخلي (فتح سجل من قائمة) فيُحدّث نفس التاب النشط مكانه. |
| **المرفقات (Attachments)** | `frontend/src/ui-kit/AttachmentPanel.tsx` | مكوّن عام يعمل بأي `{entityType, entityId}` — رفع متعدد، عرض/تنزيل/حذف، يمنع الرفع قبل حفظ السجل نفسه. |
| **الطباعة والتصدير** | `frontend/src/lib/export.ts`, `ui-kit/ExportMenu.tsx`, `ui-kit/ActionBar.tsx`, `ui-kit/DataGrid.tsx` | Excel (`xlsx`) وCSV وPDF (عبر HTML + `html2canvas` + `jsPDF` — لتفادي مشكلة عرض العربي بخطوط jsPDF الافتراضية) وطباعة مباشرة. تأكيد إجباري عند تصدير أكثر من 10,000 صف. زر طباعة موحّد في كل شاشة List/Edit عبر `ActionBar`/`DataGrid`. |
| **الاستيراد مع Template** | `frontend/src/lib/import.ts`, `ui-kit/ImportPanel.tsx` | قراءة ملفات Excel/CSV، تنزيل قالب فارغ بمثال، معاينة عدد الصفوف، ملخص نجاح/فشل لكل صف. |

---

## 6. الـ API Controllers الموجودة

**إجمالي: 55 ملف Controller** موزّعة على 6 مجلدات تحت `src/Habbak.ERP.API/Controllers/`.

### Common (4)
| Controller | المسار | الوظيفة |
|---|---|---|
| `AttachmentsController` | `api/v1/attachments` | المرفقات العامة متعددة الأشكال |
| `CodingRulesController` | `api/v1/settings/coding-rules` | إعدادات الشاشات/الترقيم |
| `FieldLabelsController` | `api/v1/field-labels` | تسميات الحقول الديناميكية |
| `NavigationController` | `api/v1/navigation` | شجرة القائمة الجانبية |

### Dev (2 — محجوبة خارج بيئة التطوير)
| Controller | المسار | الوظيفة |
|---|---|---|
| `DevAuthController` | `api/v1/dev/auth` | إصدار JWT تطويري بدون تحقق هوية |
| `DevSeedController` | `api/v1/dev/seed` | تغذية بيانات تجريبية (دليل حسابات، أبعاد تكلفة) |

### Organization (3)
| Controller | المسار | الوظيفة |
|---|---|---|
| `CompaniesController` | `api/v1/organization/companies` | بيانات الشركات |
| `BranchesController` | `api/v1/organization/branches` | بيانات الفروع |
| `CurrenciesController` | `api/v1/organization/currencies` | بيانات العملات |

### Accounting (13)
| Controller | المسار | الوظيفة |
|---|---|---|
| `AccountsController` | `api/v1/accounting/accounts` | دليل الحسابات |
| `DimensionsController` | `api/v1/accounting/dimensions` | أبعاد التكلفة وقيمها |
| `JournalEntriesController` | `api/v1/accounting/journal-entries` | القيود اليومية |
| `ReceiptVouchersController` | `api/v1/accounting/receipt-vouchers` | سندات القبض |
| `PaymentVouchersController` | `api/v1/accounting/payment-vouchers` | سندات الصرف |
| `VouchersControllerBase` | (بدون مسار خاص) | منطق مشترك بين سندات القبض/الصرف |
| `TreasuryTransfersController` | `api/v1/accounting/treasury-transfers` | تحويلات الخزائن/البنوك |
| `CustodyRegistersController` | `api/v1/accounting/custody-registers` | العهد وتسويتها |
| `CashReconciliationsController` | `api/v1/accounting/cash-reconciliations` | مطابقة الخزينة |
| `BankReconciliationsController` | `api/v1/accounting/bank-reconciliations` | مطابقة البنك |
| `AccountingPeriodsController` | `api/v1/accounting/accounting-periods` | الفترات المالية |
| `AccountOpeningBalancesController` | `api/v1/accounting/opening-balances` | أرصدة افتتاحية للحسابات |
| `PaymentMethodsController` | `api/v1/accounting/payment-methods` | طرق الدفع |
| `ReportsController` | `api/v1/accounting/reports` | تقارير الحسابات |

### Inventory (22)
| Controller | المسار | الوظيفة |
|---|---|---|
| `UnitsOfMeasureController` | `api/v1/inventory/units-of-measure` | وحدات القياس |
| `ItemGroupsController` | `api/v1/inventory/item-groups` | مجموعات الأصناف |
| `POSCategoriesController` | `api/v1/inventory/pos-categories` | تصنيفات نقطة البيع |
| `WarehousesController` | `api/v1/inventory/warehouses` | المخازن |
| `ItemsController` | `api/v1/inventory/items` | الأصناف |
| `StockInController` | `api/v1/inventory/stock-in` | إذن إضافة |
| `StockOutController` | `api/v1/inventory/stock-out` | إذن صرف |
| `WarehouseDocumentsControllerBase` | (بدون مسار خاص) | منطق مشترك لمستندات المخازن |
| `OpeningBalancesController` | `api/v1/inventory/opening-balances` | أرصدة افتتاحية |
| `CustodyOfficersController` | `api/v1/inventory/custody-officers` | مسؤولو العهد |
| `TransferOrderController` | `api/v1/inventory/transfer-order` | أمر تحويل |
| `TransferReceiptController` | `api/v1/inventory/transfer-receipt` | استلام تحويل |
| `BranchRequestsController` | `api/v1/inventory/branch-requests` | طلبات توريد الفروع |
| `RecipesController` | `api/v1/inventory/recipes` | الوصفات |
| `ProductionOrdersController` | `api/v1/inventory/production-orders` | أوامر الإنتاج |
| `ProductionIssuesController` | `api/v1/inventory/production-issues` | صرف الإنتاج |
| `ProductionReceiptsController` | `api/v1/inventory/production-receipts` | استلام الإنتاج |
| `ProductionDocumentsControllerBase` | (بدون مسار خاص) | منطق مشترك لمستندات الإنتاج |
| `InventoryCountsController` | `api/v1/inventory/inventory-counts` | دورة الجرد |
| `InventoryAdjustmentsController` | `api/v1/inventory/inventory-adjustments` | تسوية جرد مستقلة |
| `WasteRecordsController` | `api/v1/inventory/waste-records` | متابعة الهالك |
| `InventorySettingsController` | `api/v1/inventory/settings` | إعدادات المخزون العامة |
| `InventoryReportsController` | `api/v1/inventory/reports` | تقارير المخزون |

### Purchasing (14)
| Controller | المسار | الوظيفة |
|---|---|---|
| `SuppliersController` | `api/v1/purchasing/suppliers` | الموردون |
| `PurchaseRequestsController` | `api/v1/purchasing/purchase-requests` | طلبات الشراء |
| `RequestsForQuotationController` | `api/v1/purchasing/rfqs` | طلبات عروض الأسعار |
| `PurchaseOrdersController` | `api/v1/purchasing/purchase-orders` | أوامر الشراء |
| `GoodsReceiptsController` | `api/v1/purchasing/goods-receipts` | إذن إضافة (استلام مشتريات) |
| `PurchaseInvoicesController` | `api/v1/purchasing/purchase-invoices` | فواتير الشراء |
| `PurchaseReturnsController` | `api/v1/purchasing/purchase-returns` | مردودات المشتريات |
| `SupplierContractsController` | `api/v1/purchasing/supplier-contracts` | عقود الموردين |
| `SupplierEvaluationsController` | `api/v1/purchasing/supplier-evaluations` | تقييم أداء الموردين |
| `SupplierPaymentsController` | `api/v1/purchasing/supplier-payments` | سداد الموردين |
| `SupplierPriceHistoryController` | `api/v1/purchasing/supplier-price-history` | تاريخ أسعار الموردين |
| `PurchaseExpensesController` | `api/v1/purchasing/purchase-expenses` | مصروفات الشراء |
| `PurchasingSettingsController` | `api/v1/purchasing/settings` | إعدادات دورة المشتريات |
| `PurchasingReportsController` | `api/v1/purchasing/reports` | تقارير المشتريات |

---

## بنود تحتاج مراجعة/تأكيد (TBD)

- هل جدول `AuditLog` منفصل (سجل تدقيق كامل) مخطط له قريبًا، أم مؤجَّل رسميًا مثل محرك الموافقات؟
- "خط الدفاع الثاني" ضد تسرّب بيانات بين الشركات (Middleware/Action Filter على مستوى الـ API) غير موجود حاليًا — هل هذا مؤجَّل أم يحتاج بناء؟
- تخزين المرفقات في قاعدة البيانات (`varbinary(max)`) مقابل التخزين على نظام ملفات الخادم المذكور في التوثيق الأحدث — أيهما القرار النهائي؟
- محرك سير الموافقات (Approval Workflow): يوجد `NullApprovalWorkflowService` كـ Placeholder فقط، متسق مع قرار التأجيل الموثّق — لم يُراجَع بعمق في هذا التقرير.
