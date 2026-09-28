# تحليل موديول المخازن (Inventory Management)
## Analysis File for Claude Code — NozomSoft ERP
## نسخة شاملة — مستوى Odoo

---

## 🎯 نطاق الموديول

```
وحدات القياس والتحويل
        ↓
فئات الأصناف (شجرة)
        ↓
تعريف الأصناف (Item Master)
        ↓
المستودعات والمواقع
        ↓
حركات المخزون
    ├── استلام (Receipt)
    ├── صرف (Issue)
    ├── تحويل (Transfer)
    ├── تسوية (Adjustment)
    └── مرتجع (Return)
        ↓
طرق التكلفة
    ├── Average Cost
    ├── FIFO (عبر Lots)
    └── Standard Cost
        ↓
الجرد الفعلي (Physical Inventory)
        ↓
تقارير المخزون
```

---

## 🔑 FormCodes — نطاق هذا الموديول: 500–599

| FormCode | FormName | FormNameAr | HasTable | HasTree | ملاحظة |
|----------|----------|------------|----------|---------|--------|
| 500 | Warehouses | المستودعات | 1 | 0 | |
| 501 | WarehouseLocations | مواقع المستودع | 1 | 1 | هرمية |
| 502 | ItemCategories | فئات الأصناف | 1 | 1 | شجرة |
| 503 | UnitOfMeasures | وحدات القياس | 1 | 0 | |
| 504 | UnitConversions | تحويلات الوحدات | 1 | 0 | |
| 505 | ItemAttributes | خصائص الأصناف | 1 | 0 | اللون، الحجم |
| 510 | Items | الأصناف | 1 | 0 | HasTabs=1 |
| 511 | ItemPrices | أسعار الأصناف | 1 | 0 | |
| 512 | ItemBarcodes | باركود الأصناف | 1 | 0 | متعدد |
| 513 | ItemAlternatives | الأصناف البديلة | 1 | 0 | |
| 520 | StockReceipts | إذن استلام | 1 | 0 | |
| 521 | StockIssues | إذن صرف | 1 | 0 | |
| 522 | StockTransfers | تحويل مخزون | 1 | 0 | |
| 523 | StockAdjustments | تسوية مخزون | 1 | 0 | |
| 530 | PhysicalInventory | الجرد الفعلي | 1 | 0 | |
| 540 | ItemLots | الدفعات | 1 | 0 | للـ FIFO |
| 550 | StockValuation | تقييم المخزون | 0 | 0 | Report |

---

## 🗄️ الجداول الكاملة

### 1. WEBERP_UnitOfMeasures — وحدات القياس
```sql
CREATE TABLE [dbo].[WEBERP_UnitOfMeasures] (
    [Id]          INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]        NVARCHAR(20)  NOT NULL,
    [NameAr]      NVARCHAR(50)  NOT NULL,
    [NameEn]      NVARCHAR(50)  NULL,
    [UnitType]    NVARCHAR(50)  NOT NULL DEFAULT 'unit',
    -- unit / weight / volume / length / area / time
    [Rounding]    DECIMAL(10,6) NOT NULL DEFAULT 0.01,
    -- دقة التقريب (0.001 = 3 خانات عشرية)
    [IsActive]    BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_UOMCode UNIQUE (Code, CompanyID, BranchID)
);

-- Seed
-- INSERT INTO WEBERP_UnitOfMeasures (Code,NameAr,NameEn,UnitType,...) VALUES
-- ('PCS','قطعة','Piece','unit',...),
-- ('BOX','صندوق','Box','unit',...),
-- ('KG','كيلوجرام','Kilogram','weight',...),
-- ('LTR','لتر','Liter','volume',...),
-- ('MTR','متر','Meter','length',...),
-- ('HRS','ساعة','Hour','time',...);
```

### 2. WEBERP_UnitConversions — تحويلات الوحدات
```sql
CREATE TABLE [dbo].[WEBERP_UnitConversions] (
    [Id]         INT           IDENTITY(1,1) PRIMARY KEY,
    [ItemId]     INT           NULL,
    -- NULL = ينطبق على كل الأصناف
    -- NOT NULL = خاص بصنف معين (يتغلب على القاعدة العامة)
    [FromUOMId]  INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [ToUOMId]    INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [Factor]     DECIMAL(18,6) NOT NULL,
    -- 1 صندوق → 24 قطعة : Factor = 24
    -- 1 كيلو   → 1000 جرام : Factor = 1000
    [FactorInv]  AS (1.0 / NULLIF(Factor, 0)), -- Computed: العكس
    [IsActive]   BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_UOMConversion UNIQUE (ISNULL(ItemId,-1), FromUOMId, ToUOMId, CompanyID, BranchID),
    CONSTRAINT CHK_UOMConversion_Different CHECK (FromUOMId <> ToUOMId)
);
```

### 3. WEBERP_ItemCategories — فئات الأصناف (شجرة)
```sql
CREATE TABLE [dbo].[WEBERP_ItemCategories] (
    [Id]                   INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]                 NVARCHAR(20)  NOT NULL,
    [NameAr]               NVARCHAR(100) NOT NULL,
    [NameEn]               NVARCHAR(100) NULL,
    [ParentId]             INT           NULL REFERENCES WEBERP_ItemCategories(Id),
    [Level]                INT           NOT NULL DEFAULT 1,
    [IsParent]             BIT           NOT NULL DEFAULT 0,
    -- حسابات افتراضية للأصناف في هذه الفئة
    [InventoryAccountId]   INT           NULL,  -- حساب المخزون
    [COGSAccountId]        INT           NULL,  -- تكلفة البضاعة المباعة
    [SalesAccountId]       INT           NULL,  -- الإيراد
    [PurchaseAccountId]    INT           NULL,  -- المشتريات
    -- إعدادات افتراضية
    [DefaultCostMethod]    NVARCHAR(20)  NOT NULL DEFAULT 'average',
    -- average / fifo / standard
    [RemovalStrategy]      NVARCHAR(20)  NOT NULL DEFAULT 'fifo',
    -- fifo / lifo / closest_expiry
    [IsActive]             BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_ItemCategoryCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 4. WEBERP_Warehouses — المستودعات
```sql
CREATE TABLE [dbo].[WEBERP_Warehouses] (
    [Id]                INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]              NVARCHAR(20)  NOT NULL,
    [NameAr]            NVARCHAR(100) NOT NULL,
    [NameEn]            NVARCHAR(100) NULL,
    [WarehouseType]     NVARCHAR(30)  NOT NULL DEFAULT 'internal',
    -- internal / transit / view / customer / supplier / production
    [Address]           NVARCHAR(300] NULL,
    [ManagerId]         INT           NULL,
    -- الـ Locations الافتراضية لهذا المستودع
    [InputLocationId]   INT           NULL,  -- موقع الاستلام
    [OutputLocationId]  INT           NULL,  -- موقع الشحن
    [StockLocationId]   INT           NULL,  -- موقع المخزون الرئيسي
    [PackLocationId]    INT           NULL,  -- موقع التعبئة
    [IsActive]          BIT           NOT NULL DEFAULT 1,
    [Notes]             NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_WarehouseCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 5. WEBERP_WarehouseLocations — مواقع المستودع (شجرة)
```sql
CREATE TABLE [dbo].[WEBERP_WarehouseLocations] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]         NVARCHAR(20)  NOT NULL,
    [NameAr]       NVARCHAR(100] NOT NULL,
    [NameEn]       NVARCHAR(100) NULL,
    [WarehouseId]  INT           NULL REFERENCES WEBERP_Warehouses(Id),
    -- NULL للمواقع الافتراضية (Customers/Suppliers/Scrap)
    [ParentId]     INT           NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [Level]        INT           NOT NULL DEFAULT 1,
    [IsParent]     BIT           NOT NULL DEFAULT 0,
    [LocationType] NVARCHAR(30)  NOT NULL DEFAULT 'internal',
    -- internal / customer / supplier / transit / inventory / production / scrap / view
    [Barcode]      NVARCHAR(50)  NULL,
    [IsActive]     BIT           NOT NULL DEFAULT 1,
    [ScrapLocation] BIT          NOT NULL DEFAULT 0,
    -- هل هذا الموقع للمهملات؟
    [ReturnLocation] BIT         NOT NULL DEFAULT 0,
    -- هل هذا موقع للمرتجعات؟
    [Notes]        NVARCHAR(200) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_LocationCode UNIQUE (Code, CompanyID, BranchID)
);

-- Seed: المواقع الافتراضية الإلزامية
-- INSERT INTO WEBERP_WarehouseLocations (Code,NameAr,LocationType,...) VALUES
-- ('WH/STOCK',   'المخزون الرئيسي',    'internal',...),
-- ('CUSTOMERS',  'العملاء',             'customer',...),
-- ('SUPPLIERS',  'الموردون',            'supplier',...),
-- ('TRANSIT',    'عبور',                'transit',...),
-- ('SCRAP',      'مهملات',              'scrap',...),
-- ('VIRTUAL',    'افتراضي',             'view',...),
-- ('PRODUCTION', 'الإنتاج',             'production',...);
```

### 6. WEBERP_Items — الأصناف (الجدول الرئيسي)
```sql
CREATE TABLE [dbo].[WEBERP_Items] (
    [Id]                 INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]               NVARCHAR(50)  NOT NULL,
    [NameAr]             NVARCHAR(200) NOT NULL,
    [NameEn]             NVARCHAR(200) NULL,
    [CategoryId]         INT           NULL REFERENCES WEBERP_ItemCategories(Id),
    -- وحدات القياس
    [UOMId]              INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- الوحدة الأساسية (قطعة)
    [PurchaseUOMId]      INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- وحدة الشراء (صندوق = 24 قطعة)
    [SalesUOMId]         INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- وحدة البيع
    -- نوع الصنف
    [ItemType]           NVARCHAR(30)  NOT NULL DEFAULT 'storable',
    -- storable    ← يُخزن ويُتتبع (الأكثر شيوعاً)
    -- consumable  ← يُصرف مباشرة بدون تتبع مخزون
    -- service     ← خدمة — لا مخزون
    -- Costing
    [CostMethod]         NVARCHAR(20)  NOT NULL DEFAULT 'average',
    -- average / fifo / standard
    [StandardCost]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [AverageCost]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [LastCost]           DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- Pricing
    [SalesPrice]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [MinSalesPrice]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxRate]            DECIMAL(5,2)  NOT NULL DEFAULT 0,
    -- Reorder
    [ReorderLevel]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ReorderQty]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [MaxStockLevel]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [LeadTimeDays]       INT           NOT NULL DEFAULT 0,
    -- وقت الاستلام بعد الطلب
    -- Tracking
    [HasLotTracking]     BIT           NOT NULL DEFAULT 0,
    -- تتبع الدفعات (FIFO)
    [HasSerialTracking]  BIT           NOT NULL DEFAULT 0,
    -- تتبع بالأرقام التسلسلية (1 سيريال = 1 وحدة)
    [HasExpiryDate]      BIT           NOT NULL DEFAULT 0,
    -- تاريخ انتهاء الصلاحية
    [ExpiryAlertDays]    INT           NOT NULL DEFAULT 0,
    -- تنبيه قبل الانتهاء بكذا يوم
    -- Accounts — تتغلب على إعدادات الفئة
    [InventoryAccountId] INT           NULL,
    [COGSAccountId]      INT           NULL,
    [SalesAccountId]     INT           NULL,
    [PurchaseAccountId]  INT           NULL,
    -- Physical
    [Weight]             DECIMAL(10,4) NULL,
    [WeightUOMId]        INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [Volume]             DECIMAL(10,4) NULL,
    [VolumeUOMId]        INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- Info
    [Description]        NVARCHAR(MAX) NULL,
    [InternalNotes]      NVARCHAR(MAX) NULL,
    [ImagePath]          NVARCHAR(500) NULL,
    [IsActive]           BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_ItemCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 7. WEBERP_ItemBarcodes — باركود الأصناف (متعدد)
```sql
CREATE TABLE [dbo].[WEBERP_ItemBarcodes] (
    [Id]        INT           IDENTITY(1,1) PRIMARY KEY,
    [ItemId]    INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [Barcode]   NVARCHAR(100) NOT NULL,
    [UOMId]     INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- ممكن يكون باركود الصندوق مختلف عن باركود القطعة
    [IsDefault] BIT           NOT NULL DEFAULT 0,
    [CompanyID] INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy] INT NULL, [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Barcode UNIQUE (Barcode, CompanyID, BranchID)
);
```

### 8. WEBERP_ItemStock — رصيد المخزون
```sql
CREATE TABLE [dbo].[WEBERP_ItemStock] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [ItemId]       INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [WarehouseId]  INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    [LocationId]   INT           NULL     REFERENCES WEBERP_WarehouseLocations(Id),
    [LotId]        INT           NULL,
    -- للأصناف ذات التتبع بالدفعات
    [Quantity]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ReservedQty]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- محجوز لأوامر بيع مؤكدة
    [IncomingQty]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- متوقع الوصول (أوامر شراء)
    [AvailableQty] AS (Quantity - ReservedQty),
    [AverageCost]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalValue]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_ItemStock UNIQUE (ItemId, WarehouseId, ISNULL(LocationId,-1), ISNULL(LotId,-1), CompanyID, BranchID)
);
```

### 9. WEBERP_ItemLots — الدفعات والأرقام التسلسلية
```sql
CREATE TABLE [dbo].[WEBERP_ItemLots] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [LotNumber]       NVARCHAR(50)  NOT NULL,
    [LotType]         NVARCHAR(10)  NOT NULL DEFAULT 'lot',
    -- lot    ← دفعة (كمية > 1)
    -- serial ← رقم تسلسلي (كمية = 1 دائماً)
    [WarehouseId]     INT           NULL REFERENCES WEBERP_Warehouses(Id),
    [LocationId]      INT           NULL REFERENCES WEBERP_WarehouseLocations(Id),
    -- Dates
    [PurchaseDate]    DATE          NOT NULL DEFAULT GETDATE(),
    [ExpiryDate]      DATE          NULL,
    [RemovalDate]     DATE          NULL,
    -- تاريخ الإزالة (قبل ExpiryDate للأغذية)
    -- Quantities
    [ReceivedQty]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [RemainingQty]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- Cost
    [UnitCost]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalCost]       AS (RemainingQty * UnitCost),
    -- References
    [SupplierRef]     NVARCHAR(100) NULL,
    [SupplierLot]     NVARCHAR(100) NULL,
    -- Lot رقم المورد الأصلي
    [GRNId]           INT           NULL,
    -- Status
    [IsExpired]       BIT           NOT NULL DEFAULT 0,
    [IsBlocked]       BIT           NOT NULL DEFAULT 0,
    -- محجوز للجودة أو خلافه
    [BlockReason]     NVARCHAR(200) NULL,
    [Notes]           NVARCHAR(500] NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_LotNumber UNIQUE (ItemId, LotNumber, CompanyID, BranchID)
);
```

### 10. WEBERP_StockPickings — حركات المخزون (Master)
```sql
-- اسم Odoo: stock.picking
-- يمثل أي حركة مخزون (إذن استلام / صرف / تحويل / إلخ)
CREATE TABLE [dbo].[WEBERP_StockPickings] (
    [Id]                INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]              NVARCHAR(30)  NOT NULL,
    -- رقم تلقائي: REC/2025/001 أو ISSUE/2025/001
    [PickingTypeId]     INT           NOT NULL REFERENCES WEBERP_StockPickingTypes(Id),
    [PickingDate]       DATETIME2     NOT NULL DEFAULT GETDATE(),
    [ScheduledDate]     DATETIME2     NULL,
    -- الموقع المصدر والهدف
    [LocationId]        INT           NOT NULL REFERENCES WEBERP_WarehouseLocations(Id),
    -- من أين
    [LocationDestId]    INT           NOT NULL REFERENCES WEBERP_WarehouseLocations(Id),
    -- إلى أين
    [State]             NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / waiting / confirmed / assigned / done / cancel
    [Origin]            NVARCHAR(100) NULL,
    -- المستند المنشئ: PO/2025/001 أو SO/2025/001
    [PartnerId]         INT           NULL,
    -- المورد أو العميل
    [PartnerType]       NVARCHAR(20)  NULL,
    -- supplier / customer
    [Note]              NVARCHAR(MAX) NULL,
    [BackorderPickingId] INT          NULL REFERENCES WEBERP_StockPickings(Id),
    -- لو فيه كميات لم تكتمل ينشئ Backorder
    [IsBackorder]       BIT           NOT NULL DEFAULT 0,
    -- Dates
    [DateDone]          DATETIME2     NULL,
    -- تاريخ التأكيد الفعلي
    -- Related Document
    [SourceDocType]     NVARCHAR(50)  NULL,
    -- PurchaseOrder / SalesOrder / Manufacturing / Transfer
    [SourceDocId]       INT           NULL,
    -- Journal Entry
    [JournalEntryId]    INT           NULL,
    [IsPosted]          BIT           NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_PickingName UNIQUE (Name, CompanyID, BranchID)
);
```

### 11. WEBERP_StockPickingTypes — أنواع حركات المخزون
```sql
CREATE TABLE [dbo].[WEBERP_StockPickingTypes] (
    [Id]                    INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]                  NVARCHAR(20)  NOT NULL,
    [NameAr]                NVARCHAR(100] NOT NULL,
    [NameEn]                NVARCHAR(100) NULL,
    [PickingTypeCode]       NVARCHAR(20)  NOT NULL,
    -- incoming / outgoing / internal / mrp_operation
    [WarehouseId]           INT           NULL REFERENCES WEBERP_Warehouses(Id),
    [DefaultLocationId]     INT           NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [DefaultLocationDestId] INT           NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [SequencePrefix]        NVARCHAR(20)  NULL,
    -- REC/ ISSUE/ INT/ SCRAP/
    [ReservationMethod]     NVARCHAR(20)  NOT NULL DEFAULT 'at_confirm',
    -- at_confirm / manual / before_scheduled_date
    [ShowLots]              BIT           NOT NULL DEFAULT 0,
    [ShowOperations]        BIT           NOT NULL DEFAULT 1,
    [CreateBackorder]       NVARCHAR(20)  NOT NULL DEFAULT 'ask',
    -- ask / always / never
    [IsActive]              BIT           NOT NULL DEFAULT 1,
    [SortOrder]             INT           NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);

-- Seed: الأنواع الافتراضية
-- WH-RECEIPTS  ← incoming  : المورد → WH/INPUT
-- WH-DELIVERY  ← outgoing  : WH/OUTPUT → العميل
-- WH-INTERNAL  ← internal  : موقع → موقع داخلي
-- WH-RETURN    ← incoming  : مرتجع
-- WH-SCRAP     ← internal  : إتلاف
```

### 12. WEBERP_StockMoveLines — سطور الحركة
```sql
-- اسم Odoo: stock.move.line (الحركات التفصيلية)
CREATE TABLE [dbo].[WEBERP_StockMoveLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [PickingId]       INT           NOT NULL REFERENCES WEBERP_StockPickings(Id),
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [LotId]           INT           NULL     REFERENCES WEBERP_ItemLots(Id),
    [LocationId]      INT           NOT NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [LocationDestId]  INT           NOT NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [UOMId]           INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- الكميات
    [QtyDemand]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الكمية المطلوبة
    [QtyDone]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الكمية المنفذة فعلاً
    [QtyReserved]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الكمية المحجوزة
    -- في الوحدة الأساسية
    [QtyDemandBase]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyDoneBase]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [UOMConvFactor]   DECIMAL(18,6) NOT NULL DEFAULT 1,
    -- التكلفة
    [UnitCost]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalCost]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- معلومات إضافية
    [ExpiryDate]      DATE          NULL,
    [PackageId]       INT           NULL,
    -- ممكن تضاف لاحقاً
    [State]           NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / assigned / done / cancel
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 13. WEBERP_StockValuationLayers — طبقات التقييم
```sql
-- اسم Odoo: stock.valuation.layer
-- سجل كل تغيير في قيمة المخزون
CREATE TABLE [dbo].[WEBERP_StockValuationLayers] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [LotId]           INT           NULL     REFERENCES WEBERP_ItemLots(Id),
    [PickingId]       INT           NULL     REFERENCES WEBERP_StockPickings(Id),
    [MoveLineId]      INT           NULL     REFERENCES WEBERP_StockMoveLines(Id),
    [Quantity]        DECIMAL(18,4) NOT NULL,
    -- موجب = دخول، سالب = خروج
    [UnitCost]        DECIMAL(18,4) NOT NULL,
    [Value]           DECIMAL(18,4) NOT NULL,
    -- = Quantity × UnitCost
    [RemainingQty]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- للـ FIFO: الكمية المتبقية من هذه الطبقة
    [RemainingValue]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [Description]     NVARCHAR(200) NULL,
    [AccountMoveId]   INT           NULL,
    -- القيد المحاسبي المرتبط
    [StockMoveDate]   DATETIME2     NOT NULL DEFAULT GETDATE(),
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
-- هذا الجدول هو مصدر الحقيقة للتكلفة — لا تعدّل AverageCost مباشرة
```

### 14. WEBERP_PhysicalInventory — الجرد الفعلي
```sql
CREATE TABLE [dbo].[WEBERP_PhysicalInventory] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [InventoryNumber] NVARCHAR(30)  NOT NULL,
    [InventoryDate]   DATE          NOT NULL,
    [WarehouseId]     INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    [LocationId]      INT           NULL REFERENCES WEBERP_WarehouseLocations(Id),
    -- NULL = كل المواقع
    [FilterByCategory] INT          NULL REFERENCES WEBERP_ItemCategories(Id),
    -- فلترة على فئة معينة
    [State]           NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / in_progress / done / cancelled
    [StartedAt]       DATETIME2     NULL,
    [DoneAt]          DATETIME2     NULL,
    [Notes]           NVARCHAR(500] NULL,
    [JournalEntryId]  INT           NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_InventoryNumber UNIQUE (InventoryNumber, CompanyID, BranchID)
);
```

### 15. WEBERP_PhysicalInventoryLines — سطور الجرد
```sql
CREATE TABLE [dbo].[WEBERP_PhysicalInventoryLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [InventoryId]     INT           NOT NULL REFERENCES WEBERP_PhysicalInventory(Id),
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [LotId]           INT           NULL     REFERENCES WEBERP_ItemLots(Id),
    [LocationId]      INT           NOT NULL REFERENCES WEBERP_WarehouseLocations(Id),
    [SystemQty]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الكمية حسب النظام وقت الجرد
    [ActualQty]       DECIMAL(18,4) NULL,
    -- الكمية الفعلية المعدودة
    [DifferenceQty]   AS (ISNULL(ActualQty,0) - SystemQty),
    [UnitCost]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [DifferenceValue] AS ((ISNULL(ActualQty,0) - SystemQty) * UnitCost),
    [IsChecked]       BIT           NOT NULL DEFAULT 0,
    -- تم العد؟
    [Notes]           NVARCHAR(200] NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

---

## 🔧 Stored Procedures الكاملة

### SP 1: WEBERP_UOM_Convert — تحويل الوحدات
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_UOM_Convert]
    @Quantity    DECIMAL(18,4),
    @FromUOMId   INT,
    @ToUOMId     INT,
    @ItemId      INT = NULL,
    @CompanyID   INT,
    @Result      DECIMAL(18,4) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromUOMId = @ToUOMId
    BEGIN SET @Result = @Quantity; RETURN; END

    DECLARE @Factor DECIMAL(18,6);

    -- أولاً: ابحث عن تحويل خاص بالصنف
    SELECT TOP 1 @Factor = Factor
    FROM WEBERP_UnitConversions
    WHERE FromUOMId = @FromUOMId AND ToUOMId = @ToUOMId
      AND ItemId = @ItemId AND CompanyID = @CompanyID
      AND IsActive = 1 AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- ثانياً: ابحث عن تحويل عام
    IF @Factor IS NULL
        SELECT TOP 1 @Factor = Factor
        FROM WEBERP_UnitConversions
        WHERE FromUOMId = @FromUOMId AND ToUOMId = @ToUOMId
          AND ItemId IS NULL AND CompanyID = @CompanyID
          AND IsActive = 1 AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- ثالثاً: جرب العكس (1/Factor)
    IF @Factor IS NULL
    BEGIN
        DECLARE @InvFactor DECIMAL(18,6);
        SELECT TOP 1 @InvFactor = Factor
        FROM WEBERP_UnitConversions
        WHERE FromUOMId = @ToUOMId AND ToUOMId = @FromUOMId
          AND (ItemId = @ItemId OR ItemId IS NULL)
          AND CompanyID = @CompanyID
          AND IsActive = 1 AND (IsCanceled=0 OR IsCanceled IS NULL)
        ORDER BY ItemId DESC;

        IF @InvFactor IS NOT NULL AND @InvFactor <> 0
            SET @Factor = 1.0 / @InvFactor;
    END

    IF @Factor IS NULL
    BEGIN
        RAISERROR('لا يوجد تحويل معرّف بين الوحدتين', 16, 1);
        RETURN;
    END

    -- التحويل مع التقريب
    DECLARE @Rounding DECIMAL(10,6);
    SELECT @Rounding = Rounding FROM WEBERP_UnitOfMeasures WHERE Id = @ToUOMId;

    SET @Result = ROUND(@Quantity * @Factor / ISNULL(@Rounding, 0.01), 0) * ISNULL(@Rounding, 0.01);
END;
```

### SP 2: WEBERP_Items_GetByBarcode — جلب الصنف بالباركود
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Items_GetByBarcode]
    @Barcode     NVARCHAR(100),
    @WarehouseId INT = NULL,
    @PriceListId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.Id, i.Code, i.NameAr, i.NameEn,
        i.ItemType, i.CostMethod,
        i.AverageCost, i.StandardCost,
        i.SalesPrice, i.TaxRate,
        i.HasLotTracking, i.HasSerialTracking, i.HasExpiryDate,
        u.Id AS UOMId, u.NameAr AS UOMName, u.Code AS UOMCode,
        b.UOMId AS BarcodeUOMId,
        bu.NameAr AS BarcodeUOMName,
        -- تحويل الباركود UOM إلى UOM الأساسي
        CASE WHEN b.UOMId IS NOT NULL AND b.UOMId <> i.UOMId
             THEN (SELECT Factor FROM WEBERP_UnitConversions
                   WHERE (ItemId=i.Id OR ItemId IS NULL)
                   AND FromUOMId=b.UOMId AND ToUOMId=i.UOMId
                   AND CompanyID=@CompanyID ORDER BY ItemId DESC)
             ELSE 1 END AS BarcodeConvFactor,
        -- الرصيد المتاح
        ISNULL(s.AvailableQty, 0) AS AvailableQty,
        ISNULL(s.Quantity, 0) AS TotalQty
    FROM WEBERP_ItemBarcodes b
    JOIN WEBERP_Items i ON i.Id = b.ItemId
    JOIN WEBERP_UnitOfMeasures u ON u.Id = i.UOMId
    LEFT JOIN WEBERP_UnitOfMeasures bu ON bu.Id = b.UOMId
    LEFT JOIN WEBERP_ItemStock s ON s.ItemId = i.Id
        AND (@WarehouseId IS NULL OR s.WarehouseId = @WarehouseId)
        AND s.CompanyID = @CompanyID
    WHERE b.Barcode = @Barcode
      AND b.CompanyID = @CompanyID
      AND (b.IsCanceled = 0 OR b.IsCanceled IS NULL)
      AND i.IsActive = 1
      AND (i.IsCanceled = 0 OR i.IsCanceled IS NULL);
END;
```

### SP 3: WEBERP_StockPickings_Validate — تأكيد الحركة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_StockPickings_Validate]
    @PickingId INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق من الحالة
    DECLARE @State NVARCHAR(20), @LocationId INT, @LocationDestId INT, @PickingTypeId INT;

    SELECT @State=State, @LocationId=LocationId, @LocationDestId=LocationDestId,
           @PickingTypeId=PickingTypeId
    FROM WEBERP_StockPickings
    WHERE Id=@PickingId AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @State NOT IN ('confirmed','assigned')
    BEGIN ROLLBACK; RAISERROR('الحركة ليست في حالة صحيحة للتأكيد',16,1); RETURN; END

    -- 2. التحقق من وجود كميات منفذة
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_StockMoveLines
        WHERE PickingId=@PickingId AND QtyDone>0 AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('لا توجد كميات منفذة',16,1); RETURN; END

    -- 3. تحديث الرصيد
    -- ← خصم من الموقع المصدر
    MERGE WEBERP_ItemStock AS target
    USING (
        SELECT ItemId, @LocationId AS LocId,
               WarehouseId = (SELECT WarehouseId FROM WEBERP_WarehouseLocations WHERE Id=@LocationId),
               LotId, SUM(QtyDoneBase) AS Qty, SUM(TotalCost) AS Cost
        FROM WEBERP_StockMoveLines
        WHERE PickingId=@PickingId AND (IsCanceled=0 OR IsCanceled IS NULL)
        GROUP BY ItemId, LotId
    ) AS src ON target.ItemId=src.ItemId AND target.LocationId=src.LocId
           AND target.CompanyID=@CompanyID
    WHEN MATCHED THEN UPDATE SET
        target.Quantity   = target.Quantity   - src.Qty,
        target.TotalValue = target.TotalValue - src.Cost,
        target.ModifiedBy=@UserId, target.ModifiedAt=GETDATE();

    -- ← إضافة للموقع الهدف
    MERGE WEBERP_ItemStock AS target
    USING (
        SELECT ItemId, @LocationDestId AS LocId,
               WarehouseId = (SELECT WarehouseId FROM WEBERP_WarehouseLocations WHERE Id=@LocationDestId),
               LotId, SUM(QtyDoneBase) AS Qty, SUM(TotalCost) AS Cost
        FROM WEBERP_StockMoveLines
        WHERE PickingId=@PickingId AND (IsCanceled=0 OR IsCanceled IS NULL)
        GROUP BY ItemId, LotId
    ) AS src ON target.ItemId=src.ItemId AND target.LocationId=src.LocId
           AND target.CompanyID=@CompanyID
    WHEN MATCHED THEN UPDATE SET
        target.Quantity   = target.Quantity   + src.Qty,
        target.TotalValue = target.TotalValue + src.Cost,
        target.AverageCost = (target.TotalValue+src.Cost)/NULLIF(target.Quantity+src.Qty,0),
        target.ModifiedBy=@UserId, target.ModifiedAt=GETDATE()
    WHEN NOT MATCHED THEN INSERT
        (ItemId,WarehouseId,LocationId,LotId,Quantity,TotalValue,AverageCost,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (src.ItemId,src.WarehouseId,src.LocId,src.LotId,src.Qty,src.Cost,
         src.Cost/NULLIF(src.Qty,0),@CompanyID,@BranchID,@UserId,GETDATE());

    -- 4. تسجيل طبقة التقييم (Valuation Layer)
    INSERT INTO WEBERP_StockValuationLayers
        (ItemId,LotId,PickingId,MoveLineId,Quantity,UnitCost,Value,RemainingQty,RemainingValue,StockMoveDate,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT ItemId, LotId, @PickingId, Id,
           QtyDoneBase, UnitCost, TotalCost,
           QtyDoneBase, TotalCost,
           GETDATE(), @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_StockMoveLines
    WHERE PickingId=@PickingId AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- 5. تحديث حالة Lots
    UPDATE l SET l.RemainingQty = l.RemainingQty - ml.QtyDoneBase,
                 l.ModifiedBy=@UserId, l.ModifiedAt=GETDATE()
    FROM WEBERP_ItemLots l
    JOIN WEBERP_StockMoveLines ml ON ml.LotId=l.Id
    WHERE ml.PickingId=@PickingId
      AND l.CompanyID=@CompanyID;

    -- 6. تحديث حالة الحركة
    UPDATE WEBERP_StockPickings SET
        State='done', DateDone=GETDATE(),
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@PickingId;

    -- 7. إنشاء القيد المحاسبي
    EXEC WEBERP_StockPickings_CreateJournalEntry @PickingId, @CompanyID, @BranchID, @UserId;

    COMMIT;
END;
```

### SP 4: WEBERP_StockPickings_CreateJournalEntry — القيد المحاسبي
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_StockPickings_CreateJournalEntry]
    @PickingId INT, @CompanyID INT, @BranchID INT, @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LocationType NVARCHAR(30), @LocationDestType NVARCHAR(30);
    DECLARE @LocationId INT, @LocationDestId INT;

    SELECT @LocationId=LocationId, @LocationDestId=LocationDestId
    FROM WEBERP_StockPickings WHERE Id=@PickingId;

    SELECT @LocationType    = LocationType FROM WEBERP_WarehouseLocations WHERE Id=@LocationId;
    SELECT @LocationDestType= LocationType FROM WEBERP_WarehouseLocations WHERE Id=@LocationDestId;

    -- تحديد نوع الحركة لتحديد القيد المناسب
    -- internal → internal : لا قيد محاسبي
    IF @LocationType='internal' AND @LocationDestType='internal' RETURN;

    DECLARE @TotalValue DECIMAL(18,4);
    SELECT @TotalValue = SUM(TotalCost) FROM WEBERP_StockMoveLines
    WHERE PickingId=@PickingId AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF ISNULL(@TotalValue,0) = 0 RETURN;

    DECLARE @JId INT, @DebitAccountId INT, @CreditAccountId INT;

    -- incoming (استلام من مورد): Dr مخزون / Cr مشتريات أو GR/IR
    IF @LocationType='supplier' AND @LocationDestType='internal'
    BEGIN
        -- يُحدد من ItemCategory أو Item مباشرة
        SELECT TOP 1 @DebitAccountId  = ISNULL(i.InventoryAccountId, c.InventoryAccountId),
                     @CreditAccountId = ISNULL(i.PurchaseAccountId,   c.PurchaseAccountId)
        FROM WEBERP_StockMoveLines ml
        JOIN WEBERP_Items i ON i.Id=ml.ItemId
        JOIN WEBERP_ItemCategories c ON c.Id=i.CategoryId
        WHERE ml.PickingId=@PickingId AND (ml.IsCanceled=0 OR ml.IsCanceled IS NULL);
    END

    -- outgoing (شحن للعميل): Dr COGS / Cr مخزون
    ELSE IF @LocationType='internal' AND @LocationDestType='customer'
    BEGIN
        SELECT TOP 1 @DebitAccountId  = ISNULL(i.COGSAccountId,      c.COGSAccountId),
                     @CreditAccountId = ISNULL(i.InventoryAccountId,  c.InventoryAccountId)
        FROM WEBERP_StockMoveLines ml
        JOIN WEBERP_Items i ON i.Id=ml.ItemId
        JOIN WEBERP_ItemCategories c ON c.Id=i.CategoryId
        WHERE ml.PickingId=@PickingId AND (ml.IsCanceled=0 OR ml.IsCanceled IS NULL);
    END

    IF @DebitAccountId IS NULL OR @CreditAccountId IS NULL RETURN;

    -- إنشاء القيد
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,
         IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT 'STOCK-'+CAST(@PickingId AS NVARCHAR), CAST(GETDATE() AS DATE),
           'Inventory', 'حركة مخزون - '+Name, @TotalValue, @TotalValue,
           1, GETDATE(), @UserId, @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_StockPickings WHERE Id=@PickingId;

    SET @JId=SCOPE_IDENTITY();

    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@JId,@DebitAccountId, @TotalValue,0, @CompanyID,@BranchID,@UserId,GETDATE()),
        (@JId,@CreditAccountId,0,@TotalValue, @CompanyID,@BranchID,@UserId,GETDATE());

    UPDATE WEBERP_StockPickings SET JournalEntryId=@JId,IsPosted=1 WHERE Id=@PickingId;
END;
```

### SP 5: WEBERP_StockValuation_GetAverageCost — حساب التكلفة المتوسطة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_StockValuation_GetAverageCost]
    @ItemId    INT,
    @CompanyID INT, @BranchID INT,
    @Cost      DECIMAL(18,4) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @Cost = CASE
        WHEN SUM(RemainingQty) > 0
        THEN SUM(RemainingValue) / SUM(RemainingQty)
        ELSE MAX(UnitCost)   -- آخر سعر معروف
    END
    FROM WEBERP_StockValuationLayers
    WHERE ItemId=@ItemId
      AND CompanyID=@CompanyID
      AND Quantity > 0   -- طبقات الدخول فقط
      AND RemainingQty > 0
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @Cost IS NULL SET @Cost = 0;
END;
```

### SP 6: WEBERP_StockFIFO_GetLayers — جلب الطبقات بـ FIFO
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_StockFIFO_GetLayers]
    @ItemId      INT,
    @WarehouseId INT,
    @QtyNeeded   DECIMAL(18,4),
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- يعرض الطبقات بالترتيب الزمني مع الكمية المأخوذة من كل طبقة
    DECLARE @Running DECIMAL(18,4) = @QtyNeeded;

    SELECT TOP 100
        vl.Id AS LayerId,
        vl.LotId,
        l.LotNumber,
        l.PurchaseDate,
        l.ExpiryDate,
        vl.UnitCost,
        vl.RemainingQty,
        CASE
            WHEN vl.RemainingQty >= @Running THEN @Running
            ELSE vl.RemainingQty
        END AS QtyToConsume,
        CASE
            WHEN vl.RemainingQty >= @Running THEN @Running * vl.UnitCost
            ELSE vl.RemainingQty * vl.UnitCost
        END AS CostToConsume
    FROM WEBERP_StockValuationLayers vl
    LEFT JOIN WEBERP_ItemLots l ON l.Id = vl.LotId
    WHERE vl.ItemId = @ItemId
      AND vl.CompanyID = @CompanyID
      AND vl.RemainingQty > 0
      AND (vl.IsCanceled=0 OR vl.IsCanceled IS NULL)
    ORDER BY vl.StockMoveDate ASC, vl.Id ASC;  -- FIFO
END;
```

### SP 7: WEBERP_PhysicalInventory_Validate — تأكيد الجرد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PhysicalInventory_Validate]
    @InventoryId INT,
    @CompanyID   INT, @BranchID INT,
    @UserId      INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- التحقق من أن كل السطور تم فحصها
    IF EXISTS (
        SELECT 1 FROM WEBERP_PhysicalInventoryLines
        WHERE InventoryId=@InventoryId AND IsChecked=0
          AND ActualQty IS NULL
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('يوجد أصناف لم يتم جردها',16,1); RETURN; END

    -- إنشاء حركات تسوية للفروق
    INSERT INTO WEBERP_StockPickings
        (Name, PickingTypeId, PickingDate, LocationId, LocationDestId,
         State, Origin, CompanyID, BranchID, CreatedBy, CreatedAt)
    SELECT
        'INV-ADJ-'+CAST(@InventoryId AS NVARCHAR),
        (SELECT Id FROM WEBERP_StockPickingTypes WHERE PickingTypeCode='internal' AND Code LIKE '%ADJ%' AND CompanyID=@CompanyID),
        GETDATE(),
        CASE WHEN DifferenceQty > 0
             THEN (SELECT Id FROM WEBERP_WarehouseLocations WHERE LocationType='inventory' AND CompanyID=@CompanyID)
             ELSE il.LocationId END,
        CASE WHEN DifferenceQty > 0
             THEN il.LocationId
             ELSE (SELECT Id FROM WEBERP_WarehouseLocations WHERE LocationType='inventory' AND CompanyID=@CompanyID) END,
        'done', 'INV/'+CAST(@InventoryId AS NVARCHAR),
        @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_PhysicalInventoryLines il
    WHERE InventoryId=@InventoryId
      AND DifferenceQty <> 0
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- تحديث الرصيد للسطور ذات الفروق
    UPDATE s SET
        s.Quantity   = il.ActualQty,
        s.TotalValue = il.ActualQty * il.UnitCost,
        s.AverageCost= il.UnitCost,
        s.ModifiedBy=@UserId, s.ModifiedAt=GETDATE()
    FROM WEBERP_ItemStock s
    JOIN WEBERP_PhysicalInventoryLines il ON il.ItemId=s.ItemId AND il.LocationId=s.LocationId
    WHERE il.InventoryId=@InventoryId
      AND il.DifferenceQty <> 0
      AND s.CompanyID=@CompanyID;

    -- تحديث حالة الجرد
    UPDATE WEBERP_PhysicalInventory SET
        State='done', DoneAt=GETDATE(),
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@InventoryId;

    COMMIT;
END;
```

### SP 8: WEBERP_Stock_GetLowStockAlert — تنبيه الحد الأدنى
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Stock_GetLowStockAlert]
    @WarehouseId INT = NULL,
    @CategoryId  INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.Id AS ItemId, i.Code, i.NameAr, i.NameEn,
        i.ReorderLevel, i.ReorderQty, i.LeadTimeDays,
        w.NameAr AS WarehouseName,
        s.Quantity       AS CurrentStock,
        s.ReservedQty,
        s.AvailableQty,
        s.IncomingQty,
        (i.ReorderLevel - ISNULL(s.AvailableQty,0)) AS ShortageQty,
        -- الإجراء المقترح
        CASE
            WHEN ISNULL(s.Quantity,0) = 0                     THEN 'نفاد المخزون'
            WHEN ISNULL(s.AvailableQty,0) <= 0                THEN 'محجوز بالكامل'
            WHEN ISNULL(s.AvailableQty,0) < i.ReorderLevel    THEN 'تحت الحد الأدنى'
        END AS AlertType,
        DATEADD(DAY, i.LeadTimeDays, GETDATE()) AS ExpectedArrival
    FROM WEBERP_Items i
    JOIN WEBERP_ItemStock s ON s.ItemId=i.Id
    JOIN WEBERP_Warehouses w ON w.Id=s.WarehouseId
    WHERE i.CompanyID=@CompanyID AND i.BranchID=@BranchID
      AND (i.IsCanceled=0 OR i.IsCanceled IS NULL)
      AND i.IsActive=1 AND i.ItemType='storable'
      AND ISNULL(s.AvailableQty,0) < i.ReorderLevel
      AND i.ReorderLevel > 0
      AND (@WarehouseId IS NULL OR s.WarehouseId=@WarehouseId)
      AND (@CategoryId  IS NULL OR i.CategoryId=@CategoryId)
    ORDER BY
        CASE WHEN ISNULL(s.Quantity,0)=0 THEN 0 ELSE 1 END,
        (i.ReorderLevel - ISNULL(s.AvailableQty,0)) DESC;
END;
```

### SP 9: WEBERP_Stock_GetExpiryAlert — تنبيه انتهاء الصلاحية
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Stock_GetExpiryAlert]
    @DaysAhead INT = 30,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.Id AS ItemId, i.Code, i.NameAr,
        l.LotNumber, l.ExpiryDate,
        DATEDIFF(DAY, GETDATE(), l.ExpiryDate) AS DaysRemaining,
        l.RemainingQty,
        w.NameAr AS WarehouseName,
        loc.NameAr AS LocationName,
        CASE
            WHEN l.ExpiryDate < GETDATE()                      THEN 'منتهي الصلاحية'
            WHEN DATEDIFF(DAY,GETDATE(),l.ExpiryDate) <= 7     THEN 'حرج (أقل من أسبوع)'
            WHEN DATEDIFF(DAY,GETDATE(),l.ExpiryDate) <= 30    THEN 'تحذير (أقل من شهر)'
            ELSE 'قريب من الانتهاء'
        END AS AlertLevel
    FROM WEBERP_ItemLots l
    JOIN WEBERP_Items i ON i.Id=l.ItemId
    LEFT JOIN WEBERP_Warehouses w ON w.Id=l.WarehouseId
    LEFT JOIN WEBERP_WarehouseLocations loc ON loc.Id=l.LocationId
    WHERE l.ExpiryDate IS NOT NULL
      AND l.RemainingQty > 0
      AND l.ExpiryDate <= DATEADD(DAY, @DaysAhead, GETDATE())
      AND l.IsBlocked=0
      AND l.CompanyID=@CompanyID AND l.BranchID=@BranchID
      AND (l.IsCanceled=0 OR l.IsCanceled IS NULL)
    ORDER BY l.ExpiryDate ASC;
END;
```

---

## 📊 Views

### vw_StockSummary — ملخص المخزون
```sql
CREATE OR ALTER VIEW [dbo].[vw_StockSummary] AS
SELECT
    i.CompanyID, i.BranchID,
    i.Id   AS ItemId,
    i.Code AS ItemCode,
    i.NameAr,
    i.ItemType, i.CostMethod,
    c.NameAr AS CategoryName,
    u.NameAr AS UOMName,
    w.Id   AS WarehouseId,
    w.NameAr AS WarehouseName,
    loc.NameAr AS LocationName,
    ISNULL(s.Quantity,0)    AS Quantity,
    ISNULL(s.ReservedQty,0) AS ReservedQty,
    ISNULL(s.AvailableQty,0) AS AvailableQty,
    ISNULL(s.IncomingQty,0)  AS IncomingQty,
    ISNULL(s.AverageCost,0)  AS AverageCost,
    ISNULL(s.TotalValue,0)   AS TotalValue,
    i.ReorderLevel,
    CASE WHEN ISNULL(s.AvailableQty,0) <= 0           THEN 'out_of_stock'
         WHEN ISNULL(s.AvailableQty,0) < i.ReorderLevel THEN 'low_stock'
         ELSE 'ok'
    END AS StockStatus
FROM WEBERP_Items i
LEFT JOIN WEBERP_ItemCategories c ON c.Id=i.CategoryId
LEFT JOIN WEBERP_UnitOfMeasures u ON u.Id=i.UOMId
LEFT JOIN WEBERP_ItemStock s ON s.ItemId=i.Id
LEFT JOIN WEBERP_Warehouses w ON w.Id=s.WarehouseId
LEFT JOIN WEBERP_WarehouseLocations loc ON loc.Id=s.LocationId
WHERE (i.IsCanceled=0 OR i.IsCanceled IS NULL) AND i.IsActive=1;
```

---

## 🎛️ FormControls الأساسية

### FormCode 510 — الأصناف (HasTabs=1)
```sql
-- Tab 1: البيانات الأساسية
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,TabName,CompanyID,BranchID) VALUES
(1,  510,'Code',             'كود الصنف',         'Code',          1,'nvarchar',1,1,1,150,'الأساسية',1,1),
(2,  510,'NameAr',           'الاسم عربي',         'Name AR',       1,'nvarchar',1,1,1,280,'الأساسية',1,1),
(3,  510,'NameEn',           'الاسم إنجليزي',      'Name EN',       1,'nvarchar',0,1,1,280,'الأساسية',1,1),
(4,  510,'CategoryId',       'الفئة',              'Category',      2,'int',     1,1,2,220,'الأساسية',1,1),
(5,  510,'ItemType',         'النوع',              'Type',          2,'nvarchar',1,0,2,150,'الأساسية',1,1),
(6,  510,'UOMId',            'وحدة القياس',        'UOM',           2,'int',     1,0,2,150,'الأساسية',1,1),
(7,  510,'Barcode',          'الباركود',           'Barcode',       1,'nvarchar',0,1,3,200,'الأساسية',1,1),
(8,  510,'IsActive',         'فعال',               'Active',        3,'bit',     0,0,3,100,'الأساسية',1,1),
-- Tab 2: التكلفة والأسعار
(9,  510,'CostMethod',       'طريقة التكلفة',      'Cost Method',   2,'nvarchar',1,0,1,180,'التكلفة', 1,1),
(10, 510,'StandardCost',     'التكلفة المعيارية',  'Standard Cost', 6,'decimal', 0,0,1,150,'التكلفة', 1,1),
(11, 510,'AverageCost',      'متوسط التكلفة',      'Avg Cost',      6,'decimal', 0,0,1,150,'التكلفة', 1,1),
(12, 510,'SalesPrice',       'سعر البيع',          'Sales Price',   6,'decimal', 0,0,2,150,'التكلفة', 1,1),
(13, 510,'TaxRate',          'نسبة الضريبة',       'Tax Rate',      6,'decimal', 0,0,2,120,'التكلفة', 1,1),
-- Tab 3: المخزون
(14, 510,'ReorderLevel',     'الحد الأدنى',        'Reorder Level', 6,'decimal', 0,0,1,150,'المخزون', 1,1),
(15, 510,'ReorderQty',       'كمية إعادة الطلب',   'Reorder Qty',   6,'decimal', 0,0,1,150,'المخزون', 1,1),
(16, 510,'MaxStockLevel',    'الحد الأقصى',        'Max Stock',     6,'decimal', 0,0,2,150,'المخزون', 1,1),
(17, 510,'LeadTimeDays',     'أيام الانتظار',      'Lead Time',     6,'int',     0,0,2,120,'المخزون', 1,1),
(18, 510,'HasLotTracking',   'تتبع الدفعات',       'Lot Tracking',  3,'bit',     0,0,3,150,'المخزون', 1,1),
(19, 510,'HasExpiryDate',    'تاريخ انتهاء',       'Expiry Date',   3,'bit',     0,0,3,150,'المخزون', 1,1),
(20, 510,'ExpiryAlertDays',  'تنبيه قبل (يوم)',    'Alert Days',    6,'int',     0,0,3,120,'المخزون', 1,1);
```

---

## 🗂️ Menus
```sql
INSERT INTO Menus (MenuCode,MenuNameAr,MenuNameEn,ParentCode,IsParent,FormCode,[Order],IsActive,CompanyID,BranchID) VALUES
(500,'المخازن',               'Inventory',            NULL,1,NULL,5,1,1,1),
-- إعدادات
(501,'الإعدادات',             'Configuration',         500,1,NULL,1,1,1,1),
(502,'المستودعات',            'Warehouses',            501,0,500, 1,1,1,1),
(503,'مواقع المستودع',        'Locations',             501,0,501, 2,1,1,1),
(504,'وحدات القياس',          'Units of Measure',      501,0,503, 3,1,1,1),
(505,'تحويلات الوحدات',       'UOM Conversions',       501,0,504, 4,1,1,1),
(506,'فئات الأصناف',          'Item Categories',       501,0,502, 5,1,1,1),
(507,'أنواع الحركات',         'Operation Types',       501,0,NULL,6,1,1,1),
-- الأصناف
(510,'الأصناف',               'Items',                 500,0,510, 2,1,1,1),
(511,'الدفعات',               'Lots & Serials',        500,0,540, 3,1,1,1),
-- الحركات
(520,'العمليات',              'Operations',            500,1,NULL,4,1,1,1),
(521,'إذن استلام',            'Receipts',              520,0,520, 1,1,1,1),
(522,'إذن صرف',               'Delivery Orders',       520,0,521, 2,1,1,1),
(523,'تحويل داخلي',           'Internal Transfers',    520,0,522, 3,1,1,1),
(524,'تسوية المخزون',         'Adjustments',           520,0,523, 4,1,1,1),
-- التقارير
(530,'التقارير',              'Reporting',             500,1,NULL,5,1,1,1),
(531,'رصيد المخزون',          'Stock Summary',         530,0,NULL,1,1,1,1),
(532,'تقييم المخزون',         'Stock Valuation',       530,0,550, 2,1,1,1),
(533,'حركة الأصناف',          'Stock History',         530,0,NULL,3,1,1,1),
(534,'تنبيهات الحد الأدنى',   'Low Stock Alerts',      530,0,NULL,4,1,1,1),
(535,'تنبيهات انتهاء الصلاحية','Expiry Alerts',        530,0,NULL,5,1,1,1),
(536,'الجرد الفعلي',          'Physical Inventory',    530,0,530, 6,1,1,1);
```

---

## ⚠️ تحذيرات مهمة لـ Claude Code

### نقاط لا تتجاوزها:
1. **ItemType='service'** — لا يُنشئ رصيداً ولا حركات مخزون — فقط فوترة
2. **ItemType='consumable'** — يُصرف مباشرة بدون تتبع رصيد
3. **FIFO** — يعتمد على `StockValuationLayers` لا على `ItemLots` مباشرة
4. **تحديث AverageCost** — دائماً من `StockValuationLayers` لا يدوياً
5. **Locations** — كل حركة من موقع إلى موقع، الموقع الافتراضي للمورد = `SUPPLIERS`
6. **internal → internal** — لا قيد محاسبي (تحويل داخلي فقط)
7. **Backorder** — لو كمية منفذة < مطلوبة: اسأل المستخدم عن إنشاء Backorder
8. **SerialTracking** — كمية = 1 دائماً، LotNumber فريد عالمياً لكل وحدة
9. **IsBlocked** — الـ Lots المحجوزة لا تُصرف ولا تُحسب في المتاح
10. **ReservedQty** — يزيد عند تأكيد أمر البيع، يخفض عند الشحن الفعلي

---

## 🌱 Seed Data الإلزامية

### Locations الافتراضية
```sql
-- يجب إنشاؤها أول ما يتم إنشاء الشركة
DECLARE @CompanyID INT = 1, @BranchID INT = 1, @UserId INT = 1;

INSERT INTO WEBERP_WarehouseLocations
    (Code, NameAr, NameEn, WarehouseId, ParentId, LocationType,
     IsActive, CompanyID, BranchID, CreatedBy, CreatedAt)
VALUES
-- View الجذر
('ALL',           'كل المواقع',        'All Locations',      NULL, NULL, 'view',     1, @CompanyID, @BranchID, @UserId, GETDATE()),

-- مواقع خارجية (خارج المستودع)
('PARTNERS',      'شركاء الأعمال',     'Partners',           NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='ALL' AND CompanyID=@CompanyID),
    'view',     1, @CompanyID, @BranchID, @UserId, GETDATE()),

('SUPPLIERS',     'الموردون',          'Vendors',            NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='PARTNERS' AND CompanyID=@CompanyID),
    'supplier', 1, @CompanyID, @BranchID, @UserId, GETDATE()),

('CUSTOMERS',     'العملاء',           'Customers',          NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='PARTNERS' AND CompanyID=@CompanyID),
    'customer', 1, @CompanyID, @BranchID, @UserId, GETDATE()),

-- مواقع افتراضية
('VIRTUAL',       'مواقع افتراضية',   'Virtual Locations',  NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='ALL' AND CompanyID=@CompanyID),
    'view',     1, @CompanyID, @BranchID, @UserId, GETDATE()),

('INVENTORY',     'تسوية المخزون',    'Inventory Adjustment', NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='VIRTUAL' AND CompanyID=@CompanyID),
    'inventory', 1, @CompanyID, @BranchID, @UserId, GETDATE()),

('SCRAP',         'مهملات',           'Scrap',              NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='VIRTUAL' AND CompanyID=@CompanyID),
    'scrap',    1, @CompanyID, @BranchID, @UserId, GETDATE()),

('TRANSIT',       'عبور',             'Transit',            NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='VIRTUAL' AND CompanyID=@CompanyID),
    'transit',  1, @CompanyID, @BranchID, @UserId, GETDATE()),

('PRODUCTION',    'الإنتاج',          'Production',         NULL,
    (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='VIRTUAL' AND CompanyID=@CompanyID),
    'production', 1, @CompanyID, @BranchID, @UserId, GETDATE());
```

### Warehouse + Locations عند إنشاء مستودع جديد
```sql
-- كل مستودع له 4 مواقع تُنشأ تلقائياً
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Warehouses_Create]
    @Code      NVARCHAR(20),
    @NameAr    NVARCHAR(100),
    @NameEn    NVARCHAR(100) = NULL,
    @CompanyID INT, @BranchID INT, @UserId INT,
    @NewId     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. إنشاء المستودع
    INSERT INTO WEBERP_Warehouses (Code,NameAr,NameEn,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@Code,@NameAr,@NameEn,1,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @NewId = SCOPE_IDENTITY();

    -- 2. إنشاء المواقع الافتراضية
    DECLARE @ParentVirtual INT =
        (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='VIRTUAL' AND CompanyID=@CompanyID);

    -- WH/INPUT ← موقع استلام البضاعة من المورد
    DECLARE @InputId INT;
    INSERT INTO WEBERP_WarehouseLocations
        (Code,NameAr,NameEn,WarehouseId,ParentId,LocationType,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@Code+'/INPUT', @NameAr+' - استلام', @NameEn+' Input',@NewId,NULL,'internal',1,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @InputId = SCOPE_IDENTITY();

    -- WH/STOCK ← موقع المخزون الرئيسي
    DECLARE @StockId INT;
    INSERT INTO WEBERP_WarehouseLocations
        (Code,NameAr,NameEn,WarehouseId,ParentId,LocationType,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@Code+'/STOCK', @NameAr+' - مخزون رئيسي', @NameEn+' Stock',@NewId,NULL,'internal',1,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @StockId = SCOPE_IDENTITY();

    -- WH/OUTPUT ← موقع الشحن للعميل
    DECLARE @OutputId INT;
    INSERT INTO WEBERP_WarehouseLocations
        (Code,NameAr,NameEn,WarehouseId,ParentId,LocationType,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@Code+'/OUTPUT', @NameAr+' - شحن', @NameEn+' Output',@NewId,NULL,'internal',1,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @OutputId = SCOPE_IDENTITY();

    -- WH/PACK ← موقع التعبئة (اختياري)
    DECLARE @PackId INT;
    INSERT INTO WEBERP_WarehouseLocations
        (Code,NameAr,NameEn,WarehouseId,ParentId,LocationType,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@Code+'/PACK', @NameAr+' - تعبئة', @NameEn+' Pack',@NewId,NULL,'internal',1,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @PackId = SCOPE_IDENTITY();

    -- ربط المواقع بالمستودع
    UPDATE WEBERP_Warehouses SET
        InputLocationId  = @InputId,
        StockLocationId  = @StockId,
        OutputLocationId = @OutputId,
        PackLocationId   = @PackId
    WHERE Id = @NewId;

    -- 3. إنشاء أنواع العمليات الافتراضية
    DECLARE @InventoryLocId INT =
        (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='INVENTORY' AND CompanyID=@CompanyID);
    DECLARE @SuppliersLocId INT =
        (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='SUPPLIERS' AND CompanyID=@CompanyID);
    DECLARE @CustomersLocId INT =
        (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='CUSTOMERS' AND CompanyID=@CompanyID);
    DECLARE @ScrapLocId INT =
        (SELECT Id FROM WEBERP_WarehouseLocations WHERE Code='SCRAP' AND CompanyID=@CompanyID);

    INSERT INTO WEBERP_StockPickingTypes
        (Code,NameAr,NameEn,PickingTypeCode,WarehouseId,DefaultLocationId,DefaultLocationDestId,
         SequencePrefix,ShowLots,IsActive,SortOrder,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
    -- استلام من المورد
    (@Code+'-REC',  'استلام - '+@NameAr, 'Receipts',       'incoming', @NewId, @SuppliersLocId, @StockId,    @Code+'/REC/', 1, 1, 1, @CompanyID,@BranchID,@UserId,GETDATE()),
    -- شحن للعميل
    (@Code+'-DEL',  'شحن - '+@NameAr,    'Delivery Orders', 'outgoing', @NewId, @StockId,        @CustomersLocId, @Code+'/DEL/', 1, 1, 2, @CompanyID,@BranchID,@UserId,GETDATE()),
    -- تحويل داخلي
    (@Code+'-INT',  'تحويل - '+@NameAr,  'Internal Transfers','internal',@NewId, @StockId,        @StockId,    @Code+'/INT/', 1, 1, 3, @CompanyID,@BranchID,@UserId,GETDATE()),
    -- تسوية مخزون
    (@Code+'-ADJ',  'جرد - '+@NameAr,    'Adjustments',    'internal', @NewId, @InventoryLocId, @StockId,    @Code+'/ADJ/', 0, 1, 4, @CompanyID,@BranchID,@UserId,GETDATE()),
    -- إتلاف
    (@Code+'-SCRAP','إتلاف - '+@NameAr,  'Scrapping',      'internal', @NewId, @StockId,        @ScrapLocId, @Code+'/SCRAP/',0,1, 5, @CompanyID,@BranchID,@UserId,GETDATE());

    COMMIT;
END;
```

### PickingTypes الافتراضية — Seed
```sql
-- يُستدعى بعد إنشاء أول مستودع
-- EXEC WEBERP_Warehouses_Create 'WH', 'المستودع الرئيسي', 'Main Warehouse', 1, 1, 1, @NewId OUTPUT;
```

### UnitOfMeasures — Seed
```sql
INSERT INTO WEBERP_UnitOfMeasures (Code,NameAr,NameEn,UnitType,Rounding,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('PCS', 'قطعة',       'Piece',      'unit',   1.00,    1, 1, 1, 1, GETDATE()),
('DZN', 'دزينة',      'Dozen',      'unit',   1.00,    1, 1, 1, 1, GETDATE()),
('BOX', 'صندوق',      'Box',        'unit',   1.00,    1, 1, 1, 1, GETDATE()),
('CTN', 'كرتون',      'Carton',     'unit',   1.00,    1, 1, 1, 1, GETDATE()),
('KG',  'كيلوجرام',   'Kilogram',   'weight', 0.001,   1, 1, 1, 1, GETDATE()),
('G',   'جرام',       'Gram',       'weight', 0.001,   1, 1, 1, 1, GETDATE()),
('T',   'طن',         'Ton',        'weight', 0.001,   1, 1, 1, 1, GETDATE()),
('LTR', 'لتر',        'Liter',      'volume', 0.001,   1, 1, 1, 1, GETDATE()),
('ML',  'مليلتر',     'Milliliter', 'volume', 0.001,   1, 1, 1, 1, GETDATE()),
('MTR', 'متر',        'Meter',      'length', 0.01,    1, 1, 1, 1, GETDATE()),
('CM',  'سنتيمتر',    'Centimeter', 'length', 0.01,    1, 1, 1, 1, GETDATE()),
('HRS', 'ساعة',       'Hour',       'time',   0.25,    1, 1, 1, 1, GETDATE()),
('DAY', 'يوم',        'Day',        'time',   1.00,    1, 1, 1, 1, GETDATE()),
('M2',  'متر مربع',   'Sq Meter',   'area',   0.01,    1, 1, 1, 1, GETDATE());

-- تحويلات الوحدات
INSERT INTO WEBERP_UnitConversions (FromUOMId,ToUOMId,Factor,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
SELECT f.Id, t.Id, v.Factor, 1, 1, 1, 1, GETDATE()
FROM (VALUES
    ('DZN','PCS',12),
    ('BOX','PCS',24),
    ('CTN','PCS',48),
    ('CTN','BOX',2),
    ('KG', 'G',  1000),
    ('T',  'KG', 1000),
    ('LTR','ML', 1000),
    ('MTR','CM', 100)
) AS v(F,T,Factor)
JOIN WEBERP_UnitOfMeasures f ON f.Code=v.F AND f.CompanyID=1
JOIN WEBERP_UnitOfMeasures t ON t.Code=v.T AND t.CompanyID=1;
```

---

## 🔄 دورة حياة الحركة (Picking Lifecycle)

```
┌─────────────────────────────────────────────────────────────────┐
│                    دورة حياة حركة المخزون                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│   [Draft]                                                         │
│      │   ← إنشاء الحركة (يدوي أو من PO/SO)                       │
│      ↓                                                            │
│   [Confirmed]                                                     │
│      │   ← تأكيد الحركة                                          │
│      │   ← النظام يتحقق من توفر الكميات                          │
│      ↓                                                            │
│   [Assigned]                      [Waiting]                       │
│      │   ← تم حجز الكميات         │ ← كميات غير كافية             │
│      │                             │   ينتظر التوريد              │
│      ↓                             │                              │
│   [Done] ←─────────────────────────┘                             │
│      │   ← تنفيذ + validate                                       │
│      │   ← تحديث الرصيد                                          │
│      │   ← إنشاء Valuation Layer                                  │
│      │   ← إنشاء القيد المحاسبي                                   │
│      │   ← إنشاء Backorder إن وجد                                 │
│      ↓                                                            │
│   [Backorder?]                                                    │
│      ├── نعم → Picking جديد بالكميات المتبقية                    │
│      └── لا  → إغلاق نهائي                                       │
│                                                                   │
│   [Cancel] ← إلغاء في أي وقت قبل Done                            │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### خطوات Validate بالتفصيل
```
1. التحقق من State ∈ {confirmed, assigned}
2. التحقق أن QtyDone > 0 في سطر واحد على الأقل
3. للـ SerialTracking: QtyDone = 1 بالضبط لكل سطر
4. للـ LotTracking: LotId مطلوب في كل سطر
5. تحديث رصيد المصدر (خصم)
6. تحديث رصيد الهدف (إضافة)
7. تحديث RemainingQty في ItemLots
8. إنشاء StockValuationLayer
9. تحديث AverageCost للصنف في الهدف
10. إنشاء القيد المحاسبي (إذا كانت الحركة تستوجبه)
11. إنشاء Backorder إذا QtyDone < QtyDemand
12. تحديث State = 'done'
```

---

## 🔗 Integration Points — الربط مع الموديولات

### مع موديول المشتريات
```
PurchaseOrder → يُنشئ StockPicking تلقائياً (incoming)
    PickingType: WH-REC
    LocationId:  SUPPLIERS
    LocationDestId: WH/STOCK أو WH/INPUT

عند Validate الـ GRN:
    → يُحدث PurchaseOrderDetails.ReceivedQty
    → ينشئ LotId إذا HasLotTracking=1
    → ينشئ ValuationLayer بتكلفة الفاتورة
    → يُحدث AverageCost
```

### مع موديول المبيعات
```
SalesOrder → يُنشئ StockPicking تلقائياً (outgoing)
    PickingType: WH-DEL
    LocationId:  WH/STOCK
    LocationDestId: CUSTOMERS

عند Validate:
    → يُحدث ReservedQty (يخفض)
    → يخصم الرصيد الفعلي
    → ينشئ ValuationLayer (Quantity سالب)
    → ينشئ قيد: Dr COGS / Cr Inventory
    → يُحدث SalesOrderDetails.ShippedQty
```

### مع موديول نقاط البيع
```
POSTransaction → لا يُنشئ Picking
    بدلاً منه يُحدث ItemStock مباشرة عند الترحيل
    وينشئ ValuationLayer مباشرة
    (أسرع — لا يحتاج workflow)
```

### مع موديول الحسابات العامة
```
كل Validate → WEBERP_StockPickings_CreateJournalEntry
    incoming (Supplier→Internal):  Dr Inventory   / Cr Purchases
    outgoing (Internal→Customer):  Dr COGS         / Cr Inventory
    internal (Internal→Internal):  لا قيد
    adjustment (Inventory→Internal): Dr Inventory  / Cr InventoryAdjustment
    scrap (Internal→Scrap):         Dr Scrap Loss  / Cr Inventory
```

---

## 📊 التقارير الأساسية

### SP 10: WEBERP_Stock_GetValuationReport — تقييم المخزون
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Stock_GetValuationReport]
    @WarehouseId INT = NULL,
    @CategoryId  INT = NULL,
    @AsOfDate    DATE = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @AsOfDate IS NULL SET @AsOfDate = GETDATE();

    SELECT
        i.Id   AS ItemId,
        i.Code AS ItemCode,
        i.NameAr, i.NameEn,
        c.NameAr AS CategoryName,
        u.NameAr AS UOMName,
        i.CostMethod,
        w.NameAr AS WarehouseName,
        -- الكميات
        ISNULL(s.Quantity,    0) AS Quantity,
        ISNULL(s.ReservedQty, 0) AS ReservedQty,
        ISNULL(s.AvailableQty,0) AS AvailableQty,
        -- التكلفة
        ISNULL(s.AverageCost, 0) AS AverageCost,
        ISNULL(s.TotalValue,  0) AS TotalValue,
        i.StandardCost,
        ISNULL(s.Quantity,0) * i.StandardCost AS StandardValue,
        -- الفرق (Variance)
        ISNULL(s.TotalValue,0) - (ISNULL(s.Quantity,0) * i.StandardCost) AS CostVariance,
        -- أسعار البيع والهامش
        i.SalesPrice,
        ISNULL(s.Quantity,0) * i.SalesPrice AS SalesValue,
        CASE WHEN i.SalesPrice > 0
             THEN (i.SalesPrice - ISNULL(s.AverageCost,0)) / i.SalesPrice * 100
             ELSE 0
        END AS MarginPercent
    FROM WEBERP_Items i
    LEFT JOIN WEBERP_ItemCategories c ON c.Id=i.CategoryId
    LEFT JOIN WEBERP_UnitOfMeasures u ON u.Id=i.UOMId
    LEFT JOIN WEBERP_ItemStock s       ON s.ItemId=i.Id
    LEFT JOIN WEBERP_Warehouses w      ON w.Id=s.WarehouseId
    WHERE i.CompanyID=@CompanyID AND i.BranchID=@BranchID
      AND (i.IsCanceled=0 OR i.IsCanceled IS NULL)
      AND i.IsActive=1 AND i.ItemType='storable'
      AND (@WarehouseId IS NULL OR s.WarehouseId=@WarehouseId)
      AND (@CategoryId  IS NULL OR i.CategoryId=@CategoryId)
    ORDER BY c.NameAr, i.Code;
END;
```

### SP 11: WEBERP_Stock_GetMovementHistory — حركة الصنف
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Stock_GetMovementHistory]
    @ItemId      INT,
    @WarehouseId INT = NULL,
    @LotId       INT = NULL,
    @FromDate    DATE = NULL,
    @ToDate      DATE = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id AS PickingId, p.Name AS PickingName,
        p.PickingDate, p.DateDone,
        pt.NameAr AS OperationType,
        pt.PickingTypeCode,
        locFrom.NameAr AS FromLocation,
        locTo.NameAr   AS ToLocation,
        ml.UOMId, u.NameAr AS UOMName,
        ml.QtyDone, ml.QtyDoneBase,
        ml.UnitCost, ml.TotalCost,
        l.LotNumber, l.ExpiryDate,
        -- الإشارة: + دخول / - خروج
        CASE
            WHEN locFrom.LocationType IN ('supplier','customer','inventory','production') THEN '+'
            WHEN locTo.LocationType   IN ('supplier','customer','inventory','scrap')     THEN '-'
            ELSE '↔'
        END AS Direction,
        p.Origin,
        p.SourceDocType, p.SourceDocId
    FROM WEBERP_StockMoveLines ml
    JOIN WEBERP_StockPickings p    ON p.Id  = ml.PickingId
    JOIN WEBERP_StockPickingTypes pt ON pt.Id = p.PickingTypeId
    JOIN WEBERP_WarehouseLocations locFrom ON locFrom.Id = ml.LocationId
    JOIN WEBERP_WarehouseLocations locTo   ON locTo.Id   = ml.LocationDestId
    LEFT JOIN WEBERP_UnitOfMeasures u  ON u.Id  = ml.UOMId
    LEFT JOIN WEBERP_ItemLots l        ON l.Id  = ml.LotId
    WHERE ml.ItemId    = @ItemId
      AND p.State      = 'done'
      AND ml.CompanyID = @CompanyID
      AND (ml.IsCanceled=0 OR ml.IsCanceled IS NULL)
      AND (@WarehouseId IS NULL OR p.LocationId=
           (SELECT StockLocationId FROM WEBERP_Warehouses WHERE Id=@WarehouseId))
      AND (@LotId   IS NULL OR ml.LotId=@LotId)
      AND (@FromDate IS NULL OR p.PickingDate>=@FromDate)
      AND (@ToDate   IS NULL OR p.PickingDate<=@ToDate)
    ORDER BY p.PickingDate DESC, p.Id DESC;
END;
```

### SP 12: WEBERP_Stock_GetFIFOLayers — عرض طبقات التكلفة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Stock_GetFIFOLayers]
    @ItemId      INT,
    @WarehouseId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vl.Id AS LayerId,
        vl.StockMoveDate,
        p.Name  AS PickingName,
        l.LotNumber,
        l.PurchaseDate,
        l.ExpiryDate,
        vl.Quantity      AS ReceivedQty,
        vl.RemainingQty,
        vl.Quantity - vl.RemainingQty AS ConsumedQty,
        vl.UnitCost,
        vl.Value         AS OriginalValue,
        vl.RemainingValue,
        -- نسبة الاستهلاك
        CASE WHEN vl.Quantity > 0
             THEN (vl.Quantity - vl.RemainingQty) / vl.Quantity * 100
             ELSE 100
        END AS ConsumedPercent
    FROM WEBERP_StockValuationLayers vl
    LEFT JOIN WEBERP_StockPickings p ON p.Id = vl.PickingId
    LEFT JOIN WEBERP_ItemLots l      ON l.Id = vl.LotId
    WHERE vl.ItemId    = @ItemId
      AND vl.Quantity  > 0    -- طبقات الدخول فقط
      AND vl.CompanyID = @CompanyID
      AND (vl.IsCanceled=0 OR vl.IsCanceled IS NULL)
    ORDER BY vl.StockMoveDate ASC, vl.Id ASC;
END;
```

---

## 🏷️ FormControls الكاملة

### FormCode 500 — المستودعات
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,CompanyID,BranchID) VALUES
(1, 500, 'Code',          'كود المستودع',  'Code',          1, 'nvarchar', 1, 1, 1, 120, 1, 1),
(2, 500, 'NameAr',        'الاسم عربي',    'Name AR',       1, 'nvarchar', 1, 1, 1, 250, 1, 1),
(3, 500, 'NameEn',        'الاسم إنجليزي', 'Name EN',       1, 'nvarchar', 0, 0, 1, 250, 1, 1),
(4, 500, 'WarehouseType', 'النوع',         'Type',          2, 'nvarchar', 1, 0, 2, 150, 1, 1),
(5, 500, 'ManagerId',     'المسؤول',       'Manager',       2, 'int',      0, 0, 2, 200, 1, 1),
(6, 500, 'Address',       'العنوان',       'Address',       7, 'nvarchar', 0, 0, 3, 400, 1, 1),
(7, 500, 'IsActive',      'فعال',          'Active',        3, 'bit',      0, 0, 4, 100, 1, 1),
(8, 500, 'Notes',         'ملاحظات',       'Notes',         7, 'nvarchar', 0, 0, 4, 400, 1, 1);
```

### FormCode 503 — وحدات القياس
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,CompanyID,BranchID) VALUES
(1, 503, 'Code',     'الكود',       'Code',      1, 'nvarchar', 1, 1, 1, 100, 1, 1),
(2, 503, 'NameAr',   'الاسم عربي',  'Name AR',   1, 'nvarchar', 1, 1, 1, 200, 1, 1),
(3, 503, 'NameEn',   'الاسم إنجليزي','Name EN',  1, 'nvarchar', 0, 0, 1, 200, 1, 1),
(4, 503, 'UnitType', 'النوع',       'Type',      2, 'nvarchar', 1, 0, 2, 150, 1, 1),
(5, 503, 'Rounding', 'دقة التقريب', 'Rounding',  6, 'decimal',  1, 0, 2, 120, 1, 1),
(6, 503, 'IsActive', 'فعال',        'Active',    3, 'bit',      0, 0, 2, 100, 1, 1);
```

### FormCode 540 — الدفعات (Lots)
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,CompanyID,BranchID) VALUES
(1,  540, 'ItemId',       'الصنف',            'Item',           2, 'int',      1, 1, 1, 250, 1, 1),
(2,  540, 'LotNumber',    'رقم الدفعة',        'Lot Number',     1, 'nvarchar', 1, 1, 1, 200, 1, 1),
(3,  540, 'LotType',      'النوع',             'Type',           2, 'nvarchar', 1, 0, 1, 120, 1, 1),
(4,  540, 'PurchaseDate', 'تاريخ الاستلام',    'Purchase Date',  5, 'date',     1, 0, 2, 150, 1, 1),
(5,  540, 'ExpiryDate',   'تاريخ الانتهاء',    'Expiry Date',    5, 'date',     0, 1, 2, 150, 1, 1),
(6,  540, 'ReceivedQty',  'الكمية المستلمة',   'Received Qty',   6, 'decimal',  0, 0, 3, 150, 1, 1),
(7,  540, 'RemainingQty', 'الكمية المتبقية',   'Remaining Qty',  6, 'decimal',  0, 0, 3, 150, 1, 1),
(8,  540, 'UnitCost',     'تكلفة الوحدة',      'Unit Cost',      6, 'decimal',  0, 0, 3, 150, 1, 1),
(9,  540, 'SupplierRef',  'مرجع المورد',        'Supplier Ref',   1, 'nvarchar', 0, 1, 4, 200, 1, 1),
(10, 540, 'IsBlocked',    'محجوز',             'Blocked',        3, 'bit',      0, 0, 4, 100, 1, 1),
(11, 540, 'BlockReason',  'سبب الحجز',         'Block Reason',   1, 'nvarchar', 0, 0, 4, 250, 1, 1);
```

---

## 📐 Constraints & Indexes

```sql
-- Indexes للأداء
CREATE INDEX IX_ItemStock_ItemWarehouse ON WEBERP_ItemStock(ItemId, WarehouseId) INCLUDE(Quantity, AvailableQty, AverageCost);
CREATE INDEX IX_ItemStock_Warehouse ON WEBERP_ItemStock(WarehouseId, CompanyID);
CREATE INDEX IX_ItemLots_ItemWarehouse ON WEBERP_ItemLots(ItemId, WarehouseId, RemainingQty);
CREATE INDEX IX_ItemLots_Expiry ON WEBERP_ItemLots(ExpiryDate) WHERE ExpiryDate IS NOT NULL;
CREATE INDEX IX_StockPickings_State ON WEBERP_StockPickings(State, CompanyID);
CREATE INDEX IX_StockPickings_Date ON WEBERP_StockPickings(PickingDate DESC);
CREATE INDEX IX_StockMoveLines_Picking ON WEBERP_StockMoveLines(PickingId);
CREATE INDEX IX_StockMoveLines_Item ON WEBERP_StockMoveLines(ItemId);
CREATE INDEX IX_StockValuation_Item ON WEBERP_StockValuationLayers(ItemId, StockMoveDate);
CREATE INDEX IX_StockValuation_Remaining ON WEBERP_StockValuationLayers(ItemId, RemainingQty) WHERE RemainingQty > 0;
CREATE INDEX IX_Items_Barcode ON WEBERP_ItemBarcodes(Barcode, CompanyID);
CREATE INDEX IX_Items_Category ON WEBERP_Items(CategoryId, CompanyID);

-- Constraints منطقية
ALTER TABLE WEBERP_ItemLots ADD CONSTRAINT CHK_LotQty CHECK (RemainingQty >= 0);
ALTER TABLE WEBERP_ItemStock ADD CONSTRAINT CHK_StockQty CHECK (Quantity >= -0.001);
-- السماح بفارق بسيط للتقريب
ALTER TABLE WEBERP_UnitConversions ADD CONSTRAINT CHK_ConvFactor CHECK (Factor > 0);
```

---

## 🚦 Business Rules الكاملة

```
┌─────────────────────────────────────────────────────────────────┐
│                      Business Rules                              │
├──────────────────────────┬──────────────────────────────────────┤
│ القاعدة                  │ التطبيق                              │
├──────────────────────────┼──────────────────────────────────────┤
│ لا رصيد سالب             │ CHECK في ItemStock                   │
│                          │ + تحقق في Validate SP                │
├──────────────────────────┼──────────────────────────────────────┤
│ Serial = كمية 1 دائماً   │ CHECK في StockMoveLines              │
│                          │ WHERE HasSerialTracking=1            │
├──────────────────────────┼──────────────────────────────────────┤
│ Lot مطلوب للـ FIFO       │ تحقق في Validate قبل التأكيد        │
│                          │ إذا HasLotTracking=1                 │
├──────────────────────────┼──────────────────────────────────────┤
│ لا صرف من Lot منتهي      │ تحقق ExpiryDate < GETDATE()          │
│                          │ IsBlocked=0                          │
├──────────────────────────┼──────────────────────────────────────┤
│ الكميات المحجوزة         │ ReservedQty يزيد = تأكيد SO          │
│                          │ ReservedQty يخفض = شحن فعلي         │
├──────────────────────────┼──────────────────────────────────────┤
│ تكلفة صفر ممنوعة         │ تحذير (Warning) لا خطأ               │
│ للأصناف المخزنة          │ يُسجل بـ UnitCost=0 مع Log           │
├──────────────────────────┼──────────────────────────────────────┤
│ Backorder تلقائي         │ إذا QtyDone < QtyDemand:             │
│                          │ اسأل المستخدم → أنشئ Picking جديد  │
├──────────────────────────┼──────────────────────────────────────┤
│ Internal Transfer        │ لا قيد محاسبي                        │
│ لا قيد محاسبي            │ فقط تحديث رصيد الموقعين             │
├──────────────────────────┼──────────────────────────────────────┤
│ خدمة لا مخزون           │ ItemType='service':                  │
│                          │ skip كل منطق الرصيد                 │
└──────────────────────────┴──────────────────────────────────────┘
```

---

## ⚠️ تحذيرات لـ Claude Code — ملخص

1. **StockValuationLayers** هو مصدر الحقيقة — لا تعدل `AverageCost` مباشرة في Items
2. **FIFO** يعتمد على `StockMoveDate ASC` في `StockValuationLayers` مش `PurchaseDate`
3. **OPENJSON** لـ AnalyticDistribution يحتاج SQL Server 2016+
4. **Backorder** ينشئ Picking جديد بالكميات المتبقية (QtyDemand - QtyDone)
5. **ReservedQty** مش الـ Quantity — `AvailableQty = Quantity - ReservedQty`
6. **IncomingQty** تتحدث من PurchaseOrders المؤكدة غير المستلمة
7. **Scrap** يحتاج قيد محاسبي: Dr Scrap Loss / Cr Inventory
8. **Adjustment (±)** يحتاج قيد: Dr/Cr Inventory Adjustment Account
9. **UOM Conversion** دايماً عبر SP — لا تحسب Factor يدوياً في الكود
10. **Serial** رقم تسلسلي واحد = وحدة واحدة — مش batch
