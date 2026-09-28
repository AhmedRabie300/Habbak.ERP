# موديول المشتريات (Purchasing) - النسخة المعدلة

> هذا الملف يتبع القالب الموحّد المحدد في `00-Project-Overview.md` (قسم 27)، ويطبّق الدروس المستفادة من الموديولات السابقة. أي مصطلح عام (RowVersion، IAuditableEntity، Idempotency...) مرجعه `00-Project-Overview.md` ولا يُعاد شرحه هنا.

---

## 1. الهدف من الموديول

إدارة دورة شراء الخامات والمستلزمات والمنتجات من الموردين، مع ربطها بالمخازن والحسابات العامة.

**الميزة الأساسية:** دورة المشتريات **قابلة للتخصيص** حسب احتياجات كل شركة/عميل، مع الحفاظ على التكامل مع باقي الموديولات.

---

## 2. دورة المشتريات القابلة للتخصيص (Configurable Purchase Cycle)

### 2.1 الفكرة العامة

دورة المشتريات ليست ثابتة في النظام — العميل (الشركة) يختار من شاشة الإعدادات **النمط المناسب لطبيعة عمله**، والنظام يتكيّف مع هذا النمط في كل شاشات الموديول.

### 2.2 الأنماط المدعومة

| النمط | الوصف | الخطوات | مناسب لـ |
|---|---|---|---|
| **النمط الكامل (Full Cycle)** | دورة كاملة بكل الخطوات | طلب شراء ← عروض أسعار ← أمر شراء ← فاتورة شراء ← إذن إضافة | شركات التصنيع، المشتريات المعقدة |
| **النمط المباشر (Direct)** | فاتورة شراء مباشرة مع الإذن | فاتورة شراء ← إذن إضافة (تلقائي) | محلات التجزئة، المشتريات البسيطة |
| **النمط بالأمر (Order-Based)** | أمر شراء ثم فاتورة ثم إذن | أمر شراء ← فاتورة شراء ← إذن إضافة | الشركات المتوسطة، المشتريات المخططة |
| **النمط بالطلب (Request-Based)** | طلب شراء ثم أمر شراء ثم فاتورة | طلب شراء ← أمر شراء ← فاتورة شراء ← إذن إضافة | الشركات الكبيرة، المشتريات بالطلبيات |
| **النمط المبسّط (Simplified)** | أمر شراء + إذن إضافة معاً | أمر شراء (يُنشئ إذن إضافة تلقائياً عند الاستلام) | محلات الجملة، المشتريات السريعة |

### 2.3 إعدادات دورة المشتريات (Purchase Cycle Settings)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `CycleType` | `enum` | ✅ | `Full` / `Direct` / `OrderBased` / `RequestBased` / `Simplified` |
| `RequiresPurchaseRequest` | `bool` | ✅ | هل طلب الشراء إلزامي قبل أي خطوة؟ |
| `RequiresQuotation` | `bool` | ✅ | هل عروض الأسعار إلزامية قبل أمر الشراء؟ |
| `RequiresPurchaseOrder` | `bool` | ✅ | هل أمر الشراء إلزامي قبل الفاتورة؟ |
| `RequiresGoodsReceipt` | `bool` | ✅ | هل إذن الإضافة إلزامي (أم تلقائي مع الفاتورة)؟ |
| `AllowInvoiceWithoutOrder` | `bool` | ✅ | هل يسمح بإنشاء فاتورة شراء بدون أمر شراء؟ |
| `AllowReceiptWithoutInvoice` | `bool` | ✅ | هل يسمح بإذن إضافة بدون فاتورة شراء؟ |
| `AutoCreateReceiptOnInvoicePost` | `bool` | ✅ | هل يُنشأ إذن إضافة تلقائياً عند ترحيل الفاتورة؟ |
| `AutoCreateInvoiceOnReceipt` | `bool` | ✅ | هل تُنشأ فاتورة تلقائياً عند إذن الإضافة؟ |
| `RequiresApprovalForPurchaseOrder` | `bool` | ✅ | هل أمر الشراء يحتاج موافقة؟ |
| `RequiresApprovalForInvoice` | `bool` | ✅ | هل فاتورة الشراء تحتاج موافقة؟ |
| `DefaultPaymentTerms` | `enum` | ✅ | شروط الدفع الافتراضية للموردين |
| `CapitalizeAdditionalCosts` | `bool` | ✅ | تُخانة المصروفات الإضافية على تكلفة المخزون (True) أو تُسجَّل كمصروف فترة (False) |

### 2.4 إعدادات الترقيم لكل مستند (Coding Rules)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `PurchaseRequestCodingRuleId` | `long` | ✅ | إعدادات ترقيم طلبات الشراء |
| `RFQCodingRuleId` | `long` | ✅ | إعدادات ترقيم عروض الأسعار |
| `PurchaseOrderCodingRuleId` | `long` | ✅ | إعدادات ترقيم أوامر الشراء |
| `PurchaseInvoiceCodingRuleId` | `long` | ✅ | إعدادات ترقيم فواتير الشراء |
| `GoodsReceiptCodingRuleId` | `long` | ✅ | إعدادات ترقيم أذون الإضافة |
| `PurchaseReturnCodingRuleId` | `long` | ✅ | إعدادات ترقيم المردودات |

---

## 3. التكامل بين المستندات (Document Linking)

### 3.1 المبدأ الأساسي

جميع مستندات المشتريات مترابطة بحيث يمكن **إنشاء مستند تابع من مستند سابق** مع **نسخ البيانات تلقائياً** مع إمكانية التعديل.

### 3.2 علاقات المستندات

```
طلب شراء (PurchaseRequest)
↓ (يمكن تحويله إلى)
عرض سعر (RFQ) ← → عروض موردين متعددة
↓ (اختيار أفضل عرض)
أمر شراء (PurchaseOrder)
↓ (يمكن إنشاء منه)
استلام/إذن إضافة (GoodsReceipt) ← → فاتورة شراء (PurchaseInvoice)
↓                                      ↓
└──────────────────────────────────────┘
                  ↓
         سداد مورد (SupplierPayment)
```

### 3.3 آلية النسخ التلقائي (Auto-Copy)

عند إنشاء مستند تابع من مستند سابق:

| الخطوة | ما يُنسَخ | ما يُسمح بتعديله |
|---|---|---|
| RFQ ← Purchase Order | الأصناف، الكميات، الوحدات | السعر، الخصم، شروط الدفع |
| Purchase Order ← Invoice | الأصناف، الكميات، الأسعار | الكمية (في حالة الاستلام الجزئي)، الخصم، الضريبة |
| Purchase Order ← Receipt | الأصناف، الكميات | الكمية (في حالة الاستلام الجزئي) |
| Purchase Request ← Purchase Order | الأصناف، الكميات | المورد، السعر، شروط الدفع |

### 3.4 قاعدة العمل الأساسية

أي مستند تابع يُنشأ من مستند سابق، ينسخ البيانات تلقائياً، مع إمكانية التعديل الكامل في المستند الجديد — مع الحفاظ على العلاقة المرجعية بينهما (`SourceDocumentType` + `SourceDocumentId`).

---

## 4. الكيانات والجداول

### 4.1 الموردون

#### `Supplier` (المورد)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | قياسي |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `TaxNumber` | `string?` | ❌ | الرقم الضريبي |
| `Phone` / `Email` / `Address` | `string?` | ❌ | — |
| `PaymentTerms` | `enum` | ✅ | `Cash` / `Net15` / `Net30` / `Net60` |
| `CreditLimit` | `decimal?` | ❌ | حد الائتمان |
| `CurrencyCode` | `string` | ✅ | عملة التعامل الافتراضية |
| `DefaultWarehouseId` | `long?` | ❌ | مخزن الاستلام الافتراضي |
| `IsActive` | `bool` | ✅ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

حسابات المورد (للربط المحاسبي):

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `DefaultPayableAccountId` | `long?` | ❌ | الحساب الافتراضي من إعدادات الشركة (يتجاوزه لو محدد) |
| `PayableAccountId` | `long?` | ❌ | حساب المورد (دائنون) — يتجاوز إعداد الشركة |
| `ExpenseAccountId` | `long?` | ❌ | حساب المصروفات الإضافية (نقل، شحن) |

### 4.2 طلب الشراء

#### `PurchaseRequest` (طلب شراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | — |
| `RequestNumber` | `string` | ✅ | تسلسل سنوي فريد |
| `RequestDate` | `DateOnly` | ✅ | — |
| `RequestedByUserId` | `long` | ✅ | مقدم الطلب |
| `Priority` | `enum` | ✅ | `Low` / `Normal` / `High` / `Urgent` |
| `Reason` | `string?` | ❌ | سبب الطلب |
| `Status` | `enum` | ✅ | `Draft` / `PendingApproval` / `Approved` / `Converted` / `Rejected` / `Cancelled` / `Archived` |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `Notes` | `string?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `PurchaseRequestLine` (بند طلب الشراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PurchaseRequestId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | — |
| `UnitId` | `long` | ✅ | — |
| `Notes` | `string?` | ❌ | — |

### 4.3 عروض الأسعار (RFQ)

#### `RequestForQuotation` (طلب عروض أسعار)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | — |
| `RFQNumber` | `string` | ✅ | — |
| `RFQDate` | `DateOnly` | ✅ | — |
| `PurchaseRequestId` | `long?` | ❌ | مرجع لطلب الشراء (إن وجد) |
| `Status` | `enum` | ✅ | `Draft` / `Sent` / `UnderReview` / `Awarded` / `Cancelled` / `Archived` |
| `RequiredDate` | `DateOnly?` | ❌ | — |
| `Notes` | `string?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `RFQSupplier` (مورد في طلب عروض)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `RFQId`, `SupplierId` | — | ✅ | — |
| `Status` | `enum` | ✅ | `Pending` / `Responded` / `Declined` |
| `ResponseDate` | `DateOnly?` | ❌ | — |

#### `RFQLine` (بند طلب عروض)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `RFQId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | — |
| `UnitId` | `long` | ✅ | — |

#### `RFQSupplierQuote` (عرض سعر من مورد)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `RFQSupplierId`, `RFQLineId` | — | ✅ | — |
| `UnitPrice` | `decimal` | ✅ | — |
| `DiscountPercentage` | `decimal?` | ❌ | — |
| `DeliveryDays` | `int?` | ❌ | — |
| `ValidUntil` | `DateOnly` | ✅ | تاريخ انتهاء صلاحية العرض |
| `IsExpired` | `bool` | ❌ | محسوب من `ValidUntil` |
| `IsSelected` | `bool` | ✅ | تم اختيار هذا العرض |
| `Notes` | `string?` | ❌ | — |

### 4.4 أمر الشراء

#### `PurchaseOrder` (أمر شراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | — |
| `OrderNumber` | `string` | ✅ | تسلسل سنوي فريد |
| `OrderDate` | `DateOnly` | ✅ | — |
| `SupplierId` | `long` | ✅ | — |
| `PurchaseRequestId` | `long?` | ❌ | مرجع لطلب الشراء (إن وجد) |
| `RFQId` | `long?` | ❌ | مرجع لطلب العروض (إن وجد) |
| `SelectedRFQSupplierQuoteId` | `long?` | ❌ | العرض المختار (من عروض الأسعار) |
| `CurrencyCode` | `string` | ✅ | عملة الأمر |
| `ExchangeRate` | `decimal` | ✅ | سعر الصرف وقت الأمر |
| `PaymentTerms` | `enum` | ✅ | شروط الدفع |
| `DeliveryTerms` | `enum` | ❌ | شروط التسليم (`FOB`, `CIF`, `EXW`...) |
| `ExpectedDeliveryDate` | `DateOnly?` | ❌ | — |
| `DeliveryAddress` | `string?` | ❌ | — |
| `Status` | `enum` | ✅ | `Draft` / `Sent` / `Confirmed` / `PartiallyReceived` / `FullyReceived` / `Invoiced` / `Closed` / `Cancelled` / `Rejected` / `Archived` |
| `Subtotal` / `TaxAmount` / `TotalAmount` | `decimal` | ✅ | — |
| `DiscountAmount` / `DiscountReason` | `decimal?` / `string?` | ❌ | — |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `Notes` | `string?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `PurchaseOrderLine` (بند أمر الشراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PurchaseOrderId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | الكمية المطلوبة |
| `ReceivedQuantity` | `decimal` | ✅ | الكمية المستلمة حتى الآن |
| `UnitPrice` | `decimal` | ✅ | — |
| `TotalPrice` | `decimal` | ✅ | — |
| `DiscountAmount` | `decimal?` | ❌ | — |
| `UnitId` | `long` | ✅ | — |
| `ExpectedDeliveryDate` | `DateOnly?` | ❌ | — |
| `Weight` | `decimal?` | ❌ | الوزن (لتوزيع المصروفات الإضافية `ByWeight`) |

### 4.5 فاتورة الشراء

#### `PurchaseInvoice` (فاتورة شراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | — |
| `InvoiceNumber` | `string` | ✅ | تسلسل سنوي فريد |
| `InvoiceDate` | `DateOnly` | ✅ | — |
| `DueDate` | `DateOnly` | ✅ | تاريخ استحقاق الدفع |
| `SupplierId` | `long` | ✅ | — |
| `SupplierInvoiceNumber` | `string?` | ❌ | رقم فاتورة المورد |
| `PurchaseOrderId` | `long?` | ❌ | مرجع لأمر الشراء (إن وجد) |
| `GoodsReceiptId` | `long?` | ❌ | مرجع لإذن الإضافة (إن وجد) |
| `SourceDocumentType` / `SourceDocumentId` | `string` / `long?` | ❌ | مرجع مرن للمستند المصدر |
| `CurrencyCode` | `string` | ✅ | — |
| `ExchangeRate` | `decimal` | ✅ | — |
| `PaymentTerms` | `enum` | ✅ | — |
| `Status` | `enum` | ✅ | `Draft` / `PendingApproval` / `Posted` / `PendingPayment` / `PartiallyPaid` / `Paid` / `Overdue` / `Cancelled` / `Rejected` / `Archived` |
| `Subtotal` / `TaxAmount` / `TotalAmount` | `decimal` | ✅ | — |
| `DiscountAmount` / `DiscountReason` | `decimal?` / `string?` | ❌ | — |
| `AdditionalCosts` | `decimal` | ✅ | نقل، شحن، جمارك (تُحمّل على تكلفة المخزون) |
| `AdditionalCostAllocationMethod` | `enum` | ❌ | `ByValue` / `ByQuantity` / `ByWeight` / `Manual` |
| `CommissionRate` | `decimal?` | ❌ | نسبة عمولة المشتريات |
| `CommissionAmount` | `decimal?` | ❌ | قيمة العمولة |
| `CommissionAccountId` | `long?` | ❌ | حساب العمولة في الدليل المحاسبي |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `PostedAtUtc` / `PostedBy` | — | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | القيد المحاسبي عبر `IPostingService` |
| `ETAStatus` | `enum` | ❌ | تكامل مع موديول ETA |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `PurchaseInvoiceLine` (بند فاتورة الشراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PurchaseInvoiceId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | — |
| `ReceivedQuantity` | `decimal` | ✅ | الكمية المستلمة فعلاً |
| `UnitPrice` | `decimal` | ✅ | — |
| `TotalPrice` | `decimal` | ✅ | — |
| `DiscountAmount` | `decimal?` | ❌ | — |
| `UnitId` | `long` | ✅ | — |
| `LineCurrencyCode` | `string?` | ❌ | عملة البند (لو مختلفة عن عملة الفاتورة) |
| `LineExchangeRate` | `decimal?` | ❌ | سعر صرف البند |
| `LineAmountInBaseCurrency` | `decimal?` | ❌ | القيمة المحولة للعملة الأساسية |
| `AllocatedAdditionalCost` | `decimal` | ✅ | حصة هذا البند من المصروفات الإضافية |
| `AllocationPercentage` | `decimal?` | ❌ | للتوزيع اليدوي (`Manual`) |
| `Weight` | `decimal?` | ❌ | الوزن (لتوزيع المصروفات `ByWeight`) |

### 4.6 إذن إضافة (استلام مشتريات)

#### `GoodsReceipt` (إذن إضافة)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId`, `WarehouseId` | — | ✅ | — |
| `ReceiptNumber` | `string` | ✅ | — |
| `ReceiptDate` | `DateOnly` | ✅ | — |
| `SupplierId` | `long` | ✅ | — |
| `PurchaseOrderId` | `long?` | ❌ | مرجع لأمر الشراء (إن وجد) |
| `PurchaseInvoiceId` | `long?` | ❌ | مرجع لفاتورة الشراء (إن وجد) |
| `SourceDocumentType` / `SourceDocumentId` | `string` / `long?` | ❌ | مرجع مرن |
| `Status` | `enum` | ✅ | `Draft` / `Posted` / `Cancelled` / `Archived` |
| `Notes` | `string?` | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `GoodsReceiptLine` (بند إذن الإضافة)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `GoodsReceiptId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | الكمية المستلمة فعلاً |
| `AcceptedQuantity` | `decimal` | ✅ | الكمية المقبولة (نجحت في فحص الجودة) |
| `RejectedQuantity` | `decimal` | ✅ | الكمية المرفوضة (فشلت في فحص الجودة) |
| `RejectedReason` | `string?` | ⚠️ | إلزامي لو `RejectedQuantity > 0` |
| `RejectedWarehouseId` | `long?` | ⚠️ | مخزن التالف/المرتجعات (إلزامي لو `RejectedQuantity > 0`) |
| `UnitCost` | `decimal` | ✅ | — |
| `UnitId` | `long` | ✅ | — |
| `ExpectedQuantity` | `decimal?` | ❌ | الكمية المتوقعة (من أمر الشراء) |
| `VarianceQuantity` | `decimal?` | ❌ | الفرق (`Quantity - ExpectedQuantity`) |
| `VarianceReason` | `string?` | ⚠️ | إلزامي لو `VarianceQuantity ≠ 0` |
| `BatchNumber` | `string?` | ⚠️ | إلزامي لو `Item.IsTracked = true` |
| `ExpiryDate` | `DateOnly?` | ⚠️ | إلزامي لو `Item.IsTracked = true` |
| `QualityCheckStatus` | `enum` | ✅ | `Pending` / `Passed` / `PartiallyPassed` / `Failed` |
| `QualityCheckNotes` | `string?` | ❌ | — |

### 4.7 مردودات المشتريات

#### `PurchaseReturn` (مردود مشتريات)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | — |
| `ReturnNumber` | `string` | ✅ | — |
| `ReturnDate` | `DateOnly` | ✅ | — |
| `SupplierId` | `long` | ✅ | — |
| `PurchaseInvoiceId` | `long?` | ❌ | الفاتورة الأصلية |
| `Reason` | `enum` | ✅ | `Damaged` / `Expired` / `WrongItem` / `WrongQuantity` / `PriceMismatch` / `QualityIssue` / `Other` |
| `Status` | `enum` | ✅ | `Draft` / `Posted` / `Cancelled` / `Archived` |
| `Notes` | `string?` | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `PurchaseReturnLine` (بند المردود)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PurchaseReturnId`, `LineNumber` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `Quantity` | `decimal` | ✅ | — |
| `UnitCost` | `decimal` | ✅ | — |
| `UnitId` | `long` | ✅ | — |

### 4.8 مصروفات الشراء

#### `PurchaseExpense` (مصروف شراء)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId`, `PurchaseInvoiceId` | — | ✅ | — |
| `ExpenseType` | `enum` | ✅ | `Freight` / `Shipping` / `Customs` / `Loading` / `Insurance` / `Other` |
| `Amount` | `decimal` | ✅ | — |
| `AllocationMethod` | `enum` | ✅ | `ByValue` / `ByQuantity` / `ByWeight` / `Manual` |
| `Notes` | `string?` | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | — |

### 4.9 تاريخ أسعار الموردين (جديد)

#### `SupplierPriceHistory` (سجل أسعار الموردين)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id` | `long` | ✅ | — |
| `SupplierId` | `long` | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `UnitPrice` | `decimal` | ✅ | — |
| `UnitId` | `long` | ✅ | — |
| `EffectiveDate` | `DateOnly` | ✅ | — |
| `PurchaseInvoiceId` | `long?` | ❌ | مرجع للفاتورة (إن وجد) |
| `Notes` | `string?` | ❌ | — |
| `CreatedBy` / `CreatedAt` | — | ✅ | — |

### 4.10 عقود الموردين (جديد)

#### `SupplierContract` (عقد مورد)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id` | `long` | ✅ | — |
| `PublicId` | `string` | ✅ | — |
| `CompanyId` | `long` | ✅ | — |
| `SupplierId` | `long` | ✅ | — |
| `ContractNumber` | `string` | ✅ | — |
| `StartDate` | `DateOnly` | ✅ | — |
| `EndDate` | `DateOnly` | ✅ | — |
| `AutoRenew` | `bool` | ✅ | — |
| `Status` | `enum` | ✅ | `Active` / `Expired` / `Cancelled` / `Archived` |
| `Notes` | `string?` | ❌ | — |
| `RowVersion` | `byte[]` | ✅ | Concurrency |
| `IsDeleted` | `bool` | ✅ | Soft Delete |

#### `ContractItem` (صنف في العقد)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `SupplierContractId` | — | ✅ | — |
| `ItemId` | `long` | ✅ | — |
| `UnitPrice` | `decimal` | ✅ | — |
| `MinQuantity` | `decimal?` | ❌ | الحد الأدنى للشراء |
| `MaxQuantity` | `decimal?` | ❌ | الحد الأقصى للشراء |
| `DiscountPercentage` | `decimal?` | ❌ | خصم خاص بالعقد |

---

## 5. قواعد العمل (Business Rules)

1. **دورة المشتريات قابلة للتخصيص** — يحددها العميل من شاشة الإعدادات (`PurchaseCycleSettings`).
2. **أي مستند تابع يُنشأ من مستند سابق، ينسخ البيانات تلقائياً مع إمكانية التعديل** — مع الحفاظ على العلاقة المرجعية (`SourceDocumentType` + `SourceDocumentId`).
3. **لا يمكن إنشاء أمر شراء بدون مورد** — المورد إلزامي في كل الأحوال.
4. **لا يمكن ترحيل فاتورة شراء بدون استلام (إذن إضافة)** — إلا إذا كان الإعداد `AllowInvoiceWithoutReceipt = true`.
5. **لا يمكن ترحيل إذن إضافة بدون فاتورة شراء** — إلا إذا كان الإعداد `AllowReceiptWithoutInvoice = true`.
6. **المصروفات الإضافية (نقل، شحن، جمارك) تُحمّل على تكلفة المخزون** — حسب طريقة التوزيع المختارة (`AllocationMethod`).
7. **مردود المشتريات ينشئ قيداً محاسبياً عكسياً** — ويُقلل المخزون (أو يُعدِم التالف حسب السبب).
8. **سداد المورد يُغلَق الفاتورة** — يُحدّث `Status` إلى `PartiallyPaid` أو `Paid`.
9. **أي فاتورة شراء تتجاوز حداً معيناً تحتاج موافقة** — حسب `PurchaseCycleSettings.RequiresApprovalForInvoice`.
10. **الاستلام الجزئي مدعوم** — `PurchaseOrderLine.ReceivedQuantity` تُحدّث مع كل إذن إضافة.
11. **فروقات الكمية في الاستلام تُسجَّل** — `GoodsReceiptLine.VarianceQuantity` مع سبب إلزامي.
12. **فحص الجودة في الاستلام** — `GoodsReceiptLine.QualityCheckStatus` يمكن أن يكون `PartiallyPassed`، والكمية المرفوضة تُحوّل لمخزن تالف/مرتجعات.
13. **أسعار الشراء التاريخية تُسجَّل** — عبر `PurchaseInvoiceLine.UnitPrice` و`PurchaseOrderLine.UnitPrice` و`SupplierPriceHistory`.
14. **أمر الشراء لا يمكن تعديله بعد `Confirmed`** — أي تعديل يتطلب إلغاء الأمر وإنشاء أمر جديد.
15. **الفاتورة لا يمكن تعديلها بعد `Posted`** — أي تصحيح يكون عبر مردود مشتريات أو فاتورة تعديل.
16. **تكلفة المخزون تُحدّث عند ترحيل إذن الإضافة** — بناءً على `GoodsReceiptLine.UnitCost` + حصة المصروفات الإضافية.
17. **الموافقات متعددة المستويات مدعومة** — حسب `ApprovalLevel` و`Approvers` في `PurchaseCycleSettings`.
18. **العروض منتهية الصلاحية لا يمكن اختيارها** — `RFQSupplierQuote.IsExpired = true` تمنع الاختيار.
19. **لا يمكن إلغاء مستند بعد ترحيله** — فقط يمكن إنشاء مستند عكسي (مردود أو فاتورة تعديل).
20. **عقود الموردين تُستخدم في تحديد الأسعار التلقائية** — عند إنشاء أمر شراء، النظام يقترح سعر العقد إن وجد.

---

## 6. دورة حياة المستند / الحالات (Workflow / State Machine)

### 6.1 طلب الشراء (`PurchaseRequest`)

```
مسودة → بانتظار الاعتماد → معتمد → محوّل لأمر شراء → مغلق
       ↘ مرفوض (نهائي)
       ↘ ملغى
       ↘ مؤرشف
```

### 6.2 عروض الأسعار (`RequestForQuotation`)

```
مسودة → مرسل → قيد المراجعة → تم الترسية → مغلق
              ↘ ملغى
              ↘ مؤرشف
```

### 6.3 أمر الشراء (`PurchaseOrder`)

```
مسودة → مرسل → مؤكد → مستلم جزئياً / مستلم بالكامل → مفوترة → مغلق
                                                    ↘ ملغى
                                                    ↘ مرفوض (نهائي)
                                                    ↘ مؤرشف
```

### 6.4 فاتورة الشراء (`PurchaseInvoice`)

```
مسودة → بانتظار الاعتماد → مرحّل (ينشئ قيداً محاسبياً) → بانتظار السداد → مدفوعة جزئياً → مدفوعة
                          ↘ مرفوض (نهائي)
                          ↘ ملغى
                          ↘ مؤرشفة
```

### 6.5 إذن إضافة (`GoodsReceipt`)

```
مسودة → مرحّل (يزيد المخزون + ينشئ قيداً محاسبياً)
       ↘ ملغى
       ↘ مؤرشف
```

---

## 7. نظام الموافقات (Approval Workflow)

### 7.1 إعدادات الموافقات

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `ApprovalLevel` | `int` | ✅ | عدد مستويات الموافقة (1، 2، 3) |
| `ApprovalThreshold` | `decimal` | ❌ | حد المبلغ اللي يحتاج موافقة |
| `Approvers` | `List<long>` | ✅ | قائمة المعرفين بالموافقة لكل مستوى |
| `ApprovalTimeout` | `int` | ✅ | مدة انتظار الموافقة بالأيام |
| `AutoRejectAfterTimeout` | `bool` | ✅ | هل يرفض تلقائيًا بعد انتهاء المدة؟ |
| `EscalationApproverId` | `long?` | ❌ | المستخدم اللي يتصاعد له الطلب لو تأخر |

### 7.2 جدول الموافقات

#### `ApprovalInstance` (سجل الموافقة)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id` | `long` | ✅ | — |
| `DocumentType` | `enum` | ✅ | `PurchaseRequest` / `PurchaseOrder` / `PurchaseInvoice` |
| `DocumentId` | `long` | ✅ | — |
| `CurrentLevel` | `int` | ✅ | المستوى الحالي (يبدأ بـ 1) |
| `Status` | `enum` | ✅ | `Pending` / `Approved` / `Rejected` / `Expired` / `Escalated` |
| `RequestedBy` | `long` | ✅ | — |
| `RequestedAt` | `DateTime` | ✅ | — |
| `CompletedAt` | `DateTime?` | ❌ | — |
| `Notes` | `string?` | ❌ | — |

#### `ApprovalLevelLog` (سجل كل مستوى موافقة)

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `ApprovalInstanceId` | — | ✅ | — |
| `Level` | `int` | ✅ | — |
| `ApproverId` | `long` | ✅ | — |
| `Status` | `enum` | ✅ | `Pending` / `Approved` / `Rejected` |
| `DecisionDate` | `DateTime?` | ❌ | — |
| `Comments` | `string?` | ❌ | — |

---

## 8. الشاشات المطلوبة

| # | الشاشة | النوع | ملاحظات خاصة |
|---|---|---|---|
| 1 | الموردون | List/Edit | — |
| 2 | طلبات الشراء | List/Edit | — |
| 3 | عروض الأسعار (RFQ) | List/Edit | مع مقارنة عروض الموردين |
| 4 | أوامر الشراء | List/Edit | — |
| 5 | فواتير الشراء | List/Edit | مع توزيع المصروفات الإضافية |
| 6 | إذن إضافة (استلام مشتريات) | List/Edit | مع فحص الجودة وفروقات الكمية |
| 7 | مردودات المشتريات | List/Edit | — |
| 8 | مصروفات الشراء | List/Edit | توزيع على بنود الفاتورة |
| 9 | سداد الموردين | List/Edit | — |
| 10 | عقود الموردين | List/Edit | — |
| 11 | إعدادات دورة المشتريات | شاشة إعدادات | اختيار النمط، تفعيل/تعطيل الخطوات، إعدادات الترقيم والموافقات |
| 12 | تقييم أداء الموردين | List/Edit | جودة، التزام بالوقت، التزام بالكمية |
| 13 | تاريخ أسعار الموردين | تقرير | تتبع تغير الأسعار |

---

## 9. نقاط التكامل مع موديولات أخرى

| الموديول | الاتجاه | التفاصيل |
|---|---|---|
| المخازن (`02-Module-Inventory-Manufacturing.md`) | ➡️ خارج | إذن الإضافة يزيد المخزون، المردود يقلل المخزون |
| الحسابات العامة (`01-Module-Accounting.md`) | ➡️ خارج | قيود المشتريات، المدفوعات، المردودات عبر `IPostingService` |
| المبيعات (`04-Module-Sales.md`) | ⬅️ داخل | بيانات الموردين تُستخدم في التقارير المجمّعة |
| الامتثال الضريبي — ETA (`06-Module-ETA-Compliance.md`) | ➡️ خارج | إرسال فواتير المشتريات إلكترونياً |
| الإعدادات (`07-Module-Settings-Permissions.md`) | ⬅️ داخل | تعريف العملات، الضرائب، الفروع |

---

## 10. الصلاحيات الخاصة بالموديول

| الصلاحية | ملاحظة |
|---|---|
| إنشاء/تعديل مورد | — |
| إنشاء طلب شراء | — |
| اعتماد طلب شراء | منفصلة عن الإنشاء |
| إنشاء/تعديل أمر شراء | — |
| اعتماد أمر شراء | منفصلة عن الإنشاء |
| إنشاء/تعديل فاتورة شراء | — |
| ترحيل فاتورة شراء | منفصلة عن الإنشاء |
| إنشاء/تعديل إذن إضافة | — |
| ترحيل إذن إضافة | منفصلة عن الإنشاء |
| سداد مورد | — |
| إنشاء/تعديل عقد مورد | — |
| تجاوز حد ائتمان المورد | صلاحية استثنائية |
| تجاوز حد المبلغ للموافقة | صلاحية استثنائية |
| إدارة إعدادات دورة المشتريات | صلاحية إعدادات عامة |
| إدارة عقود الموردين | — |
| عرض تقارير المشتريات | — |

---

## 11. التقارير

| # | التقرير | نوعه |
|---|---|---|
| 1 | مشتريات حسب المورد | تفاعلي |
| 2 | مشتريات حسب الصنف | تفاعلي |
| 3 | أوامر شراء مفتوحة | ثابت |
| 4 | فواتير شراء غير مدفوعة | ثابت |
| 5 | مردودات المشتريات | ثابت |
| 6 | تحليل أسعار الشراء التاريخية | تفاعلي |
| 7 | تقييم أداء الموردين | ثابت |
| 8 | مصروفات الشراء (نقل، شحن، جمارك) | ثابت |
| 9 | عقود الموردين المنتهية | ثابت |
| 10 | تحليل توزيع المصروفات الإضافية | تفاعلي |
