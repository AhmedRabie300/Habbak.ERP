# تحليل موديول نقاط البيع (Point of Sale)
## Analysis File for Claude Code — NozomSoft ERP
## نسخة شاملة — مستوى Odoo + تكامل كامل

---

## 🎯 نطاق الموديول

```
إعدادات POS
    ├── تكوين نقطة البيع (Config)
    ├── الطرفيات والأجهزة
    ├── وسائل الدفع
    ├── الطابعات وأجهزة الباركود
    └── المنتجات المتاحة في POS

جلسة الكاشير (Session)
    ├── فتح الجلسة + رصيد افتتاحي
    ├── الفاتورة السريعة
    ├── إدارة العربة (Cart)
    ├── تطبيق الخصم
    ├── وسائل الدفع المتعددة
    ├── الباقي للعميل
    └── إغلاق الجلسة + تسوية

الفواتير والمرتجعات
    ├── فاتورة POS
    ├── مرتجع POS
    └── إعادة طباعة الإيصال

العمل بدون إنترنت (Offline)
    ├── تخزين مؤقت في الجهاز
    └── مزامنة عند الاتصال

التكامل
    ├── المخازن (خصم فوري)
    ├── الحسابات (قيد إقفال الجلسة)
    ├── المبيعات (فاتورة عميل)
    └── النقدية (تحديث رصيد الخزينة)
```

---

## 🔑 FormCodes — نطاق هذا الموديول: 800–899

| FormCode | FormName | FormNameAr | HasTable | HasTabs | ملاحظة |
|----------|----------|------------|----------|---------|--------|
| 800 | POSConfigs | إعدادات نقاط البيع | 1 | 1 | |
| 801 | POSTerminals | الطرفيات | 1 | 0 | |
| 802 | POSPaymentMethods | وسائل الدفع | 1 | 0 | |
| 803 | POSProductCategories | تصنيفات POS | 1 | 1 | شجرة |
| 810 | POSSessions | جلسات الكاشير | 1 | 1 | |
| 820 | POSOrders | فواتير نقاط البيع | 1 | 1 | |
| 821 | POSOrderLines | سطور الفاتورة | 1 | 0 | |
| 822 | POSPayments | مدفوعات الفاتورة | 1 | 0 | |
| 830 | POSReturns | مرتجعات POS | 1 | 0 | |
| 840 | POSCashControl | إقفال الجلسة | 1 | 0 | |

---

## 🗄️ الجداول الكاملة

### 1. WEBERP_POSConfigs — إعداد نقطة البيع
```sql
CREATE TABLE [dbo].[WEBERP_POSConfigs] (
    [Id]                      INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]                    NVARCHAR(20)  NOT NULL,
    [NameAr]                  NVARCHAR(100] NOT NULL,
    [NameEn]                  NVARCHAR(100) NULL,
    -- المستودع والمالية
    [WarehouseId]             INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    [SafeId]                  INT           NULL REFERENCES WEBERP_Safes(Id),
    -- خزينة الكاشير
    [JournalId]               INT           NULL,
    -- دفتر اليومية الخاص بـ POS
    [CashAccountId]           INT           NULL,
    -- حساب النقدية المحاسبي
    [SalesAccountId]          INT           NULL,
    -- حساب الإيراد الافتراضي
    -- العميل الافتراضي
    [DefaultCustomerId]       INT           NULL REFERENCES WEBERP_Customers(Id),
    -- عميل "نقدي" عام
    -- قائمة الأسعار
    [PriceListId]             INT           NULL REFERENCES WEBERP_PriceLists(Id),
    -- الضرائب
    [DefaultTaxRate]          DECIMAL(5,2)  NOT NULL DEFAULT 15,
    -- إعدادات العرض
    [ProductViewMode]         NVARCHAR(20)  NOT NULL DEFAULT 'grid',
    -- grid / list
    [ShowProductImages]       BIT           NOT NULL DEFAULT 1,
    [ShowCategoryFilter]      BIT           NOT NULL DEFAULT 1,
    [ShowSearchBar]           BIT           NOT NULL DEFAULT 1,
    -- إعدادات المبيعات
    [AllowDiscount]           BIT           NOT NULL DEFAULT 1,
    [MaxDiscountPercent]      DECIMAL(5,2)  NOT NULL DEFAULT 100,
    [AllowPriceChange]        BIT           NOT NULL DEFAULT 0,
    [AllowNegativeStock]      BIT           NOT NULL DEFAULT 0,
    [RequireCustomer]         BIT           NOT NULL DEFAULT 0,
    -- إعدادات الدفع
    [AllowPartialPayment]     BIT           NOT NULL DEFAULT 0,
    [AllowCredit]             BIT           NOT NULL DEFAULT 0,
    -- بيع على الحساب
    -- الطباعة
    [AutoPrintReceipt]        BIT           NOT NULL DEFAULT 1,
    [ReceiptHeader]           NVARCHAR(MAX) NULL,
    [ReceiptFooter]           NVARCHAR(MAX) NULL,
    [LogoPath]                NVARCHAR(500) NULL,
    -- إعدادات الإغلاق
    [CloseSessionValidation]  BIT           NOT NULL DEFAULT 1,
    -- هل يتطلب التحقق عند الإغلاق؟
    [CashDifferenceAccountId] INT           NULL,
    -- حساب فوارق الخزينة
    -- إعدادات عامة
    [IsActive]                BIT           NOT NULL DEFAULT 1,
    [Notes]                   NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_POSConfigCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 2. WEBERP_POSTerminals — الطرفيات
```sql
CREATE TABLE [dbo].[WEBERP_POSTerminals] (
    [Id]             INT           IDENTITY(1,1) PRIMARY KEY,
    [POSConfigId]    INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [Code]           NVARCHAR(20)  NOT NULL,
    [NameAr]         NVARCHAR(100] NOT NULL,
    [DeviceId]       NVARCHAR(200) NULL,
    -- معرف فريد للجهاز (MAC address أو UUID)
    [IPAddress]      NVARCHAR(50)  NULL,
    [TerminalType]   NVARCHAR(30)  NOT NULL DEFAULT 'desktop',
    -- desktop / tablet / mobile / kiosk
    [PrinterName]    NVARCHAR(100) NULL,
    [BarcodeScanner] BIT           NOT NULL DEFAULT 0,
    [CardReader]     BIT           NOT NULL DEFAULT 0,
    [CashDrawer]     BIT           NOT NULL DEFAULT 0,
    [IsActive]       BIT           NOT NULL DEFAULT 1,
    [LastSeenAt]     DATETIME2     NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_TerminalCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 3. WEBERP_POSPaymentMethods — وسائل الدفع في POS
```sql
CREATE TABLE [dbo].[WEBERP_POSPaymentMethods] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [POSConfigId]     INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [PaymentMethodId] INT           NOT NULL REFERENCES WEBERP_PaymentMethods(Id),
    -- من جدول PaymentMethods العام
    [IsDefault]       BIT           NOT NULL DEFAULT 0,
    [AllowChange]     BIT           NOT NULL DEFAULT 0,
    -- هل يعطي باقي (change)؟
    [MaxAmount]       DECIMAL(18,4) NULL,
    -- حد أقصى للمبلغ بهذه الوسيلة
    [SortOrder]       INT           NOT NULL DEFAULT 0,
    [IsActive]        BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 4. WEBERP_POSProductCategories — تصنيفات المنتجات في POS
```sql
-- تصنيفات خاصة بشاشة POS (مختلفة عن ItemCategories)
CREATE TABLE [dbo].[WEBERP_POSProductCategories] (
    [Id]        INT           IDENTITY(1,1) PRIMARY KEY,
    [NameAr]    NVARCHAR(100) NOT NULL,
    [NameEn]    NVARCHAR(100) NULL,
    [ParentId]  INT           NULL REFERENCES WEBERP_POSProductCategories(Id),
    [Level]     INT           NOT NULL DEFAULT 1,
    [IsParent]  BIT           NOT NULL DEFAULT 0,
    [IconName]  NVARCHAR(50)  NULL,
    -- اسم الأيقونة
    [ColorHex]  NVARCHAR(10)  NULL,
    -- لون الزر في الشاشة
    [SortOrder] INT           NOT NULL DEFAULT 0,
    [IsActive]  BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);

-- ربط الأصناف بتصنيفات POS
CREATE TABLE [dbo].[WEBERP_POSProductCategoryItems] (
    [Id]            INT IDENTITY(1,1) PRIMARY KEY,
    [POSCategoryId] INT NOT NULL REFERENCES WEBERP_POSProductCategories(Id),
    [ItemId]        INT NOT NULL REFERENCES WEBERP_Items(Id),
    [SortOrder]     INT NOT NULL DEFAULT 0,
    [CompanyID]     INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]     INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]    BIT NOT NULL DEFAULT 0
);
```

### 5. WEBERP_POSSessions — جلسات الكاشير
```sql
CREATE TABLE [dbo].[WEBERP_POSSessions] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [SessionNumber]   NVARCHAR(30)  NOT NULL,
    -- POS/2025/0001
    [POSConfigId]     INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [TerminalId]      INT           NULL REFERENCES WEBERP_POSTerminals(Id),
    [CashierId]       INT           NOT NULL,
    -- FK → Users
    [SessionDate]     DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    [OpenedAt]        DATETIME2     NOT NULL DEFAULT GETDATE(),
    [ClosedAt]        DATETIME2     NULL,
    -- الأرصدة
    [OpeningCash]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- رصيد افتتاح الجلسة
    [ClosingCash]     DECIMAL(18,4) NULL,
    -- الرصيد النقدي المُودَّع عند الإغلاق
    [ExpectedCash]    DECIMAL(18,4) NULL,
    -- الرصيد المتوقع من المبيعات
    [CashDifference]  DECIMAL(18,4) NULL,
    -- = ClosingCash - ExpectedCash
    -- الإجماليات
    [TotalSales]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalReturns]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    [NetSales]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalTax]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalDiscount]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalOrders]     INT           NOT NULL DEFAULT 0,
    [TotalReturnOrders] INT         NOT NULL DEFAULT 0,
    -- الحالة
    [State]           NVARCHAR(20)  NOT NULL DEFAULT 'opening_control',
    -- opening_control ← تحديد رصيد الافتتاح
    -- open            ← الجلسة مفتوحة
    -- closing_control ← تحديد رصيد الإغلاق
    -- closed          ← مغلقة
    -- القيد المحاسبي (عند الإغلاق)
    [JournalEntryId]  INT           NULL,
    [IsPosted]        BIT           NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SessionNumber UNIQUE (SessionNumber, CompanyID, BranchID)
);
```

### 6. WEBERP_POSOrders — فواتير نقاط البيع
```sql
CREATE TABLE [dbo].[WEBERP_POSOrders] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [OrderNumber]      NVARCHAR(30)  NOT NULL,
    -- POS-ORD/2025/00001
    [OrderDate]        DATETIME2     NOT NULL DEFAULT GETDATE(),
    [SessionId]        INT           NOT NULL REFERENCES WEBERP_POSSessions(Id),
    [POSConfigId]      INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [TerminalId]       INT           NULL REFERENCES WEBERP_POSTerminals(Id),
    [CashierId]        INT           NOT NULL,
    -- العميل
    [CustomerId]       INT           NULL REFERENCES WEBERP_Customers(Id),
    [CustomerName]     NVARCHAR(200) NULL,
    -- للعملاء بدون حساب
    [CustomerPhone]    NVARCHAR(50]  NULL,
    -- المستودع
    [WarehouseId]      INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    -- الإجماليات
    [SubTotal]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [DiscountAmount]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [DiscountPercent]  DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [TaxAmount]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [PaidAmount]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ChangeAmount]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الباقي للعميل
    [RoundingAmount]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- فرق التقريب
    -- النوع والحالة
    [OrderType]        NVARCHAR(20)  NOT NULL DEFAULT 'sale',
    -- sale / return / exchange
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / paid / done / cancelled / invoiced
    [IsVoided]         BIT           NOT NULL DEFAULT 0,
    [VoidedBy]         INT           NULL,
    [VoidedAt]         DATETIME2     NULL,
    [VoidReason]       NVARCHAR(200) NULL,
    -- للمرتجع
    [ReturnedOrderId]  INT           NULL REFERENCES WEBERP_POSOrders(Id),
    -- الفاتورة الأصلية للمرتجع
    -- الفوترة الرسمية
    [SalesInvoiceId]   INT           NULL,
    -- FK → WEBERP_SalesInvoices (بعد إصدار فاتورة رسمية)
    [JournalEntryId]   INT           NULL,
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    -- ZATCA
    [QRCode]           NVARCHAR(MAX) NULL,
    [UUID]             NVARCHAR(100] NULL,
    [InvoiceHash]      NVARCHAR(200] NULL,
    [ZATCAStatus]      NVARCHAR(20)  NULL,
    -- pending / reported / cleared / rejected
    -- Offline
    [IsOffline]        BIT           NOT NULL DEFAULT 0,
    -- أُنشئت بدون إنترنت
    [SyncedAt]         DATETIME2     NULL,
    -- وقت المزامنة مع الخادم
    [OfflineId]        NVARCHAR(100) NULL,
    -- ID المؤقت من الجهاز قبل المزامنة
    [Notes]            NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_POSOrderNumber UNIQUE (OrderNumber, CompanyID, BranchID),
    CONSTRAINT CHK_POSOrder_Amounts CHECK (TotalAmount >= 0 AND PaidAmount >= 0)
);
```

### 7. WEBERP_POSOrderLines — سطور فاتورة POS
```sql
CREATE TABLE [dbo].[WEBERP_POSOrderLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [OrderId]         INT           NOT NULL REFERENCES WEBERP_POSOrders(Id),
    [LineNumber]      INT           NOT NULL,
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    -- نسخ البيانات وقت البيع (snapshot)
    [ItemCode]        NVARCHAR(50)  NOT NULL,
    [ItemNameAr]      NVARCHAR(200) NOT NULL,
    [ItemNameEn]      NVARCHAR(200) NULL,
    [Barcode]         NVARCHAR(50)  NULL,
    [UOMId]           INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [UOMName]         NVARCHAR(50)  NOT NULL,
    -- الكميات والأسعار
    [Quantity]        DECIMAL(18,4) NOT NULL,
    [UnitPrice]       DECIMAL(18,4) NOT NULL,
    [DiscountPercent] DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [DiscountAmount]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxRate]         DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [TaxAmount]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [SubTotal]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- التكلفة (لحساب الربح)
    [UnitCost]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CostAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- Lot للتتبع
    [LotId]           INT           NULL REFERENCES WEBERP_ItemLots(Id),
    [LotNumber]       NVARCHAR(50)  NULL,
    -- نسخة من اللوت
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT CHK_POSLine_Qty CHECK (Quantity <> 0)
    -- السالب مسموح للمرتجعات
);
```

### 8. WEBERP_POSPayments — تفاصيل الدفع لكل فاتورة
```sql
CREATE TABLE [dbo].[WEBERP_POSPayments] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [OrderId]         INT           NOT NULL REFERENCES WEBERP_POSOrders(Id),
    [SessionId]       INT           NOT NULL REFERENCES WEBERP_POSSessions(Id),
    [PaymentMethodId] INT           NOT NULL REFERENCES WEBERP_POSPaymentMethods(Id),
    [Amount]          DECIMAL(18,4) NOT NULL,
    [IsChange]        BIT           NOT NULL DEFAULT 0,
    -- هل هذا باقي مُعطى للعميل؟
    [Reference]       NVARCHAR(100) NULL,
    -- رقم عملية البطاقة أو التحويل
    [PaymentDate]     DATETIME2     NOT NULL DEFAULT GETDATE(),
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT CHK_POSPayment_Amount CHECK (Amount >= 0)
);
```

### 9. WEBERP_POSCashControl — جرد الخزينة عند الإغلاق
```sql
CREATE TABLE [dbo].[WEBERP_POSCashControl] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [SessionId]    INT           NOT NULL REFERENCES WEBERP_POSSessions(Id),
    [ControlType]  NVARCHAR(20)  NOT NULL,
    -- opening / closing
    [ControlDate]  DATETIME2     NOT NULL DEFAULT GETDATE(),
    -- إجماليات الجلسة
    [CashIn]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CashOut]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- فئات العملات المعدودة
    [Bill500]      INT           NOT NULL DEFAULT 0,
    [Bill100]      INT           NOT NULL DEFAULT 0,
    [Bill50]       INT           NOT NULL DEFAULT 0,
    [Bill10]       INT           NOT NULL DEFAULT 0,
    [Bill5]        INT           NOT NULL DEFAULT 0,
    [Bill1]        INT           NOT NULL DEFAULT 0,
    [Coin100Hala]  INT           NOT NULL DEFAULT 0,
    [Coin50Hala]   INT           NOT NULL DEFAULT 0,
    [Coin25Hala]   INT           NOT NULL DEFAULT 0,
    [Coin10Hala]   INT           NOT NULL DEFAULT 0,
    [Coin5Hala]    INT           NOT NULL DEFAULT 0,
    [CoinOther]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الإجمالي المعدود
    [TotalCounted] AS (
        Bill500*500 + Bill100*100 + Bill50*50 + Bill10*10 + Bill5*5 + Bill1*1 +
        Coin100Hala*1.0 + Coin50Hala*0.5 + Coin25Hala*0.25 +
        Coin10Hala*0.10 + Coin5Hala*0.05 + CoinOther
    ),
    [Notes]        NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 10. WEBERP_POSPriceTags — بطاقات الأسعار (لقارئ الباركود)
```sql
-- كاش محلي للأسعار — يُحدَّث عند فتح الجلسة
CREATE TABLE [dbo].[WEBERP_POSPriceTags] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [POSConfigId]  INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [ItemId]       INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [Barcode]      NVARCHAR(100] NOT NULL,
    [ItemNameAr]   NVARCHAR(200] NOT NULL,
    [ItemNameEn]   NVARCHAR(200] NULL,
    [UOMId]        INT           NOT NULL,
    [UOMName]      NVARCHAR(50]  NOT NULL,
    [SalesPrice]   DECIMAL(18,4) NOT NULL,
    [TaxRate]      DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [StockQty]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CategoryId]   INT           NULL,
    [ImagePath]    NVARCHAR(500] NULL,
    [IsActive]     BIT           NOT NULL DEFAULT 1,
    [LastUpdated]  DATETIME2     NOT NULL DEFAULT GETDATE(),
    [CompanyID]    INT NOT NULL, [BranchID] INT NOT NULL,
    CONSTRAINT UQ_POSPriceTag UNIQUE (POSConfigId, Barcode, CompanyID, BranchID)
);
```

---

## 🔧 Stored Procedures الكاملة

### SP 1: WEBERP_POSSessions_Open — فتح جلسة الكاشير
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSSessions_Open]
    @POSConfigId  INT,
    @TerminalId   INT = NULL,
    @CashierId    INT,
    @OpeningCash  DECIMAL(18,4) = 0,
    @CompanyID    INT, @BranchID INT,
    @NewSessionId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- التحقق من عدم وجود جلسة مفتوحة لنفس الكاشير في نفس النقطة
    IF EXISTS (
        SELECT 1 FROM WEBERP_POSSessions
        WHERE POSConfigId=@POSConfigId
          AND CashierId=@CashierId
          AND State IN ('opening_control','open','closing_control')
          AND CompanyID=@CompanyID
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN RAISERROR('يوجد جلسة مفتوحة مسبقاً لهذا الكاشير',16,1); RETURN; END

    -- جلب اسم تسلسلي
    DECLARE @SessionNumber NVARCHAR(50);
    EXEC WEBERP_DocumentSequences_GetNext 'POSSession', @CompanyID, @BranchID, @SessionNumber OUTPUT;

    INSERT INTO WEBERP_POSSessions
        (SessionNumber, POSConfigId, TerminalId, CashierId,
         SessionDate, OpenedAt, OpeningCash, State,
         CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES
        (@SessionNumber, @POSConfigId, @TerminalId, @CashierId,
         CAST(GETDATE() AS DATE), GETDATE(), @OpeningCash, 'open',
         @CompanyID, @BranchID, @CashierId, GETDATE());

    SET @NewSessionId = SCOPE_IDENTITY();

    -- تسجيل رصيد الافتتاح
    INSERT INTO WEBERP_POSCashControl
        (SessionId, ControlType, CashIn, CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES (@NewSessionId, 'opening', @OpeningCash, @CompanyID, @BranchID, @CashierId, GETDATE());
END;
```

### SP 2: WEBERP_POSPriceTags_Refresh — تحديث كاش الأسعار
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSPriceTags_Refresh]
    @POSConfigId INT,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @WarehouseId INT, @PriceListId INT;
    SELECT @WarehouseId=WarehouseId, @PriceListId=PriceListId
    FROM WEBERP_POSConfigs WHERE Id=@POSConfigId AND CompanyID=@CompanyID;

    -- حذف الكاش القديم
    DELETE FROM WEBERP_POSPriceTags
    WHERE POSConfigId=@POSConfigId AND CompanyID=@CompanyID;

    -- إعادة بناء الكاش
    INSERT INTO WEBERP_POSPriceTags
        (POSConfigId, ItemId, Barcode, ItemNameAr, ItemNameEn,
         UOMId, UOMName, SalesPrice, TaxRate, StockQty,
         CategoryId, ImagePath, IsActive, LastUpdated, CompanyID, BranchID)
    SELECT
        @POSConfigId,
        i.Id,
        ISNULL(b.Barcode, i.Code),
        -- الباركود الأساسي أو الكود
        i.NameAr, i.NameEn,
        i.UOMId, u.NameAr,
        -- السعر من قائمة الأسعار أو السعر الأساسي
        ISNULL((
            SELECT TOP 1 pli.FixedPrice
            FROM WEBERP_PriceListItems pli
            WHERE pli.PriceListId=@PriceListId AND pli.ItemId=i.Id
              AND (pli.StartDate IS NULL OR pli.StartDate<=GETDATE())
              AND (pli.EndDate   IS NULL OR pli.EndDate  >=GETDATE())
              AND (pli.IsCanceled=0 OR pli.IsCanceled IS NULL)
            ORDER BY pli.MinQty DESC
        ), i.SalesPrice),
        i.TaxRate,
        ISNULL(s.Quantity, 0),
        i.CategoryId,
        i.ImagePath,
        i.IsActive,
        GETDATE(),
        @CompanyID, @BranchID
    FROM WEBERP_Items i
    JOIN WEBERP_UnitOfMeasures u ON u.Id=i.UOMId
    LEFT JOIN WEBERP_ItemBarcodes b ON b.ItemId=i.Id AND b.IsDefault=1
        AND (b.IsCanceled=0 OR b.IsCanceled IS NULL)
    LEFT JOIN WEBERP_ItemStock s ON s.ItemId=i.Id AND s.WarehouseId=@WarehouseId
        AND s.CompanyID=@CompanyID
    WHERE i.CompanyID=@CompanyID AND i.IsActive=1
      AND i.ItemType IN ('storable','consumable','service')
      AND (i.IsCanceled=0 OR i.IsCanceled IS NULL);

    -- إضافة كل الباركودات للصنف الواحد
    INSERT INTO WEBERP_POSPriceTags
        (POSConfigId, ItemId, Barcode, ItemNameAr, ItemNameEn,
         UOMId, UOMName, SalesPrice, TaxRate, StockQty,
         CategoryId, IsActive, LastUpdated, CompanyID, BranchID)
    SELECT
        @POSConfigId, b.ItemId, b.Barcode,
        i.NameAr, i.NameEn, b.UOMId, u.NameAr,
        i.SalesPrice, i.TaxRate,
        ISNULL(s.Quantity,0),
        i.CategoryId, 1, GETDATE(), @CompanyID, @BranchID
    FROM WEBERP_ItemBarcodes b
    JOIN WEBERP_Items i ON i.Id=b.ItemId AND i.CompanyID=@CompanyID
    JOIN WEBERP_UnitOfMeasures u ON u.Id=ISNULL(b.UOMId,i.UOMId)
    LEFT JOIN WEBERP_ItemStock s ON s.ItemId=b.ItemId AND s.WarehouseId=@WarehouseId
        AND s.CompanyID=@CompanyID
    WHERE b.IsDefault=0 -- باركودات إضافية
      AND b.CompanyID=@CompanyID
      AND (b.IsCanceled=0 OR b.IsCanceled IS NULL)
      AND NOT EXISTS (
          SELECT 1 FROM WEBERP_POSPriceTags
          WHERE POSConfigId=@POSConfigId AND Barcode=b.Barcode AND CompanyID=@CompanyID
      );
END;
```

### SP 3: WEBERP_POSOrders_Pay — إتمام الدفع وإغلاق الطلب
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSOrders_Pay]
    @OrderId   INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق
    DECLARE @SessionId INT, @TotalAmount DECIMAL(18,4),
            @PaidAmount DECIMAL(18,4), @ChangeAmount DECIMAL(18,4),
            @WarehouseId INT, @CustomerId INT,
            @POSConfigId INT, @OrderType NVARCHAR(20),
            @OrderNumber NVARCHAR(30), @OrderDate DATETIME2;

    SELECT @SessionId=SessionId, @TotalAmount=TotalAmount,
           @PaidAmount=PaidAmount, @ChangeAmount=ChangeAmount,
           @WarehouseId=WarehouseId, @CustomerId=CustomerId,
           @POSConfigId=POSConfigId, @OrderType=OrderType,
           @OrderNumber=OrderNumber, @OrderDate=OrderDate
    FROM WEBERP_POSOrders
    WHERE Id=@OrderId AND State='draft' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @TotalAmount IS NULL
    BEGIN ROLLBACK; RAISERROR('الطلب غير موجود أو مكتمل',16,1); RETURN; END

    -- التحقق من اكتمال الدفع
    DECLARE @TotalPaid DECIMAL(18,4);
    SELECT @TotalPaid = SUM(CASE WHEN IsChange=0 THEN Amount ELSE 0 END)
    FROM WEBERP_POSPayments
    WHERE OrderId=@OrderId AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @TotalPaid < @TotalAmount - 0.01
    BEGIN
        ROLLBACK;
        RAISERROR('المبلغ المدفوع غير كافٍ — المطلوب: %f | المدفوع: %f',16,1,@TotalAmount,@TotalPaid);
        RETURN;
    END

    -- 2. خصم المخزون فوراً (بدون StockPicking)
    IF @OrderType = 'sale'
    BEGIN
        -- التحقق من توفر المخزون
        IF NOT EXISTS (
            SELECT 1 FROM WEBERP_POSConfigs WHERE Id=@POSConfigId AND AllowNegativeStock=1
        )
        AND EXISTS (
            SELECT 1
            FROM WEBERP_POSOrderLines pol
            JOIN WEBERP_Items i ON i.Id=pol.ItemId AND i.ItemType='storable'
            JOIN WEBERP_ItemStock s ON s.ItemId=pol.ItemId
                AND s.WarehouseId=@WarehouseId AND s.CompanyID=@CompanyID
            WHERE pol.OrderId=@OrderId AND s.AvailableQty < pol.Quantity
              AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
        )
        BEGIN ROLLBACK; RAISERROR('بعض الأصناف غير متوفرة في المخزون',16,1); RETURN; END

        -- خصم المخزون
        UPDATE s SET
            s.Quantity   = s.Quantity   - pol.Quantity,
            s.TotalValue = s.TotalValue - pol.CostAmount,
            s.ModifiedBy=@UserId, s.ModifiedAt=GETDATE()
        FROM WEBERP_ItemStock s
        JOIN WEBERP_POSOrderLines pol ON pol.ItemId=s.ItemId
        JOIN WEBERP_Items i ON i.Id=pol.ItemId AND i.ItemType='storable'
        WHERE pol.OrderId=@OrderId
          AND s.WarehouseId=@WarehouseId AND s.CompanyID=@CompanyID
          AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

        -- تحديث رصيد اللوتات (FIFO)
        UPDATE l SET l.RemainingQty = l.RemainingQty - pol.Quantity,
                     l.ModifiedBy=@UserId, l.ModifiedAt=GETDATE()
        FROM WEBERP_ItemLots l
        JOIN WEBERP_POSOrderLines pol ON pol.LotId=l.Id
        WHERE pol.OrderId=@OrderId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

        -- تسجيل Valuation Layer
        INSERT INTO WEBERP_StockValuationLayers
            (ItemId,LotId,PickingId,MoveLineId,Quantity,UnitCost,Value,
             RemainingQty,RemainingValue,Description,StockMoveDate,CompanyID,BranchID,CreatedBy,CreatedAt)
        SELECT ItemId, LotId, NULL, NULL,
               -Quantity, UnitCost, -CostAmount,  -- سالب = خروج
               0, 0,
               'POS Sale: '+@OrderNumber,
               @OrderDate,
               @CompanyID, @BranchID, @UserId, GETDATE()
        FROM WEBERP_POSOrderLines
        WHERE OrderId=@OrderId AND (IsCanceled=0 OR IsCanceled IS NULL);
    END
    ELSE IF @OrderType = 'return'
    BEGIN
        -- إضافة للمخزون عند المرتجع
        MERGE WEBERP_ItemStock AS target
        USING (
            SELECT ItemId, SUM(ABS(Quantity)) AS Qty, SUM(ABS(CostAmount)) AS Cost
            FROM WEBERP_POSOrderLines
            WHERE OrderId=@OrderId AND (IsCanceled=0 OR IsCanceled IS NULL)
            GROUP BY ItemId
        ) AS src ON target.ItemId=src.ItemId
               AND target.WarehouseId=@WarehouseId
               AND target.CompanyID=@CompanyID
        WHEN MATCHED THEN UPDATE SET
            target.Quantity=target.Quantity+src.Qty,
            target.TotalValue=target.TotalValue+src.Cost,
            target.AverageCost=(target.TotalValue+src.Cost)/NULLIF(target.Quantity+src.Qty,0),
            target.ModifiedBy=@UserId, target.ModifiedAt=GETDATE()
        WHEN NOT MATCHED THEN INSERT
            (ItemId,WarehouseId,Quantity,TotalValue,AverageCost,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (src.ItemId,@WarehouseId,src.Qty,src.Cost,src.Cost/NULLIF(src.Qty,0),@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- 3. تحديث أرصدة وسائل الدفع
    UPDATE safe SET safe.CurrentBalance = safe.CurrentBalance +
        (CASE WHEN @OrderType='sale' THEN 1 ELSE -1 END) *
        (SELECT SUM(CASE WHEN pp.IsChange=0 THEN pp.Amount ELSE -pp.Amount END)
         FROM WEBERP_POSPayments pp
         JOIN WEBERP_POSPaymentMethods ppm ON ppm.Id=pp.PaymentMethodId
         JOIN WEBERP_PaymentMethods pm ON pm.Id=ppm.PaymentMethodId
         WHERE pp.OrderId=@OrderId AND pm.MethodType='cash'
           AND (pp.IsCanceled=0 OR pp.IsCanceled IS NULL))
    FROM WEBERP_Safes safe
    WHERE safe.Id=(SELECT SafeId FROM WEBERP_POSConfigs WHERE Id=@POSConfigId)
      AND safe.CompanyID=@CompanyID;

    -- 4. تحديث إجماليات الجلسة
    UPDATE WEBERP_POSSessions SET
        TotalSales    = TotalSales    + CASE WHEN @OrderType='sale'   THEN @TotalAmount ELSE 0 END,
        TotalReturns  = TotalReturns  + CASE WHEN @OrderType='return' THEN @TotalAmount ELSE 0 END,
        NetSales      = NetSales      + CASE WHEN @OrderType='sale'   THEN @TotalAmount ELSE -@TotalAmount END,
        TotalTax      = TotalTax      + (SELECT ISNULL(SUM(TaxAmount),0) FROM WEBERP_POSOrderLines WHERE OrderId=@OrderId AND (IsCanceled=0 OR IsCanceled IS NULL)),
        TotalDiscount = TotalDiscount + (SELECT ISNULL(SUM(DiscountAmount),0) FROM WEBERP_POSOrderLines WHERE OrderId=@OrderId AND (IsCanceled=0 OR IsCanceled IS NULL)),
        TotalOrders   = TotalOrders   + CASE WHEN @OrderType='sale'   THEN 1 ELSE 0 END,
        TotalReturnOrders = TotalReturnOrders + CASE WHEN @OrderType='return' THEN 1 ELSE 0 END,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@SessionId;

    -- 5. تحديث حالة الطلب
    UPDATE WEBERP_POSOrders SET
        State='paid', IsPosted=1,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@OrderId;

    -- 6. توليد ZATCA QR Code
    EXEC WEBERP_ZATCA_GenerateQR @OrderId, @CompanyID, @BranchID;

    COMMIT;
END;
```

### SP 4: WEBERP_POSSessions_Close — إغلاق الجلسة مع القيد المحاسبي
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSSessions_Close]
    @SessionId    INT,
    @ClosingCash  DECIMAL(18,4),
    @CompanyID    INT, @BranchID INT,
    @UserId       INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @POSConfigId INT, @CashierId INT, @SessionDate DATE,
            @TotalSales DECIMAL(18,4), @TotalReturns DECIMAL(18,4),
            @OpeningCash DECIMAL(18,4), @SessionNumber NVARCHAR(30),
            @SafeId INT, @CashAccountId INT, @SalesAccountId INT,
            @CashDifferenceAccountId INT;

    SELECT @POSConfigId=POSConfigId, @CashierId=CashierId, @SessionDate=SessionDate,
           @TotalSales=TotalSales, @TotalReturns=TotalReturns,
           @OpeningCash=OpeningCash, @SessionNumber=SessionNumber
    FROM WEBERP_POSSessions
    WHERE Id=@SessionId AND State='open' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @SessionNumber IS NULL
    BEGIN ROLLBACK; RAISERROR('الجلسة غير موجودة أو مغلقة',16,1); RETURN; END

    SELECT @SafeId=SafeId, @CashAccountId=CashAccountId,
           @SalesAccountId=SalesAccountId,
           @CashDifferenceAccountId=CashDifferenceAccountId
    FROM WEBERP_POSConfigs WHERE Id=@POSConfigId;

    DECLARE @NetSales       DECIMAL(18,4) = @TotalSales - @TotalReturns;
    DECLARE @ExpectedCash   DECIMAL(18,4) = @OpeningCash + @NetSales;
    DECLARE @CashDifference DECIMAL(18,4) = @ClosingCash - @ExpectedCash;

    -- تسجيل جرد الإغلاق
    INSERT INTO WEBERP_POSCashControl
        (SessionId,ControlType,CashIn,CashOut,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@SessionId,'closing',@ClosingCash,0,@CompanyID,@BranchID,@UserId,GETDATE());

    -- ===== القيد المحاسبي لإغلاق الجلسة =====
    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,
         TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        ('POS-'+@SessionNumber, @SessionDate, 'POSSession',
         'إغلاق جلسة POS: '+@SessionNumber,
         @NetSales + ABS(CASE WHEN @CashDifference<0 THEN @CashDifference ELSE 0 END),
         @NetSales + ABS(CASE WHEN @CashDifference<0 THEN @CashDifference ELSE 0 END),
         1, GETDATE(), @UserId, @CompanyID, @BranchID, @UserId, GETDATE());
    SET @JId=SCOPE_IDENTITY();

    -- مدين: وسائل الدفع
    -- نقدي
    DECLARE @CashTotal DECIMAL(18,4);
    SELECT @CashTotal = SUM(
        CASE WHEN p.IsChange=0 THEN pp.Amount ELSE -pp.Amount END *
        CASE WHEN o.OrderType='return' THEN -1 ELSE 1 END
    )
    FROM WEBERP_POSPayments pp
    JOIN WEBERP_POSPaymentMethods ppm ON ppm.Id=pp.PaymentMethodId
    JOIN WEBERP_PaymentMethods pm ON pm.Id=ppm.PaymentMethodId AND pm.MethodType='cash'
    JOIN WEBERP_POSOrders o ON o.Id=pp.OrderId
    WHERE pp.SessionId=@SessionId AND (pp.IsCanceled=0 OR pp.IsCanceled IS NULL);

    IF ISNULL(@CashTotal,0) <> 0 AND @CashAccountId IS NOT NULL
        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (@JId,@CashAccountId,ISNULL(@CashTotal,0),0,'نقدي POS',@CompanyID,@BranchID,@UserId,GETDATE());

    -- غير نقدي (بطاقات، تحويلات)
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @JId, pm2.AccountId,
           SUM(CASE WHEN o2.OrderType='sale'   THEN pp2.Amount ELSE 0 END),
           SUM(CASE WHEN o2.OrderType='return' THEN pp2.Amount ELSE 0 END),
           pm2.NameAr,
           @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_POSPayments pp2
    JOIN WEBERP_POSPaymentMethods ppm2 ON ppm2.Id=pp2.PaymentMethodId
    JOIN WEBERP_PaymentMethods pm2 ON pm2.Id=ppm2.PaymentMethodId AND pm2.MethodType<>'cash'
    JOIN WEBERP_POSOrders o2 ON o2.Id=pp2.OrderId
    WHERE pp2.SessionId=@SessionId AND pp2.IsChange=0 AND (pp2.IsCanceled=0 OR pp2.IsCanceled IS NULL)
    GROUP BY pm2.AccountId, pm2.NameAr
    HAVING SUM(CASE WHEN o2.OrderType='sale' THEN pp2.Amount ELSE -pp2.Amount END) <> 0;

    -- دائن: الإيراد حسب الحسابات
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @JId, ISNULL(i.SalesAccountId, @SalesAccountId),
           SUM(CASE WHEN o3.OrderType='return' THEN pol.SubTotal ELSE 0 END),
           SUM(CASE WHEN o3.OrderType='sale'   THEN pol.SubTotal ELSE 0 END),
           'إيراد POS', @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_POSOrders o3 ON o3.Id=pol.OrderId AND o3.State='paid'
    JOIN WEBERP_Items i ON i.Id=pol.ItemId
    WHERE o3.SessionId=@SessionId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
    GROUP BY ISNULL(i.SalesAccountId, @SalesAccountId)
    HAVING SUM(CASE WHEN o3.OrderType='sale' THEN pol.SubTotal ELSE -pol.SubTotal END) <> 0;

    -- دائن: ضريبة القيمة المضافة
    DECLARE @TotalTax DECIMAL(18,4);
    SELECT @TotalTax = SUM(pol.TaxAmount * CASE WHEN o4.OrderType='sale' THEN 1 ELSE -1 END)
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_POSOrders o4 ON o4.Id=pol.OrderId AND o4.State='paid'
    WHERE o4.SessionId=@SessionId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

    IF ISNULL(@TotalTax,0) <> 0
    BEGIN
        DECLARE @VATAccountId INT;
        SELECT TOP 1 @VATAccountId=AccountId FROM WEBERP_AccountTaxes
        WHERE TaxType='sale' AND IsActive=1 AND CompanyID=@CompanyID;

        IF @VATAccountId IS NOT NULL
            INSERT INTO WEBERP_JournalEntryDetails
                (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@VATAccountId,0,@TotalTax,'ضريبة POS',@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- قيد فرق الخزينة
    IF ABS(@CashDifference) > 0.001 AND @CashDifferenceAccountId IS NOT NULL
    BEGIN
        IF @CashDifference > 0  -- فائض
            INSERT INTO WEBERP_JournalEntryDetails
                (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@CashDifferenceAccountId,0,@CashDifference,'فائض خزينة POS',@CompanyID,@BranchID,@UserId,GETDATE());
        ELSE  -- عجز
            INSERT INTO WEBERP_JournalEntryDetails
                (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@CashDifferenceAccountId,ABS(@CashDifference),0,'عجز خزينة POS',@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- قيد COGS
    DECLARE @TotalCOGS DECIMAL(18,4);
    SELECT @TotalCOGS = SUM(pol.CostAmount * CASE WHEN o5.OrderType='sale' THEN 1 ELSE -1 END)
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_POSOrders o5 ON o5.Id=pol.OrderId AND o5.State='paid'
    JOIN WEBERP_Items i5 ON i5.Id=pol.ItemId AND i5.ItemType='storable'
    WHERE o5.SessionId=@SessionId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

    IF ISNULL(@TotalCOGS,0) > 0
    BEGIN
        DECLARE @COGSJId INT;
        INSERT INTO WEBERP_JournalEntries
            (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES ('POS-COGS-'+@SessionNumber,@SessionDate,'POSCogs','تكلفة POS: '+@SessionNumber,@TotalCOGS,@TotalCOGS,1,GETDATE(),@UserId,@CompanyID,@BranchID,@UserId,GETDATE());
        SET @COGSJId=SCOPE_IDENTITY();

        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        SELECT @COGSJId, ISNULL(i6.COGSAccountId,cat.COGSAccountId),
               SUM(pol.CostAmount),0,'تكلفة البضاعة POS',@CompanyID,@BranchID,@UserId,GETDATE()
        FROM WEBERP_POSOrderLines pol
        JOIN WEBERP_POSOrders o6 ON o6.Id=pol.OrderId AND o6.State='paid' AND o6.SessionId=@SessionId
        JOIN WEBERP_Items i6 ON i6.Id=pol.ItemId AND i6.ItemType='storable'
        LEFT JOIN WEBERP_ItemCategories cat ON cat.Id=i6.CategoryId
        WHERE (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
        GROUP BY ISNULL(i6.COGSAccountId,cat.COGSAccountId);

        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        SELECT @COGSJId, ISNULL(i7.InventoryAccountId,cat7.InventoryAccountId),
               0,SUM(pol.CostAmount),'مخزون POS',@CompanyID,@BranchID,@UserId,GETDATE()
        FROM WEBERP_POSOrderLines pol
        JOIN WEBERP_POSOrders o7 ON o7.Id=pol.OrderId AND o7.State='paid' AND o7.SessionId=@SessionId
        JOIN WEBERP_Items i7 ON i7.Id=pol.ItemId AND i7.ItemType='storable'
        LEFT JOIN WEBERP_ItemCategories cat7 ON cat7.Id=i7.CategoryId
        WHERE (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
        GROUP BY ISNULL(i7.InventoryAccountId,cat7.InventoryAccountId);
    END

    -- تحديث الجلسة
    UPDATE WEBERP_POSSessions SET
        State='closed', ClosedAt=GETDATE(),
        ClosingCash=@ClosingCash,
        ExpectedCash=@ExpectedCash,
        CashDifference=@CashDifference,
        JournalEntryId=@JId, IsPosted=1,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@SessionId;

    -- تحديث رصيد الخزينة للرصيد الفعلي المُودَع
    IF @SafeId IS NOT NULL
        UPDATE WEBERP_Safes SET CurrentBalance=@ClosingCash,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@SafeId;

    COMMIT;
END;
```

### SP 5: WEBERP_POSOrders_Return — مرتجع POS
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSOrders_Return]
    @OriginalOrderId INT,
    @ReturnLines     NVARCHAR(MAX), -- JSON: [{"ItemId":1,"Quantity":2,"Reason":"defective"}]
    @PaymentMethodId INT,
    -- وسيلة الإعادة للعميل
    @CompanyID       INT, @BranchID INT,
    @UserId          INT,
    @NewOrderId      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @SessionId INT, @POSConfigId INT, @WarehouseId INT;
    SELECT @SessionId=SessionId, @POSConfigId=POSConfigId, @WarehouseId=WarehouseId
    FROM WEBERP_POSOrders
    WHERE Id=@OriginalOrderId AND State='paid' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @SessionId IS NULL
    BEGIN ROLLBACK; RAISERROR('الفاتورة الأصلية غير موجودة أو لم تُدفع',16,1); RETURN; END

    -- إنشاء طلب المرتجع
    DECLARE @ReturnNumber NVARCHAR(50);
    EXEC WEBERP_DocumentSequences_GetNext 'POSReturn', @CompanyID, @BranchID, @ReturnNumber OUTPUT;

    INSERT INTO WEBERP_POSOrders
        (OrderNumber,OrderDate,SessionId,POSConfigId,WarehouseId,
         OrderType,State,ReturnedOrderId,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@ReturnNumber, GETDATE(), @SessionId, @POSConfigId, @WarehouseId,
         'return', 'draft', @OriginalOrderId, @CompanyID, @BranchID, @UserId, GETDATE());
    SET @NewOrderId = SCOPE_IDENTITY();

    -- نسخ السطور من JSON
    INSERT INTO WEBERP_POSOrderLines
        (OrderId,LineNumber,ItemId,ItemCode,ItemNameAr,UOMId,UOMName,
         Quantity,UnitPrice,TaxRate,TaxAmount,SubTotal,TotalAmount,UnitCost,CostAmount,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @NewOrderId, ROW_NUMBER() OVER (ORDER BY orig.LineNumber),
           orig.ItemId, orig.ItemCode, orig.ItemNameAr, orig.UOMId, orig.UOMName,
           -CAST(rl.Quantity AS DECIMAL(18,4)),  -- سالب للمرتجع
           orig.UnitPrice,
           orig.TaxRate,
           -orig.TaxRate * CAST(rl.Quantity AS DECIMAL(18,4)) * orig.UnitPrice / 100,
           -CAST(rl.Quantity AS DECIMAL(18,4)) * orig.UnitPrice,
           -CAST(rl.Quantity AS DECIMAL(18,4)) * orig.UnitPrice * (1 + orig.TaxRate/100),
           orig.UnitCost,
           -CAST(rl.Quantity AS DECIMAL(18,4)) * orig.UnitCost,
           @CompanyID, @BranchID, @UserId, GETDATE()
    FROM OPENJSON(@ReturnLines) WITH (
        ItemId INT '$.ItemId',
        Quantity DECIMAL(18,4) '$.Quantity'
    ) rl
    JOIN WEBERP_POSOrderLines orig ON orig.OrderId=@OriginalOrderId AND orig.ItemId=rl.ItemId
        AND (orig.IsCanceled=0 OR orig.IsCanceled IS NULL);

    -- حساب الإجماليات
    UPDATE WEBERP_POSOrders SET
        TotalAmount    = ABS((SELECT ISNULL(SUM(TotalAmount),0) FROM WEBERP_POSOrderLines WHERE OrderId=@NewOrderId)),
        SubTotal       = ABS((SELECT ISNULL(SUM(SubTotal),0)    FROM WEBERP_POSOrderLines WHERE OrderId=@NewOrderId)),
        TaxAmount      = ABS((SELECT ISNULL(SUM(TaxAmount),0)   FROM WEBERP_POSOrderLines WHERE OrderId=@NewOrderId))
    WHERE Id=@NewOrderId;

    -- إضافة وسيلة الإعادة
    DECLARE @ReturnTotal DECIMAL(18,4);
    SELECT @ReturnTotal=TotalAmount FROM WEBERP_POSOrders WHERE Id=@NewOrderId;

    INSERT INTO WEBERP_POSPayments (OrderId,SessionId,PaymentMethodId,Amount,IsChange,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@NewOrderId, @SessionId, @PaymentMethodId, @ReturnTotal, 0, @CompanyID,@BranchID,@UserId,GETDATE());

    UPDATE WEBERP_POSOrders SET PaidAmount=@ReturnTotal WHERE Id=@NewOrderId;

    -- إتمام المرتجع
    EXEC WEBERP_POSOrders_Pay @NewOrderId, @CompanyID, @BranchID, @UserId;

    COMMIT;
END;
```

### SP 6: WEBERP_ZATCA_GenerateQR — توليد QR Code للفواتير الإلكترونية
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_ZATCA_GenerateQR]
    @OrderId   INT,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ZATCA QR يتكون من TLV encoding لـ 5 حقول:
    -- Tag 1: اسم البائع (NameAr)
    -- Tag 2: الرقم الضريبي (TaxNumber)
    -- Tag 3: تاريخ ووقت الفاتورة
    -- Tag 4: الإجمالي شامل الضريبة
    -- Tag 5: مبلغ الضريبة

    DECLARE @SellerName NVARCHAR(200), @TaxNumber NVARCHAR(50),
            @InvoiceDate NVARCHAR(30), @Total NVARCHAR(20), @TaxAmount NVARCHAR(20);

    SELECT @SellerName=c.ArabicName, @TaxNumber=c.TaxId
    FROM Companies c WHERE c.CompanyId=@CompanyID;

    SELECT @InvoiceDate=FORMAT(OrderDate,'yyyy-MM-ddTHH:mm:ssZ'),
           @Total=CAST(TotalAmount AS NVARCHAR),
           @TaxAmount=CAST(TaxAmount AS NVARCHAR)
    FROM WEBERP_POSOrders WHERE Id=@OrderId;

    -- توليد QR (Base64 of TLV)
    -- هذا مثال مبسط — التطبيق الحقيقي يحتاج TLV encoding كامل
    DECLARE @QRData NVARCHAR(MAX) =
        ISNULL(@SellerName,'') + '|' + ISNULL(@TaxNumber,'') + '|' +
        ISNULL(@InvoiceDate,'') + '|' + ISNULL(@Total,'0') + '|' + ISNULL(@TaxAmount,'0');

    DECLARE @QRBase64 NVARCHAR(MAX) =
        CAST('' AS XML).value('xs:base64Binary(sql:variable("@QRData"))', 'NVARCHAR(MAX)');

    UPDATE WEBERP_POSOrders SET
        QRCode=@QRBase64,
        UUID=LOWER(NEWID()),
        ZATCAStatus='pending'
    WHERE Id=@OrderId;
END;
```

### SP 7: WEBERP_POSOrders_CreateInvoice — إصدار فاتورة رسمية من POS
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSOrders_CreateInvoice]
    @OrderId       INT,
    @CustomerId    INT,
    -- العميل المراد إصدار الفاتورة له
    @CompanyID     INT, @BranchID INT,
    @UserId        INT,
    @NewInvoiceId  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @WarehouseId INT, @TotalAmount DECIMAL(18,4),
            @TaxAmount DECIMAL(18,4), @OrderDate DATETIME2,
            @POSConfigId INT, @OrderNumber NVARCHAR(30);

    SELECT @WarehouseId=WarehouseId, @TotalAmount=TotalAmount,
           @TaxAmount=TaxAmount, @OrderDate=OrderDate,
           @POSConfigId=POSConfigId, @OrderNumber=OrderNumber
    FROM WEBERP_POSOrders
    WHERE Id=@OrderId AND State='paid' AND CompanyID=@CompanyID
      AND SalesInvoiceId IS NULL
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @TotalAmount IS NULL
    BEGIN ROLLBACK; RAISERROR('الطلب غير موجود أو صدرت له فاتورة',16,1); RETURN; END

    -- إنشاء فاتورة مبيعات رسمية
    DECLARE @InvName NVARCHAR(50);
    EXEC WEBERP_DocumentSequences_GetNext 'SalesInvoice', @CompanyID, @BranchID, @InvName OUTPUT;

    INSERT INTO WEBERP_SalesInvoices
        (Name,InvoiceDate,DueDate,CustomerId,WarehouseId,
         CurrencyCode,ExchangeRate,SubTotal,TaxAmount,TotalAmount,TotalAmountLocal,
         InvoiceType,State,PaymentState,IsPosted,PostedDate,PostedBy,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @InvName, CAST(@OrderDate AS DATE), CAST(@OrderDate AS DATE),
           @CustomerId, @WarehouseId, 'SAR', 1,
           SubTotal, TaxAmount, TotalAmount, TotalAmount,
           'out_invoice', 'posted', 'paid', 1, GETDATE(), @UserId,
           @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_POSOrders WHERE Id=@OrderId;

    SET @NewInvoiceId=SCOPE_IDENTITY();

    -- نسخ السطور
    INSERT INTO WEBERP_SalesInvoiceLines
        (InvoiceId,LineNumber,ItemId,Description,AccountId,UOMId,
         Quantity,UnitPrice,DiscountPercent,DiscountAmount,TaxRate,TaxAmount,
         SubTotal,TotalAmount,UnitCost,CostAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @NewInvoiceId, LineNumber, pol.ItemId, pol.ItemNameAr,
           ISNULL(i.SalesAccountId,(SELECT SalesAccountId FROM WEBERP_POSConfigs WHERE Id=@POSConfigId)),
           pol.UOMId, pol.Quantity, pol.UnitPrice,
           pol.DiscountPercent, pol.DiscountAmount, pol.TaxRate, pol.TaxAmount,
           pol.SubTotal, pol.TotalAmount, pol.UnitCost, pol.CostAmount,
           @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_Items i ON i.Id=pol.ItemId
    WHERE pol.OrderId=@OrderId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

    -- إنشاء القيد المحاسبي
    EXEC WEBERP_SalesInvoices_Post @NewInvoiceId, @CompanyID, @BranchID, @UserId;

    -- ربط الفاتورة بطلب POS
    UPDATE WEBERP_POSOrders SET SalesInvoiceId=@NewInvoiceId,
        ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@OrderId;

    COMMIT;
END;
```

### SP 8: WEBERP_POSSessions_GetReport — تقرير الجلسة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSSessions_GetReport]
    @SessionId INT,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ملخص الجلسة
    SELECT s.*,
           u.UserName AS CashierName,
           pc.NameAr  AS POSName
    FROM WEBERP_POSSessions s
    JOIN Users u ON u.Id=s.CashierId
    JOIN WEBERP_POSConfigs pc ON pc.Id=s.POSConfigId
    WHERE s.Id=@SessionId AND s.CompanyID=@CompanyID;

    -- ملخص حسب وسيلة الدفع
    SELECT pm.NameAr AS PaymentMethod, pm.MethodType,
           SUM(CASE WHEN o.OrderType='sale'   THEN pp.Amount ELSE 0 END) AS SalesAmount,
           SUM(CASE WHEN o.OrderType='return' THEN pp.Amount ELSE 0 END) AS ReturnAmount,
           SUM(CASE WHEN o.OrderType='sale'   THEN pp.Amount ELSE -pp.Amount END) AS NetAmount,
           COUNT(DISTINCT o.Id) AS Transactions
    FROM WEBERP_POSPayments pp
    JOIN WEBERP_POSPaymentMethods ppm ON ppm.Id=pp.PaymentMethodId
    JOIN WEBERP_PaymentMethods pm ON pm.Id=ppm.PaymentMethodId
    JOIN WEBERP_POSOrders o ON o.Id=pp.OrderId AND o.State='paid'
    WHERE pp.SessionId=@SessionId AND pp.IsChange=0
      AND (pp.IsCanceled=0 OR pp.IsCanceled IS NULL)
    GROUP BY pm.NameAr, pm.MethodType
    ORDER BY NetAmount DESC;

    -- أكثر الأصناف مبيعاً في الجلسة
    SELECT TOP 10 pol.ItemCode, pol.ItemNameAr,
           SUM(CASE WHEN o.OrderType='sale' THEN pol.Quantity ELSE -pol.Quantity END) AS NetQty,
           SUM(CASE WHEN o.OrderType='sale' THEN pol.TotalAmount ELSE -pol.TotalAmount END) AS NetRevenue
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_POSOrders o ON o.Id=pol.OrderId AND o.State='paid'
    WHERE o.SessionId=@SessionId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
    GROUP BY pol.ItemCode, pol.ItemNameAr
    ORDER BY NetRevenue DESC;
END;
```

---

## 🔄 دورة حياة الجلسة

```
┌──────────────────────────────────────────────────────────────────┐
│                   دورة حياة جلسة POS                             │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  1. [فتح الجلسة]                                                  │
│       EXEC WEBERP_POSSessions_Open                                │
│       ← إدخال رصيد الافتتاح                                      │
│       ← EXEC WEBERP_POSPriceTags_Refresh (تحميل الأصناف)        │
│       State: opening_control → open                               │
│                                                                    │
│  2. [البيع المتكرر]                                               │
│       ── مسح باركود الصنف                                        │
│       ── تطبيق السعر من POSPriceTags                             │
│       ── إضافة للعربة                                             │
│       ── تطبيق الخصم (إذا مسموح)                                 │
│       ── اختيار وسيلة الدفع                                      │
│       ── إدخال المبلغ                                             │
│       ── حساب الباقي                                              │
│       ── EXEC WEBERP_POSOrders_Pay                               │
│           ← خصم مخزون فوري                                       │
│           ← تحديث رصيد الجلسة                                    │
│           ← توليد ZATCA QR                                        │
│           ← طباعة الإيصال                                         │
│                                                                    │
│  3. [مرتجع]                                                       │
│       ── إدخال رقم الفاتورة الأصلية                              │
│       ── اختيار الأصناف والكميات                                  │
│       ── EXEC WEBERP_POSOrders_Return                            │
│           ← إضافة للمخزون                                         │
│           ← إعادة المبلغ للعميل                                   │
│                                                                    │
│  4. [إغلاق الجلسة]                                               │
│       ── عد النقدية الفعلي                                        │
│       ── EXEC WEBERP_POSSessions_Close                           │
│           ← قيد مجمَّع لكل الجلسة                                │
│           ← قيد COGS                                              │
│           ← قيد فرق الخزينة (إن وجد)                             │
│           ← تحديث رصيد الخزينة                                   │
│           State: closing_control → closed                         │
│                                                                    │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🔗 Integration Points التفصيلية

### مع موديول المخازن
```
POSOrders_Pay (sale) →
    يخصم ItemStock.Quantity فوراً
    يخفض LotId.RemainingQty (للـ FIFO)
    ينشئ ValuationLayer (Quantity سالب)
    -- لا StockPicking — مباشر لأداء أفضل

POSOrders_Pay (return) →
    يُضيف للمخزون فوراً (MERGE)
    ينشئ ValuationLayer (Quantity موجب)

POSSessions_Open →
    EXEC WEBERP_POSPriceTags_Refresh
    يقرأ StockQty من ItemStock
    يُحدّث POSPriceTags.StockQty
```

### مع موديول الحسابات العامة
```
POSOrders_Pay →
    لا قيد فوري
    القيود تُجمَّع في نهاية الجلسة

POSSessions_Close →
    قيد الجلسة:
    Dr وسائل الدفع (نقدي + بطاقات...)
    Cr حسابات الإيراد (مجمَّع)
    Cr ضريبة القيمة المضافة
    Dr/Cr فارق الخزينة (إن وجد)

    قيد COGS:
    Dr تكلفة البضاعة المباعة
    Cr حسابات المخزون
```

### مع موديول النقدية والبنوك
```
POSOrders_Pay →
    يُحدّث Safe.CurrentBalance فوراً
    للمبيعات النقدية

POSSessions_Close →
    يُحدّث Safe.CurrentBalance = ClosingCash
    (رصيد فعلي يتغلب على الحسابي)
```

### مع موديول المبيعات
```
POSOrders_CreateInvoice (اختياري) →
    ينشئ SalesInvoice رسمية بناءً على طلب العميل
    يستدعي WEBERP_SalesInvoices_Post
    يُحدّث Customer.CurrentBalance

بدون فاتورة رسمية →
    POSOrder مستقل
    لا ذمم مدينة
    الدفع نقدي/بطاقة فوري
```

### مع مراكز التكلفة
```
POSSessions_Close →
    القيد يُمكن إضافة AnalyticDistribution له
    مثال: فرع POS → CostCenter
```

---

## ⚡ Offline Mode

```
┌──────────────────────────────────────────────────────────────────┐
│                      Offline Support                              │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  عند انقطاع الإنترنت:                                            │
│  ── POSPriceTags مُخزَّن في المتصفح (IndexedDB / localStorage)   │
│  ── الفواتير تُحفظ محلياً بـ OfflineId (UUID مؤقت)              │
│  ── IsOffline = 1                                                  │
│                                                                    │
│  عند عودة الإنترنت:                                               │
│  ── مزامنة الفواتير مع الخادم                                     │
│  ── WEBERP_POSOrders.SyncedAt = GETDATE()                        │
│  ── تحديث OrderNumber من الخادم                                   │
│  ── تحديث المخزون والقيود                                         │
│                                                                    │
│  قاعدة: OfflineId (من الجهاز) → يُربط بـ Id (من الخادم)         │
│         بعد المزامنة                                              │
│                                                                    │
└──────────────────────────────────────────────────────────────────┘
```

### SP 9: WEBERP_POSOrders_SyncOffline — مزامنة الطلبات الأوفلاين
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POSOrders_SyncOffline]
    @OfflineOrdersJson NVARCHAR(MAX),
    -- JSON array من الطلبات المحلية
    @CompanyID         INT, @BranchID INT,
    @UserId            INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- معالجة كل طلب من JSON
    DECLARE @OfflineId NVARCHAR(100), @SessionId INT, @TotalAmount DECIMAL(18,4);

    DECLARE orderCursor CURSOR FOR
    SELECT OfflineId, SessionId, TotalAmount
    FROM OPENJSON(@OfflineOrdersJson) WITH (
        OfflineId   NVARCHAR(100) '$.offlineId',
        SessionId   INT           '$.sessionId',
        TotalAmount DECIMAL(18,4) '$.totalAmount'
    );

    OPEN orderCursor;
    FETCH NEXT FROM orderCursor INTO @OfflineId, @SessionId, @TotalAmount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- تحقق: لم تُزامَن مسبقاً
        IF NOT EXISTS (
            SELECT 1 FROM WEBERP_POSOrders
            WHERE OfflineId=@OfflineId AND CompanyID=@CompanyID
        )
        BEGIN
            -- جلب اسم تسلسلي جديد
            DECLARE @OrderNumber NVARCHAR(50);
            EXEC WEBERP_DocumentSequences_GetNext 'POSOrder', @CompanyID, @BranchID, @OrderNumber OUTPUT;

            -- إدراج الطلب
            -- (في التطبيق الحقيقي: تُدرَج كل بيانات الطلب من JSON)
            UPDATE WEBERP_POSOrders SET
                OrderNumber=@OrderNumber, SyncedAt=GETDATE(), IsOffline=1
            WHERE OfflineId=@OfflineId AND CompanyID=@CompanyID;
        END

        FETCH NEXT FROM orderCursor INTO @OfflineId, @SessionId, @TotalAmount;
    END

    CLOSE orderCursor; DEALLOCATE orderCursor;
    COMMIT;
END;
```

---

## 📊 Views

### vw_POSDailySummary — ملخص يومي
```sql
CREATE OR ALTER VIEW [dbo].[vw_POSDailySummary] AS
SELECT
    o.CompanyID, o.BranchID,
    CAST(o.OrderDate AS DATE)  AS SaleDate,
    o.POSConfigId,
    pc.NameAr                  AS POSName,
    o.CashierId,
    u.UserName                 AS CashierName,
    COUNT(DISTINCT CASE WHEN o.OrderType='sale'   THEN o.Id END) AS TotalSales,
    COUNT(DISTINCT CASE WHEN o.OrderType='return' THEN o.Id END) AS TotalReturns,
    SUM(CASE WHEN o.OrderType='sale'   THEN o.SubTotal      ELSE 0 END) AS GrossSales,
    SUM(CASE WHEN o.OrderType='return' THEN o.SubTotal      ELSE 0 END) AS TotalRefunds,
    SUM(CASE WHEN o.OrderType='sale'   THEN o.DiscountAmount ELSE 0 END) AS TotalDiscounts,
    SUM(CASE WHEN o.OrderType='sale'   THEN o.TaxAmount     ELSE 0 END) AS TotalTax,
    SUM(CASE WHEN o.OrderType='sale'   THEN o.TotalAmount   ELSE -o.TotalAmount END) AS NetRevenue,
    SUM(CASE WHEN o.OrderType='sale'
             THEN (SELECT SUM(pol.CostAmount) FROM WEBERP_POSOrderLines pol
                   WHERE pol.OrderId=o.Id AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL))
             ELSE 0 END) AS TotalCOGS
FROM WEBERP_POSOrders o
JOIN WEBERP_POSConfigs pc ON pc.Id=o.POSConfigId
JOIN Users u ON u.Id=o.CashierId
WHERE o.State='paid' AND (o.IsCanceled=0 OR o.IsCanceled IS NULL)
GROUP BY o.CompanyID, o.BranchID, CAST(o.OrderDate AS DATE),
         o.POSConfigId, pc.NameAr, o.CashierId, u.UserName;
```

---

## 🌱 Seed Data

```sql
-- ترقيم مستندات POS
INSERT INTO WEBERP_DocumentSequences (DocType,Prefix,CurrentNumber,Padding,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('POSSession','POS',0,4,1,1,1,GETDATE()),
('POSOrder',  'POS-ORD',0,5,1,1,1,GETDATE()),
('POSReturn', 'POS-RET',0,4,1,1,1,GETDATE());

-- تصنيفات POS الأساسية
INSERT INTO WEBERP_POSProductCategories (NameAr,NameEn,SortOrder,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('الكل',         'All',       0, 1, 1, 1, 1, GETDATE()),
('عروض اليوم',   'Today''s Deals', 1, 1, 1, 1, 1, GETDATE()),
('الأكثر مبيعاً','Best Sellers',   2, 1, 1, 1, 1, GETDATE());
```

---

## 🎛️ FormControls — إعدادات POS (FormCode 800)

```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,TabName,CompanyID,BranchID) VALUES
-- الأساسية
(1,  800,'Code',                   'الكود',                  'Code',            1,'nvarchar',1,1,1,120,'الأساسية',1,1),
(2,  800,'NameAr',                 'الاسم',                  'Name',            1,'nvarchar',1,1,1,250,'الأساسية',1,1),
(3,  800,'WarehouseId',            'المستودع',               'Warehouse',       2,'int',     1,0,2,200,'الأساسية',1,1),
(4,  800,'SafeId',                 'الخزينة',                'Safe',            2,'int',     0,0,2,200,'الأساسية',1,1),
(5,  800,'PriceListId',            'قائمة الأسعار',          'Price List',      2,'int',     0,0,2,200,'الأساسية',1,1),
(6,  800,'DefaultCustomerId',      'العميل الافتراضي',       'Default Customer',2,'int',     0,0,3,250,'الأساسية',1,1),
(7,  800,'DefaultTaxRate',         'نسبة الضريبة الافتراضية','Default Tax %',   6,'decimal', 1,0,3,120,'الأساسية',1,1),
-- إعدادات البيع
(8,  800,'AllowDiscount',          'السماح بالخصم',          'Allow Discount',  3,'bit',     0,0,1,'البيع',100,1,1),
(9,  800,'MaxDiscountPercent',     'أقصى خصم %',             'Max Discount %',  6,'decimal', 0,0,1,'البيع',120,1,1),
(10, 800,'AllowPriceChange',       'السماح بتغيير السعر',    'Allow Price Chg', 3,'bit',     0,0,1,'البيع',100,1,1),
(11, 800,'AllowNegativeStock',     'السماح بمخزون سالب',     'Neg. Stock',      3,'bit',     0,0,2,'البيع',100,1,1),
(12, 800,'RequireCustomer',        'إلزامية العميل',         'Require Customer',3,'bit',     0,0,2,'البيع',100,1,1),
(13, 800,'AllowCredit',            'البيع بالآجل',           'Allow Credit',    3,'bit',     0,0,2,'البيع',100,1,1),
-- الطباعة
(14, 800,'AutoPrintReceipt',       'طباعة تلقائية',          'Auto Print',      3,'bit',     0,0,1,'الطباعة',100,1,1),
(15, 800,'ReceiptHeader',          'رأس الإيصال',            'Header',          7,'nvarchar',0,0,1,'الطباعة',400,1,1),
(16, 800,'ReceiptFooter',          'تذييل الإيصال',          'Footer',          7,'nvarchar',0,0,2,'الطباعة',400,1,1);
```

---

## 📐 Indexes و Constraints

```sql
CREATE INDEX IX_POSOrders_Session   ON WEBERP_POSOrders(SessionId, State, CompanyID);
CREATE INDEX IX_POSOrders_Date      ON WEBERP_POSOrders(OrderDate DESC, POSConfigId);
CREATE INDEX IX_POSOrders_Customer  ON WEBERP_POSOrders(CustomerId) WHERE CustomerId IS NOT NULL;
CREATE INDEX IX_POSOrders_Offline   ON WEBERP_POSOrders(OfflineId) WHERE IsOffline=1;
CREATE INDEX IX_POSOrderLines_Item  ON WEBERP_POSOrderLines(ItemId, OrderId);
CREATE INDEX IX_POSPayments_Session ON WEBERP_POSPayments(SessionId, IsChange);
CREATE INDEX IX_POSPriceTags_Barcode ON WEBERP_POSPriceTags(Barcode, POSConfigId);
CREATE INDEX IX_POSSessions_Date    ON WEBERP_POSSessions(SessionDate DESC, POSConfigId);
CREATE INDEX IX_POSSessions_Cashier ON WEBERP_POSSessions(CashierId, State);

ALTER TABLE WEBERP_POSOrders ADD CONSTRAINT CHK_POSOrder_Type
CHECK (OrderType IN ('sale','return','exchange'));

ALTER TABLE WEBERP_POSOrders ADD CONSTRAINT CHK_POSOrder_State
CHECK (State IN ('draft','paid','done','cancelled','invoiced'));

ALTER TABLE WEBERP_POSSessions ADD CONSTRAINT CHK_POSSession_State
CHECK (State IN ('opening_control','open','closing_control','closed'));
```

---

## 🗂️ Menus
```sql
INSERT INTO Menus (MenuCode,MenuNameAr,MenuNameEn,ParentCode,IsParent,FormCode,[Order],IsActive,CompanyID,BranchID) VALUES
(800,'نقاط البيع',             'Point of Sale',     NULL,1,NULL, 8,1,1,1),
-- إعدادات
(801,'الإعدادات',              'Configuration',      800,1,NULL,  1,1,1,1),
(802,'إعدادات نقاط البيع',    'POS Config',          801,0,800,   1,1,1,1),
(803,'الطرفيات',              'Terminals',           801,0,801,   2,1,1,1),
(804,'وسائل الدفع',           'Payment Methods',     801,0,802,   3,1,1,1),
(805,'تصنيفات المنتجات',      'Product Categories',  801,0,803,   4,1,1,1),
-- العمليات
(810,'العمليات',              'Operations',          800,1,NULL,  2,1,1,1),
(811,'فتح جلسة',             'Open Session',         810,0,810,   1,1,1,1),
(812,'فواتير POS',           'POS Orders',           810,0,820,   2,1,1,1),
(813,'مرتجعات POS',          'POS Returns',          810,0,830,   3,1,1,1),
(814,'إغلاق الجلسة',         'Close Session',        810,0,840,   4,1,1,1),
-- التقارير
(820,'التقارير',              'Reports',             800,1,NULL,  3,1,1,1),
(821,'ملخص الجلسات',         'Sessions Report',      820,0,NULL,  1,1,1,1),
(822,'مبيعات اليوم',          'Today Sales',          820,0,NULL,  2,1,1,1),
(823,'أداء الكاشيرين',        'Cashier Performance',  820,0,NULL,  3,1,1,1),
(824,'أكثر الأصناف مبيعاً',  'Top Items',            820,0,NULL,  4,1,1,1);
```

---

## ⚠️ تحذيرات لـ Claude Code

```
┌────────────────────────────┬────────────────────────────────────────┐
│ القاعدة                    │ التطبيق                                │
├────────────────────────────┼────────────────────────────────────────┤
│ لا StockPicking في POS     │ المخزون يُخصم مباشرة بدون workflow    │
│                            │ لأداء أسرع في نقطة البيع             │
├────────────────────────────┼────────────────────────────────────────┤
│ القيد مجمَّع               │ لا قيد فوري لكل فاتورة               │
│                            │ القيد ينشأ عند إغلاق الجلسة          │
├────────────────────────────┼────────────────────────────────────────┤
│ UnitCost وقت البيع         │ يُجلب من ItemStock.AverageCost        │
│                            │ وليس بعد الجلسة                      │
├────────────────────────────┼────────────────────────────────────────┤
│ ZATCA                      │ كل فاتورة = QRCode + UUID + Hash      │
│                            │ توليد فوري بعد الدفع                 │
├────────────────────────────┼────────────────────────────────────────┤
│ Offline                    │ IsOffline=1 + OfflineId               │
│                            │ المزامنة تُعطي OrderNumber الرسمي    │
├────────────────────────────┼────────────────────────────────────────┤
│ الباقي (Change)            │ IsChange=1 في POSPayments             │
│                            │ لا يُحسَب في TotalPaid               │
├────────────────────────────┼────────────────────────────────────────┤
│ POSPriceTags               │ يُحدَّث عند فتح كل جلسة              │
│                            │ يشمل رصيد المخزون الحالي             │
├────────────────────────────┼────────────────────────────────────────┤
│ فاتورة رسمية               │ اختيارية — للعملاء الذين يطلبونها    │
│                            │ تُنشئ SalesInvoice مرتبطة بـ POSOrder│
├────────────────────────────┼────────────────────────────────────────┤
│ إغلاق الجلسة يتغلب         │ Safe.CurrentBalance = ClosingCash     │
│                            │ (الرصيد الفعلي يتجاوز الحسابي)       │
└────────────────────────────┴────────────────────────────────────────┘
```

---

## 📊 SPs التقارير الإضافية

### SP 10: WEBERP_POS_CashierPerformance — أداء الكاشيرين
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_CashierPerformance]
    @FromDate  DATE,
    @ToDate    DATE,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.CashierId,
        u.UserName        AS CashierName,
        pc.NameAr         AS POSName,
        COUNT(DISTINCT s.Id)                          AS TotalSessions,
        COUNT(DISTINCT CASE WHEN o.OrderType='sale'   THEN o.Id END) AS TotalOrders,
        COUNT(DISTINCT CASE WHEN o.OrderType='return' THEN o.Id END) AS TotalReturns,
        SUM(CASE WHEN o.OrderType='sale'   THEN o.TotalAmount ELSE 0 END) AS TotalSales,
        SUM(CASE WHEN o.OrderType='return' THEN o.TotalAmount ELSE 0 END) AS TotalRefunds,
        SUM(CASE WHEN o.OrderType='sale'   THEN o.TotalAmount
                 WHEN o.OrderType='return' THEN -o.TotalAmount
                 ELSE 0 END)                          AS NetSales,
        SUM(CASE WHEN o.OrderType='sale'   THEN o.DiscountAmount ELSE 0 END) AS TotalDiscounts,
        SUM(CASE WHEN o.OrderType='sale'   THEN o.TaxAmount      ELSE 0 END) AS TotalTax,
        -- متوسط قيمة الفاتورة
        AVG(CASE WHEN o.OrderType='sale'   THEN o.TotalAmount    ELSE NULL END) AS AvgOrderValue,
        -- أسرع وقت بين الفواتير (الكفاءة)
        AVG(DATEDIFF(SECOND,
            LAG(o.OrderDate) OVER (PARTITION BY o.CashierId ORDER BY o.OrderDate),
            o.OrderDate))                             AS AvgSecsBetweenOrders,
        -- نسبة المرتجعات
        CASE WHEN COUNT(DISTINCT CASE WHEN o.OrderType='sale' THEN o.Id END) > 0
             THEN COUNT(DISTINCT CASE WHEN o.OrderType='return' THEN o.Id END) * 100.0
                / COUNT(DISTINCT CASE WHEN o.OrderType='sale'   THEN o.Id END)
             ELSE 0
        END                                           AS ReturnRate
    FROM WEBERP_POSOrders o
    JOIN Users u ON u.Id=o.CashierId
    JOIN WEBERP_POSConfigs pc ON pc.Id=o.POSConfigId
    JOIN WEBERP_POSSessions s ON s.Id=o.SessionId
    WHERE o.CompanyID=@CompanyID AND o.BranchID=@BranchID
      AND o.State='paid'
      AND CAST(o.OrderDate AS DATE) BETWEEN @FromDate AND @ToDate
      AND (o.IsCanceled=0 OR o.IsCanceled IS NULL)
    GROUP BY o.CashierId, u.UserName, pc.NameAr
    ORDER BY NetSales DESC;
END;
```

### SP 11: WEBERP_POS_HourlySales — توزيع المبيعات بالساعة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_HourlySales]
    @FromDate    DATE,
    @ToDate      DATE,
    @POSConfigId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DATEPART(HOUR, o.OrderDate) AS HourOfDay,
        -- ترجمة الساعة لنص مقروء
        RIGHT('0' + CAST(DATEPART(HOUR,o.OrderDate) AS NVARCHAR),2)+':00' AS HourLabel,
        COUNT(o.Id)                 AS OrderCount,
        SUM(o.TotalAmount)          AS Revenue,
        AVG(o.TotalAmount)          AS AvgOrderValue,
        SUM(o.TaxAmount)            AS TaxAmount,
        SUM(o.DiscountAmount)       AS DiscountAmount,
        -- عدد العملاء الفريدين
        COUNT(DISTINCT o.CustomerId) AS UniqueCustomers
    FROM WEBERP_POSOrders o
    WHERE o.CompanyID=@CompanyID AND o.BranchID=@BranchID
      AND o.State='paid' AND o.OrderType='sale'
      AND CAST(o.OrderDate AS DATE) BETWEEN @FromDate AND @ToDate
      AND (@POSConfigId IS NULL OR o.POSConfigId=@POSConfigId)
      AND (o.IsCanceled=0 OR o.IsCanceled IS NULL)
    GROUP BY DATEPART(HOUR,o.OrderDate)
    ORDER BY HourOfDay;
END;
```

### SP 12: WEBERP_POS_PaymentMethodSummary — ملخص وسائل الدفع
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_PaymentMethodSummary]
    @FromDate    DATE,
    @ToDate      DATE,
    @POSConfigId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pm.Id   AS PaymentMethodId,
        pm.NameAr AS PaymentMethod,
        pm.MethodType,
        COUNT(DISTINCT pp.OrderId)                    AS Transactions,
        SUM(CASE WHEN o.OrderType='sale'   THEN pp.Amount ELSE 0 END) AS TotalReceived,
        SUM(CASE WHEN o.OrderType='return' THEN pp.Amount ELSE 0 END) AS TotalRefunded,
        SUM(CASE WHEN pp.IsChange=1        THEN pp.Amount ELSE 0 END) AS TotalChange,
        SUM(CASE WHEN o.OrderType='sale'   THEN pp.Amount
                 WHEN o.OrderType='return' THEN -pp.Amount ELSE 0 END)
          - SUM(CASE WHEN pp.IsChange=1 THEN pp.Amount ELSE 0 END)    AS NetAmount,
        -- نسبة من الإجمالي
        SUM(CASE WHEN o.OrderType='sale' THEN pp.Amount ELSE 0 END) * 100.0
        / NULLIF(SUM(SUM(CASE WHEN o.OrderType='sale' THEN pp.Amount ELSE 0 END)) OVER(),0)
                                                      AS PercentOfTotal
    FROM WEBERP_POSPayments pp
    JOIN WEBERP_POSPaymentMethods ppm ON ppm.Id=pp.PaymentMethodId
    JOIN WEBERP_PaymentMethods pm ON pm.Id=ppm.PaymentMethodId
    JOIN WEBERP_POSOrders o ON o.Id=pp.OrderId AND o.State='paid'
    WHERE pp.CompanyID=@CompanyID AND pp.BranchID=@BranchID
      AND CAST(o.OrderDate AS DATE) BETWEEN @FromDate AND @ToDate
      AND (@POSConfigId IS NULL OR o.POSConfigId=@POSConfigId)
      AND (pp.IsCanceled=0 OR pp.IsCanceled IS NULL)
    GROUP BY pm.Id, pm.NameAr, pm.MethodType
    ORDER BY NetAmount DESC;
END;
```

### SP 13: WEBERP_POS_TopItems — أكثر الأصناف مبيعاً في POS
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_TopItems]
    @FromDate    DATE,
    @ToDate      DATE,
    @Top         INT = 20,
    @POSConfigId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        pol.ItemId,
        pol.ItemCode,
        pol.ItemNameAr,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.Quantity ELSE 0 END) AS TotalSold,
        SUM(CASE WHEN o.OrderType='return' THEN pol.Quantity ELSE 0 END) AS TotalReturned,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.Quantity
                 WHEN o.OrderType='return' THEN -pol.Quantity ELSE 0 END) AS NetQty,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.SubTotal ELSE 0 END) AS GrossRevenue,
        SUM(CASE WHEN o.OrderType='return' THEN pol.SubTotal ELSE 0 END) AS ReturnValue,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.SubTotal
                 WHEN o.OrderType='return' THEN -pol.SubTotal ELSE 0 END) AS NetRevenue,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.CostAmount ELSE 0 END) AS TotalCost,
        SUM(CASE WHEN o.OrderType='sale'   THEN pol.SubTotal - pol.CostAmount ELSE 0 END) AS GrossProfit,
        CASE WHEN SUM(CASE WHEN o.OrderType='sale' THEN pol.SubTotal ELSE 0 END) > 0
             THEN SUM(CASE WHEN o.OrderType='sale' THEN pol.SubTotal - pol.CostAmount ELSE 0 END) * 100.0
                / SUM(CASE WHEN o.OrderType='sale' THEN pol.SubTotal ELSE 0 END)
             ELSE 0
        END                                           AS MarginPercent,
        AVG(pol.UnitPrice)                            AS AvgSellingPrice,
        SUM(pol.DiscountAmount)                       AS TotalDiscounts,
        COUNT(DISTINCT o.Id)                          AS TransactionCount
    FROM WEBERP_POSOrderLines pol
    JOIN WEBERP_POSOrders o ON o.Id=pol.OrderId AND o.State='paid'
    WHERE pol.CompanyID=@CompanyID AND pol.BranchID=@BranchID
      AND CAST(o.OrderDate AS DATE) BETWEEN @FromDate AND @ToDate
      AND (@POSConfigId IS NULL OR o.POSConfigId=@POSConfigId)
      AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
    GROUP BY pol.ItemId, pol.ItemCode, pol.ItemNameAr
    ORDER BY NetRevenue DESC;
END;
```

### SP 14: WEBERP_POS_SessionsComparison — مقارنة الجلسات
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_SessionsComparison]
    @FromDate    DATE,
    @ToDate      DATE,
    @POSConfigId INT = NULL,
    @CompanyID   INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Id AS SessionId,
        s.SessionNumber,
        s.SessionDate,
        pc.NameAr AS POSName,
        u.UserName AS CashierName,
        -- التوقيت
        s.OpenedAt,
        s.ClosedAt,
        DATEDIFF(MINUTE, s.OpenedAt, ISNULL(s.ClosedAt, GETDATE())) AS SessionMinutes,
        -- الأداء
        s.TotalOrders,
        s.TotalReturnOrders,
        s.TotalSales,
        s.TotalReturns,
        s.NetSales,
        s.TotalTax,
        s.TotalDiscount,
        -- الخزينة
        s.OpeningCash,
        s.ClosingCash,
        s.ExpectedCash,
        s.CashDifference,
        -- معدل البيع بالدقيقة
        CASE WHEN DATEDIFF(MINUTE, s.OpenedAt, ISNULL(s.ClosedAt, GETDATE())) > 0
             THEN s.TotalSales / DATEDIFF(MINUTE, s.OpenedAt, ISNULL(s.ClosedAt, GETDATE()))
             ELSE 0
        END AS SalesPerMinute,
        -- متوسط قيمة الطلب
        CASE WHEN s.TotalOrders > 0
             THEN s.TotalSales / s.TotalOrders
             ELSE 0
        END AS AvgOrderValue
    FROM WEBERP_POSSessions s
    JOIN WEBERP_POSConfigs pc ON pc.Id=s.POSConfigId
    JOIN Users u ON u.Id=s.CashierId
    WHERE s.CompanyID=@CompanyID AND s.BranchID=@BranchID
      AND s.SessionDate BETWEEN @FromDate AND @ToDate
      AND (@POSConfigId IS NULL OR s.POSConfigId=@POSConfigId)
      AND (s.IsCanceled=0 OR s.IsCanceled IS NULL)
    ORDER BY s.SessionDate DESC, s.OpenedAt DESC;
END;
```

### SP 15: WEBERP_POS_CustomerStatement — كشف حساب عميل POS
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_POS_CustomerStatement]
    @CustomerId INT,
    @FromDate   DATE = NULL,
    @ToDate     DATE = NULL,
    @CompanyID  INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OrderNumber   AS DocNumber,
        o.OrderDate     AS DocDate,
        o.OrderType,
        CASE o.OrderType
            WHEN 'sale'   THEN o.TotalAmount
            ELSE 0
        END              AS Debit,
        CASE o.OrderType
            WHEN 'return' THEN o.TotalAmount
            ELSE 0
        END              AS Credit,
        o.TotalAmount,
        o.PaidAmount,
        o.State,
        -- تفاصيل وسيلة الدفع
        (SELECT STRING_AGG(pm.NameAr+': '+CAST(pp.Amount AS NVARCHAR), ' | ')
         FROM WEBERP_POSPayments pp
         JOIN WEBERP_POSPaymentMethods ppm ON ppm.Id=pp.PaymentMethodId
         JOIN WEBERP_PaymentMethods pm ON pm.Id=ppm.PaymentMethodId
         WHERE pp.OrderId=o.Id AND pp.IsChange=0
           AND (pp.IsCanceled=0 OR pp.IsCanceled IS NULL))  AS PaymentDetail,
        pc.NameAr AS POSName,
        u.UserName AS CashierName,
        -- رصيد متراكم
        SUM(CASE WHEN o.OrderType='sale' THEN o.TotalAmount ELSE -o.TotalAmount END)
            OVER (ORDER BY o.OrderDate, o.Id)               AS RunningBalance
    FROM WEBERP_POSOrders o
    JOIN WEBERP_POSConfigs pc ON pc.Id=o.POSConfigId
    JOIN Users u ON u.Id=o.CashierId
    WHERE o.CustomerId=@CustomerId
      AND o.CompanyID=@CompanyID AND o.BranchID=@BranchID
      AND o.State='paid'
      AND (@FromDate IS NULL OR CAST(o.OrderDate AS DATE) >= @FromDate)
      AND (@ToDate   IS NULL OR CAST(o.OrderDate AS DATE) <= @ToDate)
      AND (o.IsCanceled=0 OR o.IsCanceled IS NULL)
    ORDER BY o.OrderDate, o.Id;
END;
```

---

## 🗄️ جداول إضافية ناقصة

### WEBERP_POSProductConfig — الأصناف المتاحة في كل POS
```sql
-- تحديد أي الأصناف تظهر في شاشة POS
CREATE TABLE [dbo].[WEBERP_POSProductConfig] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [POSConfigId]  INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [ItemId]       INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [SortOrder]    INT           NOT NULL DEFAULT 0,
    [FavoriteBtn]  BIT           NOT NULL DEFAULT 0,
    -- يظهر كزر سريع في الشاشة
    [CustomPrice]  DECIMAL(18,4) NULL,
    -- سعر خاص بهذه النقطة (يتجاوز قائمة الأسعار)
    [IsActive]     BIT           NOT NULL DEFAULT 1,
    [CompanyID]    INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_POSProduct UNIQUE (POSConfigId, ItemId, CompanyID, BranchID)
);
```

### WEBERP_POSDiscountCodes — أكواد الخصم
```sql
CREATE TABLE [dbo].[WEBERP_POSDiscountCodes] (
    [Id]             INT           IDENTITY(1,1) PRIMARY KEY,
    [POSConfigId]    INT           NOT NULL REFERENCES WEBERP_POSConfigs(Id),
    [Code]           NVARCHAR(50)  NOT NULL,
    [DiscountType]   NVARCHAR(20)  NOT NULL DEFAULT 'percent',
    -- percent / fixed
    [DiscountValue]  DECIMAL(10,4) NOT NULL,
    [MaxUsageCount]  INT           NULL,
    -- NULL = بلا حد
    [UsageCount]     INT           NOT NULL DEFAULT 0,
    [StartDate]      DATE          NULL,
    [EndDate]        DATE          NULL,
    [MinOrderAmount] DECIMAL(18,4) NOT NULL DEFAULT 0,
    [IsActive]       BIT           NOT NULL DEFAULT 1,
    [CompanyID]      INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]      INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]     INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]     BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_DiscountCode UNIQUE (POSConfigId, Code, CompanyID, BranchID)
);
```

### WEBERP_POSCashMovements — حركات النقدية خلال الجلسة
```sql
-- لتسجيل إيداعات وسحوبات أثناء الجلسة
CREATE TABLE [dbo].[WEBERP_POSCashMovements] (
    [Id]            INT           IDENTITY(1,1) PRIMARY KEY,
    [SessionId]     INT           NOT NULL REFERENCES WEBERP_POSSessions(Id),
    [MovementType]  NVARCHAR(20)  NOT NULL,
    -- cash_in  ← إضافة نقدية للخزينة
    -- cash_out ← سحب نقدية من الخزينة
    [Amount]        DECIMAL(18,4) NOT NULL,
    [Reason]        NVARCHAR(200] NULL,
    [ApprovedBy]    INT           NULL,
    [MovementDate]  DATETIME2     NOT NULL DEFAULT GETDATE(),
    [CompanyID]     INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]     INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]    BIT NOT NULL DEFAULT 0,
    CONSTRAINT CHK_CashMovement_Amount CHECK (Amount > 0)
);
```

---

## 🏗️ مثال عملي كامل — من فتح الجلسة حتى الإغلاق

```
═══════════════════════════════════════════════════════════
 نقطة البيع: صيدلية الشفاء — فرع الرياض — كاشير: أحمد
═══════════════════════════════════════════════════════════

1. الكاشير يفتح الجلسة الصباحية
   ─────────────────────────────
   EXEC WEBERP_POSSessions_Open
       @POSConfigId=1, @CashierId=5, @OpeningCash=500.00
   → يتحمّل كاش الأصناف والأسعار (POSPriceTags_Refresh)
   → Session #POS/2025/0001 مفتوحة

2. عميل يشتري (كاش)
   ─────────────────
   مسح باركود: 6931668412261 → باراسيتامول 500mg
   الكمية: 3 علب × 15 ريال = 45 ريال
   ضريبة 15%: 6.75 ريال
   الإجمالي: 51.75 ريال
   دفع: 100 ريال نقداً
   الباقي: 48.25 ريال

   EXEC WEBERP_POSOrders_Pay @OrderId=1
   → ItemStock.Quantity -= 3
   → Session.TotalSales += 51.75
   → Safe.CurrentBalance += 51.75
   → QR Code ZATCA تلقائي
   → طباعة إيصال

3. عميل آخر يدفع بمدى + نقد
   ──────────────────────────
   إجمالي: 200 ريال
   مدى: 150 ريال
   نقد: 50 ريال
   → POSPayments: صفين (مدى + نقد)
   → Safe.CurrentBalance += 50

4. مرتجع (علبة تالفة)
   ───────────────────
   EXEC WEBERP_POSOrders_Return
       @OriginalOrderId=1, @Qty=1, @PaymentMethodId=CASH
   → ItemStock.Quantity += 1
   → Session.TotalReturns += 17.25 (15 + ضريبة)
   → Safe.CurrentBalance -= 17.25

5. إيداع نقدية إضافية
   ────────────────────
   INSERT INTO WEBERP_POSCashMovements (cash_in, 200, 'إيداع من الإدارة')
   → Safe.CurrentBalance += 200

6. الكاشير يعدّ النقدية ويغلق الجلسة
   ─────────────────────────────────
   الرصيد المتوقع = 500 + 51.75 + 50 - 17.25 + 200 = 784.5
   الرصيد الفعلي المعدود = 782.0
   الفرق = -2.5 (عجز)

   EXEC WEBERP_POSSessions_Close
       @SessionId=1, @ClosingCash=782.0

   → ينشئ قيداً مجمَّعاً:
   ─────────────────────────────────────────────────────
   مدين: نقدية POS           732.0    (نقد إجمالي صافي)
   مدين: حساب مدى            150.0    (بطاقات)
   مدين: عجز خزينة POS         2.5    (الفرق)
   ──────────────────────────────────────────────────────
   دائن: إيراد مبيعات        247.83   (بدون ضريبة)
   دائن: ضريبة القيمة المضافة 37.17   (ضريبة 15%)
   دائن: إيداع إداري          200.0   (حركة نقدية)
   ──────────────────────────────────────────────────────
   مجموع مدين = مجموع دائن = 884.5 ✅

   → ينشئ قيد COGS منفصل
   → Safe.CurrentBalance = 782.0 (الرصيد الفعلي)
   → Session.State = 'closed'

═══════════════════════════════════════════════════════════
```

---

## 🌱 Seed Data الكاملة

```sql
DECLARE @CompanyID INT=1, @BranchID INT=1, @UserId INT=1;

-- ترقيم المستندات
INSERT INTO WEBERP_DocumentSequences (DocType,Prefix,CurrentNumber,Padding,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('POSSession', 'POS',     0, 4, @CompanyID, @BranchID, @UserId, GETDATE()),
('POSOrder',   'POS-ORD', 0, 5, @CompanyID, @BranchID, @UserId, GETDATE()),
('POSReturn',  'POS-RET', 0, 4, @CompanyID, @BranchID, @UserId, GETDATE());

-- إعدادات الشركة المطلوبة لـ POS
INSERT INTO WEBERP_CompanySettings (SettingKey, SettingValue, Description, CompanyID)
VALUES
-- حسابات POS
('POSCashDifferenceIncomeAccountId',  '4901', 'حساب فوائض خزينة POS',      @CompanyID),
('POSCashDifferenceExpenseAccountId', '5901', 'حساب عجز خزينة POS',         @CompanyID),
('POSDefaultSalesAccountId',          '4001', 'حساب إيراد المبيعات الافتراضي', @CompanyID),
('POSDefaultCOGSAccountId',           '5001', 'حساب تكلفة البضاعة الافتراضي', @CompanyID),
('POSDefaultInventoryAccountId',      '1301', 'حساب المخزون الافتراضي',      @CompanyID),
-- ZATCA
('ZATCASellerNameAr',  'شركة التجارة السعودية', 'اسم البائع بالعربي',         @CompanyID),
('ZATCATaxNumber',     '300000000000003',        'الرقم الضريبي للشركة',       @CompanyID),
('ZATCAEnvironment',   'sandbox',                'sandbox / production',        @CompanyID);

-- تصنيفات POS الافتراضية
INSERT INTO WEBERP_POSProductCategories (NameAr,NameEn,SortOrder,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('الكل',          'All Products',  0, 1, @CompanyID, @BranchID, @UserId, GETDATE()),
('عروض',          'Offers',        1, 1, @CompanyID, @BranchID, @UserId, GETDATE()),
('الأكثر مبيعاً', 'Best Sellers',  2, 1, @CompanyID, @BranchID, @UserId, GETDATE()),
('جديد',          'New Arrivals',  3, 1, @CompanyID, @BranchID, @UserId, GETDATE());
```

---

## 🔗 ملخص نقاط التكامل الكاملة

```
┌──────────────────────────────────────────────────────────────────┐
│              POS ↔ باقي الموديولات                               │
├───────────────┬──────────────────────────────────────────────────┤
│ الموديول      │ التكامل                                          │
├───────────────┼──────────────────────────────────────────────────┤
│ المخازن       │ • فتح جلسة → تحميل StockQty في POSPriceTags     │
│               │ • Pay (sale) → خصم ItemStock فوراً             │
│               │ • Pay (return) → إضافة ItemStock فوراً          │
│               │ • إنشاء ValuationLayer لكل فاتورة               │
│               │ • تحديث LotId.RemainingQty للـ FIFO             │
├───────────────┼──────────────────────────────────────────────────┤
│ الحسابات      │ • إغلاق الجلسة → قيد مجمَّع شامل               │
│               │ • قيد COGS منفصل عند إغلاق الجلسة              │
│               │ • قيد فارق الخزينة (عجز/فائض)                  │
│               │ • لا قيود فورية — كلها عند إغلاق الجلسة        │
├───────────────┼──────────────────────────────────────────────────┤
│ النقدية       │ • Pay (cash) → Safe.CurrentBalance ++ فوراً     │
│               │ • Pay return (cash) → Safe.CurrentBalance --    │
│               │ • CashMovements → تحديث رصيد الخزينة            │
│               │ • Close Session → Safe = ClosingCash (فعلي)     │
├───────────────┼──────────────────────────────────────────────────┤
│ المبيعات      │ • CreateInvoice (اختياري) → SalesInvoice رسمية  │
│               │ • يستدعي SalesInvoices_Post                      │
│               │ • يُحدِّث Customer.CurrentBalance                │
│               │ • بدونها: الدفع نقدي فوري بدون ذمم              │
├───────────────┼──────────────────────────────────────────────────┤
│ العملاء       │ • تحقق من Blacklisted قبل البيع الآجل           │
│               │ • تحديث CurrentBalance عند فاتورة رسمية          │
│               │ • كشف حساب خاص بمشتريات POS                     │
├───────────────┼──────────────────────────────────────────────────┤
│ قوائم الأسعار │ • POSPriceTags_Refresh يقرأ PriceListItems       │
│               │ • الأولوية: PriceList > CustomPrice > SalesPrice  │
│               │ • يتحدث عند كل فتح جلسة                         │
├───────────────┼──────────────────────────────────────────────────┤
│ ZATCA         │ • كل فاتورة POS = QRCode + UUID + Hash          │
│               │ • توليد فوري بعد الدفع                           │
│               │ • ZATCAStatus: pending → reported/cleared        │
└───────────────┴──────────────────────────────────────────────────┘
```

---

## 📐 Constraints و Indexes إضافية

```sql
-- منع تداخل الجلسات (كاشير واحد ← جلسة واحدة مفتوحة)
CREATE UNIQUE INDEX UX_POSSession_ActiveCashier
ON WEBERP_POSSessions(POSConfigId, CashierId, CompanyID)
WHERE State IN ('opening_control','open','closing_control')
  AND (IsCanceled=0 OR IsCanceled IS NULL);

-- منع بيع صنف ذي رصيد سالب (إذا AllowNegativeStock=0)
-- هذا يُطبَّق في SP لا على مستوى Constraint

-- فهرس للباركود السريع (< 50ms)
CREATE INDEX IX_POSPriceTags_Fast
ON WEBERP_POSPriceTags(Barcode, POSConfigId, CompanyID)
INCLUDE (ItemId, ItemNameAr, SalesPrice, TaxRate, StockQty, UOMName);

-- فهرس للـ Offline sync
CREATE INDEX IX_POSOrders_Offline
ON WEBERP_POSOrders(IsOffline, SyncedAt, CompanyID)
WHERE IsOffline=1;

-- فهرس للتقارير اليومية
CREATE INDEX IX_POSOrders_DailySummary
ON WEBERP_POSOrders(POSConfigId, OrderDate, State, OrderType, CompanyID)
INCLUDE (TotalAmount, TaxAmount, DiscountAmount);

-- constraints إضافية
ALTER TABLE WEBERP_POSConfigs ADD
CONSTRAINT CHK_POSConfig_MaxDiscount CHECK (MaxDiscountPercent BETWEEN 0 AND 100);

ALTER TABLE WEBERP_POSConfigs ADD
CONSTRAINT CHK_POSConfig_TaxRate CHECK (DefaultTaxRate BETWEEN 0 AND 100);

ALTER TABLE WEBERP_POSOrders ADD
CONSTRAINT CHK_POSOrder_Change CHECK (ChangeAmount >= 0);

ALTER TABLE WEBERP_POSDiscountCodes ADD
CONSTRAINT CHK_DiscountCode_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate);

ALTER TABLE WEBERP_POSDiscountCodes ADD
CONSTRAINT CHK_DiscountCode_Value CHECK (DiscountValue > 0);
```

---

## 🔒 إعدادات الأمان والصلاحيات

```sql
-- صلاحيات خاصة بـ POS
-- يُضاف لجدول Permissions أو Roles

-- المدير (Manager):
--   ✅ فتح وإغلاق الجلسة
--   ✅ تجاوز حد الخصم
--   ✅ إلغاء الفاتورة (Void)
--   ✅ قبول المرتجع
--   ✅ سحب وإيداع نقدية
--   ✅ عرض التقارير

-- الكاشير (Cashier):
--   ✅ فتح وإغلاق جلسته فقط
--   ✅ إنشاء فواتير البيع
--   ✅ تطبيق خصم ≤ MaxDiscountPercent
--   ❌ تجاوز حد الخصم بدون موافقة مدير
--   ❌ Void بدون موافقة مدير
--   ❌ عرض تقارير الكاشيرين الآخرين

-- مشرف (Supervisor):
--   ✅ الموافقة على الخصومات الزائدة
--   ✅ قبول المرتجعات فوق حد معين
--   ✅ Void مع سبب
--   ✅ عرض تقارير فرعه
```
