# موديول المخازن والتصنيع — تقرير محدّث

> كل حقيقة في الملف ده متقروءة من الكود الفعلي بتاريخ **2026-09-25**. المقارنة مع آخر تقرير
> (`Docs/Reports/Modules/02-Inventory-Manufacturing.md`, مكتوب 2026-09-14) في قسم 11.

**21 كيان دومين (22 كلاس عدّ منهم `InventorySettings` جوه نفس ملف `ShortagePolicy.cs`) · 54 أمر ·
31 استعلام فعلي (25 منهم `record`، والباقي `class : ListQuery` مُصفّح — قسم 00 بيعدّهم 25 لأنه
بيدوّر على `record` بس) · 23 Controller في 22 ملف · 23 شاشة واجهة.**

---

## 1. الكيانات الفعلية + العلاقات — `src/Habbak.ERP.Domain/Inventory/`

### 1.1 البيانات الأساسية
| الكيان | أهم الحقول | الملاحظة |
|---|---|---|
| `Item` | `ItemType`(5 قيم: خامة/نصف مصنع/تام/مستهلكات/خدمة) · `BaseUnitOfMeasureId` · `PurchaseUnitOfMeasureId?` · `SellUnitOfMeasureId?` · `SaleMethod`(قطعة/وزن/حجم) · `CostMethod` · `IsStocked` · `IsTracked` · `TrackSerial` · `ShelfLifeDays` · `StandardCost` · `TaxCode?` | `ItemGroupId` تصنيف مخزني هرمي، `POSCategoryId` تصنيف كاشير — **منفصلين تمامًا**. `TaxCode` اتضاف في Remarks3 (ETA لسه مش مبني). |
| `ItemGroup` | `ParentId` (هرمي) | `ILookupEntity` قياسي |
| `POSCategory` | `DisplayOrder` | تبويبات شاشة الكاشير فقط |
| `UnitOfMeasure` | `Category`(وزن/حجم/عدد) | تصنيف وصفي بس — التحويل الفعلي مش هنا |
| `ItemUnitConversion` | `ItemId` + `AlternateUnitOfMeasureId` + `ConversionFactor` | تحويل **لكل صنف** (قاعدة 32) — نفس "شكارة" ممكن تبقى كمية مختلفة لصنفين |
| `Warehouse` | `BranchId?` (null = مركزي) · `WarehouseType`(5 قيم) · `AllowNegativeBalance` | الـflag ده هو تجاوز الرصيد السالب الفعلي — `ShortagePolicy` العام لسه مش متصل بيه في الكود |
| `ItemWarehouseSettings` | `MinStockLevel` · `MaxStockLevel` · `ReorderPoint` | حدود **لكل مخزن** لا حد عام للصنف |
| `BranchItemLimit` | `MinRequestQuantity?` · `MaxRequestQuantity` | حدود حجم طلب التوريد الواحد — **منفصلة تمامًا** عن `ItemWarehouseSettings` |
| `CustodyOfficer` | `Code/NameAr/NameEn` مباشرة | بدون `EmployeeId` FK — موديول HR لسه مش مبني |

### 1.2 الأرصدة والحركة
| الكيان | أهم الحقول | الملاحظة |
|---|---|---|
| `StockBalance` | `QuantityOnHand` · **`AverageCost`** · `LastCostUpdateAtUtc` | فريد على `(ItemId, WarehouseId)` — تكلفة **لكل مخزن مش لكل صنف** |
| `StockTransaction` | `TransactionType`(15 قيمة) · `Quantity`(موجبة دايمًا) · `UnitCost` · `BatchNumber?` · `ExpiryDate?` · `SourceDocumentType/Id` (مرجع مرن) · `JournalEntryId?` | سجل غير قابل للتعديل، أي تصحيح = حركة عكسية جديدة |

### 1.3 المستندات
| الكيان | أهم الحقول | الملاحظة |
|---|---|---|
| `WarehouseDocument` | `DocumentType`(**8 أنواع** — قسم 2) · `SourceWarehouseId?` · `DestinationWarehouseId?` · `RelatedWarehouseDocumentId?` (لـTransferReceipt) · `CustodyOfficerId?` (لـTransferOrder) · `Status` · `JournalEntryId?` | تعليق قديم في الملف نفسه بيقول "لا يتكامل مع IPostingService" — **ده بقى غير دقيق**، شوف قسم 11 |
| `WarehouseDocumentLine` | `Quantity` · `UnitCost` · **`UnitId` + `UnitFactor`** (لقطة، Remarks3) · `BatchNumber?` · `ExpiryDate?` · `ExpectedQuantity?`/`VarianceQuantity?` (لسه غير مُفعّلين) | |
| `BranchRequest` / `BranchRequestLine` | `Status`(7 قيم) · `RequestedQuantity` · `ApprovedQuantity?` · `UnitId`+`UnitFactor` | دايمًا موجّه لمخزن رئيسي ضمنيًا |
| `InventoryCount` / `InventoryCountLine` | `Status`(7 قيم) · `SystemQuantity`(لقطة وقت الإنشاء) · `CountedQuantity?` · `UnitId`+`UnitFactor` · `VarianceQuantity?` · `SettlementDecision` · `JournalEntryId?` | |
| `Recipe` / `RecipeLine` | `RecipeFamilyCode` + `VersionNumber` + `PreviousVersionId?` + `IsCurrentVersion` · `OutputItemId` (Semi/Finished بس) · `OutputQuantity` · `WastePercentage` · `Status`(4 قيم) | تعديل وصفة معتمدة = إصدار جديد، مش تعديل السجل |
| `ProductionOrder` | `RecipeId` · `PlannedQuantity` · `ActualQuantity?` · `StandardCost` · `ActualCost?` · `ProductionIssueDocumentId?` / `ProductionReceiptDocumentId?` | مفيهوش `JournalEntryId` — لسه مش موصول بالترحيل (قسم 4) |
| `WasteRecord` | `Quantity` · `SourceDocumentType`(`ProductionOrder`/`POSRealtimeLoss`/`Manual`) · `JournalEntryId?` | |

### 1.4 الإعدادات — `Inventory/ShortagePolicy.cs`
| الكيان | الملاحظة |
|---|---|
| `ProductionSalesModeSetting` | `ScopeType`(Company/Branch/POS/Item) + `ScopeId?` + `Mode`(RealTime/Stocked) |
| `ShortagePolicy` | صف واحد للشركة: `AllowOverrideOnShortage` + `RequiresApprovalForOverride` — **`AllowOverrideOnShortage` غير متصل فعليًا** بمنطق `StockMovementService` (اللي بيستخدم `Warehouse.AllowNegativeBalance` مباشرة) |
| `InventorySettings` | صف واحد للشركة: `SlowMovingThresholdDays` (افتراضي 90) |

---

## 2. دورة المستند المخزني الموحّد — 8 أنواع مش 7

```
مسودة (Draft) ──PostWarehouseDocumentCommand──► مرحّل (Posted)
```
`StockIn`(1) · `StockOut`(2) · `TransferOrder`(3) · `TransferReceipt`(4) · `InventoryAdjustment`(5) ·
`ProductionIssue`(6) · `ProductionReceipt`(7) · **`OpeningBalance`(8)**.

- `StockIn/StockOut/InventoryAdjustment/OpeningBalance/TransferOrder/TransferReceipt` بتتعمل
  Draft من شاشة وبتتترحّل يدويًا (`PostWarehouseDocumentCommand`).
- `ProductionIssue/ProductionReceipt` بتتولّد **مباشرة Posted** من `CompleteProductionOrderCommand`
  (مافيش شاشة إنشاء يدوي لهم — `ProductionIssuesController`/`ProductionReceiptsController`
  للعرض فقط، `ProductionDocumentsControllerBase`).
- التحويل مستندان: `TransferOrder` (خروج من المصدر، `CustodyOfficerId` إلزامي) ثم `TransferReceipt`
  (دخول للوجهة، بيترفض لو الأمر الأصلي مش `Posted` — `INV-R37-TRANSFER-ORDER-NOT-POSTED`).
- الفرع بيتحدد من المخزن نفسه دلوقتي، مش من اللي بعت الطلب —
  `WarehouseDocuments/Common/WarehouseDocumentBranch.cs` (قسم 11).
- تاريخ الانتهاء لصنف متتبّع بيتحسب تلقائيًا من `Item.ShelfLifeDays` لو الطالب ما بعتوش —
  `WarehouseDocuments/Common/ExpiryDateResolver.cs`.

---

## 3. الـ Commands (54) — `src/Habbak.ERP.Application/Inventory/**/Commands/`

| المجموعة | الأوامر |
|---|---|
| Items (3) | Create/Update/Delete Item |
| ItemGroups (3) | Create/Update/Delete ItemGroup |
| POSCategories (3) | Create/Update/Delete POSCategory |
| UnitsOfMeasure (3) | Create/Update/Delete UnitOfMeasure |
| Warehouses (3) | Create/Update/Delete Warehouse |
| CustodyOfficers (3) | Create/Update/Delete CustodyOfficer |
| WarehouseDocuments (5) | CreateWarehouseDocument · UpdateWarehouseDocument · PostWarehouseDocument · CreateTransferReceipt · UpdateTransferReceipt |
| BranchRequests (4) | CreateBranchRequest · UpdateBranchRequest · SubmitBranchRequest · ApproveBranchRequest |
| InventoryCounts (9) | CreateInventoryCount · StartInventoryCount · RecordCountedQuantities · SubmitInventoryCountForSettlement · SettleInventoryCountLine · CompleteInventoryCountSettlement · CloseInventoryCount · RejectInventoryCount · CancelInventoryCount |
| Recipes (6) | CreateRecipe · UpdateRecipe · SubmitRecipe · ApproveRecipe · RejectRecipe · CreateNewRecipeVersion |
| ProductionOrders (5) | CreateProductionOrder · UpdateProductionOrder · StartProductionOrder · CompleteProductionOrder · CancelProductionOrder |
| WasteRecords (2) | CreateWasteRecord · UpdateWasteRecord |
| Settings (5) | UpdateInventorySettings · UpdateShortagePolicy · Create/Update/DeleteProductionSalesModeSetting |

## 4. الـ Queries (25 كـ`record` + 6 كـ`class : ListQuery` = 31)

**قوائم بسيطة (`record`, بدون paging):** GetItemsListQuery · GetItemGroupsListQuery ·
GetPOSCategoriesListQuery · GetUnitsOfMeasureListQuery · GetWarehousesListQuery ·
GetCustodyOfficersListQuery.

**قوائم مُصفّحة (`class : ListQuery`, مش معدودة في إحصاء `00-`):** GetRecipesListQuery ·
GetBranchRequestsListQuery · GetProductionOrdersListQuery · GetInventoryCountsListQuery ·
GetWarehouseDocumentsListQuery · GetWasteRecordsListQuery.

**تفاصيل:** GetItemByIdQuery · GetBranchRequestByIdQuery · GetInventoryCountByIdQuery ·
GetRecipeByIdQuery · GetProductionOrderByIdQuery · GetWarehouseDocumentByIdQuery ·
GetWasteRecordByIdQuery · GetPostedTransferOrdersListQuery.

**إعدادات:** GetInventorySettingsQuery · GetShortagePolicyQuery · GetProductionSalesModeSettingsListQuery.

**تقارير (8، `Inventory/Reports/Queries/`):** GetStockReportQuery · GetBelowMinimumItemsReportQuery ·
GetItemMovementReportQuery · GetDailyMovementReportQuery · GetRawMaterialConsumptionReportQuery ·
GetStockTransfersReportQuery · GetInventoryCountsReportQuery · GetWasteReportQuery.

## 5. الـ Validators (31 `AbstractValidator`)

كل أمر بيغيّر بيانات (مش الـ workflow transitions البسيطة زي `Start`/`Approve`) عنده Validator خاص
بيه في نفس ملف الـ Command. أبرزها فعليًا:
- `CreateRecipeCommandValidator`/`UpdateRecipeCommandValidator`: `OutputQuantity > 0`،
  `Lines.Count > 0`، وبينادي `RecipeCycleChecker.EnsureNoCycleAsync` (قاعدة 13، BFS على كل
  `RecipeLines` بغض النظر عن حالة أو إصدار الوصفة الأب — نسخة Draft قديمة أو إصدار متجاوَز لسه
  بيتحسب في الرسم البياني لمنع حلقة مستقبلية).
- `CreateWasteRecordCommandValidator`: `Reason` إلزامي وبحد 500 حرف.
- `CompleteProductionOrderCommandValidator`: `WasteReason` إلزامي لو `ActualWasteQuantity > 0`.
- `CreateWarehouseDocumentCommandValidator`: بيفرض المخزن المصدر/الوجهة المطلوبين حسب `DocumentType`.

---

## 6. محرك التكلفة — `Habbak.ERP.Infrastructure/Services/StockMovementService.cs`

كل حركة مخزنية في النظام (WarehouseDocument, ProductionOrder, InventoryCount, WasteRecord, POS,
Sales DeliveryOrder) بتعدّي حصريًا على `IStockMovementService.ApplyMovementAsync` —
مفيش كود بيلمس `StockBalance`/`StockTransaction` مباشرة.

### 6.1 AverageCost لكل مخزن (قاعدة 39/40)
- **داخل**: التكلفة **من المُنادي إلزاميًا**، `INV-R41-UNIT-COST-REQUIRED` لو `UnitCost <= 0`.
- **خارج**: التكلفة **من `StockBalance.AverageCost`**، وأي تكلفة بعتها المُنادي بيتم تجاهلها —
  كده مستحيل موديول يغلط في تسجيل الـCOGS حتى بالخطأ.

### 6.2 المعادلة (`ResolveUnitCost` + جسم `ApplyMovementAsync`)
```csharp
New = (OldQty × OldAvg + RecvQty × RecvCost) / (OldQty + RecvQty)   // OldQty>0 و Total>0
New = RecvCost                                                      // غير كده (أول حركة أو رصيد بالسالب)
```
محسوبة **جوّه نفس الـtransaction** اللي بتحرّك الكمية — الكمية والتكلفة ما بيوصلوش منفصلين.

### 6.3 `ResolveInboundCostAsync` — تكلفة بدون سعر مصدر
للمرتجعات وفوائض الجرد (مفيش فاتورة شراء وراهم): متوسط المخزن الحالي → `Item.StandardCost` →
استثناء `INV-R41-UNIT-COST-REQUIRED`. أبدًا صفر.

### 6.4 حفظ التكلفة عبر التحويل (قاعدة 40)
`PostWarehouseDocumentCommand` بيكتب `movement.UnitCost` (اللي جه من متوسط المخزن المصدر) **رجوع
على `line.UnitCost`** بعد الترحيل — فالـ`TransferReceipt` بينسخ نفس الرقم حرفيًا بدل ما يشتق واحد
جديد من متوسط الوجهة.

### 6.5 الوحدات — `Inventory/Common/ItemUnits.cs`
كل سطر (وصفة/مستند/طلب فرع/جرد) بيخزّن `UnitId`+`UnitFactor` **لقطة وقت الحفظ**؛ الكمية والتكلفة
بتفضل بوحدة السطر، والتحويل للوحدة الأساسية بيحصل بس لحظة تحريك المخزون:
`ToBase(qty, factor) = qty × factor` و `CostPerBase(cost, factor) = cost / factor`. تغيير تحويلات
الصنف بعدين ما بيعيدش كتابة مستندات قديمة. `Resolve()` بيرفض أي وحدة مش الأساسية ولا من
`ItemUnitConversion` بتاعة نفس الصنف (`INV-UNIT-NOT-ALLOWED`).

---

## 7. استهلاك الوصفات — RealTime vs Stocked

المنطق في `Habbak.ERP.Application/POS/Invoices/Commands/CompleteCheckPayment/CompleteCheckPaymentCommand.cs`
(`BuildStockConsumptionAsync` + `GuardAgainstShortagesAsync`)، والحل مشترك مع الإنتاج عبر نفس
معادلة القياس في `CompleteProductionOrderCommand`.

| الوضع | السلوك |
|---|---|
| `Stocked` (الافتراضي) | بيخصم الصنف المباع نفسه من رصيده — سلوك قديم زي ما هو |
| `RealTime` + وصفة `Approved`/`IsCurrentVersion` | بيخصم **مكوّنات الوصفة** بدل الصنف، بمعامل `line.Quantity ÷ Recipe.OutputQuantity`، كحركة `ProductionIssue` |
| صنف بلا وصفة معتمدة، حتى تحت RealTime | بيخصم نفسه (fallback) |
| `!item.IsStocked` | بيتجاوَز تمامًا — مفيش حركة |

**الأولوية**: `Item > POS > Branch > Company` — `Inventory/Settings/ProductionSalesModeResolver.cs`.
الافتراضي `Stocked` عن قصد (تفعيل RealTime بيغيّر سلوك المخزون وممكن يرفض بيعات).

**قاعدة 18 — منع النقص قبل الخصم**: `GuardAgainstShortagesAsync` بيجمع كل المتطلبات **على مستوى
الفاتورة كلها** (لاتيهين بيستخدموا نفس اللبن) وبيرفض بـ`POS-COMPONENT-SHORTAGE` قبل أي خصم فعلي —
بيسمّي كل مكوّن ناقص والمتاح/المطلوب. `Warehouse.AllowNegativeBalance` بيلغي الفحص بالكامل.

---

## 8. الـ Frontend — `frontend/src/features/inventory/**`

**23 شاشة (45 Route)** مسجّلة في `frontend/src/app/routeTable.tsx` تحت `/inventory/*`:
items · item-groups · units-of-measure · pos-categories · warehouses · reports ·
opening-balances · stock-in · stock-out · transfer-order · transfer-receipt ·
inventory-adjustments · inventory-counts · settings (3 شاشات فرعية) · custody-officers ·
branch-requests (+ شاشة اعتماد منفصلة `branch-requests/:id/approve`) · recipes ·
production-orders · waste-records · production-issues/production-receipts (List+Detail للقراءة بس).

**نمط موحّد**: `WarehouseDocumentsListPage`/`WarehouseDocumentEditPage` كلاهما بياخد `kind` prop
(`stock-in`/`stock-out`/`transfer-order`/`inventory-adjustments`/`opening-balances`) — **مكوّن واحد
لـ5 شاشات**، مش 5 مكوّنات منفصلة. نفس الفكرة لـ`ProductionDocumentsListPage`/`ProductionDocumentDetailPage`
(`production-issues`/`production-receipts`، للعرض فقط).

**مكوّنات خاصة بالموديول**: `items/UnitSelect.tsx` (اختيار وحدة من وحدات الصنف + الأساسية،
مستخدم في كل شاشة بها سطر مخزني). التصدير والبحث عبر `ui-kit` المشترك (`DataGrid`/`ActionBar`/
`SearchableSelect`/`ExportMenu`) زي كل موديول تاني — مفيش مكوّنات تصدير خاصة بالمخزون.

---

## 9. الـ Database Schema — `Persistence/Configurations/Inventory/*.cs` (15 ملف)

| الفهرس الفريد | الملاحظة |
|---|---|
| `Item(CompanyId, Code)` | فلتر `IsDeleted=0` |
| `ItemUnitConversion(ItemId, AlternateUnitOfMeasureId)` | فلتر `IsDeleted=0` |
| `ItemWarehouseSettings(ItemId, WarehouseId)` | فلتر `IsDeleted=0` |
| `BranchItemLimit(ItemId, BranchId)` | فلتر `IsDeleted=0` |
| `StockBalance(ItemId, WarehouseId)` | فلتر `IsDeleted=0` — صف واحد بالضبط لكل زوج |
| `WarehouseDocument(CompanyId, DocumentType, DocumentNumber)` | **بدون فلتر** `IsDeleted` |
| `InventoryCount(CompanyId, CountNumber)` · `(WarehouseId, Status)` غير فريد | فلتر `IsDeleted=0` على الأول |
| `Recipe(RecipeFamilyCode, VersionNumber)` | فلتر `IsDeleted=0` — **مش مربوط بالشركة** (خطر عند LocalDB مشتركة) |
| `ProductionOrder(CompanyId, OrderNumber)` | فلتر `IsDeleted=0` |
| `BranchRequest(CompanyId, RequestNumber)` | فلتر `IsDeleted=0`؛ + فهرس غير فريد `(CompanyId, BranchId, Status)` |

**Restrict معظم العلاقات** (Item/Warehouse/Unit references) — **Cascade** بس على سطور المستندات
التابعة (WarehouseDocumentLine، RecipeLine، BranchRequestLine، InventoryCountLine،
ItemUnitConversion، ItemWarehouseSettings، BranchItemLimit) عند حذف الأب.

**Migrations خاصة بالموديول من آخر تقرير**: `AddStockBalanceAverageCost` + `RepairTransferOrphanedStockCosts`
(09-14) · `AddPostingEngineDocuments` (09-18، أضافت `JournalEntryId` لـWarehouseDocument/
InventoryCount/WasteRecord) · `InventoryUnitsTaxCodeDefaultCurrency` (09-19، أضافت `UnitId`/`UnitFactor`
لكل أنواع السطور + `Item.TaxCode`). مفيش migration خاص بالمخزون بين 09-19 و09-25.

---

## 10. الـ Business Rules — أبرز القواعد المُنفَّذة فعليًا في الكود

| # | القاعدة | مكان التنفيذ |
|---|---|---|
| 1 | ممنوع رصيد بالسالب إلا لو `Warehouse.AllowNegativeBalance` | `StockMovementService.ApplyMovementAsync` |
| 8 | صنف `IsTracked` يتطلب `BatchNumber` إلزاميًا | نفس الملف |
| 13 | منع الحلقة الدائرية في مكوّنات الوصفة | `RecipeCycleChecker` |
| 17 | أولوية نموذج البيع/الإنتاج: صنف > كاشير > فرع > شركة | `ProductionSalesModeResolver` |
| 18 | رفض البيع لو أي مكوّن ناقص، مجمّع على الفاتورة كلها | `GuardAgainstShortagesAsync` |
| 30 | `CustodyOfficerId` إلزامي على `TransferOrder`، منسوخ تلقائيًا لـ`TransferReceipt` | `CreateWarehouseDocumentCommand`/`CreateTransferReceiptCommand` |
| 31 | تعديل وصفة معتمدة = إصدار جديد بـ`VersionNumber+1`، مش تعديل مباشر | `CreateNewRecipeVersionCommand` |
| 32 | تحويل الوحدات لكل صنف، مش عام | `ItemUnitConversion` + `ItemUnits` |
| 37 | استلام تحويل يرفض بدون أمر تحويل `Posted` | `PostWarehouseDocumentCommand` (`INV-R37-...`) |
| 39/40 | متوسط مرجّح لكل مخزن + حفظ التكلفة عبر التحويل | قسم 6 أعلاه |
| 41 | لا حركة داخلة بدون تكلفة > صفر، ولا حركة تسوية/مرتجع بدون مصدر تكلفة | `ResolveUnitCost` / `ResolveInboundCostAsync` |

---

## 11. اللي اتعمل من آخر تقرير (09-14 → 09-25)

1. **محرك الترحيل اتوصّل فعليًا** (`AddPostingEngineDocuments`, 09-18): تعليق `WarehouseDocument.cs`
   الحالي لسه بيقول "لا يتكامل مع IPostingService" — **ده بقى غير دقيق**. الكود الفعلي
   (`PostWarehouseDocumentCommand`, `CloseInventoryCountCommand`, `CreateWasteRecordCommand`)
   بينادي `IPostingTemplateEngine.PostIfConfiguredAsync` لشاشات `StockIn`/`StockOut`/
   `InventoryAdjustment`/`OpeningBalance`/`InventoryCount`/`Waste` (6 من 20 شاشة الترحيل الكلية،
   [00-System-Overview-UPDATED.md](00-System-Overview-UPDATED.md) قسم 3). **`ProductionIssue`/
   `ProductionReceipt` لسه بدون ترحيل** — نفس السبب: مفيش حساب مخزون/فرق تكلفة في دليل الحسابات بعد.
2. **الوحدات بقت لقطة على مستوى السطر** (Remarks3, migration `InventoryUnitsTaxCodeDefaultCurrency`,
   09-19): `WarehouseDocumentLine`/`BranchRequestLine`/`RecipeLine`/`InventoryCountLine` كلهم
   عندهم `UnitId`+`UnitFactor` دلوقتي (قسم 6.5) — قبل كده كل حاجة كانت بالوحدة الأساسية فقط.
3. **فرع المستند المخزني بقى مُشتق من المخزن نفسه** (`WarehouseDocumentBranch.cs`) بدل ما يتبعت من
   الشاشة — قبل كده كل المستندات كانت company-level وأي مستخدم مقيّد بفرع كان شايف كل التحويلات.
4. **تاريخ الانتهاء بقى بيتحسب تلقائيًا** لصنف متتبّع بلا تاريخ صريح (`ExpiryDateResolver.cs`).
5. **`Item.TaxCode`** اتضاف (اختياري، لحد ما موديول ETA يتبني).
6. مفيش تغيير في محرك التكلفة نفسه (قسم 6) ولا في استهلاك الوصفات (قسم 7) — اتبنوا في 09-14
   ولسه زي ما هما.

---

## 12. الـ Risks / Gaps

| # | الفجوة | الأثر |
|---|---|---|
| 1 | `ProductionOrder` بلا ترحيل محاسبي (لا `JournalEntryId` ولا نداء `IPostingService`) | فرق التكلفة (قاعدة 16، Standard vs Actual) محسوب في `CompleteProductionOrderResult` بس مش بيتحوّل لقيد |
| 2 | `ShortagePolicy.AllowOverrideOnShortage` **غير متصل** — `StockMovementService` بيقرا `Warehouse.AllowNegativeBalance` مباشرة | إعداد الشاشة #22 حاليًا بلا أثر فعلي على منع/سماح الرصيد السالب |
| 3 | فهرس `Recipe(RecipeFamilyCode, VersionNumber)` **مش مربوط بالشركة** | تضارب أكواد محتمل بين شركتين على نفس الـLocalDB (تأثير مباشر على اختبارات مشتركة، قسم 13) |
| 4 | `StockBalance.RowVersion` بدون إعادة محاولة (retry) عند تعارض التزامن | تحديثين متزامنين على نفس الرصيد = استثناء يوصل للمستخدم مباشرة، مش إعادة تلقائية |
| 5 | `WarehouseDocumentLine.ExpectedQuantity`/`VarianceQuantity` معرّفين بس **غير مُفعّلين** في أي Command | حقلين جاهزين في الـschema لاستلام التحويل بفارق، بدون منطق يملأهم لحد الآن |
| 6 | `CustodyOfficer.EmployeeId` غير موجود (اسم مباشر بدل FK) | لحد ما موديول HR يتبني — migration منفصلة مطلوبة وقتها |
| 7 | تعليق `WarehouseDocument.cs` نفسه قديم وبيوصف حالة قبل قسم 11.1 | مصدر تضليل لو حد قرا الكود من غير التقرير ده |

---

## 13. الـ Tests

| الملف | التغطية |
|---|---|
| `Habbak.ERP.IntegrationTests/StockMovementCycleTests.cs` | ثوابت محرك الحركة نفسه (متوسط مرجّح، رفض السالب، البدفعة) |
| `Habbak.ERP.ApiTests/OperationalCycle/BranchRequestFlowTests.cs` | طلب فرع → اعتماد → أمر تحويل تلقائي |
| `Habbak.ERP.ApiTests/OperationalCycle/TransferOrderFlowTests.cs` / `TransferReceiptFlowTests.cs` | دورة التحويل الكاملة بمخزنين ومسؤول عهدة |
| `Habbak.ERP.ApiTests/OperationalCycle/StockDeductionOnSaleTests.cs` | RealTime vs Stocked + قاعدة 18 (النقص) عبر HTTP فعلي |
| `Habbak.ERP.ApiTests/OperationalCycle/DocumentPostingTests.cs` | تكامل `PostWarehouseDocumentCommand` مع محرك الترحيل |
| `Habbak.ERP.ApiTests/OperationalCycle/GoodsReceiptFlowTests.cs` | **لسه بيؤكّد سلوك خاطئ قصدًا** (تعليق داخل الملف بالتأكيد الصحيح البديل) — إشارة انتظار لا حذف |
| `Habbak.ERP.ApiTests/QaBatch20260919Tests.cs` | دفعة Remarks3: الوحدات، فرع المستند، تاريخ الانتهاء التلقائي |

لا يوجد ملف اختبار مستقل لـ`ProductionOrder`/`Recipe`/`InventoryCount`/`WasteRecord` بمعزل — تغطيتهم
حاليًا عن طريق دوران الدورة التشغيلية الكاملة (Operational Cycle) بس، مش وحدات معزولة.

---

خلصت التقرير 2.
