# تحليل موديول الحركات المالية
## النقدية والبنوك والشيكات والتحويلات
## Analysis File for Claude Code — NozomSoft ERP
## نسخة شاملة — مستوى Odoo

---

## 🎯 نطاق الموديول

```
إعدادات
    ├── العملات وأسعار الصرف
    ├── الخزائن والصناديق
    └── الحسابات البنكية

وسائل الدفع
    ├── نقدي (Cash)
    ├── شيك (Check)
    ├── تحويل بنكي (Bank Transfer)
    ├── بطاقة ائتمان (Card)
    └── دفع إلكتروني (SADAD / SARIE / STCPay)

المقبوضات
    ├── سند القبض النقدي
    ├── شيك مستلم
    └── تحويل بنكي وارد

المدفوعات
    ├── سند الصرف النقدي
    ├── شيك صادر
    └── تحويل بنكي صادر

التحويلات الداخلية
    ├── خزينة → بنك
    ├── بنك → خزينة
    └── بنك → بنك

إدارة الشيكات
    ├── الشيكات المستلمة (دورة حياة كاملة)
    └── الشيكات الصادرة (دورة حياة كاملة)

تسوية البنك
    ├── كشف حساب البنك
    └── مطابقة الحركات

التقارير
    ├── رصيد الخزائن والبنوك
    ├── كشف الشيكات
    ├── تقرير التدفق النقدي
    └── أعمار الديون
```

---

## 🔑 FormCodes — نطاق هذا الموديول: 400–499

| FormCode | FormName | FormNameAr | HasTable | ملاحظة |
|----------|----------|------------|----------|--------|
| 400 | Currencies | العملات | 1 | |
| 401 | ExchangeRates | أسعار الصرف | 1 | |
| 410 | Safes | الخزائن | 1 | |
| 411 | BankAccounts | الحسابات البنكية | 1 | |
| 412 | PaymentMethods | وسائل الدفع | 1 | |
| 420 | CashReceipts | سندات القبض | 1 | HasTabs |
| 421 | CashPayments | سندات الصرف | 1 | HasTabs |
| 430 | BankTransactions | الحركات البنكية | 1 | |
| 440 | InternalTransfers | التحويلات الداخلية | 1 | |
| 450 | ReceivedChecks | الشيكات المستلمة | 1 | |
| 451 | IssuedChecks | الشيكات الصادرة | 1 | |
| 460 | BankReconciliation | تسوية البنك | 1 | |
| 461 | BankReconciliationLines | سطور التسوية | 1 | |
| 470 | CashClosing | إقفال الخزينة | 1 | |

---

## 🗄️ الجداول الكاملة

### 1. WEBERP_Currencies — العملات
```sql
CREATE TABLE [dbo].[WEBERP_Currencies] (
    [Id]             INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]           NVARCHAR(10)  NOT NULL,
    -- ISO 4217: SAR, USD, EUR, GBP, AED...
    [NameAr]         NVARCHAR(50)  NOT NULL,
    [NameEn]         NVARCHAR(50)  NULL,
    [Symbol]         NVARCHAR(10)  NULL,
    -- ر.س ، $ ، €
    [NumericCode]    NVARCHAR(3)   NULL,
    -- ISO 4217 Numeric: 682=SAR, 840=USD
    [DecimalPlaces]  INT           NOT NULL DEFAULT 2,
    [Rounding]       DECIMAL(10,6) NOT NULL DEFAULT 0.01,
    [ExchangeRate]   DECIMAL(18,6) NOT NULL DEFAULT 1,
    -- السعر الحالي نسبةً للعملة الأساسية
    [IsBaseCurrency] BIT           NOT NULL DEFAULT 0,
    -- عملة الشركة الأساسية — واحدة فقط لكل شركة
    [IsActive]       BIT           NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_CurrencyCode UNIQUE (Code, CompanyID, BranchID),
    CONSTRAINT CHK_OneBaseCurrency CHECK (IsBaseCurrency = 0 OR IsBaseCurrency = 1)
    -- التحقق من وجود عملة أساسية واحدة فقط يتم في الـ SP
);

-- Seed
-- INSERT INTO WEBERP_Currencies (Code,NameAr,NameEn,Symbol,DecimalPlaces,ExchangeRate,IsBaseCurrency,...) VALUES
-- ('SAR','ريال سعودي','Saudi Riyal','ر.س',2,1,1,...),
-- ('USD','دولار أمريكي','US Dollar','$',2,3.75,0,...),
-- ('EUR','يورو','Euro','€',2,4.10,0,...),
-- ('AED','درهم إماراتي','UAE Dirham','د.إ',2,1.02,0,...),
-- ('GBP','جنيه إسترليني','British Pound','£',2,4.75,0,...);
```

### 2. WEBERP_ExchangeRates — تاريخ أسعار الصرف
```sql
CREATE TABLE [dbo].[WEBERP_ExchangeRates] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [CurrencyCode] NVARCHAR(10)  NOT NULL,
    [RateDate]     DATE          NOT NULL,
    [BuyRate]      DECIMAL(18,6) NOT NULL,
    [SellRate]     DECIMAL(18,6) NOT NULL,
    [MidRate]      DECIMAL(18,6) NOT NULL,
    -- (BuyRate + SellRate) / 2
    [Source]       NVARCHAR(50)  NOT NULL DEFAULT 'manual',
    -- manual / SAMA / ECB / OpenExchangeRates
    [Notes]        NVARCHAR(200) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_ExchangeRate UNIQUE (CurrencyCode, RateDate, CompanyID, BranchID),
    CONSTRAINT CHK_ExchangeRate CHECK (BuyRate > 0 AND SellRate > 0)
);
```

### 3. WEBERP_Safes — الخزائن والصناديق
```sql
CREATE TABLE [dbo].[WEBERP_Safes] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]            NVARCHAR(20)  NOT NULL,
    [NameAr]          NVARCHAR(100] NOT NULL,
    [NameEn]          NVARCHAR(100) NULL,
    [SafeType]        NVARCHAR(20)  NOT NULL DEFAULT 'cash',
    -- cash / petty_cash / till (كاشير)
    [AccountId]       INT           NOT NULL,
    -- حساب الخزينة في الدليل المحاسبي
    [CurrencyCode]    NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [OpeningBalance]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CurrentBalance]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [MinBalance]      DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- الحد الأدنى — تنبيه إذا نزل عنه
    [MaxBalance]      DECIMAL(18,4) NULL,
    -- الحد الأقصى — لا يقبل إيداع يتجاوزه
    [ResponsibleId]   INT           NULL,
    -- المسؤول عن الخزينة (FK → Users)
    [BranchRef]       INT           NULL,
    -- الفرع المرتبط به
    [IsActive]        BIT           NOT NULL DEFAULT 1,
    [Notes]           NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_SafeCode UNIQUE (Code, CompanyID, BranchID)
);
```

### 4. WEBERP_BankAccounts — الحسابات البنكية
```sql
CREATE TABLE [dbo].[WEBERP_BankAccounts] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]             NVARCHAR(20)  NOT NULL,
    [BankName]         NVARCHAR(100] NOT NULL,
    [BankNameEn]       NVARCHAR(100) NULL,
    [BankCode]         NVARCHAR(20)  NULL,
    -- رمز البنك (الأرقام الأولى من IBAN)
    [AccountNumber]    NVARCHAR(50)  NOT NULL,
    [IBAN]             NVARCHAR(34)  NULL,
    [SwiftCode]        NVARCHAR(20)  NULL,
    [BranchName]       NVARCHAR(100] NULL,
    [BranchAddress]    NVARCHAR(300) NULL,
    -- الحساب المحاسبي
    [AccountId]        INT           NOT NULL,
    -- حساب البنك في الدليل المحاسبي
    [SuspenseAccountId] INT          NULL,
    -- حساب التعليق للمعاملات غير المطابقة
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [OpeningBalance]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [CurrentBalance]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [AvailableBalance] DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- رصيد متاح (بعد الشيكات المعلقة)
    [OverdraftLimit]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- حد السحب على المكشوف
    -- التواصل
    [ContactPerson]    NVARCHAR(100] NULL,
    [ContactPhone]     NVARCHAR(50)  NULL,
    [ContactEmail]     NVARCHAR(200) NULL,
    -- إعدادات
    [IsActive]         BIT           NOT NULL DEFAULT 1,
    [IsDefault]        BIT           NOT NULL DEFAULT 0,
    -- الحساب الافتراضي للمدفوعات
    [LastSyncDate]     DATE          NULL,
    -- آخر تاريخ مزامنة مع كشف البنك
    [Notes]            NVARCHAR(500) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_BankAccountNumber UNIQUE (AccountNumber, CompanyID, BranchID)
);
```

### 5. WEBERP_PaymentMethods — وسائل الدفع
```sql
CREATE TABLE [dbo].[WEBERP_PaymentMethods] (
    [Id]           INT           IDENTITY(1,1) PRIMARY KEY,
    [Code]         NVARCHAR(20)  NOT NULL,
    [NameAr]       NVARCHAR(50)  NOT NULL,
    [NameEn]       NVARCHAR(50)  NULL,
    [MethodType]   NVARCHAR(30)  NOT NULL,
    -- cash / check / bank_transfer / card /
    -- mada / stc_pay / apple_pay / sadad / sarie / other
    [AccountId]    INT           NOT NULL,
    -- الحساب المحاسبي المرتبط
    [SafeId]       INT           NULL,
    -- للنقدي: الخزينة المرتبطة
    [BankAccountId] INT          NULL,
    -- للبطاقات: الحساب البنكي المرتبط
    [RequiresRef]  BIT           NOT NULL DEFAULT 0,
    -- هل يستلزم رقم مرجعي؟ (رقم بطاقة، رقم تحويل)
    [IsIncoming]   BIT           NOT NULL DEFAULT 1,
    -- يُستخدم للمقبوضات؟
    [IsOutgoing]   BIT           NOT NULL DEFAULT 1,
    -- يُستخدم للمدفوعات؟
    [IsActive]     BIT           NOT NULL DEFAULT 1,
    [SortOrder]    INT           NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_PaymentMethodCode UNIQUE (Code, CompanyID, BranchID)
);

-- Seed
-- INSERT INTO WEBERP_PaymentMethods (Code,NameAr,NameEn,MethodType,RequiresRef,IsActive,...) VALUES
-- ('CASH',     'نقدي',              'Cash',          'cash',          0, 1,...),
-- ('CHECK_IN', 'شيك مستلم',         'Received Check','check',         1, 1,...),
-- ('CHECK_OUT','شيك صادر',          'Issued Check',  'check',         1, 1,...),
-- ('BANK_TRF', 'تحويل بنكي',        'Bank Transfer', 'bank_transfer', 1, 1,...),
-- ('MADA',     'مدى',               'Mada',          'mada',          1, 1,...),
-- ('STCPAY',   'STC Pay',           'STC Pay',       'stc_pay',       1, 1,...),
-- ('SADAD',    'سداد',              'SADAD',         'sadad',         1, 1,...),
-- ('SARIE',    'سريع',              'SARIE',         'sarie',         1, 1,...);
```

### 6. WEBERP_CashReceipts — سندات القبض
```sql
CREATE TABLE [dbo].[WEBERP_CashReceipts] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [ReceiptNumber]    NVARCHAR(30)  NOT NULL,
    -- RV/2025/0001
    [ReceiptDate]      DATE          NOT NULL DEFAULT GETDATE(),
    -- من يدفع
    [PayerType]        NVARCHAR(20)  NOT NULL DEFAULT 'customer',
    -- customer / vendor / employee / other
    [PayerId]          INT           NULL,
    -- FK حسب PayerType
    [PayerName]        NVARCHAR(200) NULL,
    -- للدافعين بدون حساب
    -- المبلغ
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [Amount]           DECIMAL(18,4) NOT NULL,
    -- بالعملة الأصلية
    [AmountLocal]      DECIMAL(18,4) NOT NULL,
    -- بالعملة المحلية = Amount × ExchangeRate
    -- وسيلة القبض
    [PaymentMethodId]  INT           NOT NULL REFERENCES WEBERP_PaymentMethods(Id),
    [SafeId]           INT           NULL REFERENCES WEBERP_Safes(Id),
    -- الخزينة المستلمة فيها (للنقدي)
    [BankAccountId]    INT           NULL REFERENCES WEBERP_BankAccounts(Id),
    -- الحساب البنكي (للتحويلات)
    [CheckId]          INT           NULL,
    -- رقم الشيك المستلم (FK → WEBERP_ReceivedChecks)
    -- الحساب المقابل
    [AccountId]        INT           NOT NULL,
    -- الحساب الدائن (العميل، المورد، إلخ)
    [CostCenterId]     INT           NULL,
    [AnalyticDistribution] NVARCHAR(MAX) NULL,
    -- البيان
    [DescriptionAr]    NVARCHAR(500) NULL,
    [DescriptionEn]    NVARCHAR(500) NULL,
    [Reference]        NVARCHAR(100] NULL,
    -- رقم المستند المرتبط (فاتورة، عقد...)
    [InvoiceRef]       NVARCHAR(100) NULL,
    -- الحالة
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / posted / cancelled / reconciled
    [JournalEntryId]   INT           NULL,
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    [PostedDate]       DATETIME2     NULL,
    [PostedBy]         INT           NULL,
    -- مصدر السند (التسوية مع الفاتورة)
    [SourceDocType]    NVARCHAR(50)  NULL,
    -- SalesInvoice / VendorBill / other
    [SourceDocId]      INT           NULL,
    [ReconcileId]      INT           NULL,
    -- FK → WEBERP_AccountFullReconcile
    -- الإلغاء
    [CancelledBy]      INT           NULL,
    [CancelledDate]    DATETIME2     NULL,
    [CancelReason]     NVARCHAR(300] NULL,
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_ReceiptNumber UNIQUE (ReceiptNumber, CompanyID, BranchID),
    CONSTRAINT CHK_Receipt_Amount CHECK (Amount > 0)
);
```

### 7. WEBERP_CashPayments — سندات الصرف
```sql
CREATE TABLE [dbo].[WEBERP_CashPayments] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [PaymentNumber]    NVARCHAR(30)  NOT NULL,
    -- PV/2025/0001
    [PaymentDate]      DATE          NOT NULL DEFAULT GETDATE(),
    [PayeeType]        NVARCHAR(20)  NOT NULL DEFAULT 'vendor',
    -- vendor / customer / employee / other
    [PayeeId]          INT           NULL,
    [PayeeName]        NVARCHAR(200] NULL,
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [Amount]           DECIMAL(18,4) NOT NULL,
    [AmountLocal]      DECIMAL(18,4) NOT NULL,
    -- وسيلة الصرف
    [PaymentMethodId]  INT           NOT NULL REFERENCES WEBERP_PaymentMethods(Id),
    [SafeId]           INT           NULL REFERENCES WEBERP_Safes(Id),
    [BankAccountId]    INT           NULL REFERENCES WEBERP_BankAccounts(Id),
    [CheckId]          INT           NULL,
    -- FK → WEBERP_IssuedChecks
    -- الحساب المقابل
    [AccountId]        INT           NOT NULL,
    [CostCenterId]     INT           NULL,
    [AnalyticDistribution] NVARCHAR(MAX) NULL,
    [DescriptionAr]    NVARCHAR(500) NULL,
    [DescriptionEn]    NVARCHAR(500) NULL,
    [Reference]        NVARCHAR(100) NULL,
    [InvoiceRef]       NVARCHAR(100) NULL,
    -- الضريبة
    [WithholdingTaxAmount] DECIMAL(18,4) NOT NULL DEFAULT 0,
    [NetPaidAmount]    AS (AmountLocal - WithholdingTaxAmount),
    -- الحالة
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    [JournalEntryId]   INT           NULL,
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    [PostedDate]       DATETIME2     NULL,
    [PostedBy]         INT           NULL,
    [SourceDocType]    NVARCHAR(50)  NULL,
    [SourceDocId]      INT           NULL,
    [ReconcileId]      INT           NULL,
    [CancelledBy]      INT           NULL,
    [CancelledDate]    DATETIME2     NULL,
    [CancelReason]     NVARCHAR(300) NULL,
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_PaymentNumber UNIQUE (PaymentNumber, CompanyID, BranchID),
    CONSTRAINT CHK_Payment_Amount CHECK (Amount > 0)
);
```

### 8. WEBERP_InternalTransfers — التحويلات الداخلية
```sql
CREATE TABLE [dbo].[WEBERP_InternalTransfers] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [TransferNumber]   NVARCHAR(30)  NOT NULL,
    -- TRF/2025/0001
    [TransferDate]     DATE          NOT NULL DEFAULT GETDATE(),
    -- المصدر
    [FromType]         NVARCHAR(20)  NOT NULL,
    -- safe / bank
    [FromSafeId]       INT           NULL REFERENCES WEBERP_Safes(Id),
    [FromBankId]       INT           NULL REFERENCES WEBERP_BankAccounts(Id),
    -- الهدف
    [ToType]           NVARCHAR(20)  NOT NULL,
    -- safe / bank
    [ToSafeId]         INT           NULL REFERENCES WEBERP_Safes(Id),
    [ToBankId]         INT           NULL REFERENCES WEBERP_BankAccounts(Id),
    -- المبلغ
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [Amount]           DECIMAL(18,4) NOT NULL,
    [AmountLocal]      DECIMAL(18,4) NOT NULL,
    -- رسوم التحويل (إن وجدت)
    [TransferFees]     DECIMAL(18,4) NOT NULL DEFAULT 0,
    [FeesAccountId]    INT           NULL,
    -- البيان والمرجع
    [DescriptionAr]    NVARCHAR(500) NULL,
    [Reference]        NVARCHAR(100] NULL,
    -- الحالة
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    [JournalEntryId]   INT           NULL,
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    [PostedDate]       DATETIME2     NULL,
    [PostedBy]         INT           NULL,
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_TransferNumber UNIQUE (TransferNumber, CompanyID, BranchID),
    CONSTRAINT CHK_Transfer_DiffSource CHECK (
        NOT (FromType='safe' AND ToType='safe' AND FromSafeId=ToSafeId) AND
        NOT (FromType='bank' AND ToType='bank' AND FromBankId=ToBankId)
    )
);
```

### 9. WEBERP_ReceivedChecks — الشيكات المستلمة
```sql
CREATE TABLE [dbo].[WEBERP_ReceivedChecks] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [CheckNumber]      NVARCHAR(50)  NOT NULL,
    -- رقم الشيك على ورقة الشيك
    [CheckDate]        DATE          NOT NULL,
    -- تاريخ الشيك (قد يختلف عن تاريخ الاستلام)
    [DueDate]          DATE          NOT NULL,
    -- تاريخ الاستحقاق (الصرف)
    [ReceivedDate]     DATE          NOT NULL DEFAULT GETDATE(),
    -- تاريخ الاستلام الفعلي
    -- الساحب (من أعطى الشيك)
    [DrawerName]       NVARCHAR(200] NOT NULL,
    [DrawerBank]       NVARCHAR(100) NULL,
    [DrawerAccount]    NVARCHAR(50)  NULL,
    [DrawerId]         INT           NULL,
    -- FK → Customers إذا كان عميلاً
    [DrawerType]       NVARCHAR(20)  NULL,
    -- customer / vendor / employee / other
    -- المبلغ
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [Amount]           DECIMAL(18,4) NOT NULL,
    [AmountLocal]      DECIMAL(18,4) NOT NULL,
    -- مكان الاستلام
    [SafeId]           INT           NULL REFERENCES WEBERP_Safes(Id),
    -- الخزينة التي استُلم فيها الشيك
    -- الحساب المقابل
    [AccountId]        INT           NOT NULL,
    -- حساب العميل/الدائن
    [DescriptionAr]    NVARCHAR(500) NULL,
    [Reference]        NVARCHAR(100] NULL,
    -- مرجع الفاتورة أو العقد
    -- دورة حياة الشيك
    [CheckStatus]      NVARCHAR(30)  NOT NULL DEFAULT 'received',
    -- received       ← استُلم وفي الخزينة
    -- under_collection← أُرسل للبنك للتحصيل
    -- deposited      ← أُودع في البنك
    -- cleared        ← قُبض / تم الصرف
    -- returned       ← مرتجع (بدون رصيد / توقيع ناقص / إلخ)
    -- cancelled      ← ملغي
    -- تفاصيل التحصيل/الإيداع
    [CollectionDate]   DATE          NULL,
    -- تاريخ إرسال للتحصيل
    [DepositedDate]    DATE          NULL,
    -- تاريخ الإيداع
    [DepositedToBankId] INT          NULL REFERENCES WEBERP_BankAccounts(Id),
    [DepositBatchRef]  NVARCHAR(50)  NULL,
    -- رقم دفعة الإيداع (أكثر من شيك معاً)
    [ClearedDate]      DATE          NULL,
    -- تاريخ القبض الفعلي من البنك
    [ClearedBankRef]   NVARCHAR(50)  NULL,
    -- رقم مرجع البنك عند القبض
    -- المرتجع
    [ReturnedDate]     DATE          NULL,
    [ReturnReason]     NVARCHAR(30)  NULL,
    -- no_funds / account_closed / signature_mismatch /
    -- amount_mismatch / drawer_request / other
    [ReturnNotes]      NVARCHAR(300) NULL,
    [ReturnFees]       DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- رسوم مرتجع الشيك
    -- القيود
    [JournalEntryId]   INT           NULL,
    -- القيد عند الاستلام
    [DepositJournalEntryId] INT      NULL,
    -- القيد عند الإيداع
    [ReturnJournalEntryId]  INT      NULL,
    -- القيد عند الارتجاع
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    -- السند المرتبط
    [ReceiptId]        INT           NULL,
    -- FK → WEBERP_CashReceipts
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT CHK_ReceivedCheck_Amount CHECK (Amount > 0),
    CONSTRAINT CHK_ReceivedCheck_Dates  CHECK (DueDate >= CheckDate)
);
```

### 10. WEBERP_IssuedChecks — الشيكات الصادرة
```sql
CREATE TABLE [dbo].[WEBERP_IssuedChecks] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [CheckNumber]      NVARCHAR(50)  NOT NULL,
    [CheckDate]        DATE          NOT NULL,
    [DueDate]          DATE          NOT NULL,
    [IssuedDate]       DATE          NOT NULL DEFAULT GETDATE(),
    -- المستفيد
    [PayeeName]        NVARCHAR(200) NOT NULL,
    [PayeeId]          INT           NULL,
    [PayeeType]        NVARCHAR(20)  NULL,
    -- vendor / customer / employee / other
    -- المبلغ
    [CurrencyCode]     NVARCHAR(10)  NOT NULL DEFAULT 'SAR',
    [ExchangeRate]     DECIMAL(18,6) NOT NULL DEFAULT 1,
    [Amount]           DECIMAL(18,4) NOT NULL,
    [AmountLocal]      DECIMAL(18,4) NOT NULL,
    -- البنك المُصدَر منه
    [BankAccountId]    INT           NOT NULL REFERENCES WEBERP_BankAccounts(Id),
    -- الحساب المقابل
    [AccountId]        INT           NOT NULL,
    [CostCenterId]     INT           NULL,
    [AnalyticDistribution] NVARCHAR(MAX) NULL,
    [DescriptionAr]    NVARCHAR(500) NULL,
    [Reference]        NVARCHAR(100) NULL,
    -- دورة حياة الشيك الصادر
    [CheckStatus]      NVARCHAR(30)  NOT NULL DEFAULT 'issued',
    -- issued         ← صدر ولم يُسلَّم بعد
    -- delivered      ← سُلِّم للمستفيد
    -- presented      ← قُدِّم للبنك من المستفيد
    -- cleared        ← صُرف من الحساب
    -- returned       ← مرتجع
    -- cancelled      ← ملغي (قبل التسليم)
    -- stopped        ← إيقاف صرف (أمر إيقاف للبنك)
    [DeliveredDate]    DATE          NULL,
    [DeliveredTo]      NVARCHAR(200) NULL,
    [PresentedDate]    DATE          NULL,
    [ClearedDate]      DATE          NULL,
    [ReturnedDate]     DATE          NULL,
    [ReturnReason]     NVARCHAR(200) NULL,
    [StopDate]         DATE          NULL,
    [StopReason]       NVARCHAR(300] NULL,
    [StopRef]          NVARCHAR(50)  NULL,
    -- رقم أمر الإيقاف عند البنك
    -- القيود
    [JournalEntryId]   INT           NULL,
    [IsPosted]         BIT           NOT NULL DEFAULT 0,
    [PaymentId]        INT           NULL,
    -- FK → WEBERP_CashPayments
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_IssuedCheck UNIQUE (BankAccountId, CheckNumber, CompanyID, BranchID),
    CONSTRAINT CHK_IssuedCheck_Amount CHECK (Amount > 0)
);
```

### 11. WEBERP_BankReconciliation — تسوية البنك
```sql
CREATE TABLE [dbo].[WEBERP_BankReconciliation] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [ReconciliationNo] NVARCHAR(30)  NOT NULL,
    [BankAccountId]    INT           NOT NULL REFERENCES WEBERP_BankAccounts(Id),
    [PeriodFrom]       DATE          NOT NULL,
    [PeriodTo]         DATE          NOT NULL,
    -- أرصدة
    [StatementBalance] DECIMAL(18,4) NOT NULL,
    -- رصيد كشف البنك
    [BookBalance]      DECIMAL(18,4) NOT NULL,
    -- رصيد الدفاتر في نفس التاريخ
    [DifferenceAmount] AS (StatementBalance - BookBalance),
    -- الرصيد المعدَّل بعد التسوية
    [AdjustedBalance]  DECIMAL(18,4) NULL,
    -- الحالة
    [State]            NVARCHAR(20)  NOT NULL DEFAULT 'draft',
    -- draft / in_progress / reconciled / approved
    [ReconciledDate]   DATE          NULL,
    [ReconciledBy]     INT           NULL,
    [ApprovedDate]     DATE          NULL,
    [ApprovedBy]       INT           NULL,
    [Notes]            NVARCHAR(MAX) NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_Reconciliation UNIQUE (ReconciliationNo, CompanyID, BranchID),
    CONSTRAINT CHK_Reconciliation_Dates CHECK (PeriodTo >= PeriodFrom)
);
```

### 12. WEBERP_BankReconciliationLines — سطور التسوية
```sql
CREATE TABLE [dbo].[WEBERP_BankReconciliationLines] (
    [Id]               INT           IDENTITY(1,1) PRIMARY KEY,
    [ReconciliationId] INT           NOT NULL REFERENCES WEBERP_BankReconciliation(Id),
    [LineType]         NVARCHAR(20)  NOT NULL,
    -- book_entry    ← حركة في الدفاتر
    -- statement     ← حركة في كشف البنك
    -- matched       ← مطابقة بين الاثنين
    [TransactionDate]  DATE          NOT NULL,
    [Description]      NVARCHAR(300] NULL,
    [Reference]        NVARCHAR(100) NULL,
    [Debit]            DECIMAL(18,4) NOT NULL DEFAULT 0,
    [Credit]           DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- ربط بالحركات الموجودة
    [ReceiptId]        INT           NULL,
    -- FK → CashReceipts
    [PaymentId]        INT           NULL,
    -- FK → CashPayments
    [CheckId]          INT           NULL,
    -- FK → IssuedChecks أو ReceivedChecks
    [JournalEntryLineId] INT         NULL,
    -- FK → JournalEntryDetails
    [IsMatched]        BIT           NOT NULL DEFAULT 0,
    [MatchedWith]      INT           NULL,
    -- ID السطر المقابل في كشف البنك
    [IsOutstanding]    BIT           NOT NULL DEFAULT 0,
    -- حركة في الدفاتر لم تظهر في كشف البنك
    [IsUnrecorded]     BIT           NOT NULL DEFAULT 0,
    -- حركة في كشف البنك لم تسجَّل في الدفاتر
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0
);
```

### 13. WEBERP_CashClosing — إقفال الخزينة اليومي
```sql
CREATE TABLE [dbo].[WEBERP_CashClosing] (
    [Id]              INT           IDENTITY(1,1) PRIMARY KEY,
    [ClosingNumber]   NVARCHAR(30)  NOT NULL,
    [SafeId]          INT           NOT NULL REFERENCES WEBERP_Safes(Id),
    [ClosingDate]     DATE          NOT NULL,
    [ShiftStart]      DATETIME2     NOT NULL,
    [ShiftEnd]        DATETIME2     NULL,
    [CashierId]       INT           NOT NULL,
    -- الأرصدة
    [OpeningBalance]  DECIMAL(18,4) NOT NULL,
    -- رصيد بداية الوردية
    [TotalReceipts]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [TotalPayments]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ExpectedBalance] DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- = Opening + Receipts - Payments
    [ActualBalance]   DECIMAL(18,4) NULL,
    -- الرصيد الفعلي المعدود
    [DifferenceAmount] AS (ISNULL(ActualBalance,0) - ExpectedBalance),
    [DifferenceAccountId] INT       NULL,
    -- حساب ترحيل الفرق (عجز / زيادة)
    -- الحالة
    [State]           NVARCHAR(20)  NOT NULL DEFAULT 'open',
    -- open / closed / approved
    [JournalEntryId]  INT           NULL,
    [ApprovedBy]      INT           NULL,
    [ApprovedDate]    DATETIME2     NULL,
    [Notes]           NVARCHAR(MAX] NULL,
    -- Audit
    [CompanyID]    INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy]    INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy]   INT NULL,     [ModifiedAt] DATETIME2 NULL,
    [IsCanceled]   BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CashClosing UNIQUE (SafeId, ClosingDate, CompanyID, BranchID)
);
```

---

## 🔧 Stored Procedures الكاملة

### SP 1: WEBERP_Currencies_GetRate — جلب سعر الصرف
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Currencies_GetRate]
    @CurrencyCode  NVARCHAR(10),
    @ForDate       DATE = NULL,
    @RateType      NVARCHAR(10) = 'mid',
    -- buy / sell / mid
    @CompanyID     INT,
    @Rate          DECIMAL(18,6) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @ForDate IS NULL SET @ForDate = CAST(GETDATE() AS DATE);

    -- عملة أساسية → سعرها 1
    IF EXISTS (SELECT 1 FROM WEBERP_Currencies
               WHERE Code=@CurrencyCode AND IsBaseCurrency=1 AND CompanyID=@CompanyID)
    BEGIN SET @Rate = 1; RETURN; END

    -- أحدث سعر في أو قبل التاريخ المطلوب
    SELECT TOP 1 @Rate = CASE @RateType
        WHEN 'buy'  THEN BuyRate
        WHEN 'sell' THEN SellRate
        ELSE MidRate
    END
    FROM WEBERP_ExchangeRates
    WHERE CurrencyCode=@CurrencyCode
      AND RateDate <= @ForDate
      AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL)
    ORDER BY RateDate DESC, Id DESC;

    -- Fallback: السعر الحالي من جدول العملات
    IF @Rate IS NULL
        SELECT @Rate = ExchangeRate FROM WEBERP_Currencies
        WHERE Code=@CurrencyCode AND CompanyID=@CompanyID;

    IF @Rate IS NULL SET @Rate = 1;
END;
```

### SP 2: WEBERP_CashReceipts_Post — ترحيل سند القبض
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_CashReceipts_Post]
    @ReceiptId INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @Amount DECIMAL(18,4), @AmountLocal DECIMAL(18,4),
            @SafeId INT, @BankAccountId INT, @AccountId INT,
            @PaymentMethodId INT, @MethodType NVARCHAR(30),
            @SafeAccountId INT, @BankGLAccountId INT,
            @CheckId INT, @ReceiptNumber NVARCHAR(30),
            @ReceiptDate DATE, @DescriptionAr NVARCHAR(500),
            @AnalyticDist NVARCHAR(MAX);

    SELECT @Amount=Amount, @AmountLocal=AmountLocal,
           @SafeId=SafeId, @BankAccountId=BankAccountId,
           @AccountId=AccountId, @PaymentMethodId=PaymentMethodId,
           @CheckId=CheckId, @ReceiptNumber=ReceiptNumber,
           @ReceiptDate=ReceiptDate, @DescriptionAr=DescriptionAr,
           @AnalyticDist=AnalyticDistribution
    FROM WEBERP_CashReceipts
    WHERE Id=@ReceiptId AND State='draft' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @Amount IS NULL
    BEGIN ROLLBACK; RAISERROR('السند غير موجود أو مرحّل',16,1); RETURN; END

    -- جلب نوع وسيلة الدفع
    SELECT @MethodType=MethodType FROM WEBERP_PaymentMethods WHERE Id=@PaymentMethodId;

    -- الحساب المدين (الخزينة أو البنك)
    IF @SafeId IS NOT NULL
        SELECT @SafeAccountId=AccountId FROM WEBERP_Safes WHERE Id=@SafeId;
    IF @BankAccountId IS NOT NULL
        SELECT @BankGLAccountId=AccountId FROM WEBERP_BankAccounts WHERE Id=@BankAccountId;

    DECLARE @DebitAccountId INT = ISNULL(@SafeAccountId, @BankGLAccountId);

    IF @DebitAccountId IS NULL
    BEGIN ROLLBACK; RAISERROR('لم يتم تحديد حساب الخزينة أو البنك',16,1); RETURN; END

    -- إنشاء القيد المحاسبي
    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber, EntryDate, JournalType, DescriptionAr,
         TotalDebit, TotalCredit, IsPosted, PostedDate, PostedBy,
         CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES
        (@ReceiptNumber, @ReceiptDate, 'CashReceipt',
         ISNULL(@DescriptionAr, 'سند قبض: '+@ReceiptNumber),
         @AmountLocal, @AmountLocal, 1, GETDATE(), @UserId,
         @CompanyID, @BranchID, @UserId, GETDATE());
    SET @JId = SCOPE_IDENTITY();

    -- مدين: الخزينة أو البنك
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId, AccountId, DebitAmount, CreditAmount,
         DescriptionAr, AnalyticDistribution, CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES
        (@JId, @DebitAccountId, @AmountLocal, 0,
         ISNULL(@DescriptionAr,''), NULL, @CompanyID, @BranchID, @UserId, GETDATE());

    -- دائن: الحساب المقابل (العميل / المورد / إلخ)
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId, AccountId, DebitAmount, CreditAmount,
         DescriptionAr, AnalyticDistribution, CompanyID, BranchID, CreatedBy, CreatedAt)
    VALUES
        (@JId, @AccountId, 0, @AmountLocal,
         ISNULL(@DescriptionAr,''), @AnalyticDist, @CompanyID, @BranchID, @UserId, GETDATE());

    -- تحديث رصيد الخزينة
    IF @SafeId IS NOT NULL
        UPDATE WEBERP_Safes SET
            CurrentBalance = CurrentBalance + @AmountLocal,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE Id=@SafeId;

    -- تحديث رصيد البنك
    IF @BankAccountId IS NOT NULL
        UPDATE WEBERP_BankAccounts SET
            CurrentBalance = CurrentBalance + @AmountLocal,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE Id=@BankAccountId;

    -- تحديث حالة الشيك المستلم
    IF @CheckId IS NOT NULL AND @MethodType='check'
        UPDATE WEBERP_ReceivedChecks SET
            CheckStatus='deposited', DepositedDate=@ReceiptDate,
            JournalEntryId=@JId, IsPosted=1,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE Id=@CheckId;

    -- توليد سطور التحليل
    IF @AnalyticDist IS NOT NULL AND @AnalyticDist <> '{}'
        EXEC WEBERP_AnalyticLines_Generate
            @JId, @AccountId, @AmountLocal, @ReceiptDate,
            @AnalyticDist, NULL, NULL, 'CashReceipt', @ReceiptId,
            @CompanyID, @BranchID, @UserId;

    -- تحديث السند
    UPDATE WEBERP_CashReceipts SET
        State='posted', IsPosted=1, PostedDate=GETDATE(), PostedBy=@UserId,
        JournalEntryId=@JId, ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@ReceiptId;

    COMMIT;
END;
```

### SP 3: WEBERP_CashPayments_Post — ترحيل سند الصرف
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_CashPayments_Post]
    @PaymentId INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @Amount DECIMAL(18,4), @AmountLocal DECIMAL(18,4),
            @WithholdingTax DECIMAL(18,4), @NetPaid DECIMAL(18,4),
            @SafeId INT, @BankAccountId INT, @AccountId INT,
            @PaymentMethodId INT, @MethodType NVARCHAR(30),
            @CheckId INT, @PaymentNumber NVARCHAR(30),
            @PaymentDate DATE, @DescriptionAr NVARCHAR(500),
            @AnalyticDist NVARCHAR(MAX);

    SELECT @Amount=Amount, @AmountLocal=AmountLocal,
           @WithholdingTax=WithholdingTaxAmount,
           @SafeId=SafeId, @BankAccountId=BankAccountId,
           @AccountId=AccountId, @PaymentMethodId=PaymentMethodId,
           @CheckId=CheckId, @PaymentNumber=PaymentNumber,
           @PaymentDate=PaymentDate, @DescriptionAr=DescriptionAr,
           @AnalyticDist=AnalyticDistribution
    FROM WEBERP_CashPayments
    WHERE Id=@PaymentId AND State='draft' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @Amount IS NULL
    BEGIN ROLLBACK; RAISERROR('السند غير موجود أو مرحّل',16,1); RETURN; END

    SET @NetPaid = @AmountLocal - @WithholdingTax;

    SELECT @MethodType=MethodType FROM WEBERP_PaymentMethods WHERE Id=@PaymentMethodId;

    DECLARE @CreditAccountId INT;
    IF @SafeId IS NOT NULL
        SELECT @CreditAccountId=AccountId FROM WEBERP_Safes WHERE Id=@SafeId;
    IF @BankAccountId IS NOT NULL
        SELECT @CreditAccountId=AccountId FROM WEBERP_BankAccounts WHERE Id=@BankAccountId;

    IF @CreditAccountId IS NULL
    BEGIN ROLLBACK; RAISERROR('لم يتم تحديد حساب الخزينة أو البنك',16,1); RETURN; END

    -- القيد
    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,
         TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@PaymentNumber,@PaymentDate,'CashPayment',
         ISNULL(@DescriptionAr,'سند صرف: '+@PaymentNumber),
         @AmountLocal,@AmountLocal,1,GETDATE(),@UserId,
         @CompanyID,@BranchID,@UserId,GETDATE());
    SET @JId=SCOPE_IDENTITY();

    -- مدين: الحساب المقابل (المورد / العميل / المصروف)
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,
         DescriptionAr,AnalyticDistribution,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@JId,@AccountId,@NetPaid,0,ISNULL(@DescriptionAr,''),@AnalyticDist,@CompanyID,@BranchID,@UserId,GETDATE());

    -- مدين: ضريبة الاستقطاع (إن وجدت)
    IF @WithholdingTax > 0
    BEGIN
        DECLARE @WithholdingAccId INT;
        -- يُجلب من إعدادات الشركة
        SELECT @WithholdingAccId = CAST(SettingValue AS INT)
        FROM WEBERP_CompanySettings
        WHERE SettingKey='WithholdingTaxAccountId' AND CompanyID=@CompanyID;

        IF @WithholdingAccId IS NOT NULL
            INSERT INTO WEBERP_JournalEntryDetails
                (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@WithholdingAccId,@WithholdingTax,0,'ضريبة استقطاع',@CompanyID,@BranchID,@UserId,GETDATE());
    END

    -- دائن: الخزينة أو البنك
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@JId,@CreditAccountId,0,@AmountLocal,ISNULL(@DescriptionAr,''),@CompanyID,@BranchID,@UserId,GETDATE());

    -- تحديث الأرصدة
    IF @SafeId IS NOT NULL
        UPDATE WEBERP_Safes SET CurrentBalance=CurrentBalance-@AmountLocal,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@SafeId;

    IF @BankAccountId IS NOT NULL
        UPDATE WEBERP_BankAccounts SET CurrentBalance=CurrentBalance-@AmountLocal,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@BankAccountId;

    -- تحديث الشيك الصادر
    IF @CheckId IS NOT NULL AND @MethodType='check'
        UPDATE WEBERP_IssuedChecks SET
            CheckStatus='issued', JournalEntryId=@JId, IsPosted=1,
            ModifiedBy=@UserId,ModifiedAt=GETDATE()
        WHERE Id=@CheckId;

    UPDATE WEBERP_CashPayments SET
        State='posted',IsPosted=1,PostedDate=GETDATE(),PostedBy=@UserId,
        JournalEntryId=@JId,ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE Id=@PaymentId;

    COMMIT;
END;
```

### SP 4: WEBERP_InternalTransfers_Post — ترحيل التحويل الداخلي
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_InternalTransfers_Post]
    @TransferId INT,
    @CompanyID  INT, @BranchID INT,
    @UserId     INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @Amount DECIMAL(18,4), @Fees DECIMAL(18,4),
            @FromSafeId INT, @FromBankId INT,
            @ToSafeId INT, @ToBankId INT,
            @FeesAccountId INT, @TransferNumber NVARCHAR(30),
            @TransferDate DATE, @DescriptionAr NVARCHAR(500);

    SELECT @Amount=Amount, @Fees=TransferFees,
           @FromSafeId=FromSafeId, @FromBankId=FromBankId,
           @ToSafeId=ToSafeId, @ToBankId=ToBankId,
           @FeesAccountId=FeesAccountId,
           @TransferNumber=TransferNumber, @TransferDate=TransferDate,
           @DescriptionAr=DescriptionAr
    FROM WEBERP_InternalTransfers
    WHERE Id=@TransferId AND State='draft' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @Amount IS NULL
    BEGIN ROLLBACK; RAISERROR('التحويل غير موجود أو مرحّل',16,1); RETURN; END

    DECLARE @FromAccountId INT, @ToAccountId INT;

    IF @FromSafeId IS NOT NULL
        SELECT @FromAccountId=AccountId FROM WEBERP_Safes WHERE Id=@FromSafeId;
    ELSE
        SELECT @FromAccountId=AccountId FROM WEBERP_BankAccounts WHERE Id=@FromBankId;

    IF @ToSafeId IS NOT NULL
        SELECT @ToAccountId=AccountId FROM WEBERP_Safes WHERE Id=@ToSafeId;
    ELSE
        SELECT @ToAccountId=AccountId FROM WEBERP_BankAccounts WHERE Id=@ToBankId;

    DECLARE @TotalDebit DECIMAL(18,4) = @Amount + ISNULL(@Fees,0);

    DECLARE @JId INT;
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,
         TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@TransferNumber,@TransferDate,'InternalTransfer',
         ISNULL(@DescriptionAr,'تحويل داخلي: '+@TransferNumber),
         @TotalDebit,@TotalDebit,1,GETDATE(),@UserId,
         @CompanyID,@BranchID,@UserId,GETDATE());
    SET @JId=SCOPE_IDENTITY();

    -- مدين: الوجهة
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@JId,@ToAccountId,@Amount,0,'تحويل وارد',@CompanyID,@BranchID,@UserId,GETDATE());

    -- مدين: رسوم التحويل (إن وجدت)
    IF @Fees > 0 AND @FeesAccountId IS NOT NULL
        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (@JId,@FeesAccountId,@Fees,0,'رسوم التحويل',@CompanyID,@BranchID,@UserId,GETDATE());

    -- دائن: المصدر
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (@JId,@FromAccountId,0,@TotalDebit,'تحويل صادر',@CompanyID,@BranchID,@UserId,GETDATE());

    -- تحديث الأرصدة
    IF @FromSafeId IS NOT NULL
        UPDATE WEBERP_Safes SET CurrentBalance=CurrentBalance-@TotalDebit,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@FromSafeId;
    ELSE
        UPDATE WEBERP_BankAccounts SET CurrentBalance=CurrentBalance-@TotalDebit,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@FromBankId;

    IF @ToSafeId IS NOT NULL
        UPDATE WEBERP_Safes SET CurrentBalance=CurrentBalance+@Amount,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@ToSafeId;
    ELSE
        UPDATE WEBERP_BankAccounts SET CurrentBalance=CurrentBalance+@Amount,
            ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@ToBankId;

    UPDATE WEBERP_InternalTransfers SET
        State='posted',IsPosted=1,PostedDate=GETDATE(),PostedBy=@UserId,
        JournalEntryId=@JId,ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE Id=@TransferId;

    COMMIT;
END;
```

### SP 5: WEBERP_ReceivedChecks_UpdateStatus — دورة حياة الشيك المستلم
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_ReceivedChecks_UpdateStatus]
    @CheckId       INT,
    @NewStatus     NVARCHAR(30),
    @ActionDate    DATE,
    @BankAccountId INT = NULL,
    @BatchRef      NVARCHAR(50) = NULL,
    @ReturnReason  NVARCHAR(30) = NULL,
    @ReturnNotes   NVARCHAR(300) = NULL,
    @ReturnFees    DECIMAL(18,4) = 0,
    @CompanyID     INT, @BranchID INT,
    @UserId        INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @CurrentStatus NVARCHAR(30), @AmountLocal DECIMAL(18,4),
            @AccountId INT, @SafeId INT, @DepositedToBankId INT,
            @CheckNumber NVARCHAR(50);

    SELECT @CurrentStatus=CheckStatus, @AmountLocal=AmountLocal,
           @AccountId=AccountId, @SafeId=SafeId,
           @DepositedToBankId=DepositedToBankId,
           @CheckNumber=CheckNumber
    FROM WEBERP_ReceivedChecks
    WHERE Id=@CheckId AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @CurrentStatus IS NULL
    BEGIN ROLLBACK; RAISERROR('الشيك غير موجود',16,1); RETURN; END

    -- التحقق من تسلسل الحالات المسموح به
    DECLARE @AllowedTransitions TABLE (FromStatus NVARCHAR(30), ToStatus NVARCHAR(30));
    INSERT INTO @AllowedTransitions VALUES
        ('received','under_collection'),
        ('received','deposited'),
        ('received','returned'),
        ('under_collection','deposited'),
        ('under_collection','cleared'),
        ('under_collection','returned'),
        ('deposited','cleared'),
        ('deposited','returned');

    IF NOT EXISTS (
        SELECT 1 FROM @AllowedTransitions
        WHERE FromStatus=@CurrentStatus AND ToStatus=@NewStatus
    )
    BEGIN
        ROLLBACK;
        RAISERROR('التحويل من حالة %s إلى %s غير مسموح',16,1,@CurrentStatus,@NewStatus);
        RETURN;
    END

    DECLARE @JId INT = NULL;

    -- إنشاء قيود مخصصة لكل تغيير حالة
    IF @NewStatus = 'deposited'
    BEGIN
        -- خصم من حساب الشيكات تحت التحصيل وإضافة للبنك
        DECLARE @ChecksCollectionAccountId INT;
        DECLARE @BankGLAccountId INT;

        SELECT @ChecksCollectionAccountId = CAST(SettingValue AS INT)
        FROM WEBERP_CompanySettings
        WHERE SettingKey='ChecksUnderCollectionAccountId' AND CompanyID=@CompanyID;

        SELECT @BankGLAccountId=AccountId FROM WEBERP_BankAccounts WHERE Id=@BankAccountId;

        IF @ChecksCollectionAccountId IS NOT NULL AND @BankGLAccountId IS NOT NULL
        BEGIN
            INSERT INTO WEBERP_JournalEntries
                (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES ('CHK-DEP-'+CAST(@CheckId AS NVARCHAR),@ActionDate,'CheckDeposit',
                    'إيداع شيك رقم: '+@CheckNumber,@AmountLocal,@AmountLocal,1,GETDATE(),@UserId,@CompanyID,@BranchID,@UserId,GETDATE());
            SET @JId=SCOPE_IDENTITY();

            INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@BankGLAccountId,@AmountLocal,0,@CompanyID,@BranchID,@UserId,GETDATE()),
                   (@JId,@ChecksCollectionAccountId,0,@AmountLocal,@CompanyID,@BranchID,@UserId,GETDATE());

            UPDATE WEBERP_BankAccounts SET CurrentBalance=CurrentBalance+@AmountLocal,ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@BankAccountId;
        END
    END

    ELSE IF @NewStatus = 'returned'
    BEGIN
        -- عكس قيد الاستلام + قيد رسوم الارتجاع
        DECLARE @SafeAccountId INT;
        SELECT @SafeAccountId=AccountId FROM WEBERP_Safes WHERE Id=@SafeId;

        INSERT INTO WEBERP_JournalEntries
            (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES ('CHK-RET-'+CAST(@CheckId AS NVARCHAR),@ActionDate,'CheckReturn',
                'مرتجع شيك رقم: '+@CheckNumber+' - '+ISNULL(@ReturnReason,''),
                @AmountLocal+ISNULL(@ReturnFees,0),@AmountLocal+ISNULL(@ReturnFees,0),
                1,GETDATE(),@UserId,@CompanyID,@BranchID,@UserId,GETDATE());
        SET @JId=SCOPE_IDENTITY();

        -- مدين: العميل/الدائن (عكس القيد الأصلي)
        INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (@JId,@AccountId,@AmountLocal,0,'مرتجع شيك',@CompanyID,@BranchID,@UserId,GETDATE());

        -- رسوم الارتجاع (تُحمَّل على العميل أو على المصروفات)
        IF @ReturnFees > 0
        BEGIN
            DECLARE @ReturnFeesAccountId INT;
            SELECT @ReturnFeesAccountId=CAST(SettingValue AS INT)
            FROM WEBERP_CompanySettings WHERE SettingKey='CheckReturnFeesAccountId' AND CompanyID=@CompanyID;

            IF @ReturnFeesAccountId IS NOT NULL
                INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
                VALUES (@JId,@ReturnFeesAccountId,@ReturnFees,0,'رسوم مرتجع شيك',@CompanyID,@BranchID,@UserId,GETDATE());
        END

        -- دائن: الخزينة أو البنك
        INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,DescriptionAr,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES (@JId,ISNULL(@SafeAccountId,(SELECT AccountId FROM WEBERP_BankAccounts WHERE Id=@DepositedToBankId)),
                0,@AmountLocal+ISNULL(@ReturnFees,0),'مرتجع شيك',@CompanyID,@BranchID,@UserId,GETDATE());

        -- خصم من رصيد الخزينة أو البنك
        IF @SafeId IS NOT NULL
            UPDATE WEBERP_Safes SET CurrentBalance=CurrentBalance-@AmountLocal,ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@SafeId;
        ELSE IF @DepositedToBankId IS NOT NULL
            UPDATE WEBERP_BankAccounts SET CurrentBalance=CurrentBalance-@AmountLocal,ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@DepositedToBankId;
    END

    -- تحديث حالة الشيك
    UPDATE WEBERP_ReceivedChecks SET
        CheckStatus    = @NewStatus,
        CollectionDate = CASE WHEN @NewStatus='under_collection' THEN @ActionDate ELSE CollectionDate END,
        DepositedDate  = CASE WHEN @NewStatus='deposited'  THEN @ActionDate ELSE DepositedDate END,
        DepositedToBankId = CASE WHEN @NewStatus='deposited' THEN @BankAccountId ELSE DepositedToBankId END,
        DepositBatchRef   = CASE WHEN @NewStatus='deposited' THEN @BatchRef ELSE DepositBatchRef END,
        ClearedDate    = CASE WHEN @NewStatus='cleared'    THEN @ActionDate ELSE ClearedDate END,
        ReturnedDate   = CASE WHEN @NewStatus='returned'   THEN @ActionDate ELSE ReturnedDate END,
        ReturnReason   = CASE WHEN @NewStatus='returned'   THEN @ReturnReason ELSE ReturnReason END,
        ReturnNotes    = CASE WHEN @NewStatus='returned'   THEN @ReturnNotes  ELSE ReturnNotes END,
        ReturnFees     = CASE WHEN @NewStatus='returned'   THEN @ReturnFees   ELSE ReturnFees END,
        ReturnJournalEntryId = CASE WHEN @NewStatus='returned' THEN @JId ELSE ReturnJournalEntryId END,
        DepositJournalEntryId = CASE WHEN @NewStatus='deposited' THEN @JId ELSE DepositJournalEntryId END,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@CheckId;

    COMMIT;
END;
```

### SP 6: WEBERP_BankReconciliation_Generate — توليد سطور التسوية
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_BankReconciliation_Generate]
    @ReconciliationId INT,
    @CompanyID        INT, @BranchID INT,
    @UserId           INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @BankAccountId INT, @PeriodFrom DATE, @PeriodTo DATE;
    SELECT @BankAccountId=BankAccountId, @PeriodFrom=PeriodFrom, @PeriodTo=PeriodTo
    FROM WEBERP_BankReconciliation WHERE Id=@ReconciliationId AND CompanyID=@CompanyID;

    -- حذف السطور القديمة
    DELETE FROM WEBERP_BankReconciliationLines
    WHERE ReconciliationId=@ReconciliationId AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- سندات القبض البنكية
    INSERT INTO WEBERP_BankReconciliationLines
        (ReconciliationId,LineType,TransactionDate,Description,Reference,Debit,Credit,ReceiptId,IsMatched,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @ReconciliationId,'book_entry',ReceiptDate,
           ISNULL(DescriptionAr,'قبض'),ReceiptNumber,
           AmountLocal,0,Id,0,@CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_CashReceipts
    WHERE BankAccountId=@BankAccountId AND CompanyID=@CompanyID
      AND IsPosted=1 AND ReceiptDate BETWEEN @PeriodFrom AND @PeriodTo
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- سندات الصرف البنكية
    INSERT INTO WEBERP_BankReconciliationLines
        (ReconciliationId,LineType,TransactionDate,Description,Reference,Debit,Credit,PaymentId,IsMatched,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @ReconciliationId,'book_entry',PaymentDate,
           ISNULL(DescriptionAr,'صرف'),PaymentNumber,
           0,AmountLocal,Id,0,@CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_CashPayments
    WHERE BankAccountId=@BankAccountId AND CompanyID=@CompanyID
      AND IsPosted=1 AND PaymentDate BETWEEN @PeriodFrom AND @PeriodTo
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- شيكات صادرة غير مصروفة
    INSERT INTO WEBERP_BankReconciliationLines
        (ReconciliationId,LineType,TransactionDate,Description,Reference,Debit,Credit,CheckId,IsOutstanding,IsMatched,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @ReconciliationId,'book_entry',IssuedDate,
           'شيك صادر: '+PayeeName,CheckNumber,
           0,AmountLocal,Id,1,0,@CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_IssuedChecks
    WHERE BankAccountId=@BankAccountId AND CompanyID=@CompanyID
      AND CheckStatus IN ('issued','delivered','presented')
      AND IssuedDate <= @PeriodTo
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- تحديث BookBalance
    UPDATE WEBERP_BankReconciliation SET
        BookBalance=(SELECT CurrentBalance FROM WEBERP_BankAccounts WHERE Id=@BankAccountId),
        ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE Id=@ReconciliationId;
END;
```

### SP 7: WEBERP_CashClosing_Close — إقفال الخزينة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_CashClosing_Close]
    @ClosingId    INT,
    @ActualBalance DECIMAL(18,4),
    @CompanyID    INT, @BranchID INT,
    @UserId       INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @SafeId INT, @ExpectedBalance DECIMAL(18,4),
            @DifferenceAccountId INT, @ClosingDate DATE;

    SELECT @SafeId=SafeId, @ExpectedBalance=ExpectedBalance,
           @DifferenceAccountId=DifferenceAccountId, @ClosingDate=ClosingDate
    FROM WEBERP_CashClosing
    WHERE Id=@ClosingId AND State='open' AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @SafeId IS NULL
    BEGIN ROLLBACK; RAISERROR('الإقفال غير موجود أو مغلق',16,1); RETURN; END

    DECLARE @Difference DECIMAL(18,4) = @ActualBalance - @ExpectedBalance;
    DECLARE @JId INT = NULL;

    -- قيد الفرق إن وجد
    IF ABS(@Difference) > 0.001 AND @DifferenceAccountId IS NOT NULL
    BEGIN
        DECLARE @SafeAccountId INT;
        SELECT @SafeAccountId=AccountId FROM WEBERP_Safes WHERE Id=@SafeId;

        INSERT INTO WEBERP_JournalEntries
            (JournalNumber,EntryDate,JournalType,DescriptionAr,TotalDebit,TotalCredit,IsPosted,PostedDate,PostedBy,CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES ('CC-'+CAST(@ClosingId AS NVARCHAR),@ClosingDate,'CashClosing',
                CASE WHEN @Difference>0 THEN 'فائض خزينة' ELSE 'عجز خزينة' END,
                ABS(@Difference),ABS(@Difference),1,GETDATE(),@UserId,@CompanyID,@BranchID,@UserId,GETDATE());
        SET @JId=SCOPE_IDENTITY();

        IF @Difference > 0 -- فائض
        BEGIN
            INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@SafeAccountId,@Difference,0,@CompanyID,@BranchID,@UserId,GETDATE()),
                   (@JId,@DifferenceAccountId,0,@Difference,@CompanyID,@BranchID,@UserId,GETDATE());
        END
        ELSE -- عجز
        BEGIN
            INSERT INTO WEBERP_JournalEntryDetails (JournalEntryId,AccountId,DebitAmount,CreditAmount,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES (@JId,@DifferenceAccountId,ABS(@Difference),0,@CompanyID,@BranchID,@UserId,GETDATE()),
                   (@JId,@SafeAccountId,0,ABS(@Difference),@CompanyID,@BranchID,@UserId,GETDATE());
        END

        -- تحديث رصيد الخزينة للرصيد الفعلي
        UPDATE WEBERP_Safes SET CurrentBalance=@ActualBalance,ModifiedBy=@UserId,ModifiedAt=GETDATE() WHERE Id=@SafeId;
    END

    UPDATE WEBERP_CashClosing SET
        ActualBalance=@ActualBalance, State='closed',
        ShiftEnd=GETDATE(), JournalEntryId=@JId,
        ModifiedBy=@UserId,ModifiedAt=GETDATE()
    WHERE Id=@ClosingId;

    COMMIT;
END;
```

### SP 8: WEBERP_Cash_GetBalances — أرصدة الخزائن والبنوك
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Cash_GetBalances]
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- الخزائن
    SELECT
        'safe' AS Type,
        s.Id, s.Code, s.NameAr, s.CurrencyCode,
        s.CurrentBalance AS Balance,
        s.MinBalance, s.MaxBalance,
        s.ResponsibleId,
        -- إجماليات اليوم
        ISNULL((SELECT SUM(AmountLocal) FROM WEBERP_CashReceipts
                WHERE SafeId=s.Id AND ReceiptDate=CAST(GETDATE() AS DATE)
                AND IsPosted=1 AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)),0) AS TodayReceipts,
        ISNULL((SELECT SUM(AmountLocal) FROM WEBERP_CashPayments
                WHERE SafeId=s.Id AND PaymentDate=CAST(GETDATE() AS DATE)
                AND IsPosted=1 AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)),0) AS TodayPayments,
        CASE WHEN s.CurrentBalance < s.MinBalance THEN 1 ELSE 0 END AS BelowMinimum,
        CASE WHEN s.MaxBalance IS NOT NULL AND s.CurrentBalance > s.MaxBalance THEN 1 ELSE 0 END AS AboveMaximum
    FROM WEBERP_Safes s
    WHERE s.CompanyID=@CompanyID AND s.BranchID=@BranchID
      AND s.IsActive=1 AND (s.IsCanceled=0 OR s.IsCanceled IS NULL)

    UNION ALL

    -- البنوك
    SELECT
        'bank' AS Type,
        b.Id, b.Code, b.BankName, b.CurrencyCode,
        b.CurrentBalance AS Balance,
        -b.OverdraftLimit AS MinBalance,
        NULL AS MaxBalance,
        NULL AS ResponsibleId,
        ISNULL((SELECT SUM(AmountLocal) FROM WEBERP_CashReceipts
                WHERE BankAccountId=b.Id AND ReceiptDate=CAST(GETDATE() AS DATE)
                AND IsPosted=1 AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)),0) AS TodayReceipts,
        ISNULL((SELECT SUM(AmountLocal) FROM WEBERP_CashPayments
                WHERE BankAccountId=b.Id AND PaymentDate=CAST(GETDATE() AS DATE)
                AND IsPosted=1 AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)),0) AS TodayPayments,
        CASE WHEN b.CurrentBalance < -b.OverdraftLimit THEN 1 ELSE 0 END AS BelowMinimum,
        0 AS AboveMaximum
    FROM WEBERP_BankAccounts b
    WHERE b.CompanyID=@CompanyID AND b.BranchID=@BranchID
      AND b.IsActive=1 AND (b.IsCanceled=0 OR b.IsCanceled IS NULL)

    ORDER BY Type, Code;
END;
```

### SP 9: WEBERP_Checks_DueSoon — الشيكات المستحقة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_Checks_DueSoon]
    @DaysAhead INT = 7,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- شيكات مستلمة مستحقة قريباً
    SELECT
        'received' AS Direction,
        rc.Id, rc.CheckNumber, rc.DrawerName AS PartyName,
        rc.DueDate, rc.Amount, rc.AmountLocal, rc.CurrencyCode,
        rc.CheckStatus,
        DATEDIFF(DAY, GETDATE(), rc.DueDate) AS DaysRemaining,
        CASE
            WHEN rc.DueDate < CAST(GETDATE() AS DATE) THEN 'متأخر'
            WHEN DATEDIFF(DAY,GETDATE(),rc.DueDate) = 0 THEN 'اليوم'
            ELSE CAST(DATEDIFF(DAY,GETDATE(),rc.DueDate) AS NVARCHAR)+' يوم'
        END AS DueLabel
    FROM WEBERP_ReceivedChecks rc
    WHERE rc.CheckStatus IN ('received','under_collection','deposited')
      AND rc.DueDate <= DATEADD(DAY,@DaysAhead,GETDATE())
      AND rc.CompanyID=@CompanyID AND rc.BranchID=@BranchID
      AND (rc.IsCanceled=0 OR rc.IsCanceled IS NULL)

    UNION ALL

    -- شيكات صادرة مستحقة قريباً
    SELECT
        'issued' AS Direction,
        ic.Id, ic.CheckNumber, ic.PayeeName AS PartyName,
        ic.DueDate, ic.Amount, ic.AmountLocal, ic.CurrencyCode,
        ic.CheckStatus,
        DATEDIFF(DAY,GETDATE(),ic.DueDate) AS DaysRemaining,
        CASE
            WHEN ic.DueDate < CAST(GETDATE() AS DATE) THEN 'متأخر'
            WHEN DATEDIFF(DAY,GETDATE(),ic.DueDate) = 0 THEN 'اليوم'
            ELSE CAST(DATEDIFF(DAY,GETDATE(),ic.DueDate) AS NVARCHAR)+' يوم'
        END AS DueLabel
    FROM WEBERP_IssuedChecks ic
    WHERE ic.CheckStatus IN ('issued','delivered','presented')
      AND ic.DueDate <= DATEADD(DAY,@DaysAhead,GETDATE())
      AND ic.CompanyID=@CompanyID AND ic.BranchID=@BranchID
      AND (ic.IsCanceled=0 OR ic.IsCanceled IS NULL)

    ORDER BY DaysRemaining ASC, Direction;
END;
```

### SP 10: WEBERP_CashFlow_Report — تقرير التدفق النقدي
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_CashFlow_Report]
    @FromDate  DATE,
    @ToDate    DATE,
    @SafeId    INT = NULL,
    @BankId    INT = NULL,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH CashMovements AS (
        -- المقبوضات
        SELECT
            ReceiptDate AS MovDate,
            ReceiptNumber AS DocNumber,
            'receipt' AS MovType,
            ISNULL(DescriptionAr,'قبض') AS Description,
            AmountLocal AS Inflow,
            0 AS Outflow,
            SafeId, BankAccountId,
            PaymentMethodId,
            AccountId,
            SourceDocType, SourceDocId
        FROM WEBERP_CashReceipts
        WHERE IsPosted=1 AND CompanyID=@CompanyID AND BranchID=@BranchID
          AND ReceiptDate BETWEEN @FromDate AND @ToDate
          AND (@SafeId IS NULL OR SafeId=@SafeId)
          AND (@BankId IS NULL OR BankAccountId=@BankId)
          AND (IsCanceled=0 OR IsCanceled IS NULL)

        UNION ALL

        -- المدفوعات
        SELECT
            PaymentDate, PaymentNumber,
            'payment',
            ISNULL(DescriptionAr,'صرف'),
            0, AmountLocal,
            SafeId, BankAccountId,
            PaymentMethodId, AccountId,
            SourceDocType, SourceDocId
        FROM WEBERP_CashPayments
        WHERE IsPosted=1 AND CompanyID=@CompanyID AND BranchID=@BranchID
          AND PaymentDate BETWEEN @FromDate AND @ToDate
          AND (@SafeId IS NULL OR SafeId=@SafeId)
          AND (@BankId IS NULL OR BankAccountId=@BankId)
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    SELECT
        MovDate, DocNumber, MovType, Description,
        Inflow, Outflow,
        SUM(Inflow-Outflow) OVER (ORDER BY MovDate, DocNumber) AS RunningBalance,
        pm.NameAr AS PaymentMethod,
        s.NameAr AS SafeName,
        b.BankName
    FROM CashMovements cm
    LEFT JOIN WEBERP_PaymentMethods pm ON pm.Id=cm.PaymentMethodId
    LEFT JOIN WEBERP_Safes s ON s.Id=cm.SafeId
    LEFT JOIN WEBERP_BankAccounts b ON b.Id=cm.BankAccountId
    ORDER BY MovDate, DocNumber;
END;
```

---

## 🔄 دورة حياة الشيكات

```
┌──────────────────────────────────────────────────────────────────┐
│                   الشيكات المستلمة                                │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  [received]                                                        │
│     │   ← استُلم الشيك من العميل وسُجِّل                         │
│     │   ← قيد: Dr الخزينة / Cr حساب العميل                       │
│     │                                                              │
│     ├──→ [under_collection]                                        │
│     │        ← أُرسل للبنك للتحصيل                               │
│     │        ← تحويل لحساب "شيكات تحت التحصيل"                   │
│     │        │                                                     │
│     └──→ [deposited] ←──────────────────────────────────         │
│              ← أُودع مباشرة في البنك                             │
│              ← قيد: Dr البنك / Cr شيكات تحت التحصيل             │
│              │                                                     │
│              ├──→ [cleared]                                        │
│              │        ← تم الصرف بنجاح                           │
│              │        ← لا قيد إضافي                             │
│              │                                                     │
│              └──→ [returned]                                       │
│                       ← مرتجع بدون رصيد                          │
│                       ← قيد عكسي + رسوم الارتجاع                │
│                       ← يُعاد المطالبة على العميل                │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                   الشيكات الصادرة                                 │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  [issued]                                                          │
│     │   ← صدر الشيك من سند الصرف                                │
│     │   ← قيد: Dr المورد/الدائن / Cr البنك                       │
│     │   ← رصيد البنك يُخفَّض فوراً                              │
│     │                                                              │
│     ├──→ [delivered]                                               │
│     │        ← سُلِّم للمستفيد                                   │
│     │        ← لا قيد إضافي                                      │
│     │                                                              │
│     ├──→ [presented]                                               │
│     │        ← المستفيد قدَّمه للبنك                             │
│     │                                                              │
│     │        ├──→ [cleared]                                        │
│     │        │        ← صُرف من الحساب                           │
│     │        │        ← يظهر في كشف البنك                        │
│     │        │                                                     │
│     │        └──→ [returned]                                       │
│     │                 ← مرتجع من البنك                            │
│     │                 ← قيد عكسي                                  │
│     │                                                              │
│     └──→ [stopped]                                                 │
│              ← أمر إيقاف صرف للبنك                               │
│              ← قيد: Dr البنك / Cr المورد (عكس)                    │
│              ← يحتاج معالجة يدوية                                │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🔗 Integration Points

### مع موديول المبيعات
```
SalesInvoice → CashReceipt (تسوية الفاتورة مع القبض)
    SourceDocType='SalesInvoice', SourceDocId=InvoiceId
    بعد الترحيل: تحديث PaymentState في Invoice
```

### مع موديول المشتريات
```
VendorBill → CashPayment (تسوية الفاتورة مع الدفع)
    SourceDocType='VendorBill', SourceDocId=BillId
    بعد الترحيل: تحديث PaymentState في VendorBill
    تحديث CurrentBalance في Vendors
```

### مع موديول الحسابات العامة
```
كل ترحيل → JournalEntry تلقائي

CashReceipt  → Dr Safe/Bank    / Cr Account
CashPayment  → Dr Account      / Cr Safe/Bank
Transfer     → Dr Destination  / Cr Source
CheckDeposit → Dr Bank         / Cr ChecksCollection
CheckReturn  → Dr Account      / Cr Safe/Bank + Fees
CashClosing  → Dr/Cr Safe      / Cr/Dr Difference
```

### مع موديول نقاط البيع
```
POSTransaction → CashReceipt تلقائي لكل وسيلة دفع
ShiftClose → يُحدّث رصيد Safe
```

---

## 🌱 Seed Data

```sql
-- الحسابات المحاسبية المطلوبة في إعدادات الشركة
CREATE TABLE [dbo].[WEBERP_CompanySettings] (
    [Id]           INT IDENTITY(1,1) PRIMARY KEY,
    [SettingKey]   NVARCHAR(100) NOT NULL,
    [SettingValue] NVARCHAR(MAX) NULL,
    [Description]  NVARCHAR(300) NULL,
    [CompanyID]    INT NOT NULL,
    [CreatedBy]    INT NULL, [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_CompanySetting UNIQUE (SettingKey, CompanyID)
);

-- INSERT INTO WEBERP_CompanySettings (SettingKey, SettingValue, Description, CompanyID) VALUES
-- ('ChecksUnderCollectionAccountId', '1001', 'حساب شيكات تحت التحصيل',  1),
-- ('CheckReturnFeesAccountId',       '5001', 'حساب رسوم مرتجع الشيكات', 1),
-- ('WithholdingTaxAccountId',        '2201', 'حساب ضريبة الاستقطاع',    1),
-- ('CashDifferenceIncomeAccountId',  '4901', 'حساب فوائض الخزينة',      1),
-- ('CashDifferenceExpenseAccountId', '5901', 'حساب عجز الخزينة',        1);
```

---

## 🎛️ FormControls — سندات القبض (FormCode 420)

```sql
INSERT INTO FormControls (FormControlCode,FormCode,ControlName,CaptionAr,CaptionEn,ControlTypeCode,DataType,IsRequired,Searchable,RowNumber,Width,CompanyID,BranchID) VALUES
(1,  420,'ReceiptNumber',  'رقم السند',         'Receipt No',     1,'nvarchar',1,1,1,150,1,1),
(2,  420,'ReceiptDate',    'التاريخ',            'Date',           5,'date',    1,1,1,150,1,1),
(3,  420,'PayerType',      'نوع الدافع',         'Payer Type',     2,'nvarchar',1,0,1,150,1,1),
(4,  420,'PayerId',        'الدافع',             'Payer',          2,'int',     0,1,2,280,1,1),
(5,  420,'PayerName',      'اسم الدافع',         'Payer Name',     1,'nvarchar',0,1,2,280,1,1),
(6,  420,'CurrencyCode',   'العملة',             'Currency',       2,'nvarchar',1,0,3,120,1,1),
(7,  420,'ExchangeRate',   'سعر الصرف',          'Rate',           6,'decimal', 1,0,3,120,1,1),
(8,  420,'Amount',         'المبلغ',             'Amount',         6,'decimal', 1,0,3,150,1,1),
(9,  420,'AmountLocal',    'بالعملة المحلية',    'Local Amount',   6,'decimal', 0,0,4,150,1,1),
(10, 420,'PaymentMethodId','وسيلة القبض',        'Method',         2,'int',     1,0,4,200,1,1),
(11, 420,'SafeId',         'الخزينة',            'Safe',           2,'int',     0,0,4,180,1,1),
(12, 420,'BankAccountId',  'الحساب البنكي',      'Bank Account',   2,'int',     0,0,5,220,1,1),
(13, 420,'AccountId',      'الحساب المقابل',     'Account',        2,'int',     1,0,5,280,1,1),
(14, 420,'DescriptionAr',  'البيان',             'Description',    7,'nvarchar',0,1,6,600,1,1),
(15, 420,'Reference',      'المرجع',             'Reference',      1,'nvarchar',0,1,6,200,1,1);
```

---

## 📐 Indexes

```sql
CREATE INDEX IX_CashReceipts_Date    ON WEBERP_CashReceipts(ReceiptDate DESC, CompanyID);
CREATE INDEX IX_CashReceipts_Safe    ON WEBERP_CashReceipts(SafeId, IsPosted);
CREATE INDEX IX_CashReceipts_Bank    ON WEBERP_CashReceipts(BankAccountId, IsPosted);
CREATE INDEX IX_CashReceipts_Payer   ON WEBERP_CashReceipts(PayerType, PayerId, CompanyID);
CREATE INDEX IX_CashPayments_Date    ON WEBERP_CashPayments(PaymentDate DESC, CompanyID);
CREATE INDEX IX_CashPayments_Safe    ON WEBERP_CashPayments(SafeId, IsPosted);
CREATE INDEX IX_CashPayments_Bank    ON WEBERP_CashPayments(BankAccountId, IsPosted);
CREATE INDEX IX_ReceivedChecks_Due   ON WEBERP_ReceivedChecks(DueDate, CheckStatus) WHERE CheckStatus NOT IN ('cleared','returned','cancelled');
CREATE INDEX IX_IssuedChecks_Due     ON WEBERP_IssuedChecks(DueDate, CheckStatus)   WHERE CheckStatus NOT IN ('cleared','returned','cancelled','stopped');
CREATE INDEX IX_ExchangeRates_Date   ON WEBERP_ExchangeRates(CurrencyCode, RateDate DESC);
```

---

## 🗂️ Menus
```sql
INSERT INTO Menus (MenuCode,MenuNameAr,MenuNameEn,ParentCode,IsParent,FormCode,[Order],IsActive,CompanyID,BranchID) VALUES
(400,'النقدية والبنوك',         'Cash & Banks',         NULL,1,NULL, 4,1,1,1),
-- إعدادات
(401,'الإعدادات',               'Configuration',         400,1,NULL,  1,1,1,1),
(402,'العملات',                 'Currencies',            401,0,400,   1,1,1,1),
(403,'أسعار الصرف',             'Exchange Rates',        401,0,401,   2,1,1,1),
(404,'الخزائن والصناديق',       'Safes',                 401,0,410,   3,1,1,1),
(405,'الحسابات البنكية',        'Bank Accounts',         401,0,411,   4,1,1,1),
(406,'وسائل الدفع',             'Payment Methods',       401,0,412,   5,1,1,1),
-- المقبوضات
(410,'المقبوضات',               'Receipts',              400,1,NULL,  2,1,1,1),
(411,'سندات القبض',             'Cash Receipts',         410,0,420,   1,1,1,1),
(412,'الشيكات المستلمة',        'Received Checks',       410,0,450,   2,1,1,1),
-- المدفوعات
(420,'المدفوعات',               'Payments',              400,1,NULL,  3,1,1,1),
(421,'سندات الصرف',             'Cash Payments',         420,0,421,   1,1,1,1),
(422,'الشيكات الصادرة',         'Issued Checks',         420,0,451,   2,1,1,1),
-- التحويلات
(430,'التحويلات الداخلية',      'Internal Transfers',    400,0,440,   4,1,1,1),
-- البنوك
(440,'إدارة البنوك',            'Bank Management',       400,1,NULL,  5,1,1,1),
(441,'الحركات البنكية',         'Bank Transactions',     440,0,430,   1,1,1,1),
(442,'تسوية البنك',             'Bank Reconciliation',   440,0,460,   2,1,1,1),
-- الخزائن
(450,'إدارة الخزائن',           'Safe Management',       400,1,NULL,  6,1,1,1),
(451,'إقفال الخزينة',           'Cash Closing',          450,0,470,   1,1,1,1),
-- التقارير
(460,'التقارير',                'Reports',               400,1,NULL,  7,1,1,1),
(461,'أرصدة الخزائن والبنوك',   'Balances',              460,0,NULL,  1,1,1,1),
(462,'كشف الشيكات المستحقة',    'Due Checks',            460,0,NULL,  2,1,1,1),
(463,'التدفق النقدي',           'Cash Flow',             460,0,NULL,  3,1,1,1),
(464,'كشف حساب المورد',         'Vendor Statement',      460,0,NULL,  4,1,1,1),
(465,'كشف حساب العميل',         'Customer Statement',    460,0,NULL,  5,1,1,1);
```

---

## ⚠️ تحذيرات لـ Claude Code

### قواعد لا تتجاوزها:
1. **رصيد الخزينة سالب ممنوع** — `CONSTRAINT CHK` + تحقق في SP قبل الصرف
2. **سعر الصرف مطلوب** — لا ترحيل لحركة بعملة أجنبية بدون ExchangeRate
3. **تسلسل حالات الشيك** — لا تغيير حالة مباشر — دائماً عبر SP مع `@AllowedTransitions`
4. **رصيد البنك** — يُحدَّث فوراً عند ترحيل الشيك الصادر (وليس عند الصرف الفعلي)
5. **رسوم التحويل** — تُحسب مدين على الجهة المُرسِلة وتُسجَّل على حساب منفصل
6. **الشيك المتوقف** — `stopped` يحتاج قيد عكسي: Dr Bank / Cr Vendor (استرجاع الرصيد)
7. **ضريبة الاستقطاع** — `NetPayable = Amount - WithholdingTax` — المورد يستلم الصافي فقط
8. **إقفال الخزينة اليومي** — رصيد الإقفال يصبح رصيد فتح اليوم التالي
9. **تسوية البنك** — لا تُعدِّل الرصيد — فقط مطابقة وتحديد الحركات المعلقة
10. **الشيكات تحت التحصيل** — حساب وسيط بين الخزينة والبنك — رصيده يساوي مجموع الشيكات المرسلة للتحصيل وغير المصروفة
