# تحليل موديول المشتريات (Purchasing)
## Analysis File for Claude Code — NozomSoft ERP
## نسخة شاملة — مستوى Odoo

---

## 🎯 نطاق الموديول

```
تعريف الموردين
        ↓
شروط الدفع والأسعار
        ↓
طلب الشراء الداخلي (Purchase Request)
        ↓
طلب عرض الأسعار (RFQ)
        ↓
أمر الشراء (Purchase Order)
        ↓
استلام البضاعة (GRN)
    ← يُنشئ StockPicking تلقائياً
        ↓
فاتورة المورد (Vendor Bill)
    ← 3-Way Match: PO + GRN + Invoice
        ↓
الدفع للمورد
    ← يُسوّى مع الفاتورة
        ↓
مرتجع الشراء
    ← Debit Note / Credit Note
```

---

## 🔑 FormCodes — نطاق هذا الموديول: 600–699

| FormCode | FormName | FormNameAr | HasTable | HasTabs | ملاحظة |
|----------|----------|------------|----------|---------|--------|
| 600 | VendorCategories | تصنيفات الموردين | 1 | 0 | |
| 601 | Vendors | الموردون | 1 | 1 | بيانات + حسابات + بنوك |
| 602 | VendorPriceLists | قوائم أسعار الموردين | 1 | 0 | |
| 603 | VendorTaxes | ضرائب الموردين | 1 | 0 | |
| 610 | PurchaseRequests | طلبات الشراء الداخلية | 1 | 0 | |
| 611 | PurchaseRequestLines | سطور طلب الشراء | 1 | 0 | |
| 620 | RFQs | طلبات عروض الأسعار | 1 | 0 | |
| 621 | RFQLines | سطور عروض الأسعار | 1 | 0 | |
| 630 | PurchaseOrders | أوامر الشراء | 1 | 1 | |
| 631 | PurchaseOrderLines | سطور أوامر الشراء | 1 | 0 | |
| 640 | VendorBills | فواتير الموردين | 1 | 1 | |
| 641 | VendorBillLines | سطور الفواتير | 1 | 0 | |
| 650 | PurchaseReturns | مرتجعات الشراء | 1 | 0 | |
| 651 | PurchaseReturnLines | سطور المرتجعات | 1 | 0 | |
| 660 | VendorPayments | مدفوعات الموردين | 1 | 0 | |

---

## 🗄️ الجداول الكاملة

### 1. WEBERP_VendorCategories — تصنيفات الموردين
```sql
CREATE TABLE [dbo].[WEBERP_VendorCategories] (
    [Id]       INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]     NVARCHAR(20)  NOT NULL,
    [NameAr]   NVARCHAR(100) NOT NULL,
    [NameEn]   NVARCHAR(100) NULL,
    [ParentId] INT           NULL REFERENCES WEBERP_VendorCategories(Id),
    [Level]    INT           NOT NULL DEFAULT 1,
    [IsParent] BIT           NOT NULL DEFAULT 0,
    [IsActive] BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_VendorCategoryCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 2. WEBERP_Vendors — الموردون
```sql
CREATE TABLE [dbo].[WEBERP_Vendors] (
    [Id]                 INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]               NVARCHAR(20)  NOT NULL,
    [NameAr]             NVARCHAR(200) NOT NULL,
    [NameEn]             NVARCHAR(200) NULL,
    [CategoryId]         INT           NULL REFERENCES WEBERP_VendorCategories(Id),
    [VendorType]         NVARCHAR(30)  NOT NULL DEFAULT 'local',
    -- local / foreign / individual / government
    -- حساب المحاسبة
    [AccountId]          INT           NOT NULL,
    -- حساب المورد في الدليل المحاسبي
    [AdvanceAccountId]   INT           NULL,
    -- حساب السلف المدفوعة للمورد
    [RetentionAccountId] INT           NULL,
    -- حساب استقطاع الضمان
    -- بيانات التسجيل
    [TaxNumber]          NVARCHAR(50)  NULL,
    [CommercialReg]      NVARCHAR(50)  NULL,
    [CommercialRegExpiry] DATE         NULL,
    [ZATCAScheme]        NVARCHAR(20)  NULL,
    -- standard / simplified
    -- العملة والشروط
    [CurrencyCode]       NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [PaymentTermId]      INT           NULL,
    -- FK → WEBERP_AccountPaymentTerms
    [CreditLimit]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CurrentBalance]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- إجمالي ما هو مستحق للمورد
    [AdvanceBalance]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- إجمالي السلف المدفوعة غير المصفاة
    -- التواصل
    [Phone1]             NVARCHAR(50)  NULL,
    [Phone2]             NVARCHAR(50)  NULL,
    [Email]              NVARCHAR(200) NULL,
    [Website]            NVARCHAR(200) NULL,
    [Address]            NVARCHAR(500) NULL,
    [Country]            NVARCHAR(100) NULL,
    [City]               NVARCHAR(100] NULL,
    [PostalCode]         NVARCHAR(20)  NULL,
    [ContactPerson]      NVARCHAR(200] NULL,
    [ContactPhone]       NVARCHAR(50)  NULL,
    -- بيانات بنكية
    [BankName]           NVARCHAR(200) NULL,
    [BankBranch]         NVARCHAR(200) NULL,
    [BankAccount]        NVARCHAR(50)  NULL,
    [IBAN]               NVARCHAR(34)  NULL,
    [SwiftCode]          NVARCHAR(20)  NULL,
    -- إعدادات الشراء
    [LeadTimeDays]       INT           NOT NULL DEFAULT 0,
    [MinOrderValue]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [PriceListId]        INT           NULL,
    -- قائمة أسعار خاصة بهذا المورد
    [InvoicePolicy]      NVARCHAR(20)  NOT NULL DEFAULT 'order',
    -- order ← الفوترة عند الأمر
    -- receipt ← الفوترة عند الاستلام
    -- رسوم وضرائب
    [WithholdingTaxRate] DECIMAL(5,2)  NOT NULL DEFAULT 0,
    -- نسبة ضريبة الاستقطاع
    [IsActive]           BIT           NOT NULL DEFAULT 1,
    [Blacklisted]        BIT           NOT NULL DEFAULT 0,
    -- مدرج على القائمة السوداء
    [BlacklistReason]    NVARCHAR(200) NULL,
    [Notes]              NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_VendorCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 3. WEBERP_VendorPriceLists — أسعار الموردين
```sql
CREATE TABLE [dbo].[WEBERP_VendorPriceLists] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [VendorId]     INT           NOT NULL REFERENCES WEBERP_Vendors(Id),
    [ItemId]       INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [UOMId]        INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [MinQty]       DECIMAL(18,4) NOT NULL DEFAULT 1,
    [Price]        DECIMAL(18,4) NOT NULL,
    [CurrencyCode] NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [StartDate]    DATE          NULL,
    [EndDate]      DATE          NULL,
    [LeadTimeDays] INT           NULL,
    -- وقت التوريد الخاص بهذا الصنف من هذا المورد
    [IsActive]     BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 4. WEBERP_PurchaseRequests — طلبات الشراء الداخلية
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseRequests] (
    [Id]            INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]          NVARCHAR(30)  NOT NULL,
    -- PR/2025/001
    [RequestDate]   DATE          NOT NULL DEFAULT GETDATE(),
    [RequiredDate]  DATE          NULL,
    [RequestedBy]   INT           NOT NULL,
    -- FK → Users
    [DepartmentId]  INT           NULL,
    -- FK → WEBERP_AnalyticAccounts (قسم الطالب)
    [WarehouseId]   INT           NULL REFERENCES WEBERP_Warehouses(Id),
    [State]         NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / submitted / approved / rejected / po_created / done / cancel
    [Priority]      NVARCHAR(10)  NOT NULL DEFAULT 'normal',
    -- low / normal / high / urgent
    [Reason]        NVARCHAR(500) NULL,
    [ApprovedBy]    INT           NULL,
    [ApprovedDate]  DATETIME2     NULL,
    [RejectedBy]    INT           NULL,
    [RejectedDate]  DATETIME2     NULL,
    [RejectReason]  NVARCHAR(300) NULL,
    [Notes]         NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_PRName UNIQUE (Name, CompanyID, BranchID)
);
```

### 5. WEBERP_PurchaseRequestLines — سطور طلب الشراء
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseRequestLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [RequestId]       INT           NOT NULL REFERENCES WEBERP_PurchaseRequests(Id),
    [LineNumber]      INT           NOT NULL,
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [UOMId]           INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [QtyRequested]    DECIMAL(18,4) NOT NULL,
    [QtyApproved]     DECIMAL(18,4) NULL,
    [QtyOrdered]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- ما تم رفع PO له فعلاً
    [EstimatedPrice]  DECIMAL(18,4) NULL,
    [WarehouseId]     INT           NULL REFERENCES WEBERP_Warehouses(Id),
    [AnalyticAccountId] INT         NULL,
    -- مركز التكلفة
    [Specs]           NVARCHAR(MAX) NULL,
    -- المواصفات الفنية
    [Notes]           NVARCHAR(300] NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 6. WEBERP_PurchaseOrders — أوامر الشراء
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseOrders] (
    [Id]                 INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]               NVARCHAR(30)  NOT NULL,
    -- PO/2025/001
    [PODate]             DATE          NOT NULL DEFAULT GETDATE(),
    [VendorId]           INT           NOT NULL REFERENCES WEBERP_Vendors(Id),
    [VendorRef]          NVARCHAR(100) NULL,
    -- رقم المرجع عند المورد
    [WarehouseId]        INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    [RequestId]          INT           NULL REFERENCES WEBERP_PurchaseRequests(Id),
    -- مرتبط بطلب شراء داخلي
    -- العملة والأسعار
    [CurrencyCode]       NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]       DECIMAL(18,6) NOT NULL DEFAULT 1,
    [PaymentTermId]      INT           NULL,
    -- شروط الدفع
    -- التواريخ
    [ExpectedDate]       DATE          NULL,
    -- التاريخ المتوقع للاستلام
    [ValidityDate]       DATE          NULL,
    -- صلاحية الأمر
    -- الإجماليات (تُحسب من السطور)
    [SubTotal]           DECIMAL(18,4) NOT NULL DEFAULT 0,
    [DiscountAmount]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxAmount]          DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ShippingCost]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [OtherCosts]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmountLocal]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- بالعملة المحلية
    -- الكميات
    [QtyOrdered]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyReceived]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyBilled]          DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الحالة
    [State]              NVARCHAR(30)  NOT NULL DEFAULT 'draft',
    -- draft / sent / purchase / done / cancel
    -- draft   = مسودة
    -- sent    = أُرسل للمورد (RFQ)
    -- purchase = مؤكد (PO)
    -- done    = مغلق
    -- cancel  = ملغي
    [BillingStatus]      NVARCHAR(20)  NOT NULL DEFAULT 'nothing',
    -- nothing / to_bill / fully_billed
    [ReceiptStatus]      NVARCHAR(20)  NOT NULL DEFAULT 'nothing',
    -- nothing / partial / full
    -- الشحن والتسليم
    [ShippingMethod]     NVARCHAR(100) NULL,
    [IncoTerms]          NVARCHAR(20)  NULL,
    -- FOB / CIF / DDP / EXW...
    [DestinationPort]    NVARCHAR(100) NULL,
    -- الموافقات
    [ApprovedBy]         INT           NULL,
    [ApprovedDate]       DATETIME2     NULL,
    [ApprovalLevel]      INT           NOT NULL DEFAULT 0,
    -- 0=بدون موافقة، 1=مستوى1، 2=مستوى2
    -- المستندات
    [TermsConditions]    NVARCHAR(MAX) NULL,
    [Notes]              NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_POName UNIQUE (Name, CompanyID, BranchID)
);
```

### 7. WEBERP_PurchaseOrderLines — سطور أمر الشراء
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseOrderLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [POId]            INT           NOT NULL REFERENCES WEBERP_PurchaseOrders(Id),
    [LineNumber]      INT           NOT NULL,
    [RequestLineId]   INT           NULL REFERENCES WEBERP_PurchaseRequestLines(Id),
    [ItemId]          INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [Description]     NVARCHAR(500) NULL,
    -- وصف إضافي أو مختلف عن اسم الصنف
    [UOMId]           INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [PurchaseUOMId]   INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    -- وحدة الشراء (قد تختلف عن وحدة المخزون)
    -- الكميات
    [QtyOrdered]      DECIMAL(18,4) NOT NULL,
    [QtyReceived]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyBilled]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyReturned]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [QtyPending]      AS (QtyOrdered - QtyReceived),
    -- الأسعار
    [UnitPrice]       DECIMAL(18,4) NOT NULL,
    [DiscountPercent] DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [DiscountAmount]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxIds]          NVARCHAR(200) NULL,
    -- CSV من Tax IDs
    [TaxAmount]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [SubTotal]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- التكلفة الإضافية
    [LandedCostShare] DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- حصة السطر من التكاليف الإضافية
    -- التحليل
    [AnalyticDistribution] NVARCHAR(MAX) NULL,
    -- JSON: {"AnalyticAccountId": نسبة%}
    -- التاريخ المتوقع
    [ExpectedDate]    DATE          NULL,
    -- GRN التي جاءت على هذا السطر
    [GRNLineId]       INT           NULL,
    [Notes]           NVARCHAR(300) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 8. WEBERP_VendorBills — فواتير الموردين
```sql
CREATE TABLE [dbo].[WEBERP_VendorBills] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]             NVARCHAR(30)  NOT NULL,
    -- BILL/2025/001
    [BillDate]         DATE          NOT NULL,
    [DueDate]          DATE          NOT NULL,
    [VendorId]         INT           NOT NULL REFERENCES WEBERP_Vendors(Id),
    [VendorBillNumber] NVARCHAR(100) NULL,
    -- رقم الفاتورة عند المورد
    [POId]             INT           NULL REFERENCES WEBERP_PurchaseOrders(Id),
    -- Amounts
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [SubTotal]         DECIMAL(18,4) NOT NULL DEFAULT 0,
    [DiscountAmount]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxAmount]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmountLocal] DECIMAL(18,4) NOT NULL DEFAULT 0,
    [AmountPaid]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [AmountResidual]   AS (TotalAmount - AmountPaid),
    -- الحالة
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / posted / cancel
    [PaymentState]     NVARCHAR(20)  NOT NULL DEFAULT 'not_paid',
    -- not_paid / in_payment / paid / partial / reversed
    -- الضريبة
    [WithholdingTaxAmount] DECIMAL(18,4) NOT NULL DEFAULT 0,
    [NetPayable]       AS (TotalAmount - WithholdingTaxAmount),
    -- الحسابات
    [JournalId]        INT           NULL,
    -- دفتر اليومية (Purchase Journal)
    [JournalEntryId]   INT           NULL,
    -- القيد المحاسبي
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    [PostedDate]       DATETIME2     NULL,
    [PostedBy]         INT           NULL,
    -- المطابقة (3-Way Match)
    [MatchStatus]      NVARCHAR(20)  NOT NULL DEFAULT 'no_match',
    -- no_match / partial / matched
    -- الإلغاء
    [ReversalBillId]   INT           NULL,
    -- فاتورة العكس (Credit Note)
    [IsReversal]       BIT           NOT NULL DEFAULT 0,
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_BillName UNIQUE (Name, CompanyID, BranchID)
);
```

### 9. WEBERP_VendorBillLines — سطور الفاتورة
```sql
CREATE TABLE [dbo].[WEBERP_VendorBillLines] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [BillId]          INT           NOT NULL REFERENCES WEBERP_VendorBills(Id),
    [LineNumber]      INT           NOT NULL,
    [POLineId]        INT           NULL REFERENCES WEBERP_PurchaseOrderLines(Id),
    -- مرتبط بسطر PO
    [ItemId]          INT           NULL REFERENCES WEBERP_Items(Id),
    [Description]     NVARCHAR(500) NULL,
    [AccountId]       INT           NOT NULL,
    -- حساب المصروف أو المخزون
    [UOMId]           INT           NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [Quantity]        DECIMAL(18,4) NOT NULL DEFAULT 1,
    [UnitPrice]       DECIMAL(18,4) NOT NULL,
    [DiscountPercent] DECIMAL(5,2)  NOT NULL DEFAULT 0,
    [DiscountAmount]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxIds]          NVARCHAR(200) NULL,
    [TaxAmount]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    [SubTotal]        DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [AnalyticDistribution] NVARCHAR(MAX) NULL,
    [Notes]           NVARCHAR(300) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 10. WEBERP_PurchaseReturns — مرتجعات الشراء
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseReturns] (
    [Id]             INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]           NVARCHAR(30)  NOT NULL,
    -- PRET/2025/001
    [ReturnDate]     DATE          NOT NULL DEFAULT GETDATE(),
    [VendorId]       INT           NOT NULL REFERENCES WEBERP_Vendors(Id),
    [POId]           INT           NULL REFERENCES WEBERP_PurchaseOrders(Id),
    [BillId]         INT           NULL REFERENCES WEBERP_VendorBills(Id),
    [ReturnReason]   NVARCHAR(30)  NOT NULL DEFAULT 'defective',
    -- defective / wrong_item / over_delivery / price_dispute / other
    [ReasonDetail]   NVARCHAR(500) NULL,
    [WarehouseId]    INT           NOT NULL REFERENCES WEBERP_Warehouses(Id),
    [CurrencyCode]   NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]   DECIMAL(18,6) NOT NULL DEFAULT 1,
    [TotalAmount]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TaxAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [NetAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [State]          NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / confirmed / done / cancel
    -- الحركة المخزنية والقيد
    [PickingId]      INT           NULL,
    -- StockPicking للمرتجع
    [CreditNoteId]   INT           NULL,
    -- فاتورة العكس عند المورد
    [JournalEntryId] INT           NULL,
    [IsPosted]       BIT           NOT NULL DEFAULT 0,
    [Notes]          NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_PReturnName UNIQUE (Name, CompanyID, BranchID)
);
```

### 11. WEBERP_PurchaseReturnLines — سطور المرتجع
```sql
CREATE TABLE [dbo].[WEBERP_PurchaseReturnLines] (
    [Id]             INT           IDENTITY(1,1) PRIMARY KEY,
    [ReturnId]       INT           NOT NULL REFERENCES WEBERP_PurchaseReturns(Id),
    [LineNumber]     INT           NOT NULL,
    [POLineId]       INT           NULL REFERENCES WEBERP_PurchaseOrderLines(Id),
    [ItemId]         INT           NOT NULL REFERENCES WEBERP_Items(Id),
    [LotId]          INT           NULL REFERENCES WEBERP_ItemLots(Id),
    [UOMId]          INT           NOT NULL REFERENCES WEBERP_UnitOfMeasures(Id),
    [QtyReturned]    DECIMAL(18,4) NOT NULL,
    [UnitPrice]      DECIMAL(18,4) NOT NULL,
    [TaxAmount]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalAmount]    DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ReturnReason]   NVARCHAR(200] NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 12. WEBERP_LandedCosts — التكاليف الإضافية
```sql
-- التكاليف الإضافية (شحن، جمارك، تأمين) تُوزع على الأصناف
CREATE TABLE [dbo].[WEBERP_LandedCosts] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [Name]            NVARCHAR(30)  NOT NULL,
    -- LC/2025/001
    [Date]            DATE          NOT NULL DEFAULT GETDATE(),
    [VendorId]        INT           NULL REFERENCES WEBERP_Vendors(Id),
    [BillId]          INT           NULL REFERENCES WEBERP_VendorBills(Id),
    [SplitMethod]     NVARCHAR(20)  NOT NULL DEFAULT 'equal',
    -- equal       ← بالتساوي
    -- by_quantity ← بالكمية
    -- by_value    ← بالقيمة
    -- by_weight   ← بالوزن
    -- by_volume   ← بالحجم
    [State]           NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    [JournalEntryId]  INT           NULL,
    [IsPosted]        BIT           NOT NULL DEFAULT 0,
    [Notes]           NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_LCName UNIQUE (Name, CompanyID, BranchID)
);

CREATE TABLE [dbo].[WEBERP_LandedCostLines] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [LCId]         INT           NOT NULL REFERENCES WEBERP_LandedCosts(Id),
    [AccountId]    INT           NOT NULL,
    -- حساب نوع التكلفة (شحن، جمارك)
    [Amount]       DECIMAL(18,4) NOT NULL,
    [Description]  NVARCHAR(200) NULL,
    [CompanyID]    INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]    INT NULL, [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);

CREATE TABLE [dbo].[WEBERP_LandedCostPickings] (
    -- الحركات المخزنية التي تنطبق عليها هذه التكاليف
    [Id]        INT IDENTITY(1,1) PRIMARY KEY,
    [LCId]      INT NOT NULL REFERENCES WEBERP_LandedCosts(Id),
    [PickingId] INT NOT NULL,
    [CompanyID] INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy] INT NULL, [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
);
```

---

## 🔧 Stored Procedures الكاملة

### SP 1: WEBERP_PurchaseOrders_Confirm — تأكيد أمر الشراء
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PurchaseOrders_Confirm]
    @POId      INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق من الحالة
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_PurchaseOrders
        WHERE Id=@POId AND State='draft' AND CompanyID=@CompanyID
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('أمر الشراء غير موجود أو مؤكد مسبقاً', 16, 1); RETURN; END

    -- 2. التحقق من وجود سطور
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_PurchaseOrderLines
        WHERE POId=@POId AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('أمر الشراء لا يحتوي على سطور', 16, 1); RETURN; END

    -- 3. التحقق من موافقة المستخدم المخوّل (إذا كان مطلوباً)
    DECLARE @TotalAmount DECIMAL(18,4), @ApprovalLevel INT;
    SELECT @TotalAmount=TotalAmount FROM WEBERP_PurchaseOrders WHERE Id=@POId;
    -- (يمكن إضافة جدول WEBERP_ApprovalThresholds لتحديد الحدود لاحقاً)

    -- 4. تحديث حالة الأمر
    UPDATE WEBERP_PurchaseOrders SET
        State='purchase',
        ApprovedBy=@UserId,
        ApprovedDate=GETDATE(),
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@POId;

    -- 5. إنشاء StockPicking تلقائياً للاستلام
    DECLARE @WarehouseId INT, @POName NVARCHAR(30);
    DECLARE @SuppliersLoc INT, @StockLoc INT, @PickingTypeId INT;

    SELECT @WarehouseId=WarehouseId, @POName=Name
    FROM WEBERP_PurchaseOrders WHERE Id=@POId;

    SELECT @StockLoc = StockLocationId FROM WEBERP_Warehouses WHERE Id=@WarehouseId;
    SELECT @SuppliersLoc = Id FROM WEBERP_WarehouseLocations
    WHERE LocationType='supplier' AND CompanyID=@CompanyID;
    SELECT @PickingTypeId = Id FROM WEBERP_StockPickingTypes
    WHERE PickingTypeCode='incoming' AND WarehouseId=@WarehouseId AND CompanyID=@CompanyID;

    DECLARE @PickingId INT;
    INSERT INTO WEBERP_StockPickings
        (Name, PickingTypeId, PickingDate, LocationId, LocationDestId,
         State, Origin, SourceDocType, SourceDocId, CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES
        ('GRN-'+@POName, @PickingTypeId, GETDATE(),
         @SuppliersLoc, @StockLoc, 'confirmed',
         @POName, 'PurchaseOrder', @POId,
         @CompanyID, @BranchID, @UserId, GETDATE());

    SET @PickingId = SCOPE_IDENTITY();

    -- 6. إنشاء سطور الحركة من سطور PO
    INSERT INTO WEBERP_StockMoveLines
        (PickingId, ItemId, UOMId, LocationId, LocationDestId,
         QtyDemand, QtyDemandBase, UOMConvFactor, State,
         CompanyID, BranchID, CreatedBy, CreatedAt)
    SELECT
        @PickingId, pol.ItemId,
        ISNULL(pol.PurchaseUOMId, pol.UOMId),
        @SuppliersLoc, @StockLoc,
        pol.QtyOrdered, pol.QtyOrdered,
        1, 'confirmed',
        @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_PurchaseOrderLines pol
    WHERE pol.POId = @POId AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL);

    -- 7. تحديث IncomingQty في ItemStock
    MERGE WEBERP_ItemStock AS target
    USING (
        SELECT ItemId, @WarehouseId AS WId,
               SUM(QtyOrdered) AS Qty
        FROM WEBERP_PurchaseOrderLines
        WHERE POId=@POId AND (IsCanceled=0 OR IsCanceled IS NULL)
        GROUP BY ItemId
    ) AS src ON target.ItemId=src.ItemId AND target.WarehouseId=src.WId
           AND target.CompanyID=@CompanyID
    WHEN MATCHED THEN UPDATE SET
        target.IncomingQty=target.IncomingQty+src.Qty,
        target.ModifiedBy=@UserId, target.ModifiedAt=GETDATE()
    WHEN NOT MATCHED THEN INSERT
        (ItemId,WarehouseId,Quantity,ReservedQty,IncomingQty,AverageCost,TotalValue,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (src.ItemId,src.WId,0,0,src.Qty,0,0,@CompanyID,@BranchID,@UserId,GETDATE());

    COMMIT;
END;
```

### SP 2: WEBERP_GRN_Validate — تأكيد استلام البضاعة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_GRN_Validate]
    @PickingId INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. تنفيذ الحركة المخزنية
    EXEC WEBERP_StockPickings_Validate @PickingId, @CompanyID, @BranchID, @UserId;

    -- 2. تحديث QtyReceived في PO Lines
    UPDATE pol SET
        pol.QtyReceived = pol.QtyReceived + ml.QtyDone,
        pol.ModifiedBy  = @UserId, pol.ModifiedAt = GETDATE()
    FROM WEBERP_PurchaseOrderLines pol
    JOIN WEBERP_StockMoveLines ml ON ml.ItemId = pol.ItemId
        AND ml.PickingId = @PickingId
        AND (ml.IsCanceled=0 OR ml.IsCanceled IS NULL)
    WHERE pol.POId = (
        SELECT SourceDocId FROM WEBERP_StockPickings
        WHERE Id=@PickingId AND SourceDocType='PurchaseOrder'
    );

    -- 3. تحديث IncomingQty في ItemStock (خصم ما وصل)
    UPDATE s SET
        s.IncomingQty = s.IncomingQty - ml.QtyDoneBase,
        s.ModifiedBy  = @UserId, s.ModifiedAt = GETDATE()
    FROM WEBERP_ItemStock s
    JOIN WEBERP_StockMoveLines ml ON ml.ItemId=s.ItemId
    WHERE ml.PickingId=@PickingId AND s.CompanyID=@CompanyID;

    -- 4. تحديث حالة PO
    DECLARE @POId INT;
    SELECT @POId=SourceDocId FROM WEBERP_StockPickings
    WHERE Id=@PickingId AND SourceDocType='PurchaseOrder';

    IF @POId IS NOT NULL
    BEGIN
        -- هل استُلم الكل؟
        DECLARE @TotalOrdered  DECIMAL(18,4) = 0;
        DECLARE @TotalReceived DECIMAL(18,4) = 0;

        SELECT @TotalOrdered  = SUM(QtyOrdered),
               @TotalReceived = SUM(QtyReceived)
        FROM WEBERP_PurchaseOrderLines
        WHERE POId=@POId AND (IsCanceled=0 OR IsCanceled IS NULL);

        UPDATE WEBERP_PurchaseOrders SET
            QtyReceived   = @TotalReceived,
            ReceiptStatus = CASE
                WHEN @TotalReceived >= @TotalOrdered THEN 'full'
                WHEN @TotalReceived > 0              THEN 'partial'
                ELSE 'nothing'
            END,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE Id=@POId;
    END

    COMMIT;
END;
```

### SP 3: WEBERP_VendorBills_Post — ترحيل فاتورة المورد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_VendorBills_Post]
    @BillId    INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق
    DECLARE @VendorId INT, @TotalAmount DECIMAL(18,4),
            @TaxAmount DECIMAL(18,4), @ExchangeRate DECIMAL(18,6),
            @CurrencyCode NVARCHAR(10), @WithholdingTaxAmount DECIMAL(18,4);

    SELECT @VendorId=VendorId, @TotalAmount=TotalAmount,
           @TaxAmount=TaxAmount, @ExchangeRate=ExchangeRate,
           @CurrencyCode=CurrencyCode, @WithholdingTaxAmount=WithholdingTaxAmount
    FROM WEBERP_VendorBills
    WHERE Id=@BillId AND State='draft' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @TotalAmount IS NULL
    BEGIN ROLLBACK; RAISERROR('الفاتورة غير موجودة أو مرحّلة',16,1); RETURN; END

    -- 2. جلب حسابات المورد
    DECLARE @VendorAccountId INT;
    SELECT @VendorAccountId=AccountId FROM WEBERP_Vendors WHERE Id=@VendorId;

    -- 3. بناء القيد المحاسبي
    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber, EntryDate, JournalType, DescriptionAr,
         TotalDebit, TotalCredit, IsPosted, PostedDate, PostedBy,
         CompanyID, BranchID, CreatedBy, CreatedAt)
    SELECT 'BILL-'+CAST(@BillId AS NVARCHAR),
           BillDate, 'PurchaseInvoice',
           'فاتورة مورد: '+ISNULL(VendorBillNumber, Name),
           @TotalAmount, @TotalAmount, 1, GETDATE(), @UserId,
           @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_VendorBills WHERE Id=@BillId;
    SET @JId=SCOPE_IDENTITY();

    -- 4. سطور القيد من سطور الفاتورة
    -- مدين: حسابات المصروف/المخزون لكل سطر
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId, AccountId, DebitAmount, CreditAmount,
         DescriptionAr, AnalyticDistribution,
         CompanyID, BranchID, CreatedBy, CreatedAt)
    SELECT @JId, bl.AccountId,
           bl.SubTotal, 0,
           bl.Description,
           bl.AnalyticDistribution,
           @CompanyID, @BranchID, @UserId, GETDATE()
    FROM WEBERP_VendorBillLines bl
    WHERE bl.BillId=@BillId AND (bl.IsCanceled=0 OR bl.IsCanceled IS NULL)
      AND bl.SubTotal <> 0;

    -- مدين: ضريبة المدخلات (VAT Input)
    IF @TaxAmount <> 0
    BEGIN
        -- جلب حساب ضريبة المدخلات من الإعدادات
        DECLARE @VATInputAccountId INT;
        SELECT TOP 1 @VATInputAccountId = AccountId
        FROM WEBERP_AccountTaxes
        WHERE TaxType='purchase' AND IsActive=1 AND CompanyID=@CompanyID;

        IF @VATInputAccountId IS NOT NULL
            INSERT INTO WEBERP_JournalEntryDetails
                (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@VATInputAccountId,@TaxAmount,0,'ضريبة المدخلات',@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- دائن: حساب المورد (صافي الاستحقاق بعد الاستقطاع)
    DECLARE @NetPayable DECIMAL(18,4) = @TotalAmount - @WithholdingTaxAmount;

    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@JId,@VendorAccountId,0,@NetPayable,'مورد',@CompanyID,@BranchID,@UserId,GETDATE());

    -- دائن: ضريبة الاستقطاع (إن وجدت)
    IF @WithholdingTaxAmount <> 0
    BEGIN
        DECLARE @WithholdingAccountId INT;
        SELECT @WithholdingAccountId = AccountId
        FROM WEBERP_Vendors WHERE Id=@VendorId;
        -- (يُحدد من إعدادات الضريبة)

        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (@JId,[WithholdingTaxAccountId],0,@WithholdingTaxAmount,'ضريبة استقطاع',@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- 5. تحديث رصيد المورد
    UPDATE WEBERP_Vendors SET
        CurrentBalance = CurrentBalance + @NetPayable,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@VendorId;

    -- 6. تحديث QtyBilled في PO Lines
    UPDATE pol SET pol.QtyBilled = pol.QtyBilled + bl.Quantity
    FROM WEBERP_PurchaseOrderLines pol
    JOIN WEBERP_VendorBillLines bl ON bl.POLineId=pol.Id
    WHERE bl.BillId=@BillId AND (bl.IsCanceled=0 OR bl.IsCanceled IS NULL);

    -- 7. توليد سطور التحليل
    EXEC WEBERP_AnalyticLines_Generate_FromBill @BillId, @JId, @CompanyID, @BranchID, @UserId;

    -- 8. تحديث حالة الفاتورة
    UPDATE WEBERP_VendorBills SET
        State='posted', IsPosted=1,
        PostedDate=GETDATE(), PostedBy=@UserId,
        JournalEntryId=@JId,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@BillId;

    -- 9. تحديث BillingStatus في PO
    IF EXISTS (SELECT 1 FROM WEBERP_VendorBills WHERE POId IS NOT NULL AND Id=@BillId)
    BEGIN
        DECLARE @POId2 INT;
        SELECT @POId2=POId FROM WEBERP_VendorBills WHERE Id=@BillId;
        IF @POId2 IS NOT NULL
            EXEC WEBERP_PurchaseOrders_UpdateBillingStatus @POId2, @CompanyID, @BranchID, @UserId;
    END

    COMMIT;
END;
```

### SP 4: WEBERP_PurchaseOrders_UpdateBillingStatus
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PurchaseOrders_UpdateBillingStatus]
    @POId INT, @CompanyID INT, @BranchID INT, @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalOrdered DECIMAL(18,4), @TotalBilled DECIMAL(18,4);

    SELECT @TotalOrdered = SUM(QtyOrdered * UnitPrice),
           @TotalBilled  = SUM(QtyBilled  * UnitPrice)
    FROM WEBERP_PurchaseOrderLines
    WHERE POId=@POId AND (IsCanceled=0 OR IsCanceled IS NULL);

    UPDATE WEBERP_PurchaseOrders SET
        QtyBilled     = @TotalBilled,
        BillingStatus = CASE
            WHEN @TotalBilled = 0              THEN 'nothing'
            WHEN @TotalBilled >= @TotalOrdered THEN 'fully_billed'
            ELSE 'to_bill'
        END,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@POId;
END;
```

### SP 5: WEBERP_VendorBills_MatchWithPO — مطابقة الفاتورة مع PO و GRN
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_VendorBills_MatchWithPO]
    @BillId    INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT,
    @MatchResult NVARCHAR(20) OUTPUT,
    @Discrepancies NVARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @MatchResult = 'matched';
    SET @Discrepancies = '';

    -- جلب بيانات الفاتورة
    DECLARE @POId INT;
    SELECT @POId = POId FROM WEBERP_VendorBills WHERE Id=@BillId;

    IF @POId IS NULL
    BEGIN SET @MatchResult = 'no_po'; RETURN; END

    -- مقارنة الكميات والأسعار
    DECLARE @Issues TABLE (Issue NVARCHAR(500));

    INSERT INTO @Issues
    SELECT
        'الصنف ['+ISNULL(i.Code,'')+'] - '+i.NameAr+': '+
        'كمية PO='+CAST(pol.QtyOrdered AS NVARCHAR)+
        ' | كمية GRN='+CAST(pol.QtyReceived AS NVARCHAR)+
        ' | كمية الفاتورة='+CAST(ISNULL(bl.Quantity,0) AS NVARCHAR)+
        ' | سعر PO='+CAST(pol.UnitPrice AS NVARCHAR)+
        ' | سعر الفاتورة='+CAST(ISNULL(bl.UnitPrice,0) AS NVARCHAR)
    FROM WEBERP_PurchaseOrderLines pol
    LEFT JOIN WEBERP_VendorBillLines bl ON bl.POLineId=pol.Id AND bl.BillId=@BillId
    JOIN WEBERP_Items i ON i.Id=pol.ItemId
    WHERE pol.POId=@POId
      AND (pol.IsCanceled=0 OR pol.IsCanceled IS NULL)
      AND (
        ABS(ISNULL(bl.Quantity,0) - pol.QtyReceived) > 0.001
        OR ABS(ISNULL(bl.UnitPrice,0) - pol.UnitPrice) > 0.01
      );

    IF EXISTS (SELECT 1 FROM @Issues)
    BEGIN
        SET @MatchResult = 'partial';
        SELECT @Discrepancies = STRING_AGG(Issue, CHAR(10)) FROM @Issues;
    END

    -- تحديث MatchStatus
    UPDATE WEBERP_VendorBills SET
        MatchStatus=@MatchResult,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@BillId;
END;
```

### SP 6: WEBERP_LandedCosts_Validate — توزيع التكاليف الإضافية
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_LandedCosts_Validate]
    @LCId      INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @SplitMethod NVARCHAR(20), @TotalCost DECIMAL(18,4);
    SELECT @SplitMethod=SplitMethod, @TotalCost=(SELECT SUM(Amount) FROM WEBERP_LandedCostLines WHERE LCId=@LCId)
    FROM WEBERP_LandedCosts WHERE Id=@LCId AND CompanyID=@CompanyID AND State='draft';

    IF @SplitMethod IS NULL
    BEGIN ROLLBACK; RAISERROR('التكلفة غير موجودة أو موزعة مسبقاً',16,1); RETURN; END

    -- حساب أساس التوزيع
    DECLARE @TotalBase DECIMAL(18,4);

    SELECT @TotalBase = CASE @SplitMethod
        WHEN 'by_quantity' THEN SUM(ml.QtyDoneBase)
        WHEN 'by_value'    THEN SUM(ml.QtyDoneBase * ml.UnitCost)
        WHEN 'by_weight'   THEN SUM(ml.QtyDoneBase * i.Weight)
        WHEN 'by_volume'   THEN SUM(ml.QtyDoneBase * i.Volume)
        ELSE               COUNT(DISTINCT ml.ItemId)   -- equal
    END
    FROM WEBERP_LandedCostPickings lcp
    JOIN WEBERP_StockMoveLines ml ON ml.PickingId=lcp.PickingId
        AND (ml.IsCanceled=0 OR ml.IsCanceled IS NULL)
    JOIN WEBERP_Items i ON i.Id=ml.ItemId
    WHERE lcp.LCId=@LCId AND ml.CompanyID=@CompanyID;

    IF ISNULL(@TotalBase,0) = 0
    BEGIN ROLLBACK; RAISERROR('لا يمكن التوزيع — الأساس صفر',16,1); RETURN; END

    -- توزيع التكلفة على الأصناف وتحديث UnitCost
    UPDATE ml SET
        ml.UnitCost = ml.UnitCost + (
            @TotalCost * CASE @SplitMethod
                WHEN 'by_quantity' THEN ml.QtyDoneBase
                WHEN 'by_value'    THEN ml.QtyDoneBase * ml.UnitCost
                WHEN 'by_weight'   THEN ml.QtyDoneBase * i.Weight
                WHEN 'by_volume'   THEN ml.QtyDoneBase * i.Volume
                ELSE 1.0
            END / @TotalBase / NULLIF(ml.QtyDoneBase,0)
        ),
        ml.TotalCost = ml.QtyDoneBase * (ml.UnitCost + (
            @TotalCost * CASE @SplitMethod
                WHEN 'by_quantity' THEN ml.QtyDoneBase
                WHEN 'by_value'    THEN ml.QtyDoneBase * ml.UnitCost
                ELSE 1.0
            END / @TotalBase / NULLIF(ml.QtyDoneBase,0)
        ))
    FROM WEBERP_StockMoveLines ml
    JOIN WEBERP_LandedCostPickings lcp ON lcp.PickingId=ml.PickingId
    JOIN WEBERP_Items i ON i.Id=ml.ItemId
    WHERE lcp.LCId=@LCId AND ml.CompanyID=@CompanyID;

    -- تحديث Valuation Layers والـ AverageCost
    UPDATE svl SET
        svl.UnitCost     = ml.UnitCost,
        svl.Value        = ml.TotalCost,
        svl.RemainingValue = ml.TotalCost
    FROM WEBERP_StockValuationLayers svl
    JOIN WEBERP_StockMoveLines ml ON ml.Id=svl.MoveLineId
    JOIN WEBERP_LandedCostPickings lcp ON lcp.PickingId=ml.PickingId
    WHERE lcp.LCId=@LCId;

    -- تحديث حالة التكلفة
    UPDATE WEBERP_LandedCosts SET State='done', IsPosted=1, ModifiedBy=@UserId, ModifiedAt=GETDATE() WHERE Id=@LCId;

    COMMIT;
END;
```

### SP 7: WEBERP_Vendors_GetStatement — كشف حساب المورد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Vendors_GetStatement]
    @VendorId  INT,
    @FromDate  DATE = NULL,
    @ToDate    DATE = NULL,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH Statement AS (
        -- الفواتير
        SELECT
            'فاتورة' AS DocTypeAr, 'Bill' AS DocType,
            Id AS DocId, Name AS DocNumber,
            BillDate AS DocDate, DueDate,
            TotalAmount AS Debit, 0 AS Credit,
            AmountPaid, AmountResidual AS Remaining,
            PaymentState AS Status, IsPosted
        FROM WEBERP_VendorBills
        WHERE VendorId=@VendorId AND CompanyID=@CompanyID
          AND IsPosted=1 AND (IsCanceled=0 OR IsCanceled IS NULL)
          AND (@FromDate IS NULL OR BillDate>=@FromDate)
          AND (@ToDate   IS NULL OR BillDate<=@ToDate)

        UNION ALL

        -- مرتجعات الشراء
        SELECT
            'مرتجع', 'Return',
            Id, Name, ReturnDate, NULL,
            0, NetAmount,
            0, NetAmount,
            State, IsPosted
        FROM WEBERP_PurchaseReturns
        WHERE VendorId=@VendorId AND CompanyID=@CompanyID
          AND IsPosted=1 AND (IsCanceled=0 OR IsCanceled IS NULL)
          AND (@FromDate IS NULL OR ReturnDate>=@FromDate)
          AND (@ToDate   IS NULL OR ReturnDate<=@ToDate)
    )
    SELECT *,
        SUM(Debit - Credit) OVER (ORDER BY DocDate, DocId) AS RunningBalance
    FROM Statement
    ORDER BY DocDate, DocId;
END;
```

### SP 8: WEBERP_Vendors_GetBestPrice — أفضل سعر للصنف
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Vendors_GetBestPrice]
    @ItemId    INT,
    @Quantity  DECIMAL(18,4) = 1,
    @UOMId     INT = NULL,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 5
        v.Id AS VendorId,
        v.Code AS VendorCode,
        v.NameAr AS VendorName,
        vpl.Price,
        vpl.CurrencyCode,
        vpl.MinQty,
        vpl.LeadTimeDays,
        v.PaymentTermId,
        -- آخر سعر فعلي من PO
        (SELECT TOP 1 pol.UnitPrice
         FROM WEBERP_PurchaseOrderLines pol
         JOIN WEBERP_PurchaseOrders po ON po.Id=pol.POId
         WHERE pol.ItemId=@ItemId AND po.VendorId=v.Id
           AND po.State IN ('purchase','done')
           AND po.CompanyID=@CompanyID
         ORDER BY po.PODate DESC) AS LastPOPrice,
        -- آخر تاريخ شراء
        (SELECT TOP 1 po.PODate
         FROM WEBERP_PurchaseOrders po
         JOIN WEBERP_PurchaseOrderLines pol ON pol.POId=po.Id
         WHERE pol.ItemId=@ItemId AND po.VendorId=v.Id
           AND po.CompanyID=@CompanyID
         ORDER BY po.PODate DESC) AS LastPurchaseDate
    FROM WEBERP_VendorPriceLists vpl
    JOIN WEBERP_Vendors v ON v.Id=vpl.VendorId
    WHERE vpl.ItemId=@ItemId
      AND vpl.MinQty <= @Quantity
      AND vpl.IsActive=1
      AND v.IsActive=1 AND v.Blacklisted=0
      AND vpl.CompanyID=@CompanyID
      AND (vpl.IsCanceled=0 OR vpl.IsCanceled IS NULL)
      AND (vpl.StartDate IS NULL OR vpl.StartDate<=GETDATE())
      AND (vpl.EndDate   IS NULL OR vpl.EndDate  >=GETDATE())
    ORDER BY vpl.Price ASC;
END;
```

---

## 🔄 دورة حياة أمر الشراء

```
┌─────────────────────────────────────────────────────────────────┐
│              دورة حياة أمر الشراء الكاملة                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  [Purchase Request]                                              │
│        │  draft → submitted → approved                           │
│        ↓                                                         │
│  [RFQ / Draft PO]                                               │
│        │  يُنشأ من PR أو يدوياً                                 │
│        │  يُرسل للمورد لطلب السعر                               │
│        ↓                                                         │
│  [Purchase Order] ← Confirm                                     │
│        │  State: purchase                                        │
│        │  ينشئ GRN (StockPicking) تلقائياً                     │
│        │  ينشئ IncomingQty في ItemStock                         │
│        │                                                         │
│        ↓──────────────────────────────────┐                     │
│  [GRN / Validate]                         │                     │
│        │  تحديث QtyReceived              │                     │
│        │  تحديث ItemStock Quantity       │                     │
│        │  إنشاء ValuationLayer           │                     │
│        │  إنشاء قيد: Dr Inv / Cr Purch  │                     │
│        │                                  │ Backorder           │
│        ↓                                  │ (كميات ناقصة)      │
│  [Vendor Bill]                            │                     │
│        │  3-Way Match: PO+GRN+Bill       ←┘                    │
│        │  State: draft → posted                                  │
│        │  قيد: Dr Purchase / Cr Vendor                          │
│        │  تحديث رصيد المورد                                     │
│        ↓                                                         │
│  [Payment]                                                       │
│        │  سند صرف أو تحويل بنكي                                │
│        │  تسوية مع الفاتورة                                     │
│        │  PaymentState: paid                                     │
│        │                                                         │
│  [Return] (اختياري)                                             │
│        │  إذا كان هناك مرتجع                                    │
│        │  ينشئ StockPicking (outgoing → supplier)               │
│        │  ينشئ Credit Note عند المورد                          │
│        │  قيد عكسي: Dr Vendor / Cr Inventory                  │
│        ↓                                                         │
│  [Landed Costs] (اختياري)                                       │
│        │  توزيع تكاليف الشحن والجمارك                         │
│        │  تحديث UnitCost في Valuation Layers                   │
│        │  تحديث AverageCost في ItemStock                       │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔗 Integration Points

### مع موديول المخازن
```
PO Confirm   → ينشئ StockPicking تلقائياً (Type: incoming)
GRN Validate → يستدعي WEBERP_StockPickings_Validate
             → يحدث QtyReceived في PO Lines
             → يخفض IncomingQty
             → ينشئ ItemLots إذا HasLotTracking=1
LandedCosts  → يحدث UnitCost في ValuationLayers
```

### مع موديول الحسابات العامة
```
GRN Validate    → Dr Inventory / Cr GR-IR Account (حساب بضاعة في الطريق)
Bill Post       → Dr GR-IR Account / Cr Vendor
             أو Dr Purchase / Cr Vendor (مباشرة)
Payment         → Dr Vendor / Cr Cash/Bank
PurchaseReturn  → Dr Vendor / Cr Inventory
LandedCosts     → Dr Inventory / Cr Vendor (أو Expenses)
```

### مع موديول النقدية والبنوك
```
Vendor Payment → ينشئ سند صرف نقدي أو تحويل بنكي
              → يُسوى مع الفاتورة (Partial Reconcile)
              → يحدث AmountPaid + PaymentState في VendorBills
              → يحدث CurrentBalance في Vendors
```

---

## 📊 التقارير

### SP 9: WEBERP_Purchase_AgingReport — تقرير أعمار الديون
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Purchase_AgingReport]
    @AsOfDate  DATE = NULL,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @AsOfDate IS NULL SET @AsOfDate = GETDATE();

    SELECT
        v.Id AS VendorId,
        v.Code, v.NameAr,
        -- إجمالي المستحق
        SUM(b.AmountResidual)                                           AS TotalDue,
        -- تصنيف حسب العمر
        SUM(CASE WHEN DATEDIFF(DAY, b.DueDate, @AsOfDate) <= 0        THEN b.AmountResidual ELSE 0 END) AS Current,
        SUM(CASE WHEN DATEDIFF(DAY, b.DueDate, @AsOfDate) BETWEEN 1 AND 30  THEN b.AmountResidual ELSE 0 END) AS Days1_30,
        SUM(CASE WHEN DATEDIFF(DAY, b.DueDate, @AsOfDate) BETWEEN 31 AND 60 THEN b.AmountResidual ELSE 0 END) AS Days31_60,
        SUM(CASE WHEN DATEDIFF(DAY, b.DueDate, @AsOfDate) BETWEEN 61 AND 90 THEN b.AmountResidual ELSE 0 END) AS Days61_90,
        SUM(CASE WHEN DATEDIFF(DAY, b.DueDate, @AsOfDate) > 90         THEN b.AmountResidual ELSE 0 END) AS Over90
    FROM WEBERP_VendorBills b
    JOIN WEBERP_Vendors v ON v.Id=b.VendorId
    WHERE b.CompanyID=@CompanyID AND b.BranchID=@BranchID
      AND b.State='posted'
      AND b.PaymentState NOT IN ('paid','reversed')
      AND b.AmountResidual > 0
      AND (b.IsCanceled=0 OR b.IsCanceled IS NULL)
      AND b.BillDate <= @AsOfDate
    GROUP BY v.Id, v.Code, v.NameAr
    HAVING SUM(b.AmountResidual) > 0
    ORDER BY TotalDue DESC;
END;
```

### SP 10: WEBERP_Purchase_PerformanceReport — تقرير أداء الموردين
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Purchase_PerformanceReport]
    @FromDate  DATE,
    @ToDate    DATE,
    @VendorId  INT = NULL,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.Id AS VendorId,
        v.Code, v.NameAr,
        -- الطلبات
        COUNT(DISTINCT po.Id)                    AS TotalPOs,
        SUM(po.TotalAmountLocal)                 AS TotalPOValue,
        -- الاستلام
        AVG(DATEDIFF(DAY, po.PODate,
            (SELECT MIN(p.DateDone) FROM WEBERP_StockPickings p
             WHERE p.SourceDocType='PurchaseOrder' AND p.SourceDocId=po.Id)))
                                                 AS AvgLeadTimeDays,
        -- نسبة الاستلام في الوقت المحدد
        AVG(CASE
            WHEN p.DateDone <= po.ExpectedDate   THEN 100.0
            WHEN p.DateDone IS NULL              THEN 0
            ELSE 0
        END)                                     AS OnTimeDeliveryPercent,
        -- المرتجعات
        COUNT(DISTINCT ret.Id)                   AS TotalReturns,
        SUM(ISNULL(ret.NetAmount,0))             AS TotalReturnValue,
        -- نسبة المرتجعات
        CASE WHEN SUM(po.TotalAmountLocal) > 0
             THEN SUM(ISNULL(ret.NetAmount,0)) / SUM(po.TotalAmountLocal) * 100
             ELSE 0
        END                                      AS ReturnPercent
    FROM WEBERP_PurchaseOrders po
    JOIN WEBERP_Vendors v ON v.Id=po.VendorId
    LEFT JOIN WEBERP_StockPickings p ON p.SourceDocType='PurchaseOrder'
        AND p.SourceDocId=po.Id AND p.State='done'
    LEFT JOIN WEBERP_PurchaseReturns ret ON ret.POId=po.Id
        AND ret.State='done' AND (ret.IsCanceled=0 OR ret.IsCanceled IS NULL)
    WHERE po.CompanyID=@CompanyID AND po.BranchID=@BranchID
      AND po.State IN ('purchase','done')
      AND po.PODate BETWEEN @FromDate AND @ToDate
      AND (@VendorId IS NULL OR po.VendorId=@VendorId)
      AND (po.IsCanceled=0 OR po.IsCanceled IS NULL)
    GROUP BY v.Id, v.Code, v.NameAr
    ORDER BY TotalPOValue DESC;
END;
```

---

## 🌱 Seed Data

```sql
-- تصنيفات الموردين الأساسية
INSERT INTO WEBERP_VendorCategories (Code,NameAr,NameEn,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt) VALUES
('SUPPLIER',   'موردون',         'Suppliers',     1, 1, 1, 1, GETDATE()),
('LOCAL',      'موردون محليون',  'Local',         1, 1, 1, 1, GETDATE()),
('FOREIGN',    'موردون أجانب',   'Foreign',       1, 1, 1, 1, GETDATE()),
('SERVICES',   'مقدمو خدمات',   'Service Providers',1,1,1,1,GETDATE()),
('GOVERNMENT', 'جهات حكومية',    'Government',    1, 1, 1, 1, GETDATE());
```

---

## 🎛️ FormControls الرئيسية

### FormCode 601 — الموردون (HasTabs=1)
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,TabName,CompanyID,BranchID) VALUES
-- Tab: البيانات الأساسية
(1,  601,'Code',              'الكود',              'Code',         1,'nvarchar',1,1,1,120,'الأساسية',1,1),
(2,  601,'NameAr',            'الاسم عربي',          'Name AR',      1,'nvarchar',1,1,1,280,'الأساسية',1,1),
(3,  601,'NameEn',            'الاسم إنجليزي',       'Name EN',      1,'nvarchar',0,1,1,280,'الأساسية',1,1),
(4,  601,'CategoryId',        'التصنيف',             'Category',     2,'int',     0,1,2,200,'الأساسية',1,1),
(5,  601,'VendorType',        'النوع',               'Type',         2,'nvarchar',1,0,2,150,'الأساسية',1,1),
(6,  601,'TaxNumber',         'الرقم الضريبي',        'Tax No',       1,'nvarchar',0,1,2,180,'الأساسية',1,1),
(7,  601,'Phone1',            'الهاتف',              'Phone',        1,'nvarchar',0,1,3,150,'الأساسية',1,1),
(8,  601,'Email',             'البريد',              'Email',        1,'nvarchar',0,1,3,220,'الأساسية',1,1),
(9,  601,'ContactPerson',     'مسؤول التواصل',        'Contact',      1,'nvarchar',0,0,3,200,'الأساسية',1,1),
(10, 601,'IsActive',          'فعال',                'Active',       3,'bit',     0,0,4,100,'الأساسية',1,1),
-- Tab: المحاسبة
(11, 601,'AccountId',         'الحساب المحاسبي',     'Account',      2,'int',     1,0,1,280,'المحاسبة',1,1),
(12, 601,'CurrencyCode',      'العملة',              'Currency',     2,'nvarchar',1,0,1,150,'المحاسبة',1,1),
(13, 601,'PaymentTermId',     'شروط الدفع',           'Payment Terms',2,'int',     0,0,2,200,'المحاسبة',1,1),
(14, 601,'CreditLimit',       'حد الائتمان',          'Credit Limit', 6,'decimal', 0,0,2,150,'المحاسبة',1,1),
(15, 601,'WithholdingTaxRate','نسبة الاستقطاع',       'Withholding%', 6,'decimal', 0,0,3,150,'المحاسبة',1,1),
-- Tab: البنك
(16, 601,'BankName',          'اسم البنك',           'Bank',         1,'nvarchar',0,0,1,200,'البنك',1,1),
(17, 601,'BankAccount',       'رقم الحساب',          'Account No',   1,'nvarchar',0,0,1,180,'البنك',1,1),
(18, 601,'IBAN',              'IBAN',                'IBAN',         1,'nvarchar',0,0,2,280,'البنك',1,1),
(19, 601,'SwiftCode',         'Swift Code',          'Swift',        1,'nvarchar',0,0,2,150,'البنك',1,1);
```

---

## 📐 Indexes

```sql
CREATE INDEX IX_PO_Vendor        ON WEBERP_PurchaseOrders(VendorId, CompanyID);
CREATE INDEX IX_PO_State         ON WEBERP_PurchaseOrders(State, CompanyID);
CREATE INDEX IX_PO_Date          ON WEBERP_PurchaseOrders(PODate DESC);
CREATE INDEX IX_POLine_Item      ON WEBERP_PurchaseOrderLines(ItemId);
CREATE INDEX IX_POLine_PO        ON WEBERP_PurchaseOrderLines(POId);
CREATE INDEX IX_Bill_Vendor      ON WEBERP_VendorBills(VendorId, CompanyID);
CREATE INDEX IX_Bill_State       ON WEBERP_VendorBills(State, PaymentState);
CREATE INDEX IX_Bill_DueDate     ON WEBERP_VendorBills(DueDate) WHERE PaymentState <> 'paid';
CREATE INDEX IX_VendorPrice_Item ON WEBERP_VendorPriceLists(ItemId, VendorId);
CREATE INDEX IX_PR_State         ON WEBERP_PurchaseRequests(State, CompanyID);
```

---

## 🗂️ Menus
```sql
INSERT INTO Menus (MenuCode,MenuNameAr,MenuNameEn,ParentCode,IsParent,FormCode,[Order],IsActive,CompanyID,BranchID) VALUES
(600,'المشتريات',              'Purchasing',           NULL,1,NULL, 6,1,1,1),
-- إعدادات
(601,'إعدادات المشتريات',      'Configuration',        600,1,NULL,  1,1,1,1),
(602,'تصنيفات الموردين',       'Vendor Categories',    601,0,600,   1,1,1,1),
(603,'شروط الدفع',             'Payment Terms',        601,0,NULL,  2,1,1,1),
-- الموردون
(610,'الموردون',               'Vendors',              600,0,601,   2,1,1,1),
(611,'أسعار الموردين',         'Vendor Prices',        600,0,602,   3,1,1,1),
-- العمليات
(620,'العمليات',               'Operations',           600,1,NULL,  4,1,1,1),
(621,'طلبات الشراء',           'Purchase Requests',    620,0,610,   1,1,1,1),
(622,'عروض الأسعار (RFQ)',     'RFQs',                 620,0,620,   2,1,1,1),
(623,'أوامر الشراء',           'Purchase Orders',      620,0,630,   3,1,1,1),
(624,'استلام البضاعة',         'Goods Receipts',       620,0,NULL,  4,1,1,1),
(625,'التكاليف الإضافية',      'Landed Costs',         620,0,NULL,  5,1,1,1),
-- الفوترة
(630,'الفوترة',                'Billing',              600,1,NULL,  5,1,1,1),
(631,'فواتير الموردين',        'Vendor Bills',         630,0,640,   1,1,1,1),
(632,'مرتجعات الشراء',         'Purchase Returns',     630,0,650,   2,1,1,1),
(633,'مدفوعات الموردين',       'Vendor Payments',      630,0,660,   3,1,1,1),
-- التقارير
(640,'التقارير',               'Reporting',            600,1,NULL,  6,1,1,1),
(641,'كشف حساب المورد',        'Vendor Statement',     640,0,NULL,  1,1,1,1),
(642,'أعمار الديون',           'Aging Report',         640,0,NULL,  2,1,1,1),
(643,'أداء الموردين',          'Vendor Performance',   640,0,NULL,  3,1,1,1),
(644,'أفضل أسعار الأصناف',    'Best Prices',          640,0,NULL,  4,1,1,1);
```

---

## ⚠️ تحذيرات لـ Claude Code

### قواعد أساسية:
1. **3-Way Match** — الفاتورة لا تُرحّل إذا كانت الكميات تختلف بنسبة > 5% عن PO وGRN
2. **Blacklisted Vendor** — لا يُسمح بإنشاء PO لمورد على القائمة السوداء
3. **رصيد المورد** — يزيد عند ترحيل الفاتورة، يخفض عند الدفع فقط
4. **IncomingQty** — يزيد عند تأكيد PO، يخفض عند تأكيد GRN
5. **QtyBilled** — لا يتجاوز QtyReceived في حالة الفوترة عند الاستلام
6. **LandedCosts** — تُطبق بعد GRN وقبل أو بعد Bill
7. **GR/IR Account** — حساب وسيط بين GRN والفاتورة — رصيده يجب أن يكون صفراً في نهاية الشهر
8. **WithholdingTax** — الاستقطاع يُخصم من المبلغ المدفوع للمورد ويُحمَّل على حساب منفصل
9. **Credit Note** — مرتجع الشراء ينشئ Credit Note عند المورد تُسوّى مع الفاتورة
10. **CommercialRegExpiry** — تحذير إذا كان السجل التجاري للمورد منتهياً

---

## 🌱 Seed Data الإلزامية

### Sequence Numbers — ترقيم المستندات
```sql
CREATE TABLE [dbo].[WEBERP_DocumentSequences] (
    [Id]            INT           IDENTITY(1,1) PRIMARY KEY,
    [DocType]       NVARCHAR(30)  NOT NULL,
    -- PurchaseRequest / PurchaseOrder / VendorBill / PurchaseReturn / LandedCost
    [Prefix]        NVARCHAR(20)  NOT NULL,
    -- PR / PO / BILL / PRET / LC
    [CurrentNumber] INT           NOT NULL DEFAULT 0,
    [Padding]       INT           NOT NULL DEFAULT 4,
    -- عدد الأرقام: 4 → 0001
    [FiscalYearId]  INT           NULL,
    -- NULL = تسلسل مستمر بغض النظر عن السنة
    [CompanyID]     INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy]     INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_DocSequence UNIQUE (DocType, ISNULL(FiscalYearId,-1), CompanyID, BranchID)
);

CREATE OR ALTER PROCEDURE [dbo].[WEBERP_DocumentSequences_GetNext]
    @DocType   NVARCHAR(30),
    @CompanyID INT, @BranchID INT,
    @NextName  NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Prefix NVARCHAR(20), @Next INT, @Padding INT;

    UPDATE WEBERP_DocumentSequences
    SET CurrentNumber = CurrentNumber + 1
    OUTPUT DELETED.Prefix, INSERTED.CurrentNumber, DELETED.Padding
    INTO @T(Prefix, CurrentNumber, Padding)
    WHERE DocType=@DocType AND CompanyID=@CompanyID AND BranchID=@BranchID;

    DECLARE @T TABLE (Prefix NVARCHAR(20), CurrentNumber INT, Padding INT);

    SELECT @Prefix=Prefix, @Next=CurrentNumber, @Padding=Padding FROM @T;

    SET @NextName = @Prefix + '/' +
                    CAST(YEAR(GETDATE()) AS NVARCHAR) + '/' +
                    RIGHT('0000000000' + CAST(@Next AS NVARCHAR), @Padding);
END;

-- Seed
INSERT INTO WEBERP_DocumentSequences (DocType, Prefix, CurrentNumber, Padding, CompanyID, BranchID, CreatedBy, CreatedAt)
VALUES
('PurchaseRequest', 'PR',   0, 4, 1, 1, 1, GETDATE()),
('PurchaseOrder',   'PO',   0, 4, 1, 1, 1, GETDATE()),
('VendorBill',      'BILL', 0, 4, 1, 1, 1, GETDATE()),
('PurchaseReturn',  'PRET', 0, 4, 1, 1, 1, GETDATE()),
('LandedCost',      'LC',   0, 4, 1, 1, 1, GETDATE());
```

### شروط الدفع — Seed
```sql
INSERT INTO WEBERP_AccountPaymentTerms (Name, NameAr, Note, IsActive, CompanyID, BranchID, CreatedBy, CreatedAt)
VALUES
('Immediate',  'فوري',          'الدفع فور الاستلام',    1, 1, 1, 1, GETDATE()),
('Net 15',     '15 يوم',        'الدفع خلال 15 يوم',     1, 1, 1, 1, GETDATE()),
('Net 30',     '30 يوم',        'الدفع خلال 30 يوم',     1, 1, 1, 1, GETDATE()),
('Net 45',     '45 يوم',        'الدفع خلال 45 يوم',     1, 1, 1, 1, GETDATE()),
('Net 60',     '60 يوم',        'الدفع خلال 60 يوم',     1, 1, 1, 1, GETDATE()),
('Net 90',     '90 يوم',        'الدفع خلال 90 يوم',     1, 1, 1, 1, GETDATE()),
('30/60/90',   '30/60/90',      '30% عند 30 يوم، 30% عند 60، 40% عند 90', 1, 1, 1, 1, GETDATE());

-- سطور 30/60/90
DECLARE @TermId INT = (SELECT Id FROM WEBERP_AccountPaymentTerms WHERE Name='30/60/90' AND CompanyID=1);
INSERT INTO WEBERP_AccountPaymentTermLines (PaymentTermId, Value, ValueAmount, DelayType, Days, SortOrder, CompanyID, BranchID)
VALUES
(@TermId, 'percent', 30, 'days_after_invoice', 30, 1, 1, 1),
(@TermId, 'percent', 30, 'days_after_invoice', 60, 2, 1, 1),
(@TermId, 'balance', 0,  'days_after_invoice', 90, 3, 1, 1);
```

---

## 🔄 دورة حياة كاملة — مثال عملي

```
1. طلب الشراء (PR)
   ─────────────────
   RequestedBy: محمد (IT Dept)
   Item: لابتوب Dell × 5
   EstimatedPrice: 3,000 ريال/قطعة
   State: draft → submitted → approved

2. طلب عروض الأسعار (RFQ)
   ──────────────────────────
   يُنشأ PO بحالة draft من PR
   يُرسل لـ 3 موردين
   نختار أفضل عرض: Vendor A بسعر 2,800

3. تأكيد أمر الشراء (PO)
   ──────────────────────
   EXEC WEBERP_PurchaseOrders_Confirm @POId, ...
   ↓ ينشئ تلقائياً:
     StockPicking: GRN-PO/2025/0001
     State: purchase
     IncomingQty في ItemStock += 5

4. استلام البضاعة (GRN)
   ─────────────────────
   يفتح المخزنجي الـ GRN
   يدخل QtyDone = 5 (أو أقل → Backorder)
   EXEC WEBERP_GRN_Validate @PickingId, ...
   ↓
   ItemStock.Quantity += 5
   ItemStock.IncomingQty -= 5
   ValuationLayer ينشأ بتكلفة 2,800/قطعة
   PO.QtyReceived = 5
   PO.ReceiptStatus = 'full'

5. التكاليف الإضافية (Landed Costs) — اختياري
   ──────────────────────────────────────────
   شحن: 500 ريال
   جمارك: 200 ريال
   EXEC WEBERP_LandedCosts_Validate @LCId, ...
   ↓
   التكلفة الفعلية = 2,800 + (700/5) = 2,940/قطعة
   ValuationLayer.UnitCost تتحدث
   AverageCost تتحدث

6. فاتورة المورد (Vendor Bill)
   ──────────────────────────
   EXEC WEBERP_VendorBills_Post @BillId, ...
   ↓ 3-Way Match: PO ✅ GRN ✅ Invoice ✅
   قيد محاسبي:
     Dr مشتريات  14,000
     Dr ضريبة     2,100
     Cr مورد     16,100
   Vendor.CurrentBalance += 16,100

7. الدفع للمورد
   ─────────────
   سند صرف أو تحويل بنكي
   تسوية مع الفاتورة
   VendorBill.PaymentState = 'paid'
   Vendor.CurrentBalance -= 16,100
```

---

## 📊 FormControls الكاملة

### FormCode 630 — أوامر الشراء (HasTabs=1)
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,TabName,CompanyID,BranchID) VALUES
-- Tab: رأس الأمر
(1,  630,'Name',            'رقم الأمر',          'PO Number',      1,'nvarchar',1,1,1,150,'الرأس',1,1),
(2,  630,'PODate',          'تاريخ الأمر',         'PO Date',        5,'date',    1,1,1,150,'الرأس',1,1),
(3,  630,'VendorId',        'المورد',              'Vendor',         2,'int',     1,1,1,280,'الرأس',1,1),
(4,  630,'VendorRef',       'مرجع المورد',         'Vendor Ref',     1,'nvarchar',0,1,2,180,'الرأس',1,1),
(5,  630,'WarehouseId',     'المستودع',            'Warehouse',      2,'int',     1,0,2,200,'الرأس',1,1),
(6,  630,'CurrencyCode',    'العملة',              'Currency',       2,'nvarchar',1,0,2,120,'الرأس',1,1),
(7,  630,'ExchangeRate',    'سعر الصرف',           'Rate',           6,'decimal', 1,0,3,120,'الرأس',1,1),
(8,  630,'PaymentTermId',   'شروط الدفع',           'Payment Terms',  2,'int',     0,0,3,200,'الرأس',1,1),
(9,  630,'ExpectedDate',    'تاريخ الاستلام المتوقع','Expected Date', 5,'date',    0,0,3,150,'الرأس',1,1),
(10, 630,'IncoTerms',       'Incoterms',           'Incoterms',      2,'nvarchar',0,0,4,120,'الرأس',1,1),
(11, 630,'ShippingMethod',  'طريقة الشحن',         'Shipping',       1,'nvarchar',0,0,4,200,'الرأس',1,1),
(12, 630,'Notes',           'ملاحظات',             'Notes',          7,'nvarchar',0,0,5,600,'الرأس',1,1),
-- Tab: الإجماليات (عرض فقط)
(13, 630,'SubTotal',        'المجموع قبل الضريبة', 'Subtotal',       6,'decimal', 0,0,1,'الإجماليات',150,1,1),
(14, 630,'TaxAmount',       'ضريبة القيمة المضافة','Tax Amount',     6,'decimal', 0,0,1,'الإجماليات',150,1,1),
(15, 630,'TotalAmount',     'الإجمالي',            'Total',          6,'decimal', 0,0,2,'الإجماليات',180,1,1),
(16, 630,'TotalAmountLocal','الإجمالي بالريال',    'Total (SAR)',     6,'decimal', 0,0,2,'الإجماليات',180,1,1);
```

### FormCode 640 — فواتير الموردين (HasTabs=1)
```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,TabName,CompanyID,BranchID) VALUES
(1,  640,'Name',               'رقم الفاتورة',        'Bill No',        1,'nvarchar',1,1,1,150,'الرأس',1,1),
(2,  640,'BillDate',           'تاريخ الفاتورة',      'Bill Date',      5,'date',    1,1,1,150,'الرأس',1,1),
(3,  640,'DueDate',            'تاريخ الاستحقاق',     'Due Date',       5,'date',    1,1,1,150,'الرأس',1,1),
(4,  640,'VendorId',           'المورد',              'Vendor',         2,'int',     1,1,2,280,'الرأس',1,1),
(5,  640,'VendorBillNumber',   'رقم فاتورة المورد',   'Vendor Inv No',  1,'nvarchar',0,1,2,200,'الرأس',1,1),
(6,  640,'POId',               'أمر الشراء',          'Purchase Order', 2,'int',     0,1,2,200,'الرأس',1,1),
(7,  640,'CurrencyCode',       'العملة',              'Currency',       2,'nvarchar',1,0,3,120,'الرأس',1,1),
(8,  640,'ExchangeRate',       'سعر الصرف',           'Rate',           6,'decimal', 1,0,3,120,'الرأس',1,1),
(9,  640,'WithholdingTaxAmount','ضريبة الاستقطاع',    'Withholding',    6,'decimal', 0,0,3,150,'الرأس',1,1),
(10, 640,'MatchStatus',        'حالة المطابقة',        'Match',         2,'nvarchar',0,1,4,150,'الرأس',1,1),
(11, 640,'Notes',              'ملاحظات',             'Notes',          7,'nvarchar',0,0,5,600,'الرأس',1,1),
-- الإجماليات
(12, 640,'SubTotal',           'المجموع',             'Subtotal',       6,'decimal', 0,0,1,'الإجماليات',150,1,1),
(13, 640,'TaxAmount',          'الضريبة',             'Tax',            6,'decimal', 0,0,1,'الإجماليات',150,1,1),
(14, 640,'TotalAmount',        'الإجمالي',            'Total',          6,'decimal', 0,0,2,'الإجماليات',180,1,1),
(15, 640,'AmountPaid',         'المدفوع',             'Paid',           6,'decimal', 0,0,2,'الإجماليات',150,1,1),
(16, 640,'AmountResidual',     'المتبقي',             'Remaining',      6,'decimal', 0,0,3,'الإجماليات',150,1,1);
```

---

## 📊 SPs إضافية ناقصة

### SP 11: WEBERP_PurchaseReturns_Post — ترحيل مرتجع الشراء
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PurchaseReturns_Post]
    @ReturnId  INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @VendorId INT, @TotalAmount DECIMAL(18,4),
            @TaxAmount DECIMAL(18,4), @VendorAccountId INT,
            @WarehouseId INT, @ReturnName NVARCHAR(30);

    SELECT @VendorId=VendorId, @TotalAmount=TotalAmount,
           @TaxAmount=TaxAmount, @WarehouseId=WarehouseId,
           @ReturnName=Name
    FROM WEBERP_PurchaseReturns
    WHERE Id=@ReturnId AND State='confirmed' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @TotalAmount IS NULL
    BEGIN ROLLBACK; RAISERROR('المرتجع غير موجود أو مرحّل',16,1); RETURN; END

    SELECT @VendorAccountId=AccountId FROM WEBERP_Vendors WHERE Id=@VendorId;

    -- 1. إنشاء StockPicking عكسي (outgoing → supplier)
    DECLARE @SuppliersLoc INT, @StockLoc INT, @PickingTypeId INT;
    SELECT @SuppliersLoc=Id FROM WEBERP_WarehouseLocations WHERE LocationType='supplier' AND CompanyID=@CompanyID;
    SELECT @StockLoc=StockLocationId FROM WEBERP_Warehouses WHERE Id=@WarehouseId;
    SELECT @PickingTypeId=Id FROM WEBERP_StockPickingTypes
    WHERE PickingTypeCode='outgoing' AND WarehouseId=@WarehouseId AND CompanyID=@CompanyID;

    DECLARE @PickingId INT;
    INSERT INTO WEBERP_StockPickings
        (Name,PickingTypeId,PickingDate,LocationId,LocationDestId,
         State,Origin,SourceDocType,SourceDocId,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@ReturnName,@PickingTypeId,GETDATE(),@StockLoc,@SuppliersLoc,
            'confirmed',@ReturnName,'PurchaseReturn',@ReturnId,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @PickingId=SCOPE_IDENTITY();

    INSERT INTO WEBERP_StockMoveLines
        (PickingId,ItemId,LotId,UOMId,LocationId,LocationDestId,
         QtyDemand,QtyDemandBase,QtyDone,QtyDoneBase,UOMConvFactor,
         UnitCost,TotalCost,State,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @PickingId,rl.ItemId,rl.LotId,rl.UOMId,@StockLoc,@SuppliersLoc,
           rl.QtyReturned,rl.QtyReturned,rl.QtyReturned,rl.QtyReturned,1,
           rl.UnitPrice,rl.TotalAmount,'done',@CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_PurchaseReturnLines rl
    WHERE rl.ReturnId=@ReturnId AND (rl.IsCanceled=0 OR rl.IsCanceled IS NULL);

    -- 2. تحديث المخزون
    UPDATE s SET s.Quantity=s.Quantity-rl.QtyReturned, s.ModifiedBy=@UserId, s.ModifiedAt=GETDATE()
    FROM WEBERP_ItemStock s
    JOIN WEBERP_PurchaseReturnLines rl ON rl.ItemId=s.ItemId
    WHERE rl.ReturnId=@ReturnId AND s.WarehouseId=@WarehouseId AND s.CompanyID=@CompanyID;

    -- 3. القيد المحاسبي العكسي
    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@ReturnName,GETDATE(),'PurchaseReturn','مرتجع شراء: '+@ReturnName,
            @TotalAmount,@TotalAmount,1,GETDATE(),@UserId,@CompanyID,@BranchID,@UserId,GETDATE());
    SET @JId=SCOPE_IDENTITY();

    -- مدين: حساب المورد (خصم من مديونيته)
    INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@JId,@VendorAccountId,@TotalAmount,0,'مرتجع للمورد',@CompanyID,@BranchID,@UserId,GETDATE());

    -- دائن: المخزون
    INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @JId,ISNULL(i.InventoryAccountId,c.InventoryAccountId),0,SUM(rl.TotalAmount),'مخزون مُرتجع',@CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_PurchaseReturnLines rl
    JOIN WEBERP_Items i ON i.Id=rl.ItemId
    JOIN WEBERP_ItemCategories c ON c.Id=i.CategoryId
    WHERE rl.ReturnId=@ReturnId AND (rl.IsCanceled=0 OR rl.IsCanceled IS NULL)
    GROUP BY ISNULL(i.InventoryAccountId,c.InventoryAccountId);

    -- 4. تحديث رصيد المورد
    UPDATE WEBERP_Vendors SET CurrentBalance=CurrentBalance-@TotalAmount,
        ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@VendorId;

    -- 5. تحديث حالة المرتجع
    UPDATE WEBERP_PurchaseReturns SET
        State='done',IsPosted=1,
        JournalEntryId=@JId,PickingId=@PickingId,
        ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE Id=@ReturnId;

    COMMIT;
END;
```

### SP 12: WEBERP_PurchaseRequests_Approve — موافقة على طلب الشراء
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PurchaseRequests_Approve]
    @RequestId INT,
    @Action    NVARCHAR(10),
    -- approve / reject / return
    @Reason    NVARCHAR(300) = NULL,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_PurchaseRequests
        WHERE Id=@RequestId AND State='submitted' AND CompanyID=@CompanyID
    )
    BEGIN RAISERROR('الطلب غير موجود أو ليس في حالة تسمح بالموافقة',16,1); RETURN; END

    UPDATE WEBERP_PurchaseRequests SET
        State = CASE @Action
            WHEN 'approve' THEN 'approved'
            WHEN 'reject'  THEN 'rejected'
            WHEN 'return'  THEN 'draft'
            ELSE State
        END,
        ApprovedBy   = CASE WHEN @Action='approve' THEN @UserId ELSE ApprovedBy END,
        ApprovedDate = CASE WHEN @Action='approve' THEN GETDATE() ELSE ApprovedDate END,
        RejectedBy   = CASE WHEN @Action='reject'  THEN @UserId ELSE RejectedBy END,
        RejectedDate = CASE WHEN @Action='reject'  THEN GETDATE() ELSE RejectedDate END,
        RejectReason = CASE WHEN @Action IN ('reject','return') THEN @Reason ELSE RejectReason END,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@RequestId;
END;
```

### SP 13: WEBERP_PurchaseOrders_CreateFromRequest — إنشاء PO من PR
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_PurchaseOrders_CreateFromRequest]
    @RequestId   INT,
    @VendorId    INT,
    @CompanyID   INT, @BranchID INT,
    @UserId      INT,
    @NewPOId     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- التحقق
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_PurchaseRequests
        WHERE Id=@RequestId AND State='approved' AND CompanyID=@CompanyID
    )
    BEGIN ROLLBACK; RAISERROR('الطلب غير معتمد',16,1); RETURN; END

    -- جلب اسم تسلسلي
    DECLARE @POName NVARCHAR(50);
    EXEC WEBERP_DocumentSequences_GetNext 'PurchaseOrder', @CompanyID, @BranchID, @POName OUTPUT;

    DECLARE @WarehouseId INT;
    SELECT @WarehouseId=WarehouseId FROM WEBERP_PurchaseRequests WHERE Id=@RequestId;

    -- إنشاء PO
    INSERT INTO WEBERP_PurchaseOrders
        (Name,PODate,VendorId,WarehouseId,RequestId,State,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@POName,GETDATE(),@VendorId,ISNULL(@WarehouseId,1),@RequestId,'draft',
         @CompanyID,@BranchID,@UserId,GETDATE());
    SET @NewPOId=SCOPE_IDENTITY();

    -- نقل السطور من PR → PO
    INSERT INTO WEBERP_PurchaseOrderLines
        (POId,LineNumber,RequestLineId,ItemId,UOMId,QtyOrdered,UnitPrice,
         TaxIds,SubTotal,TotalAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @NewPOId, ROW_NUMBER() OVER (ORDER BY Id),
           Id, ItemId, UOMId,
           ISNULL(QtyApproved,QtyRequested),
           ISNULL(EstimatedPrice,0),
           NULL,
           ISNULL(QtyApproved,QtyRequested)*ISNULL(EstimatedPrice,0),
           ISNULL(QtyApproved,QtyRequested)*ISNULL(EstimatedPrice,0),
           @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_PurchaseRequestLines
    WHERE RequestId=@RequestId AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- تحديث حالة PR
    UPDATE WEBERP_PurchaseRequests SET State='po_created',ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@RequestId;
    UPDATE WEBERP_PurchaseRequestLines SET QtyOrdered=ISNULL(QtyApproved,QtyRequested),ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE RequestId=@RequestId;

    COMMIT;
END;
```

---

## 🔄 دورة حياة الـ GRN — التفاصيل

```
┌──────────────────────────────────────────────────────────────────┐
│                    استلام البضاعة (GRN)                           │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  [PO Confirmed] → ينشئ GRN تلقائياً بحالة confirmed             │
│        │                                                           │
│        ↓                                                           │
│  [GRN Opened]                                                      │
│    ← يفتح المخزنجي الـ GRN                                       │
│    ← يرى الكميات المطلوبة (QtyDemand)                            │
│    ← يُدخل الكميات الفعلية (QtyDone)                            │
│    ← يُدخل أرقام Lot/Serial إذا مطلوب                           │
│        │                                                           │
│        ├── QtyDone = QtyDemand ────→ [Validate Full]             │
│        │                                  ↓                       │
│        │                             PO.ReceiptStatus = 'full'    │
│        │                                                           │
│        └── QtyDone < QtyDemand ───→ [Validate Partial]           │
│                                           ↓                       │
│                                      PO.ReceiptStatus = 'partial' │
│                                           ↓                       │
│                                      [Backorder?]                  │
│                                       ├── نعم → GRN جديد          │
│                                       │         بالكمية المتبقية  │
│                                       └── لا  → إغلاق             │
└──────────────────────────────────────────────────────────────────┘
```

---

## 📐 Constraints إضافية

```sql
-- منع تكرار الموردين للصنف الواحد بنفس النطاق السعري
ALTER TABLE WEBERP_VendorPriceLists ADD
CONSTRAINT CHK_VendorPrice_Positive CHECK (Price > 0 AND MinQty > 0);

-- التحقق من تسلسل حالات PO
ALTER TABLE WEBERP_PurchaseOrders ADD
CONSTRAINT CHK_PO_BillingStatus CHECK (BillingStatus IN ('nothing','to_bill','fully_billed'));

ALTER TABLE WEBERP_PurchaseOrders ADD
CONSTRAINT CHK_PO_ReceiptStatus CHECK (ReceiptStatus IN ('nothing','partial','full'));

-- Qty Checks
ALTER TABLE WEBERP_PurchaseOrderLines ADD
CONSTRAINT CHK_POLine_Qty CHECK (QtyOrdered > 0 AND QtyReceived >= 0 AND QtyBilled >= 0);

ALTER TABLE WEBERP_VendorBills ADD
CONSTRAINT CHK_Bill_Dates CHECK (DueDate >= BillDate);

ALTER TABLE WEBERP_PurchaseReturns ADD
CONSTRAINT CHK_Return_Reason CHECK (
    ReturnReason IN ('defective','wrong_item','over_delivery','price_dispute','other')
);

-- Indexes إضافية
CREATE INDEX IX_VendorBills_Vendor     ON WEBERP_VendorBills(VendorId, PaymentState, CompanyID);
CREATE INDEX IX_VendorBills_POId       ON WEBERP_VendorBills(POId) WHERE POId IS NOT NULL;
CREATE INDEX IX_PurchaseRequests_State ON WEBERP_PurchaseRequests(State, Priority, CompanyID);
CREATE INDEX IX_POLines_Item           ON WEBERP_PurchaseOrderLines(ItemId, CompanyID);
CREATE INDEX IX_LandedCosts_State      ON WEBERP_LandedCosts(State, CompanyID);
```

---

## 🚦 Business Rules — جدول شامل

```
┌────────────────────────────┬────────────────────────────────────────┐
│ القاعدة                    │ التطبيق                                │
├────────────────────────────┼────────────────────────────────────────┤
│ Blacklisted Vendor         │ منع PO Confirm إذا Blacklisted=1      │
├────────────────────────────┼────────────────────────────────────────┤
│ 3-Way Match                │ Bill لا تُرحَّل إذا                   │
│                            │ |BillQty - GRNQty| > 5%               │
│                            │ أو |BillPrice - POPrice| > 2%         │
├────────────────────────────┼────────────────────────────────────────┤
│ InvoicePolicy = 'receipt'  │ QtyBilled لا يتجاوز QtyReceived        │
│                            │ (لا فوترة قبل الاستلام)               │
├────────────────────────────┼────────────────────────────────────────┤
│ رصيد المورد               │ يزيد فقط عند ترحيل الفاتورة           │
│                            │ يخفض فقط عند الدفع أو المرتجع        │
├────────────────────────────┼────────────────────────────────────────┤
│ LandedCosts                │ تُطبق على GRN مؤكد (State=done)       │
│                            │ تُحدّث UnitCost في ValuationLayers    │
├────────────────────────────┼────────────────────────────────────────┤
│ Withholding Tax            │ يُخصم من المبلغ المدفوع للمورد        │
│                            │ المورد يستلم: Total - WithholdingTax  │
├────────────────────────────┼────────────────────────────────────────┤
│ PR Approval                │ طلب الشراء يحتاج موافقة               │
│                            │ قبل رفع PO                            │
├────────────────────────────┼────────────────────────────────────────┤
│ CommercialReg Expiry       │ تحذير إذا انتهى السجل التجاري         │
│                            │ للمورد (لا منع)                       │
├────────────────────────────┼────────────────────────────────────────┤
│ GR/IR Account              │ حساب وسيط بين GRN والفاتورة           │
│                            │ رصيده = 0 في نهاية الشهر             │
├────────────────────────────┼────────────────────────────────────────┤
│ Backorder                  │ GRN جزئي → GRN جديد للباقي           │
│                            │ PO لا يُغلق حتى QtyReceived=QtyOrdered│
└────────────────────────────┴────────────────────────────────────────┘
```
