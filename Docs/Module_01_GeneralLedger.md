# موديول الحسابات العامة (General Ledger) — v3
## Analysis File for Claude Code — NozomSoft ERP
## مبني على: الملف الأصلي + مراجعة Specification المرفوعة

---

## 📋 المحتويات
1. الدليل المحاسبي (Chart of Accounts)
2. مراكز التكلفة (Cost Centers)
3. ربط الحسابات بمراكز التكلفة — **مع EntryOrder وRequired**
4. العملة على مستوى الفرع
5. قيود اليومية
6. مصدر القيد (Cross-Module Integration) — **جديد**
7. الترحيل وإلغاء الترحيل
8. السنوات المالية والفترات — **مع إقفال وترحيل الأرصدة**
9. التقارير

---

## ✅ الإضافات في هذه النسخة (v3)

| # | الإضافة | المصدر |
|---|---------|--------|
| 1 | جدول `WEBERP_JournalEntrySources` لتسجيل مصدر كل قيد | Specification مرفوعة |
| 2 | حقل `EntryOrder` في ربط الحسابات بمراكز التكلفة | Specification مرفوعة |
| 3 | SP لإلغاء الترحيل (Unpost) مع قواعد | Specification مرفوعة |
| 4 | SP إقفال الفترة المحاسبية | Specification مرفوعة |
| 5 | SP إقفال السنة المالية | Specification مرفوعة |
| 6 | SP فتح سنة جديدة وترحيل الأرصدة | Specification مرفوعة |
| 7 | تخزين العملة على مستوى الفرع | Specification مرفوعة |
| 8 | SPs التقارير: دفتر الأستاذ + كشف الحساب + ميزان تحليلي | Specification مرفوعة |
| 9 | SP التحقق من مراكز التكلفة المطلوبة قبل الترحيل | Specification مرفوعة |
| 10 | Seed Data للدليل المحاسبي الأساسي | Specification مرفوعة |

---

## 🗄️ تعديلات على جداول موجودة

### WEBERP_ChartOfAccounts — إضافة حقول هرمية
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_ChartOfAccounts') AND name='IsParent')
    ALTER TABLE WEBERP_ChartOfAccounts ADD [IsParent]     BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_ChartOfAccounts') AND name='ParentId')
    ALTER TABLE WEBERP_ChartOfAccounts ADD [ParentId]     INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_ChartOfAccounts') AND name='Level')
    ALTER TABLE WEBERP_ChartOfAccounts ADD [Level]        INT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_ChartOfAccounts') AND name='AllowPosting')
    ALTER TABLE WEBERP_ChartOfAccounts ADD [AllowPosting] BIT NOT NULL DEFAULT 1;
-- AllowPosting=0 → حساب تجميعي — لا قيود مباشرة

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_ChartOfAccounts') AND name='AccountNature')
    ALTER TABLE WEBERP_ChartOfAccounts ADD [AccountNature] NVARCHAR(10) NULL;
-- D=مدين الطبيعي / C=دائن الطبيعي (من NormalSides)

-- تحديث البيانات الموجودة
UPDATE p SET p.IsParent=1, p.AllowPosting=0
FROM WEBERP_ChartOfAccounts p
WHERE EXISTS (
    SELECT 1 FROM WEBERP_ChartOfAccounts c
    WHERE c.ParentId=p.Id AND (c.IsCanceled=0 OR c.IsCanceled IS NULL)
);
```

### WEBERP_JournalEntries — إضافة حقول ناقصة
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='FiscalYearId')
    ALTER TABLE WEBERP_JournalEntries ADD [FiscalYearId]      INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='PeriodId')
    ALTER TABLE WEBERP_JournalEntries ADD [PeriodId]          INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='PostedBy')
    ALTER TABLE WEBERP_JournalEntries ADD [PostedBy]          INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='PostedDate')
    ALTER TABLE WEBERP_JournalEntries ADD [PostedDate]        DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='ReversalJournalId')
    ALTER TABLE WEBERP_JournalEntries ADD [ReversalJournalId] INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='UnpostedBy')
    ALTER TABLE WEBERP_JournalEntries ADD [UnpostedBy]        INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='UnpostedDate')
    ALTER TABLE WEBERP_JournalEntries ADD [UnpostedDate]      DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntries') AND name='UnpostReason')
    ALTER TABLE WEBERP_JournalEntries ADD [UnpostReason]      NVARCHAR(300) NULL;
```

### WEBERP_JournalEntryDetails — إضافة AnalyticDistribution
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WEBERP_JournalEntryDetails') AND name='AnalyticDistribution')
    ALTER TABLE WEBERP_JournalEntryDetails ADD [AnalyticDistribution] NVARCHAR(MAX) NULL;
-- JSON: {"CostCenterId": نسبة%}
```

### WEBERP_Branches — إضافة عملة الفرع
```sql
-- العملة تُخزَّن على مستوى الفرع لدعم الشركات متعددة الدول
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Branches') AND name='CurrencyCode')
    ALTER TABLE Branches ADD [CurrencyCode] NVARCHAR(10) NOT NULL DEFAULT 'SAR';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Branches') AND name='CurrencyDecimalPlaces')
    ALTER TABLE Branches ADD [CurrencyDecimalPlaces] INT NOT NULL DEFAULT 2;
-- لماذا على مستوى الفرع؟
-- فرع مصر → جنيه مصري، فرع السعودية → ريال سعودي
-- كل فرع يقفل قوائمه بعملته الخاصة
```

---

## 🆕 جداول جديدة

### 1. WEBERP_FiscalYears — السنوات المالية
```sql
CREATE TABLE [dbo].[WEBERP_FiscalYears] (
    [Id]        INT IDENTITY(1,1) PRIMARY KEY,
    [YearCode]  NVARCHAR(20)  NOT NULL,
    [NameAr]    NVARCHAR(100] NOT NULL,
    [NameEn]    NVARCHAR(100) NULL,
    [StartDate] DATE NOT NULL,
    [EndDate]   DATE NOT NULL,
    [IsActive]  BIT NOT NULL DEFAULT 1,
    [IsClosed]  BIT NOT NULL DEFAULT 0,
    [ClosedDate] DATETIME2 NULL,
    [ClosedBy]  INT NULL,
    [Notes]     NVARCHAR(500) NULL,
    -- Audit
    [CompanyID] INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy] INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy] INT NULL,    [ModifiedAt] DATETIME2 NULL,
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_FiscalYear UNIQUE (YearCode, CompanyID, BranchID),
    CONSTRAINT CHK_FiscalYear CHECK (EndDate > StartDate)
);
```

### 2. WEBERP_AccountingPeriods — الفترات المحاسبية
```sql
CREATE TABLE [dbo].[WEBERP_AccountingPeriods] (
    [Id]           INT IDENTITY(1,1) PRIMARY KEY,
    [FiscalYearId] INT NOT NULL REFERENCES WEBERP_FiscalYears(Id),
    [PeriodNumber] INT NOT NULL,
    -- 1 إلى 12
    [NameAr]       NVARCHAR(50) NOT NULL,
    [NameEn]       NVARCHAR(50) NULL,
    [StartDate]    DATE NOT NULL,
    [EndDate]      DATE NOT NULL,
    [IsOpen]       BIT NOT NULL DEFAULT 1,
    [ClosedDate]   DATETIME2 NULL,
    [ClosedBy]     INT NULL,
    -- Audit
    [CompanyID] INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy] INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy] INT NULL,    [ModifiedAt] DATETIME2 NULL,
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    [CanceledDate] DATETIME2 NULL, [CanceledBy] INT NULL,
    CONSTRAINT UQ_Period UNIQUE (FiscalYearId, PeriodNumber, CompanyID, BranchID)
);
```

### 3. WEBERP_AccountBalances — أرصدة الحسابات
```sql
CREATE TABLE [dbo].[WEBERP_AccountBalances] (
    [Id]            INT IDENTITY(1,1) PRIMARY KEY,
    [AccountId]     INT NOT NULL,
    [FiscalYearId]  INT NOT NULL REFERENCES WEBERP_FiscalYears(Id),
    [PeriodId]      INT NULL REFERENCES WEBERP_AccountingPeriods(Id),
    [OpeningDebit]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [OpeningCredit] DECIMAL(18,4) NOT NULL DEFAULT 0,
    [PeriodDebit]   DECIMAL(18,4) NOT NULL DEFAULT 0,
    [PeriodCredit]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ClosingDebit]  DECIMAL(18,4) NOT NULL DEFAULT 0,
    [ClosingCredit] DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- Audit
    [CompanyID] INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy] INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy] INT NULL,    [ModifiedAt] DATETIME2 NULL,
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AccountBalance UNIQUE (AccountId, FiscalYearId, PeriodId, CompanyID, BranchID)
);
```

### 4. WEBERP_AccountCostCenterLinks — ربط الحسابات بمراكز التكلفة ⭐ جديد مُحسَّن
```sql
-- هذا الجدول يحدد:
-- أي مراكز التكلفة تظهر عند إدخال قيد على حساب معين
-- هل المركز مطلوب أم اختياري؟
-- ما ترتيب ظهوره في الواجهة؟
CREATE TABLE [dbo].[WEBERP_AccountCostCenterLinks] (
    [Id]           INT IDENTITY(1,1) PRIMARY KEY,
    [AccountId]    INT NOT NULL,
    -- FK → WEBERP_ChartOfAccounts
    [CostCenterId] INT NOT NULL,
    -- FK → WEBERP_CostCenters
    [IsRequired]   BIT NOT NULL DEFAULT 0,
    -- 1 = مطلوب — القيد لا يُرحَّل بدون تحديده
    -- 0 = اختياري
    [EntryOrder]   INT NOT NULL DEFAULT 1,
    -- ترتيب الظهور في واجهة إدخال القيد (1 = الأول)
    [IsActive]     BIT NOT NULL DEFAULT 1,
    -- Audit
    [CompanyID] INT NOT NULL, [BranchID]  INT NOT NULL,
    [CreatedBy] INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ModifiedBy] INT NULL,    [ModifiedAt] DATETIME2 NULL,
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AccountCostCenterLink UNIQUE (AccountId, CostCenterId, CompanyID, BranchID)
);

-- أمثلة:
-- حساب 5-1 مصروفات رواتب → قسم الموارد البشرية (مطلوب، ترتيب 1)
-- حساب 4-1 إيرادات مبيعات → مشروع أ (اختياري، ترتيب 1)
--                           → مشروع ب (اختياري، ترتيب 2)
-- حساب 1-1-1 نقدية → لا يوجد مراكز تكلفة
```

### 5. WEBERP_JournalEntrySources — مصدر القيد ⭐ جديد كلياً
```sql
-- يُسجَّل تلقائياً عند إنشاء قيد من موديول آخر
-- يتيح التتبع الكامل: "هذا القيد جاء من أي فاتورة؟"
CREATE TABLE [dbo].[WEBERP_JournalEntrySources] (
    [Id]              INT IDENTITY(1,1) PRIMARY KEY,
    [JournalEntryId]  INT NOT NULL,
    -- FK → WEBERP_JournalEntries.JournalEntryId
    [SourceModule]    NVARCHAR(50) NOT NULL,
    -- Purchases / Sales / Inventory / Cash / POS / Payroll / Manual
    [SourceFormCode]  INT NULL,
    -- رقم الشاشة في النظام الديناميكي
    [SourceRecordId]  INT NULL,
    -- رقم السجل في جدول الموديول المصدر
    [SourceReference] NVARCHAR(100) NULL,
    -- رقم المرجع القابل للقراءة: PO-2024-001 / INV-2024-001
    [SourceCode]      NVARCHAR(50) NULL,
    -- الكود المختصر: PINV-001 / SINV-001 / CR-001
    [SourceDate]      DATE NULL,
    -- تاريخ المستند الأصلي
    -- Audit
    [CompanyID] INT NOT NULL, [BranchID] INT NOT NULL,
    [CreatedBy] INT NULL,     [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [IsCanceled] BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_JournalSource UNIQUE (JournalEntryId, CompanyID, BranchID)
);

-- ═══ سيناريوهات الاستخدام ═══
-- ┌─────────────────┬──────────┬─────────────────────────────────────────┐
-- │ SourceModule    │ FormCode │ مثال SourceReference                   │
-- ├─────────────────┼──────────┼─────────────────────────────────────────┤
-- │ Purchases       │ 630      │ PO/2025/0001 (أمر شراء)                │
-- │ Purchases       │ 640      │ BILL/2025/0001 (فاتورة مورد)           │
-- │ Sales           │ 720      │ SO/2025/0001 (أمر بيع)                 │
-- │ Sales           │ 730      │ INV/2025/0001 (فاتورة مبيعات)          │
-- │ Inventory       │ 520      │ GRN-2025-001 (إذن استلام)              │
-- │ Inventory       │ 521      │ ISSUE-2025-001 (إذن صرف)              │
-- │ Cash            │ 420      │ RV/2025/0001 (سند قبض)                 │
-- │ Cash            │ 421      │ PV/2025/0001 (سند صرف)                 │
-- │ POS             │ 810      │ POS/2025/0001 (جلسة POS)               │
-- │ Manual          │ NULL     │ NULL (قيد يدوي)                        │
-- └─────────────────┴──────────┴─────────────────────────────────────────┘
```

---

## 🔧 Stored Procedures الكاملة

### SP 1: WEBERP_ChartOfAccounts_UpdateHierarchy
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_ChartOfAccounts_UpdateHierarchy]
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- تحديث IsParent وAllowPosting
    UPDATE p SET p.IsParent=1, p.AllowPosting=0
    FROM WEBERP_ChartOfAccounts p
    WHERE EXISTS (
        SELECT 1 FROM WEBERP_ChartOfAccounts c
        WHERE c.ParentId=p.Id AND c.CompanyID=@CompanyID
          AND (c.IsCanceled=0 OR c.IsCanceled IS NULL)
    ) AND p.CompanyID=@CompanyID;

    UPDATE p SET p.IsParent=0, p.AllowPosting=1
    FROM WEBERP_ChartOfAccounts p
    WHERE NOT EXISTS (
        SELECT 1 FROM WEBERP_ChartOfAccounts c
        WHERE c.ParentId=p.Id AND c.CompanyID=@CompanyID
          AND (c.IsCanceled=0 OR c.IsCanceled IS NULL)
    ) AND p.CompanyID=@CompanyID AND (p.IsCanceled=0 OR p.IsCanceled IS NULL);

    -- تحديث Level بالـ CTE
    WITH LvlCTE AS (
        SELECT Id, 1 AS Lvl
        FROM WEBERP_ChartOfAccounts
        WHERE ParentId IS NULL AND CompanyID=@CompanyID
          AND (IsCanceled=0 OR IsCanceled IS NULL)
        UNION ALL
        SELECT c.Id, p.Lvl+1
        FROM WEBERP_ChartOfAccounts c
        JOIN LvlCTE p ON p.Id=c.ParentId
        WHERE c.CompanyID=@CompanyID AND (c.IsCanceled=0 OR c.IsCanceled IS NULL)
    )
    UPDATE a SET a.Level=l.Lvl
    FROM WEBERP_ChartOfAccounts a
    JOIN LvlCTE l ON l.Id=a.Id;
END;
```

### SP 2: WEBERP_ChartOfAccounts_Save
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_ChartOfAccounts_Save]
    @Id        INT=0, @Code NVARCHAR(50), @NameAr NVARCHAR(200),
    @NameEn    NVARCHAR(200)=NULL, @ParentId INT=NULL,
    @AccountTypeId INT=NULL, @NormalSide NVARCHAR(1)='D',
    @OpeningBalance DECIMAL(18,4)=0, @IsActive BIT=1,
    @Notes     NVARCHAR(MAX)=NULL,
    @CompanyID INT, @BranchID INT, @UserId INT,
    @NewId     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- منع الحساب من أن يكون أبًا لنفسه
    IF @ParentId IS NOT NULL AND @ParentId=@Id
    BEGIN RAISERROR('لا يمكن أن يكون الحساب أبًا لنفسه',16,1); RETURN; END

    -- منع تكرار الكود
    IF EXISTS (
        SELECT 1 FROM WEBERP_ChartOfAccounts
        WHERE Code=@Code AND CompanyID=@CompanyID
          AND Id<>@Id AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN RAISERROR('كود الحساب موجود مسبقاً',16,1); RETURN; END

    IF @Id=0
    BEGIN
        INSERT INTO WEBERP_ChartOfAccounts
            (Code,NameAr,NameEn,ParentId,AccountType,NormalSide,
             OpeningBalance,IsActive,Notes,IsParent,AllowPosting,
             CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES
            (@Code,@NameAr,@NameEn,@ParentId,@AccountTypeId,@NormalSide,
             @OpeningBalance,@IsActive,@Notes,0,1,
             @CompanyID,@BranchID,@UserId,GETDATE());
        SET @NewId=SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE WEBERP_ChartOfAccounts SET
            Code=@Code, NameAr=@NameAr, NameEn=@NameEn,
            ParentId=@ParentId, AccountType=@AccountTypeId,
            NormalSide=@NormalSide, IsActive=@IsActive, Notes=@Notes,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE Id=@Id AND CompanyID=@CompanyID;
        SET @NewId=@Id;
    END

    -- تحديث الهرم تلقائياً
    EXEC WEBERP_ChartOfAccounts_UpdateHierarchy @CompanyID, @BranchID;
END;
```

### SP 3: WEBERP_AccountCostCenterLinks_GetForEntry — جلب مراكز التكلفة لحساب معين ⭐
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_AccountCostCenterLinks_GetForEntry]
    @AccountId INT,
    @CompanyID INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;
    -- يُستدعى عند اختيار حساب في سطر القيد
    -- يُعيد المراكز مرتبة حسب EntryOrder
    SELECT
        cc.CostCenterId,
        cc.CostCenterNameAr,
        cc.CostCenterNameEn,
        lnk.IsRequired,
        lnk.EntryOrder,
        -- هل الحساب مرتبط بأي مراكز؟
        COUNT(*) OVER () AS TotalLinkedCenters
    FROM WEBERP_AccountCostCenterLinks lnk
    JOIN WEBERP_CostCenters cc ON cc.CostCenterId=lnk.CostCenterId
    WHERE lnk.AccountId=@AccountId
      AND lnk.IsActive=1
      AND lnk.CompanyID=@CompanyID
      AND (lnk.IsCanceled=0 OR lnk.IsCanceled IS NULL)
      AND cc.AllowPosting=1
      AND (cc.IsCanceled=0 OR cc.IsCanceled IS NULL)
    ORDER BY lnk.EntryOrder ASC;
    -- إذا لم تُعِد أي صفوف → الحساب غير مرتبط بمراكز تكلفة → لا حقل للمركز
END;
```

### SP 4: WEBERP_JournalEntries_ValidateCostCenters — التحقق من مراكز التكلفة المطلوبة ⭐
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntries_ValidateCostCenters]
    @JournalEntryId INT,
    @CompanyID      INT, @BranchID INT,
    @IsValid        BIT OUTPUT,
    @ErrorMessage   NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @IsValid = 1;
    SET @ErrorMessage = NULL;

    -- جلب السطور التي تحتاج مراكز تكلفة مطلوبة ولم تُحدَّد
    DECLARE @MissingCenters TABLE (AccountCode NVARCHAR(50), AccountName NVARCHAR(200), CostCenterName NVARCHAR(200));

    INSERT INTO @MissingCenters
    SELECT DISTINCT
        a.Code,
        a.NameAr,
        cc.CostCenterNameAr
    FROM WEBERP_JournalEntryDetails d
    JOIN WEBERP_ChartOfAccounts a ON a.Id=d.AccountId
    JOIN WEBERP_AccountCostCenterLinks lnk ON lnk.AccountId=d.AccountId
        AND lnk.IsRequired=1 AND lnk.IsActive=1 AND lnk.CompanyID=@CompanyID
        AND (lnk.IsCanceled=0 OR lnk.IsCanceled IS NULL)
    JOIN WEBERP_CostCenters cc ON cc.CostCenterId=lnk.CostCenterId
    WHERE d.JournalEntryId=@JournalEntryId
      AND d.CompanyID=@CompanyID
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL)
      -- السطر لم يحدد مركز التكلفة المطلوب
      AND NOT EXISTS (
          SELECT 1 FROM WEBERP_JournalEntryDetails d2
          WHERE d2.JournalEntryId=d.JournalEntryId
            AND d2.AccountId=d.AccountId
            AND d2.CostCenterId=lnk.CostCenterId
            AND (d2.IsCanceled=0 OR d2.IsCanceled IS NULL)
      );

    IF EXISTS (SELECT 1 FROM @MissingCenters)
    BEGIN
        SET @IsValid = 0;
        SELECT @ErrorMessage = 'مراكز تكلفة مطلوبة غير محددة: ' +
            STRING_AGG(AccountCode + ' - ' + AccountName + ' → ' + CostCenterName, ' | ')
        FROM @MissingCenters;
    END
END;
```

### SP 5: WEBERP_JournalEntryDetails_Save — مع التحقق من مراكز التكلفة
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntryDetails_Save]
    @DetailId      INT=0,
    @JournalEntryId INT,
    @AccountId     INT,
    @CostCenterId  INT=NULL,
    @DebitAmount   DECIMAL(18,4)=0,
    @CreditAmount  DECIMAL(18,4)=0,
    @DescriptionAr NVARCHAR(500)=NULL,
    @DescriptionEn NVARCHAR(500)=NULL,
    @AnalyticDistribution NVARCHAR(MAX)=NULL,
    @CompanyID     INT, @BranchID INT, @UserId INT,
    @NewId         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. تحقق: الحساب يقبل القيود (ليس تجميعياً)
    IF EXISTS (
        SELECT 1 FROM WEBERP_ChartOfAccounts
        WHERE Id=@AccountId AND AllowPosting=0
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN RAISERROR('هذا حساب تجميعي ولا يقبل قيوداً مباشرة',16,1); RETURN; END

    -- 2. تحقق: مدين أو دائن فقط
    IF (@DebitAmount>0 AND @CreditAmount>0) OR (@DebitAmount=0 AND @CreditAmount=0)
    BEGIN RAISERROR('يجب إدخال مبلغ مدين أو دائن فقط — ليس الاثنين',16,1); RETURN; END

    -- 3. تحقق: مركز التكلفة صالح للترحيل (إذا حُدِّد)
    IF @CostCenterId IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM WEBERP_CostCenters
        WHERE CostCenterId=@CostCenterId AND AllowPosting=1
          AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN RAISERROR('مركز التكلفة المحدد غير صالح للترحيل',16,1); RETURN; END

    IF @DetailId=0
    BEGIN
        INSERT INTO WEBERP_JournalEntryDetails
            (JournalEntryId,AccountId,CostCenterId,
             DebitAmount,CreditAmount,
             DescriptionAr,DescriptionEn,
             AnalyticDistribution,
             CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES
            (@JournalEntryId,@AccountId,@CostCenterId,
             @DebitAmount,@CreditAmount,
             @DescriptionAr,@DescriptionEn,
             @AnalyticDistribution,
             @CompanyID,@BranchID,@UserId,GETDATE());
        SET @NewId=SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE WEBERP_JournalEntryDetails SET
            AccountId=@AccountId, CostCenterId=@CostCenterId,
            DebitAmount=@DebitAmount, CreditAmount=@CreditAmount,
            DescriptionAr=@DescriptionAr, DescriptionEn=@DescriptionEn,
            AnalyticDistribution=@AnalyticDistribution,
            ModifiedBy=@UserId, ModifiedAt=GETDATE()
        WHERE DetailId=@DetailId AND CompanyID=@CompanyID;
        SET @NewId=@DetailId;
    END

    -- تحديث إجماليات القيد
    UPDATE WEBERP_JournalEntries SET
        TotalDebit  = (SELECT ISNULL(SUM(DebitAmount),0)  FROM WEBERP_JournalEntryDetails WHERE JournalEntryId=@JournalEntryId AND (IsCanceled=0 OR IsCanceled IS NULL)),
        TotalCredit = (SELECT ISNULL(SUM(CreditAmount),0) FROM WEBERP_JournalEntryDetails WHERE JournalEntryId=@JournalEntryId AND (IsCanceled=0 OR IsCanceled IS NULL)),
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE JournalEntryId=@JournalEntryId;
END;
```

### SP 6: WEBERP_JournalEntries_Post — ترحيل مع كل التحققات
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntries_Post]
    @JournalEntryId INT,
    @CompanyID      INT, @BranchID INT,
    @UserId         INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق من الحالة
    DECLARE @EntryDate DATE, @FiscalYearId INT;
    SELECT @EntryDate=EntryDate, @FiscalYearId=FiscalYearId
    FROM WEBERP_JournalEntries
    WHERE JournalEntryId=@JournalEntryId AND IsPosted=0
      AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @EntryDate IS NULL
    BEGIN ROLLBACK; RAISERROR('القيد غير موجود أو مرحّل مسبقاً',16,1); RETURN; END

    -- 2. التحقق من الفترة مفتوحة
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_AccountingPeriods ap
        JOIN WEBERP_FiscalYears fy ON fy.Id=ap.FiscalYearId
        WHERE fy.Id=@FiscalYearId AND ap.IsOpen=1
          AND @EntryDate BETWEEN ap.StartDate AND ap.EndDate
          AND ap.CompanyID=@CompanyID
          AND (ap.IsCanceled=0 OR ap.IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('الفترة المحاسبية مغلقة أو غير موجودة',16,1); RETURN; END

    -- 3. التحقق من التوازن
    DECLARE @TotalDebit DECIMAL(18,4), @TotalCredit DECIMAL(18,4);
    SELECT @TotalDebit=TotalDebit, @TotalCredit=TotalCredit
    FROM WEBERP_JournalEntries WHERE JournalEntryId=@JournalEntryId;

    IF ABS(ISNULL(@TotalDebit,0) - ISNULL(@TotalCredit,0)) > 0.001
    BEGIN ROLLBACK; RAISERROR('القيد غير متوازن — المدين لا يساوي الدائن',16,1); RETURN; END

    -- 4. التحقق من وجود تفاصيل
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_JournalEntryDetails
        WHERE JournalEntryId=@JournalEntryId AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('القيد لا يحتوي على تفاصيل',16,1); RETURN; END

    -- 5. التحقق من مراكز التكلفة المطلوبة
    DECLARE @IsValid BIT, @ErrorMsg NVARCHAR(500);
    EXEC WEBERP_JournalEntries_ValidateCostCenters
        @JournalEntryId, @CompanyID, @BranchID,
        @IsValid OUTPUT, @ErrorMsg OUTPUT;

    IF @IsValid=0
    BEGIN ROLLBACK; RAISERROR('%s',16,1,@ErrorMsg); RETURN; END

    -- 6. الترحيل
    UPDATE WEBERP_JournalEntries SET
        IsPosted=1, PostedDate=GETDATE(), PostedBy=@UserId,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE JournalEntryId=@JournalEntryId;

    -- 7. تحديث أرصدة الفترة
    DECLARE @PeriodId INT;
    SELECT @PeriodId=Id FROM WEBERP_AccountingPeriods
    WHERE FiscalYearId=@FiscalYearId
      AND @EntryDate BETWEEN StartDate AND EndDate
      AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL);

    MERGE WEBERP_AccountBalances AS target
    USING (
        SELECT AccountId,
               SUM(DebitAmount)  AS Dr,
               SUM(CreditAmount) AS Cr
        FROM WEBERP_JournalEntryDetails
        WHERE JournalEntryId=@JournalEntryId AND (IsCanceled=0 OR IsCanceled IS NULL)
        GROUP BY AccountId
    ) AS src ON target.AccountId=src.AccountId
           AND target.FiscalYearId=@FiscalYearId
           AND target.PeriodId=@PeriodId
           AND target.CompanyID=@CompanyID
    WHEN MATCHED THEN UPDATE SET
        target.PeriodDebit  = target.PeriodDebit  + src.Dr,
        target.PeriodCredit = target.PeriodCredit + src.Cr,
        target.ClosingDebit  = target.OpeningDebit  + target.PeriodDebit  + src.Dr,
        target.ClosingCredit = target.OpeningCredit + target.PeriodCredit + src.Cr,
        target.ModifiedBy=@UserId, target.ModifiedAt=GETDATE()
    WHEN NOT MATCHED THEN INSERT
        (AccountId,FiscalYearId,PeriodId,PeriodDebit,PeriodCredit,ClosingDebit,ClosingCredit,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES (src.AccountId,@FiscalYearId,@PeriodId,src.Dr,src.Cr,src.Dr,src.Cr,@CompanyID,@BranchID,@UserId,GETDATE());

    COMMIT;
END;
```

### SP 7: WEBERP_JournalEntries_Unpost — إلغاء الترحيل ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntries_Unpost]
    @JournalEntryId INT,
    @Reason         NVARCHAR(300),
    @CompanyID      INT, @BranchID INT,
    @UserId         INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @EntryDate DATE, @FiscalYearId INT,
            @JournalType NVARCHAR(50), @ReversalJournalId INT;

    SELECT @EntryDate=EntryDate, @FiscalYearId=FiscalYearId,
           @JournalType=JournalType, @ReversalJournalId=ReversalJournalId
    FROM WEBERP_JournalEntries
    WHERE JournalEntryId=@JournalEntryId AND IsPosted=1
      AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @EntryDate IS NULL
    BEGIN ROLLBACK; RAISERROR('القيد غير موجود أو غير مرحّل',16,1); RETURN; END

    -- 1. لا يمكن إلغاء ترحيل قيد من موديول آخر مباشرة
    IF EXISTS (
        SELECT 1 FROM WEBERP_JournalEntrySources
        WHERE JournalEntryId=@JournalEntryId
          AND SourceModule <> 'Manual'
          AND CompanyID=@CompanyID
    )
    BEGIN
        ROLLBACK;
        RAISERROR('لا يمكن إلغاء ترحيل قيد من موديول آخر — ألغِ المستند المصدر أولاً',16,1);
        RETURN;
    END

    -- 2. لا يمكن إلغاء ترحيل قيد تم عكسه
    IF @ReversalJournalId IS NOT NULL
    BEGIN
        ROLLBACK;
        RAISERROR('لا يمكن إلغاء ترحيل قيد تم عكسه — ألغِ القيد العكسي أولاً',16,1);
        RETURN;
    END

    -- 3. لا يمكن إلغاء ترحيل في فترة مغلقة
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_AccountingPeriods
        WHERE FiscalYearId=@FiscalYearId AND IsOpen=1
          AND @EntryDate BETWEEN StartDate AND EndDate
          AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('الفترة المحاسبية مغلقة — لا يمكن إلغاء الترحيل',16,1); RETURN; END

    -- 4. عكس تأثير القيد على الأرصدة
    DECLARE @PeriodId INT;
    SELECT @PeriodId=Id FROM WEBERP_AccountingPeriods
    WHERE FiscalYearId=@FiscalYearId AND @EntryDate BETWEEN StartDate AND EndDate
      AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL);

    UPDATE ab SET
        ab.PeriodDebit  = ab.PeriodDebit  - d.DebitAmount,
        ab.PeriodCredit = ab.PeriodCredit - d.CreditAmount,
        ab.ClosingDebit  = ab.OpeningDebit  + ab.PeriodDebit  - d.DebitAmount,
        ab.ClosingCredit = ab.OpeningCredit + ab.PeriodCredit - d.CreditAmount,
        ab.ModifiedBy=@UserId, ab.ModifiedAt=GETDATE()
    FROM WEBERP_AccountBalances ab
    JOIN WEBERP_JournalEntryDetails d ON d.AccountId=ab.AccountId
    WHERE d.JournalEntryId=@JournalEntryId
      AND ab.FiscalYearId=@FiscalYearId AND ab.PeriodId=@PeriodId
      AND ab.CompanyID=@CompanyID
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL);

    -- 5. إلغاء الترحيل مع تسجيل السبب
    UPDATE WEBERP_JournalEntries SET
        IsPosted=0, UnpostedBy=@UserId,
        UnpostedDate=GETDATE(), UnpostReason=@Reason,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE JournalEntryId=@JournalEntryId;

    COMMIT;
END;
```

### SP 8: WEBERP_JournalEntries_Reverse — قيد عكسي
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntries_Reverse]
    @JournalEntryId INT,
    @ReversalDate   DATE,
    @Reason         NVARCHAR(200)=NULL,
    @CompanyID      INT, @BranchID INT,
    @UserId         INT,
    @NewJournalId   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_JournalEntries
        WHERE JournalEntryId=@JournalEntryId AND IsPosted=1
          AND ReversalJournalId IS NULL AND CompanyID=@CompanyID
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('القيد غير مرحّل أو لديه عكس مسبق',16,1); RETURN; END

    DECLARE @OrigNum NVARCHAR(20);
    SELECT @OrigNum=JournalNumber FROM WEBERP_JournalEntries WHERE JournalEntryId=@JournalEntryId;

    -- إنشاء القيد العكسي
    INSERT INTO WEBERP_JournalEntries
        (JournalNumber,EntryDate,JournalType,DescriptionAr,
         TotalDebit,TotalCredit,IsPosted,CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT 'REV-'+@OrigNum, @ReversalDate, 'Reversal',
           'عكس قيد: '+@OrigNum+ISNULL(' - '+@Reason,''),
           TotalDebit,TotalCredit,0,
           @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_JournalEntries WHERE JournalEntryId=@JournalEntryId;
    SET @NewJournalId=SCOPE_IDENTITY();

    -- نسخ التفاصيل معكوسة
    INSERT INTO WEBERP_JournalEntryDetails
        (JournalEntryId,AccountId,CostCenterId,
         DebitAmount,CreditAmount,DescriptionAr,AnalyticDistribution,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT @NewJournalId,AccountId,CostCenterId,
           CreditAmount,DebitAmount,    -- ← العكس
           'عكس: '+ISNULL(DescriptionAr,''),AnalyticDistribution,
           @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_JournalEntryDetails
    WHERE JournalEntryId=@JournalEntryId AND (IsCanceled=0 OR IsCanceled IS NULL);

    -- ربط القيد الأصلي بالعكسي
    UPDATE WEBERP_JournalEntries
    SET ReversalJournalId=@NewJournalId, ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE JournalEntryId=@JournalEntryId;

    COMMIT;
END;
```

### SP 9: WEBERP_AccountingPeriods_Close — إقفال الفترة ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_AccountingPeriods_Close]
    @PeriodId  INT,
    @CompanyID INT, @BranchID INT,
    @UserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @FiscalYearId INT, @PeriodNumber INT;
    SELECT @FiscalYearId=FiscalYearId, @PeriodNumber=PeriodNumber
    FROM WEBERP_AccountingPeriods
    WHERE Id=@PeriodId AND IsOpen=1 AND CompanyID=@CompanyID
      AND (IsCanceled=0 OR IsCanceled IS NULL);

    IF @FiscalYearId IS NULL
    BEGIN ROLLBACK; RAISERROR('الفترة غير موجودة أو مغلقة مسبقاً',16,1); RETURN; END

    -- التحقق: لا قيود غير مرحّلة في هذه الفترة
    IF EXISTS (
        SELECT 1 FROM WEBERP_JournalEntries j
        JOIN WEBERP_AccountingPeriods ap ON j.FiscalYearId=ap.FiscalYearId
            AND j.EntryDate BETWEEN ap.StartDate AND ap.EndDate
        WHERE ap.Id=@PeriodId AND j.IsPosted=0
          AND j.CompanyID=@CompanyID AND (j.IsCanceled=0 OR j.IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('يوجد قيود غير مرحّلة في هذه الفترة',16,1); RETURN; END

    -- إقفال الفترة
    UPDATE WEBERP_AccountingPeriods SET
        IsOpen=0, ClosedDate=GETDATE(), ClosedBy=@UserId,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@PeriodId;

    COMMIT;
END;
```

### SP 10: WEBERP_FiscalYears_Close — إقفال السنة المالية ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_FiscalYears_Close]
    @FiscalYearId INT,
    @CompanyID    INT, @BranchID INT,
    @UserId       INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- التحقق
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_FiscalYears
        WHERE Id=@FiscalYearId AND IsClosed=0 AND CompanyID=@CompanyID
          AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('السنة غير موجودة أو مغلقة مسبقاً',16,1); RETURN; END

    -- التحقق: كل الفترات مغلقة
    IF EXISTS (
        SELECT 1 FROM WEBERP_AccountingPeriods
        WHERE FiscalYearId=@FiscalYearId AND IsOpen=1
          AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('يوجد فترات مفتوحة — أغلق كل الفترات أولاً',16,1); RETURN; END

    -- التحقق: لا قيود غير مرحّلة في السنة
    IF EXISTS (
        SELECT 1 FROM WEBERP_JournalEntries
        WHERE FiscalYearId=@FiscalYearId AND IsPosted=0
          AND CompanyID=@CompanyID AND (IsCanceled=0 OR IsCanceled IS NULL)
    )
    BEGIN ROLLBACK; RAISERROR('يوجد قيود غير مرحّلة في هذه السنة',16,1); RETURN; END

    -- إقفال السنة
    UPDATE WEBERP_FiscalYears SET
        IsClosed=1, ClosedDate=GETDATE(), ClosedBy=@UserId,
        IsActive=0,
        ModifiedBy=@UserId, ModifiedAt=GETDATE()
    WHERE Id=@FiscalYearId;

    COMMIT;
END;
```

### SP 11: WEBERP_FiscalYears_OpenNew — فتح سنة جديدة وترحيل الأرصدة ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_FiscalYears_OpenNew]
    @PreviousFiscalYearId INT,
    -- السنة المراد ترحيل أرصدتها
    @NewYearCode          NVARCHAR(20),
    @NewYearNameAr        NVARCHAR(100),
    @NewYearNameEn        NVARCHAR(100)=NULL,
    @NewStartDate         DATE,
    @NewEndDate           DATE,
    @RetainedEarningsAccountId INT,
    -- حساب الأرباح المحتجزة — يُقفَّل إليه صافي الدخل
    @CompanyID            INT, @BranchID INT,
    @UserId               INT,
    @NewFiscalYearId      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- 1. التحقق أن السنة السابقة مغلقة
    IF NOT EXISTS (
        SELECT 1 FROM WEBERP_FiscalYears
        WHERE Id=@PreviousFiscalYearId AND IsClosed=1 AND CompanyID=@CompanyID
    )
    BEGIN ROLLBACK; RAISERROR('السنة السابقة ليست مغلقة — أغلقها أولاً',16,1); RETURN; END

    -- 2. إنشاء السنة الجديدة
    INSERT INTO WEBERP_FiscalYears
        (YearCode,NameAr,NameEn,StartDate,EndDate,IsActive,IsClosed,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@NewYearCode,@NewYearNameAr,@NewYearNameEn,@NewStartDate,@NewEndDate,1,0,
         @CompanyID,@BranchID,@UserId,GETDATE());
    SET @NewFiscalYearId=SCOPE_IDENTITY();

    -- 3. إنشاء الفترات (12 شهر)
    DECLARE @Month INT=1, @PStart DATE=@NewStartDate, @PEnd DATE;
    WHILE @Month<=12
    BEGIN
        SET @PEnd = EOMONTH(DATEADD(MONTH,@Month-1,@NewStartDate));
        IF @PEnd > @NewEndDate SET @PEnd=@NewEndDate;

        INSERT INTO WEBERP_AccountingPeriods
            (FiscalYearId,PeriodNumber,NameAr,NameEn,StartDate,EndDate,IsOpen,
             CompanyID,BranchID,CreatedBy,CreatedAt)
        VALUES
            (@NewFiscalYearId,@Month,
             DATENAME(MONTH,@PStart)+' '+CAST(YEAR(@PStart) AS NVARCHAR),
             FORMAT(@PStart,'MMMM yyyy','en-US'),
             @PStart,@PEnd,1,
             @CompanyID,@BranchID,@UserId,GETDATE());

        SET @Month=@Month+1;
        SET @PStart=DATEADD(DAY,1,@PEnd);
        IF @PStart>@NewEndDate BREAK;
    END

    -- 4. ترحيل أرصدة حسابات الميزانية (أصول + خصوم + حقوق ملكية)
    -- حسابات الميزانية: من 1 إلى 3 (حسب الدليل المحاسبي)
    INSERT INTO WEBERP_AccountBalances
        (AccountId,FiscalYearId,PeriodId,OpeningDebit,OpeningCredit,
         PeriodDebit,PeriodCredit,ClosingDebit,ClosingCredit,
         CompanyID,BranchID,CreatedBy,CreatedAt)
    SELECT
        ab.AccountId,
        @NewFiscalYearId,
        NULL,                    -- رصيد افتتاحي (بدون فترة)
        ab.ClosingDebit,         -- الرصيد الختامي للسنة السابقة = الافتتاحي للجديدة
        ab.ClosingCredit,
        0, 0,                    -- لا حركة بعد
        ab.ClosingDebit,
        ab.ClosingCredit,
        @CompanyID,@BranchID,@UserId,GETDATE()
    FROM WEBERP_AccountBalances ab
    JOIN WEBERP_ChartOfAccounts a ON a.Id=ab.AccountId
    WHERE ab.FiscalYearId=@PreviousFiscalYearId
      AND ab.PeriodId IS NULL   -- الرصيد الإجمالي للسنة
      AND ab.CompanyID=@CompanyID
      -- الأصول (1) + الخصوم (2) + حقوق الملكية (3)
      AND LEFT(a.Code,1) IN ('1','2','3')
      AND (ab.IsCanceled=0 OR ab.IsCanceled IS NULL);

    -- 5. حساب صافي الدخل (إيرادات - مصروفات) وترحيله للأرباح المحتجزة
    DECLARE @NetIncome DECIMAL(18,4);
    SELECT @NetIncome =
        -- الإيرادات (4): طبيعتها دائن → الرصيد الصافي Cr - Dr
        SUM(CASE WHEN LEFT(a.Code,1)='4' THEN ab.ClosingCredit - ab.ClosingDebit ELSE 0 END)
        -- المصروفات (5): طبيعتها مدين → Dr - Cr
      - SUM(CASE WHEN LEFT(a.Code,1)='5' THEN ab.ClosingDebit - ab.ClosingCredit ELSE 0 END)
    FROM WEBERP_AccountBalances ab
    JOIN WEBERP_ChartOfAccounts a ON a.Id=ab.AccountId
    WHERE ab.FiscalYearId=@PreviousFiscalYearId
      AND ab.PeriodId IS NULL
      AND LEFT(a.Code,1) IN ('4','5')
      AND ab.CompanyID=@CompanyID
      AND (ab.IsCanceled=0 OR ab.IsCanceled IS NULL);

    -- إضافة صافي الدخل لحساب الأرباح المحتجزة في السنة الجديدة
    IF @NetIncome <> 0
    BEGIN
        IF EXISTS (
            SELECT 1 FROM WEBERP_AccountBalances
            WHERE AccountId=@RetainedEarningsAccountId
              AND FiscalYearId=@NewFiscalYearId AND PeriodId IS NULL
              AND CompanyID=@CompanyID
        )
            UPDATE WEBERP_AccountBalances SET
                OpeningCredit = OpeningCredit + @NetIncome,
                ClosingCredit = ClosingCredit + @NetIncome
            WHERE AccountId=@RetainedEarningsAccountId
              AND FiscalYearId=@NewFiscalYearId AND PeriodId IS NULL
              AND CompanyID=@CompanyID;
        ELSE
            INSERT INTO WEBERP_AccountBalances
                (AccountId,FiscalYearId,PeriodId,OpeningDebit,OpeningCredit,ClosingDebit,ClosingCredit,CompanyID,BranchID,CreatedBy,CreatedAt)
            VALUES
                (@RetainedEarningsAccountId,@NewFiscalYearId,NULL,0,@NetIncome,0,@NetIncome,@CompanyID,@BranchID,@UserId,GETDATE());
    END

    COMMIT;
END;
```

### SP 12: WEBERP_JournalEntries_RegisterSource — تسجيل مصدر القيد ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_JournalEntries_RegisterSource]
    @JournalEntryId  INT,
    @SourceModule    NVARCHAR(50),
    @SourceFormCode  INT=NULL,
    @SourceRecordId  INT=NULL,
    @SourceReference NVARCHAR(100)=NULL,
    @SourceCode      NVARCHAR(50)=NULL,
    @SourceDate      DATE=NULL,
    @CompanyID       INT, @BranchID INT,
    @UserId          INT
AS
BEGIN
    SET NOCOUNT ON;
    -- يُستدعى تلقائياً من كل موديول عند إنشاء قيد
    -- مثال: بعد WEBERP_SalesInvoices_Post → يُسجَّل المصدر
    MERGE WEBERP_JournalEntrySources AS target
    USING (SELECT @JournalEntryId AS JEId, @CompanyID AS CId) AS src
    ON target.JournalEntryId=src.JEId AND target.CompanyID=src.CId
    WHEN MATCHED THEN UPDATE SET
        SourceModule=@SourceModule, SourceFormCode=@SourceFormCode,
        SourceRecordId=@SourceRecordId, SourceReference=@SourceReference,
        SourceCode=@SourceCode, SourceDate=@SourceDate
    WHEN NOT MATCHED THEN INSERT
        (JournalEntryId,SourceModule,SourceFormCode,SourceRecordId,
         SourceReference,SourceCode,SourceDate,CompanyID,BranchID,CreatedBy,CreatedAt)
    VALUES
        (@JournalEntryId,@SourceModule,@SourceFormCode,@SourceRecordId,
         @SourceReference,@SourceCode,@SourceDate,@CompanyID,@BranchID,@UserId,GETDATE());
END;
```

---

## 📊 التقارير

### SP 13: WEBERP_TrialBalance_Get — ميزان المراجعة (عادي وهرمي)
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_TrialBalance_Get]
    @FiscalYearId INT=NULL,
    @FromDate     DATE=NULL,
    @ToDate       DATE=NULL,
    @Level        INT=NULL,
    -- NULL=كل المستويات، 1=المستوى الأول فقط
    @ShowHierarchy BIT=0,
    -- 1=يُضيف مسافات بادئة حسب Level
    @CompanyID    INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH Movements AS (
        SELECT d.AccountId,
               ISNULL(SUM(d.DebitAmount),0)  AS TotalDebit,
               ISNULL(SUM(d.CreditAmount),0) AS TotalCredit
        FROM WEBERP_JournalEntryDetails d
        JOIN WEBERP_JournalEntries j ON j.JournalEntryId=d.JournalEntryId
            AND j.IsPosted=1
            AND (@FiscalYearId IS NULL OR j.FiscalYearId=@FiscalYearId)
            AND (@FromDate IS NULL OR j.EntryDate>=@FromDate)
            AND (@ToDate   IS NULL OR j.EntryDate<=@ToDate)
        WHERE d.CompanyID=@CompanyID AND (d.IsCanceled=0 OR d.IsCanceled IS NULL)
        GROUP BY d.AccountId
    )
    SELECT
        a.Id        AS AccountId,
        a.Code,
        -- الاسم مع مسافة بادئة للعرض الهرمي
        CASE WHEN @ShowHierarchy=1
             THEN REPLICATE('    ', a.Level-1) + a.NameAr
             ELSE a.NameAr
        END         AS AccountNameAr,
        a.NameEn,
        a.Level, a.IsParent, a.ParentId, a.AllowPosting,
        a.NormalSide,
        ISNULL(m.TotalDebit,0)  AS TotalDebit,
        ISNULL(m.TotalCredit,0) AS TotalCredit,
        ISNULL(m.TotalDebit,0) - ISNULL(m.TotalCredit,0) AS Balance,
        -- الرصيد بالجانب الطبيعي
        CASE WHEN a.NormalSide='D'
             THEN ISNULL(m.TotalDebit,0)  - ISNULL(m.TotalCredit,0)
             ELSE ISNULL(m.TotalCredit,0) - ISNULL(m.TotalDebit,0)
        END         AS NaturalBalance,
        -- رصيد الافتتاح من AccountBalances
        ISNULL(ab.OpeningDebit,0)  AS OpeningDebit,
        ISNULL(ab.OpeningCredit,0) AS OpeningCredit
    FROM WEBERP_ChartOfAccounts a
    LEFT JOIN Movements m ON m.AccountId=a.Id
    LEFT JOIN WEBERP_AccountBalances ab ON ab.AccountId=a.Id
        AND ab.FiscalYearId=@FiscalYearId AND ab.PeriodId IS NULL
        AND ab.CompanyID=@CompanyID
    WHERE a.CompanyID=@CompanyID AND (a.IsCanceled=0 OR a.IsCanceled IS NULL)
      AND (@Level IS NULL OR a.Level=@Level)
    ORDER BY a.Code;
END;
```

### SP 14: WEBERP_GeneralLedger_Get — دفتر الأستاذ ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_GeneralLedger_Get]
    @AccountId    INT,
    @FiscalYearId INT=NULL,
    @FromDate     DATE=NULL,
    @ToDate       DATE=NULL,
    @CostCenterId INT=NULL,
    @CompanyID    INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- رصيد الافتتاح
    DECLARE @OpeningBalance DECIMAL(18,4)=0, @NormalSide NVARCHAR(1);
    SELECT @NormalSide=NormalSide FROM WEBERP_ChartOfAccounts WHERE Id=@AccountId;

    SELECT @OpeningBalance =
        CASE WHEN @NormalSide='D'
             THEN ISNULL(SUM(d.DebitAmount),0) - ISNULL(SUM(d.CreditAmount),0)
             ELSE ISNULL(SUM(d.CreditAmount),0) - ISNULL(SUM(d.DebitAmount),0)
        END
    FROM WEBERP_JournalEntryDetails d
    JOIN WEBERP_JournalEntries j ON j.JournalEntryId=d.JournalEntryId AND j.IsPosted=1
    WHERE d.AccountId=@AccountId AND d.CompanyID=@CompanyID
      AND (@FiscalYearId IS NULL OR j.FiscalYearId=@FiscalYearId)
      AND (@FromDate IS NULL OR j.EntryDate < @FromDate)
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL);

    -- الحركات
    SELECT
        j.JournalEntryId, j.JournalNumber,
        j.EntryDate, j.JournalType,
        j.DescriptionAr AS JournalDesc,
        d.DescriptionAr AS LineDesc,
        cc.CostCenterNameAr,
        d.DebitAmount,
        d.CreditAmount,
        -- الرصيد المتراكم
        @OpeningBalance +
        SUM(CASE WHEN @NormalSide='D'
                 THEN d.DebitAmount - d.CreditAmount
                 ELSE d.CreditAmount - d.DebitAmount
            END) OVER (ORDER BY j.EntryDate, j.JournalEntryId, d.DetailId) AS RunningBalance,
        -- مصدر القيد
        src.SourceModule,
        src.SourceReference,
        src.SourceCode
    FROM WEBERP_JournalEntryDetails d
    JOIN WEBERP_JournalEntries j ON j.JournalEntryId=d.JournalEntryId AND j.IsPosted=1
    LEFT JOIN WEBERP_CostCenters cc ON cc.CostCenterId=d.CostCenterId
    LEFT JOIN WEBERP_JournalEntrySources src ON src.JournalEntryId=j.JournalEntryId
        AND src.CompanyID=@CompanyID
    WHERE d.AccountId=@AccountId AND d.CompanyID=@CompanyID
      AND (@FiscalYearId IS NULL OR j.FiscalYearId=@FiscalYearId)
      AND (@FromDate IS NULL OR j.EntryDate>=@FromDate)
      AND (@ToDate   IS NULL OR j.EntryDate<=@ToDate)
      AND (@CostCenterId IS NULL OR d.CostCenterId=@CostCenterId)
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL)
    ORDER BY j.EntryDate, j.JournalEntryId, d.DetailId;
END;
```

### SP 15: WEBERP_AccountStatement_Get — كشف الحساب ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_AccountStatement_Get]
    @AccountId    INT,
    @FromDate     DATE,
    @ToDate       DATE,
    @CostCenterId INT=NULL,
    @CompanyID    INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        j.EntryDate,
        j.JournalNumber,
        ISNULL(src.SourceReference, j.JournalNumber) AS Reference,
        ISNULL(d.DescriptionAr, j.DescriptionAr) AS Description,
        cc.CostCenterNameAr AS CostCenter,
        src.SourceModule,
        d.DebitAmount,
        d.CreditAmount,
        SUM(d.DebitAmount - d.CreditAmount)
            OVER (ORDER BY j.EntryDate, j.JournalEntryId ROWS UNBOUNDED PRECEDING) AS RunningBalance
    FROM WEBERP_JournalEntryDetails d
    JOIN WEBERP_JournalEntries j ON j.JournalEntryId=d.JournalEntryId AND j.IsPosted=1
    LEFT JOIN WEBERP_CostCenters cc ON cc.CostCenterId=d.CostCenterId
    LEFT JOIN WEBERP_JournalEntrySources src ON src.JournalEntryId=j.JournalEntryId AND src.CompanyID=@CompanyID
    WHERE d.AccountId=@AccountId
      AND d.CompanyID=@CompanyID
      AND j.EntryDate BETWEEN @FromDate AND @ToDate
      AND (@CostCenterId IS NULL OR d.CostCenterId=@CostCenterId)
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL)
    ORDER BY j.EntryDate, j.JournalEntryId;
END;
```

### SP 16: WEBERP_TrialBalance_Analytic — ميزان مراجعة تحليلي ⭐ جديد
```sql
CREATE OR ALTER PROCEDURE [dbo].[WEBERP_TrialBalance_Analytic]
    @FiscalYearId INT=NULL,
    @FromDate     DATE=NULL,
    @ToDate       DATE=NULL,
    @CostCenterId INT=NULL,
    -- NULL=كل المراكز، وإلا فلتر بمركز معين
    @CompanyID    INT, @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ميزان يجمع الحركات حسب الحساب ومركز التكلفة معاً
    SELECT
        a.Code        AS AccountCode,
        a.NameAr      AS AccountName,
        a.Level, a.NormalSide,
        cc.CostCenterId,
        cc.CostCenterNameAr,
        ISNULL(SUM(d.DebitAmount),0)  AS TotalDebit,
        ISNULL(SUM(d.CreditAmount),0) AS TotalCredit,
        ISNULL(SUM(d.DebitAmount),0) - ISNULL(SUM(d.CreditAmount),0) AS Balance
    FROM WEBERP_JournalEntryDetails d
    JOIN WEBERP_JournalEntries j ON j.JournalEntryId=d.JournalEntryId AND j.IsPosted=1
        AND (@FiscalYearId IS NULL OR j.FiscalYearId=@FiscalYearId)
        AND (@FromDate IS NULL OR j.EntryDate>=@FromDate)
        AND (@ToDate   IS NULL OR j.EntryDate<=@ToDate)
    JOIN WEBERP_ChartOfAccounts a ON a.Id=d.AccountId
    LEFT JOIN WEBERP_CostCenters cc ON cc.CostCenterId=d.CostCenterId
    WHERE d.CompanyID=@CompanyID
      AND (@CostCenterId IS NULL OR d.CostCenterId=@CostCenterId)
      AND (d.IsCanceled=0 OR d.IsCanceled IS NULL)
    GROUP BY a.Code,a.NameAr,a.Level,a.NormalSide,
             cc.CostCenterId,cc.CostCenterNameAr
    ORDER BY a.Code, cc.CostCenterNameAr;
END;
```

---

## 🌱 Seed Data — الدليل المحاسبي الأساسي

```sql
-- ملاحظة: يُنفَّذ بعد إنشاء الجداول وإضافة الأعمدة الهرمية
DECLARE @CID INT=1, @BID INT=1, @UID INT=1;

-- ═══ المستوى الأول: الحسابات الجذرية ═══
INSERT INTO WEBERP_ChartOfAccounts (Code,NameAr,NameEn,NormalSide,IsParent,AllowPosting,Level,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('1','الأصول',     'Assets',      'D',1,0,1,@CID,@BID,@UID,GETDATE()),
('2','الخصوم',     'Liabilities', 'C',1,0,1,@CID,@BID,@UID,GETDATE()),
('3','حقوق الملكية','Equity',     'C',1,0,1,@CID,@BID,@UID,GETDATE()),
('4','الإيرادات',  'Revenue',     'C',1,0,1,@CID,@BID,@UID,GETDATE()),
('5','المصروفات',  'Expenses',    'D',1,0,1,@CID,@BID,@UID,GETDATE());

-- ═══ المستوى الثاني ═══
DECLARE @A1 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='1' AND CompanyID=@CID);
DECLARE @A2 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='2' AND CompanyID=@CID);
DECLARE @A3 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='3' AND CompanyID=@CID);
DECLARE @A4 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='4' AND CompanyID=@CID);
DECLARE @A5 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='5' AND CompanyID=@CID);

INSERT INTO WEBERP_ChartOfAccounts (Code,NameAr,NameEn,NormalSide,ParentId,IsParent,AllowPosting,Level,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('1-1','الأصول المتداولة',    'Current Assets',      'D',@A1,1,0,2,@CID,@BID,@UID,GETDATE()),
('1-2','الأصول الثابتة',      'Fixed Assets',        'D',@A1,1,0,2,@CID,@BID,@UID,GETDATE()),
('2-1','خصوم متداولة',        'Current Liabilities', 'C',@A2,1,0,2,@CID,@BID,@UID,GETDATE()),
('2-2','خصوم غير متداولة',    'Non-Current Liab.',   'C',@A2,1,0,2,@CID,@BID,@UID,GETDATE()),
('3-1','رأس المال',           'Capital',             'C',@A3,0,1,2,@CID,@BID,@UID,GETDATE()),
('3-2','الأرباح المحتجزة',    'Retained Earnings',   'C',@A3,0,1,2,@CID,@BID,@UID,GETDATE()),
('4-1','إيرادات مبيعات',      'Sales Revenue',       'C',@A4,0,1,2,@CID,@BID,@UID,GETDATE()),
('4-2','إيرادات خدمات',       'Service Revenue',     'C',@A4,0,1,2,@CID,@BID,@UID,GETDATE()),
('5-1','مصروفات رواتب',       'Salaries Expense',    'D',@A5,0,1,2,@CID,@BID,@UID,GETDATE()),
('5-2','مصروفات إيجار',       'Rent Expense',        'D',@A5,0,1,2,@CID,@BID,@UID,GETDATE()),
('5-3','مصروفات كهرباء',      'Electricity Expense', 'D',@A5,0,1,2,@CID,@BID,@UID,GETDATE());

-- ═══ المستوى الثالث ═══
DECLARE @A11 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='1-1' AND CompanyID=@CID);
DECLARE @A12 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='1-2' AND CompanyID=@CID);
DECLARE @A21 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='2-1' AND CompanyID=@CID);
DECLARE @A22 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='2-2' AND CompanyID=@CID);

INSERT INTO WEBERP_ChartOfAccounts (Code,NameAr,NameEn,NormalSide,ParentId,IsParent,AllowPosting,Level,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('1-1-1','نقدية بالصندوق', 'Cash on Hand',    'D',@A11,0,1,3,@CID,@BID,@UID,GETDATE()),
('1-1-2','نقدية بالبنك',   'Cash at Bank',    'D',@A11,0,1,3,@CID,@BID,@UID,GETDATE()),
('1-1-3','عملاء',           'Accounts Recv.',  'D',@A11,0,1,3,@CID,@BID,@UID,GETDATE()),
('1-2-1','مباني',           'Buildings',       'D',@A12,0,1,3,@CID,@BID,@UID,GETDATE()),
('1-2-2','سيارات',          'Vehicles',        'D',@A12,0,1,3,@CID,@BID,@UID,GETDATE()),
('1-2-3','معدات',           'Equipment',       'D',@A12,0,1,3,@CID,@BID,@UID,GETDATE()),
('2-1-1','دائنون',          'Accounts Pay.',   'C',@A21,0,1,3,@CID,@BID,@UID,GETDATE()),
('2-1-2','أوراق دفع',       'Notes Payable',   'C',@A21,0,1,3,@CID,@BID,@UID,GETDATE()),
('2-2-1','قروض طويلة الأجل','Long-term Loans', 'C',@A22,0,1,3,@CID,@BID,@UID,GETDATE());

-- ═══ مراكز التكلفة الأساسية ═══
INSERT INTO WEBERP_CostCenters (CostCenterNameAr,CostCenterNameEn,AllowPosting,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('مشاريع الشركة',      'Company Projects',  0,@CID,@BID,@UID,GETDATE()),
('الأقسام الإدارية',   'Departments',       0,@CID,@BID,@UID,GETDATE()),
('الفروع',             'Branches',          0,@CID,@BID,@UID,GETDATE());

-- أبناء المراكز
DECLARE @CC1 INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='مشاريع الشركة' AND CompanyID=@CID);
DECLARE @CC2 INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='الأقسام الإدارية' AND CompanyID=@CID);
DECLARE @CC3 INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='الفروع' AND CompanyID=@CID);

INSERT INTO WEBERP_CostCenters (CostCenterNameAr,CostCenterNameEn,ParentId,AllowPosting,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
('مشروع تطوير النظام', 'System Dev Project', @CC1,1,@CID,@BID,@UID,GETDATE()),
('مشروع تطبيق الجوال', 'Mobile App Project', @CC1,1,@CID,@BID,@UID,GETDATE()),
('قسم الموارد البشرية','HR Department',      @CC2,1,@CID,@BID,@UID,GETDATE()),
('قسم التقنية',        'IT Department',      @CC2,1,@CID,@BID,@UID,GETDATE()),
('قسم المحاسبة',       'Accounting Dept.',   @CC2,1,@CID,@BID,@UID,GETDATE()),
('فرع الرياض',         'Riyadh Branch',      @CC3,1,@CID,@BID,@UID,GETDATE()),
('فرع جدة',           'Jeddah Branch',       @CC3,1,@CID,@BID,@UID,GETDATE());

-- ═══ ربط الحسابات بمراكز التكلفة ═══
-- مثال: مصروفات الرواتب → قسم الموارد البشرية (مطلوب)
DECLARE @Acc51 INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='5-1' AND CompanyID=@CID);
DECLARE @CChr  INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='قسم الموارد البشرية' AND CompanyID=@CID);
DECLARE @CCit  INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='قسم التقنية' AND CompanyID=@CID);

-- مصروفات رواتب → قسم الموارد البشرية (مطلوب، ترتيب 1)
INSERT INTO WEBERP_AccountCostCenterLinks (AccountId,CostCenterId,IsRequired,EntryOrder,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES (@Acc51,@CChr,1,1,1,@CID,@BID,@UID,GETDATE());

-- إيرادات المبيعات → مشروع أ (اختياري، ترتيب 1)، مشروع ب (اختياري، ترتيب 2)
DECLARE @Acc41  INT=(SELECT Id FROM WEBERP_ChartOfAccounts WHERE Code='4-1' AND CompanyID=@CID);
DECLARE @CCprj1 INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='مشروع تطوير النظام' AND CompanyID=@CID);
DECLARE @CCprj2 INT=(SELECT TOP 1 CostCenterId FROM WEBERP_CostCenters WHERE CostCenterNameAr='مشروع تطبيق الجوال' AND CompanyID=@CID);

INSERT INTO WEBERP_AccountCostCenterLinks (AccountId,CostCenterId,IsRequired,EntryOrder,IsActive,CompanyID,BranchID,CreatedBy,CreatedAt)
VALUES
(@Acc41,@CCprj1,0,1,1,@CID,@BID,@UID,GETDATE()),
(@Acc41,@CCprj2,0,2,1,@CID,@BID,@UID,GETDATE());
-- حسابات النقدية (1-1-1, 1-1-2) → لا ربط بأي مراكز
```

---

## 🗂️ Menus
```sql
INSERT INTO Menus (MenuCode,MenuNameAr,MenuNameEn,ParentCode,IsParent,FormCode,[Order],IsActive,CompanyID,BranchID) VALUES
(100,'الحسابات العامة',     'General Ledger',         NULL,1,NULL,1,1,1,1),
(101,'الدليل المحاسبي',     'Chart of Accounts',       100,0,100, 1,1,1,1),
(102,'السنوات المالية',      'Fiscal Years',            100,0,102, 2,1,1,1),
(103,'الفترات المحاسبية',    'Accounting Periods',      100,0,103, 3,1,1,1),
(104,'مراكز التكلفة',       'Cost Centers',            100,0,200, 4,1,1,1),
(105,'ربط الحسابات بالمراكز','Account-CostCenter Links',100,0,NULL,5,1,1,1),
(106,'قيود اليومية',        'Journal Entries',         100,0,110, 6,1,1,1),
(107,'أرصدة الحسابات',      'Account Balances',        100,0,104, 7,1,1,1),
-- التقارير
(110,'تقارير GL',           'GL Reports',              100,1,NULL, 8,1,1,1),
(111,'ميزان المراجعة',      'Trial Balance',           110,0,NULL, 1,1,1,1),
(112,'ميزان هرمي',          'Hierarchical TB',         110,0,NULL, 2,1,1,1),
(113,'ميزان تحليلي',        'Analytic TB',             110,0,NULL, 3,1,1,1),
(114,'دفتر الأستاذ',        'General Ledger',          110,0,NULL, 4,1,1,1),
(115,'كشف الحساب',          'Account Statement',       110,0,NULL, 5,1,1,1);
```

---

## 📐 Indexes للأداء
```sql
CREATE INDEX IX_JE_FiscalYear ON WEBERP_JournalEntries(FiscalYearId, IsPosted, EntryDate);
CREATE INDEX IX_JE_Date       ON WEBERP_JournalEntries(EntryDate DESC, IsPosted, CompanyID);
CREATE INDEX IX_JED_Account   ON WEBERP_JournalEntryDetails(AccountId, CompanyID) INCLUDE(DebitAmount,CreditAmount);
CREATE INDEX IX_JED_CostCenter ON WEBERP_JournalEntryDetails(CostCenterId) WHERE CostCenterId IS NOT NULL;
CREATE INDEX IX_JES_Source    ON WEBERP_JournalEntrySources(SourceModule, SourceRecordId, CompanyID);
CREATE INDEX IX_AB_Account    ON WEBERP_AccountBalances(AccountId, FiscalYearId, PeriodId, CompanyID);
CREATE INDEX IX_ACCLINK_Account ON WEBERP_AccountCostCenterLinks(AccountId, IsActive, CompanyID);
CREATE INDEX IX_COA_Parent    ON WEBERP_ChartOfAccounts(ParentId, CompanyID) WHERE ParentId IS NOT NULL;
CREATE INDEX IX_COA_Code      ON WEBERP_ChartOfAccounts(Code, CompanyID) INCLUDE(NameAr,Level,IsParent,AllowPosting);
```

---

## ⚠️ قواعد لـ Claude Code

```
┌────────────────────────────┬────────────────────────────────────────────┐
│ القاعدة                    │ التطبيق                                    │
├────────────────────────────┼────────────────────────────────────────────┤
│ AllowPosting=0             │ تُرفض القيود على الحسابات التجميعية       │
│                            │ تحقق في JournalEntryDetails_Save           │
├────────────────────────────┼────────────────────────────────────────────┤
│ UpdateHierarchy            │ تُستدعى بعد كل Save أو Delete              │
│                            │ لتحديث IsParent + Level تلقائياً          │
├────────────────────────────┼────────────────────────────────────────────┤
│ ReversalJournalId          │ يمنع عكس القيد مرتين                      │
├────────────────────────────┼────────────────────────────────────────────┤
│ FiscalYearId + PeriodId    │ مطلوبان قبل ترحيل أي قيد                  │
├────────────────────────────┼────────────────────────────────────────────┤
│ EntryOrder في مراكز التكلفة│ يحدد ترتيب ظهور الحقول في الواجهة         │
│                            │ يُجلب عبر AccountCostCenterLinks_GetForEntry│
├────────────────────────────┼────────────────────────────────────────────┤
│ IsRequired في مراكز التكلفة│ يمنع الترحيل بدون تحديد المركز المطلوب    │
│                            │ تحقق في JournalEntries_Post                │
├────────────────────────────┼────────────────────────────────────────────┤
│ مصدر القيد                 │ كل قيد من موديول آخر →                    │
│                            │ EXEC RegisterSource بعد Post               │
├────────────────────────────┼────────────────────────────────────────────┤
│ Unpost من موديول آخر       │ ممنوع — ألغِ المستند المصدر أولاً         │
├────────────────────────────┼────────────────────────────────────────────┤
│ إقفال الفترة               │ لا قيود غير مرحّلة قبل الإقفال            │
│                            │ الفترة المغلقة لا تقبل قيوداً جديدة       │
├────────────────────────────┼────────────────────────────────────────────┤
│ ترحيل الأرصدة              │ حسابات الميزانية (1-2-3) → رصيد افتتاحي  │
│                            │ حسابات الدخل (4-5) → صافي الدخل          │
│                            │ → الأرباح المحتجزة (3-2)                  │
├────────────────────────────┼────────────────────────────────────────────┤
│ العملة على مستوى الفرع     │ CurrencyCode من جدول Branches              │
│                            │ لا من جدول Companies                       │
├────────────────────────────┼────────────────────────────────────────────┤
│ لا DELETE                  │ IsCanceled=1 دائماً                        │
└────────────────────────────┴────────────────────────────────────────────┘
```
