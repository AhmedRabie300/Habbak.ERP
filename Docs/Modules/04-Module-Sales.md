# موديول المبيعات (Sales)

> هذا الملف يتبع القالب الموحّد المحدد في `00-Project-Overview.md` (قسم 27)، ويطبّق كل الدروس المستفادة من موديولي الحسابات والمخازن — `ParentId` الموحّدة، نمط `Type+Id` للمراجع المرنة، انعكاس حالة الرفض في الكيان نفسه، `BranchId` كعمود مباشر، وربط القيود المحاسبية بمركز تكلفة دايمًا. أي مصطلح عام مرجعه `00-Project-Overview.md` ولا يُعاد شرحه هنا.

> **حد الموديول**: هذا الملف يغطي **مبيعات الفواتير والعقود والعملاء ذوي الحساب** (شركات، عملاء آجل، اشتراكات) — وليس البيع السريع من الكاشير. **نقاط البيع (`05-Module-POS-Shifts.md`) موديول منفصل** بيستهلك `PriceList` و`Discount` من هذا الموديول، لكن له دورة مستندية خاصة به بالكامل.

---

## 1. الهدف من الموديول

إدارة العملاء وقوائم الأسعار والخصومات، ودورة البيع الكاملة للعملاء ذوي الحساب: عرض سعر ← أمر بيع ← تسليم ← فاتورة ← تحصيل، بالإضافة لعقود الاشتراكات المتكررة وبرنامج ولاء العملاء. **أي فاتورة أو مرتجع بيع ذو أثر مالي ينشئ قيده عبر `IPostingService`** (`00-Project-Overview.md` قسم 11)، وأي فاتورة آجلة تخضع لفحص حد ائتمان صارم قبل القبول.

---

## 2. الكيانات والجداول

### 2.1 العملاء وبرنامج الولاء

#### `Customer` (العميل)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | الفرع الأساسي للعميل، لو محدد (عمود مباشر، قاعدة 25) |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `CustomerType` | `enum` | ✅ | `Individual` / `Corporate` |
| `Phone` / `Email` / `Address` | `string?` | ❌ | — |
| `CreditLimit` | `decimal` | ✅ (افتراضي `0`) | **صفر = ممنوع البيع الآجل نهائيًا** ما لم يُحدَّد صراحة أعلى من صفر (قاعدة 26) |
| `PaymentTermDays` | `int` | ✅ (افتراضي `0`) | مهلة السداد الافتراضية بالأيام |
| `ReceivableAccountId` | `long?` | ❌ | حساب مدينون مخصص، أو `null` لاستخدام حساب "عملاء" العام الافتراضي |
| `LoyaltyTierId` | `long?` | ❌ | الشريحة الحالية — تُعاد حسابها تلقائيًا (قاعدة 13) |
| `LoyaltyPointsBalance` | `decimal` | ✅ (افتراضي `0`) | — |
| `RowVersion` | `byte[]` | ✅ | **Optimistic Concurrency إلزامي** — رصيد النقاط عرضة لتزامن عالٍ (بيع POS + بيع فاتورة + استبدال في نفس اللحظة)، بنفس منطق `StockBalance` في المخازن |
| `IsActive` | `bool` | ✅ | — |

#### `LoyaltyTier` (شريحة ولاء)
مثال: عادي، فضي، ذهبي، VIP.
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `DisplayOrder` | `int` | ✅ | — |
| `MinPointsThreshold` | `decimal` | ✅ | حد النقاط التراكمية للدخول في الشريحة |
| `EarnRateMultiplier` | `decimal` | ✅ (افتراضي `1.0`) | مضاعف معدل كسب النقاط لهذه الشريحة |

#### `LoyaltyProgramSettings` (إعدادات برنامج الولاء — شركة واحدة)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `PointsEarnRate` | `decimal` | ✅ | كام جنيه إنفاق = نقطة واحدة |
| `PointsRedemptionValue` | `decimal` | ✅ | قيمة النقطة الواحدة بالجنيه عند الاستبدال |

#### `LoyaltyTransaction` (حركة نقاط)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId`, `CustomerId` | — | ✅ | — |
| `TransactionType` | `enum` | ✅ | `Earn` / `Redeem` / `Expire` / `ManualAdjustment` |
| `Points` | `decimal` | ✅ | موجبة لـ`Earn`، سالبة للباقي |
| `SourceDocumentType` / `SourceDocumentId` | `string` / `long?` | ❌ | **قيم ثابتة**: `SalesInvoice`, `POSSale` |
| `TransactionDate` | `DateOnly` | ✅ | — |

### 2.2 التسعير والخصومات

#### `PriceList` (قائمة أسعار) + `PriceListBranch`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `EffectiveFromDate` / `EffectiveToDate` | `DateOnly` / `DateOnly?` | ✅ / ❌ | — |
| `IsActive` | `bool` | ✅ | — |
| **`PriceListBranch`**: `PriceListId`, `BranchId` | — | ✅ | ربط متعدد (Multi-select) — نفس القائمة ممكن تُفعَّل على أكتر من فرع |

#### `PriceListLine` (بند قائمة الأسعار)
**3 أعمدة سعر مستقلة على نفس السطر — مش 3 قوائم منفصلة (قاعدة 4).**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PriceListId`, `ItemId` | — | ✅ | قيد فريد مركّب |
| `DineInPrice` | `decimal` | ✅ | سعر الصالة |
| `TakeawayPrice` | `decimal` | ✅ | سعر التيك أواي |
| `DeliveryPrice` | `decimal` | ✅ | سعر الدليفري |

#### `Discount` (خصم)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `DiscountType` | `enum` | ✅ | `Percentage` / `FixedAmount` |
| `Value` | `decimal` | ✅ | — |
| `ApplicationPriority` | `int` | ✅ | **الأقل رقمًا يتطبق أولًا** (قاعدة 8) |
| `IsStackable` | `bool` | ✅ (افتراضي `false`) | لو `false`، تطبيقه يوقف أي خصم تاني على نفس الفاتورة (قاعدة 9) |
| `MinInvoiceAmount` / `MinQuantity` | `decimal?` | ❌ | حدود دنيا لتفعيل الخصم |
| `IsHappyHour` | `bool` | ✅ (افتراضي `false`) | — |
| `HappyHourFromTime` / `HappyHourToTime` | `TimeOnly?` | ⚠️ | إلزاميان لو `IsHappyHour = true` |
| `EffectiveFromDate` / `EffectiveToDate` | `DateOnly` / `DateOnly?` | ✅ / ❌ | — |
| `IsActive` | `bool` | ✅ | — |

### 2.3 عروض الأسعار وأوامر البيع

#### `SalesQuote` (عرض سعر) + `SalesQuoteLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | عمود مباشر (قاعدة 25) |
| `CustomerId` | `long` | ✅ | — |
| `QuoteNumber` / `QuoteDate` / `ValidUntil` | — | ✅ | — |
| `Status` | `enum` | ✅ | `Draft` / `Sent` / `Accepted` / `Rejected` / `Expired` / `Converted` |
| **`SalesQuoteLine`**: `ItemId`, `Quantity`, `UnitPrice`, `DiscountAmount` | — | ✅ | — |

#### `SalesOrder` (أمر بيع) + `SalesOrderLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | — |
| `CustomerId` | `long` | ✅ | — |
| `OrderNumber` / `OrderDate` | — | ✅ | — |
| `SourceQuoteId` | `long?` | ❌ | لو ناتج تحويل عرض سعر (قاعدة 15) |
| `Status` | `enum` | ✅ | `Draft` / `Confirmed` / `PartiallyDelivered` / `Delivered` / `Invoiced` / `Rejected` / `Cancelled` |
| **`SalesOrderLine`**: `ItemId`, `Quantity`, `UnitPrice`, `DeliveredQuantity` | — | ✅ | — |

### 2.4 الفواتير والتسليم والمرتجعات

#### `SalesInvoice` (فاتورة مبيعات) + `SalesInvoiceLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | — |
| `CustomerId` | `long` | ✅ | — |
| `InvoiceNumber` / `InvoiceDate` | — | ✅ | — |
| `SourceOrderId` / `SourceDeliveryOrderId` / `SourceContractId` | `long?` | ❌ | مراجع اختيارية — `SourceDeliveryOrderId` محدد يعني المخزون اتخصم بالفعل (قاعدة 18) |
| `PaymentType` | `enum` | ✅ | `Cash` / `Credit` |
| `CreditLimitOverrideApproved` | `bool` | ✅ (افتراضي `false`) | إلزامي `true` لو الفاتورة تجاوزت حد الائتمان وتم التجاوز بصلاحية (قاعدة 1) |
| `Subtotal` / `DiscountAmount` / `TaxAmount` / `Total` / `AmountPaid` | `decimal` | ✅ | — |
| `Status` | `enum` | ✅ | `Draft` / `PendingApproval` / `Posted` / `Rejected` / `Cancelled` |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | — |
| **`SalesInvoiceLine`**: `ItemId`, `Quantity`, `UnitPrice`, `DiscountAmount`, `LineTotal` | — | ✅ | — |

#### `DeliveryOrder` (أمر تسليم) + `DeliveryOrderLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | — |
| `CustomerId` | `long` | ✅ | — |
| `WarehouseId` | `long` | ✅ | مخزن الصرف — يتأثر `StockBalance` مباشرة وقت الترحيل (قاعدة 17) |
| `DeliveryNumber` / `DeliveryDate` | — | ✅ | — |
| `SourceOrderId` | `long?` | ⚠️ | محدد لو الدورة تبدأ من أمر بيع (`Full`/`OrderBased`/`DeliveryBased`/`Simplified` — قسم 2.6) |
| `SourceInvoiceId` | `long?` | ⚠️ | محدد لو الدورة تبدأ من فاتورة مباشرة تتطلب إذن صرف منفصل (`InvoiceWithIssue` — قسم 2.6). **بالضبط واحد من `SourceOrderId`/`SourceInvoiceId` لازم يكون محدد، مش الاتنين ولا ولا واحد فيهم (قاعدة 31)** |
| `Status` | `enum` | ✅ | `Draft` / `Posted` / `Rejected` / `Cancelled` |
| **`DeliveryOrderLine`**: `ItemId`, `Quantity` | — | ✅ | — |

#### `SalesReturn` (مرتجع مبيعات) + `SalesReturnLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | — |
| `CustomerId` | `long` | ✅ | — |
| `WarehouseId` | `long` | ✅ | مخزن الاستلام — يزيد `StockBalance` وقت الترحيل (قاعدة 19) |
| `SourceInvoiceId` | `long?` | ❌ | — |
| `ReturnNumber` / `ReturnDate` / `Reason` | — | ✅ | — |
| `Status` | `enum` | ✅ | `Draft` / `Posted` / `Rejected` / `Cancelled` |
| `JournalEntryId` | `long?` | ❌ | — |
| **`SalesReturnLine`**: `ItemId`, `Quantity`, `UnitPrice` | — | ✅ | — |

### 2.5 عقود المبيعات (اشتراكات متكررة)

#### `SalesContract` (عقد مبيعات) + `SalesContractLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `CustomerId` | — | ✅ | — |
| `ContractNumber` / `StartDate` | — | ✅ | — |
| `EndDate` | `DateOnly?` | ❌ | — |
| `BillingFrequency` | `enum` | ✅ | `Weekly` / `Monthly` / `Quarterly` |
| `AutoRenew` | `bool` | ✅ | — |
| `NextBillingDate` | `DateOnly` | ✅ | تتحدّث تلقائيًا بعد كل فاتورة تُصدَر منه (قاعدة 20، 21) |
| `Status` | `enum` | ✅ | `Active` / `Expired` / `Cancelled` |
| **`SalesContractLine`**: `ItemId`, `Quantity`, `UnitPrice` | — | ✅ | — |

### 2.6 إعدادات دورة المبيعات

#### `SalesCycleSettings` (إعدادات دورة المبيعات — صف واحد لكل شركة)
> **درس مستفاد إلزامي من تدقيق موديول المشتريات الفعلي**: `PurchaseCycleSettings.CycleType` اتبنى كـ Enum وصفي **بدون** ما يتقرا في أي Command Handler فعليًا — السلوك الحقيقي اعتمد على أعلام منفصلة انحرفت عن معنى الـ Enum بمرور الوقت. **هنا العكس تمامًا: `CycleType` مجرد Preset يملّي الأعلام تلقائيًا في واجهة الإعدادات، لكن الأعلام نفسها — مش قيمة الـ Enum — هي الوحيدة اللي أي Command Handler يتحقق منها (قاعدة 28).**

| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `CycleType` | `enum` | ✅ | `InvoiceOnly` / `InvoiceWithIssue` / `Full` / `OrderBased` / `DeliveryBased` / `Simplified` — **تسمية عرض في الواجهة فقط (Preset)**، غير مقروءة من أي منطق عمل مباشرة |
| `RequiresQuoteBeforeOrder` | `bool` | ✅ | إلزامية عرض سعر مقبول قبل إنشاء أمر بيع |
| `RequiresOrderBeforeInvoice` | `bool` | ✅ | لو `false`، الفاتورة تُنشأ مباشرة بدون أمر بيع سابق (`SourceOrderId = null` مسموح) |
| `RequiresSeparateDeliveryDocument` | `bool` | ✅ | لو `false`، الفاتورة نفسها تخصم `StockBalance` مباشرة (قاعدة 18 الأصلية). لو `true`، الخصم يتم حصريًا عبر `DeliveryOrder` منفصل |
| `InvoiceBeforeDelivery` | `bool` | ⚠️ | ذات معنى فقط لو `RequiresSeparateDeliveryDocument = true`. `true` = الفاتورة تُنشأ أولًا وتنشئ/تسمح بأمر تسليم لاحق (`DeliveryOrder.SourceInvoiceId`)؛ `false` = أمر التسليم يسبق الفاتورة (`DeliveryOrder.SourceOrderId` ثم `SalesInvoice.SourceDeliveryOrderId`) |
| `AutoGenerateDownstreamDocuments` | `bool` | ✅ | لو `true`، ترحيل المستند الأول في الدورة (أمر البيع غالبًا) ينشئ ويرحّل تلقائيًا كل المستندات التالية له (فاتورة + إذن صرف) بدون تدخل يدوي — يتطلب `IdempotencyKey` لكل مستند مولَّد آليًا (قاعدة 32) |

**جدول القيم المرجعية الرسمي لكل Preset** (يُطبَّق مرة واحدة عند اختيار `CycleType` من الواجهة، وقابل للتخصيص اليدوي بعدها):

| `CycleType` | `RequiresQuoteBeforeOrder` | `RequiresOrderBeforeInvoice` | `RequiresSeparateDeliveryDocument` | `InvoiceBeforeDelivery` | `AutoGenerateDownstreamDocuments` |
|---|---|---|---|---|---|
| `InvoiceOnly` | ❌ | ❌ | ❌ | — | ❌ |
| `InvoiceWithIssue` | ❌ | ❌ | ✅ | ✅ | ❌ |
| `Full` | ✅ | ✅ | ✅ | ✅ | ❌ |
| `OrderBased` | ❌ | ✅ | ✅ | ✅ | ❌ |
| `DeliveryBased` | ❌ | ✅ | ✅ | ❌ | ❌ |
| `Simplified` | ❌ | ✅ | ✅ | — | ✅ |

> **التحصيلات (Collections)**: لا يوجد كيان منفصل — هي واجهة مخصصة على `Voucher` الموجود بالفعل في `01-Module-Accounting.md` (`VoucherType = Receipt`, `CounterpartyType = Customer`)، لتفادي ازدواجية منطق السندات (قاعدة 23).

---

## 3. قواعد العمل (Business Rules)

1. **أي فاتورة بيع آجلة (`PaymentType = Credit`) تخضع لفحص حد الائتمان قبل القبول**: `(رصيد العميل المستحق الحالي + قيمة الفاتورة الجديدة) ≤ Customer.CreditLimit` — تجاوز الحد يُرفض الحفظ إلا بصلاحية خاصة (`OverrideCreditLimit`) وتأكيد صريح (`CreditLimitOverrideApproved = true`).
2. **البيع النقدي (`PaymentType = Cash`) لا يخضع لفحص حد الائتمان إطلاقًا** — الفحص خاص بالآجل فقط.
3. **رصيد العميل المستحق** يُحسب من إجمالي فواتير `Posted` غير المسددة بالكامل، مطروحًا منها إجمالي سندات القبض `Posted` المرتبطة بيها — نفس منطق تقرير أعمار ديون العملاء (`01-Module-Accounting.md`).
4. **كل صنف في `PriceListLine` له 3 أعمدة سعر مستقلة على نفس السطر** (صالة/تيك أواي/دليفري) — مش 3 قوائم أسعار منفصلة لكل طريقة تقديم.
5. **قائمة الأسعار الواحدة ممكن تُفعَّل على أكتر من فرع معًا** عبر `PriceListBranch` (Multi-select).
6. **لو صنف مش موجود في أي `PriceListLine` سارية للفرع وقت البيع، لا يُقبل بيعه بسعر تلقائي** — يتطلب سعرًا يدويًا بصلاحية خاصة أو رفض السطر.
7. **عند وجود أكتر من قائمة أسعار سارية لنفس الفرع في نفس الوقت، الأحدث `EffectiveFromDate` هي اللي تُطبَّق** — لا يُسمح بتعارض صامت.
8. **الخصومات تُطبَّق بترتيب `ApplicationPriority` تصاعديًا** (الأقل رقمًا أولًا).
9. **خصم `IsStackable = false` يوقف تطبيق أي خصم تالٍ على نفس الفاتورة** بعد تطبيقه — لا يجوز تراكم خصمين أحدهما غير قابل للتجميع.
10. **خصم `IsHappyHour = true` يُطبَّق تلقائيًا فقط لو وقت المستند الحالي واقع بين `HappyHourFromTime` و`HappyHourToTime`** — خارج هذا النطاق الزمني الخصم غير متاح حتى لو نشط (`IsActive = true`).
11. **نقاط الولاء تُكسب تلقائيًا عند ترحيل أي `SalesInvoice` أو بيع POS**، بمعدل `LoyaltyProgramSettings.PointsEarnRate` مضروبًا في `LoyaltyTier.EarnRateMultiplier` الخاص بالعميل، وتُسجَّل كـ`LoyaltyTransaction` نوع `Earn`.
12. **استبدال النقاط (`Redeem`) ينقص `Customer.LoyaltyPointsBalance` فورًا داخل Transaction صريحة مع تحديث `RowVersion`** — نفس صرامة `StockBalance` في المخازن (`00-Project-Overview.md` قسم 13)، لأن رصيد النقاط عرضة لتزامن عالٍ (بيع POS وبيع فاتورة واستبدال في نفس اللحظة على نفس العميل).
13. **شريحة العميل (`LoyaltyTierId`) تُعاد حسابها تلقائيًا بعد كل `LoyaltyTransaction` نوع `Earn`** مقارنة برصيد النقاط التراكمي و`LoyaltyTier.MinPointsThreshold` — لا تُحدَّث يدويًا.
14. **عرض السعر منتهي الصلاحية (`ValidUntil` < تاريخ اليوم) لا يُقبل تحويله لأمر بيع** — يتطلب عرض سعر جديد.
15. **تحويل عرض سعر مقبول (`Status = Accepted`) لأمر بيع ينسخ كل البنود تلقائيًا**، ويُحدّث `SalesQuote.Status = Converted`.
16. **أمر البيع لا يُعدَّل بعد بدء التسليم الجزئي (`Status = PartiallyDelivered`)** — التعديل الوحيد المسموح هو إنشاء أوامر تسليم/فواتير جديدة على الكمية المتبقية.
17. **أمر التسليم يخصم `StockBalance` مباشرة من `WarehouseId` المحدد وقت الترحيل** — تكامل مباشر مع `02-Module-Inventory-Manufacturing.md`.
18. **آلية خصم المخزون تتحدد حصريًا حسب `SalesCycleSettings.RequiresSeparateDeliveryDocument`**: لو `false`، الفاتورة نفسها تخصم `StockBalance` مباشرة وقت الترحيل. لو `true`، الخصم يتم حصريًا عبر `DeliveryOrder` منفصل — **ولا يجوز للفاتورة تخصم مخزونًا بنفسها أبدًا في هذه الحالة**، بغض النظر عن ترتيب الإنشاء (فاتورة أولًا كما في `InvoiceWithIssue`، أو أمر تسليم أولًا كما في `DeliveryBased`) — تفاصيل الإعداد كاملة في قسم 2.6.
19. **مرتجع المبيعات يزيد `StockBalance` في `WarehouseId` المحدد وقت الترحيل**، وينشئ قيد عكسي (كامل أو جزئي حسب قيمة المرتجع) عبر `IPostingService`.
20. **عقد المبيعات يولّد فاتورة تلقائيًا في `NextBillingDate`** حسب `BillingFrequency`، عبر مهمة مجدولة (`00-Project-Overview.md` قسم 21) — **الفاتورة الناتجة تحمل `IdempotencyKey` خاصًا بها** (قسم 14) لمنع التوليد المزدوج لو المهمة المجدولة أُعيد تشغيلها بالخطأ لنفس التاريخ.
21. **زر "إصدار فاتورة الآن" اليدوي على العقد يولّد فاتورة خارج الجدول الزمني العادي فورًا، ويُحدّث `NextBillingDate` للدورة التالية** بنفس منطق التوليد الآلي.
22. **رفض سلسلة الموافقات لأي `SalesQuote`, `SalesOrder`, `SalesInvoice`, `DeliveryOrder`, أو `SalesReturn` ينعكس فورًا في `Status` الكيان نفسه لقيمة `Rejected`** — نمط قياسي (`00-Project-Overview.md` قسم 12.3؛ `01-Module-Accounting.md` قاعدة 26؛ `02-Module-Inventory-Manufacturing.md` قاعدة 20). **الرفض نهائي** — لا تعديل وإعادة إرسال، مستند جديد بالكامل مطلوب.
23. **"التحصيلات" لا تملك كيانًا منفصلًا** — هي واجهة مخصصة (فلترة + إجراءات سريعة لربط سند بفاتورة) فوق `Voucher` الموجود في موديول الحسابات (`VoucherType = Receipt`, `CounterpartyType = Customer`) — تفاديًا لازدواجية منطق ترحيل السندات.
24. **أي `SalesInvoice` أو `SalesReturn` ذات أثر مالي تنشئ قيدها عبر `IPostingService`، حاملة `CostCenterId` مأخوذًا من الفرع المرتبط** — لا يوجد قيد مبيعات بدون تصنيف تحليلي (`01-Module-Accounting.md` قسم 2.1).
25. **`BranchId` عمود مباشر Nullable على كل مستندات المبيعات** (`Customer`, `SalesQuote`, `SalesOrder`, `SalesInvoice`, `DeliveryOrder`, `SalesReturn`) — لا يُستخدم أي بُعد تحليلي كبديل عنه (نفس مبدأ `01-Module-Accounting.md` قاعدة 25، `02-Module-Inventory-Manufacturing.md` قاعدة 24).
26. **`Customer.CreditLimit` الافتراضي صفر** — عميل جديد ممنوع من البيع الآجل تمامًا حتى يُحدَّد له حد ائتمان صريح أعلى من صفر.
27. **كل فاتورة مبيعات مُرحَّلة تتطلب إرسالها لمنظومة ETA** (`06-Module-ETA-Compliance.md`) — فاتورة غير مُرسَلة لـETA تمنع إقفال الفترة المالية المرتبطة (`01-Module-Accounting.md` قاعدة 8).
28. **أعلام `SalesCycleSettings` (`RequiresQuoteBeforeOrder`, `RequiresOrderBeforeInvoice`, `RequiresSeparateDeliveryDocument`, `InvoiceBeforeDelivery`, `AutoGenerateDownstreamDocuments`) هي المصدر الوحيد الذي يتحقق منه أي Command Handler في دورة المبيعات — `CycleType` نفسه لا يُقرأ في أي منطق عمل مباشرة، هو تسمية عرض في واجهة الإعدادات فقط.** هذه القاعدة إلزامية وغير قابلة للتفاوض، ومصدرها درس مستفاد موثَّق من تدقيق فعلي لموديول المشتريات (`04-Module-Purchasing.md`) حيث انحرف `CycleType` عن الأعلام الفعلية بمرور الوقت لأنه لم يكن مقروءًا من الأساس. **أي Pull Request يضيف فحصًا على `CycleType` مباشرة في Command Handler يُرفض في المراجعة.**
29. **تغيير أي علم من أعلام `SalesCycleSettings` بعد وجود مستندات دورة مبيعات مفتوحة (غير مكتملة الدورة) لا يؤثر بأثر رجعي على المستندات القائمة** — المستند يكمل دورته بنفس الإعداد الذي كان ساريًا وقت إنشائه (يُحفَظ Snapshot من الأعلام الأربعة على المستند الأول في الدورة، مش قراءة حية من `SalesCycleSettings` في كل خطوة).
30. **`RequiresOrderBeforeInvoice = false` يسمح بـ`SalesInvoice.SourceOrderId = null`** — فاتورة مباشرة بدون أمر بيع سابق مقبولة فقط لو هذا العلم `false`، وإلا تُرفض الفاتورة المباشرة عند الحفظ.
31. **`DeliveryOrder` لازم يحمل بالضبط واحدًا من `SourceOrderId` أو `SourceInvoiceId` — مش الاثنين معًا ولا لا واحد منهما** — الأول يعني الدورة بدأت من أمر بيع (`Full`/`OrderBased`/`DeliveryBased`/`Simplified`)، والثاني يعني الدورة بدأت من فاتورة مباشرة تتطلب إذن صرف تالٍ (`InvoiceWithIssue`).
32. **`AutoGenerateDownstreamDocuments = true` (وضع `Simplified`) يتطلب `IdempotencyKey` مستقلًا لكل مستند مولَّد آليًا** (الفاتورة وأمر التسليم الناتجان عن ترحيل أمر البيع) — نفس مبدأ التوليد الآلي لفواتير عقود المبيعات (قاعدة 20)، لمنع ازدواج التوليد لو عملية الترحيل الآلي أُعيدت بالخطأ.
33. **`DeliveryOrder` أُعيد تعريفه ليخدم كل سيناريوهات "صرف/تسليم البضاعة" في دورة المبيعات** — لا يوجد كيان `WarehouseDocument` منفصل من نوع صرف مبيعات؛ "إذن الصرف" المذكور في بعض أنماط `SalesCycleSettings` (`InvoiceWithIssue`, `Full`, `OrderBased`) هو نفسه `DeliveryOrder`، ببساطة بدون تعبئة أي حقول تسليم فعلي (عنوان/مندوب) لو البضاعة بتتسلّم من نقطة الاستلام مباشرة — تفاديًا لازدواجية كيانين لنفس الغرض الفعلي (خصم المخزون لعملية بيع).

---

## 4. دورة حياة المستند / الحالات (Workflow / State Machine)

### 4.1 عرض السعر (`SalesQuote`)
```
مسودة (Draft) → مُرسَل (Sent) → مقبول (Accepted) → محوَّل (Converted إلى SalesOrder)
                              ↘ مرفوض (Rejected) / منتهي الصلاحية (Expired)
```

### 4.2 أمر البيع (`SalesOrder`)
```
مسودة → مؤكَّد (Confirmed) → تسليم جزئي (PartiallyDelivered) → تسليم كامل (Delivered) → مفوتَر (Invoiced)
                                                                                    ↘ مرفوض / ملغى
```

### 4.3 فاتورة المبيعات (`SalesInvoice`)
```
مسودة → [فحص حد الائتمان (قاعدة 1) + سلسلة موافقات إن وُجدت] → مرحّلة (Posted، قيد محاسبي + إرسال ETA)
       ↘ مرفوضة (نهائي — قاعدة 22)
```
**خصم المخزون وقت الترحيل يعتمد على `SalesCycleSettings.RequiresSeparateDeliveryDocument` (قسم 2.6، قاعدة 18)**:
- `false` (`InvoiceOnly`): الترحيل يخصم `StockBalance` مباشرة كجزء من نفس العملية.
- `true` (باقي الأنماط): الترحيل **لا يخصم أي مخزون** — الخصم مسؤولية `DeliveryOrder` منفصل (قبل الفاتورة أو بعدها حسب `InvoiceBeforeDelivery`).

### 4.4 أمر التسليم والمرتجع (`DeliveryOrder` / `SalesReturn`)
```
مسودة → مرحّل (Posted، يؤثر على StockBalance مباشرة)
       ↘ مرفوض (نهائي)
```
`DeliveryOrder` بيتولّد من مصدرين محتملين حسب `InvoiceBeforeDelivery` (قاعدة 31): إما مرتبط بأمر بيع سابق (`SourceOrderId`) قبل وجود فاتورة، أو مرتبط بفاتورة موجودة بالفعل (`SourceInvoiceId`) في نمط `InvoiceWithIssue`.

### 4.5 عقد المبيعات (`SalesContract`)
```
نشط (Active) → [توليد فواتير دورية تلقائية عبر مهمة مجدولة] → منتهي (Expired) أو ملغى (Cancelled)
```

---

## 5. الشاشات المطلوبة

كل الشاشات تتبع نمط List/Edit القياسي (`00-Frontend-Specs.md` قسم 4-7) وتستخدم `ActionBar` الموحّد ما لم يُذكر خلاف ذلك.

| # | الشاشة | النوع | ملاحظات خاصة |
|---|---|---|---|
| 1 | العملاء | List/Edit قياسي | — |
| 2 | قوائم الأسعار | **شاشة تفاعلية** (غير قياسي) | اختيار فروع Multi-select + شبكة أصناف بـ3 أعمدة سعر لكل صنف |
| 3 | الخصومات | List/Edit قياسي | حقول Happy Hour تظهر شرطيًا لو `IsHappyHour = true` |
| 4 | برنامج الولاء (الشرائح والإعدادات) | **شاشة إعدادات تفاعلية** (غير قياسي) | — |
| 5 | عروض الأسعار | List/Edit قياسي | زر "تحويل لأمر بيع" يظهر فقط لو `Status = Accepted` |
| 6 | أوامر البيع | List/Edit قياسي | — |
| 7 | فواتير المبيعات | **شاشة تفاعلية** (غير قياسي) | بانر تحذير فوري عند تجاوز حد الائتمان + زر "تجاوز بصلاحية خاصة" |
| 8 | أوامر التسليم | List/Edit قياسي | — |
| 9 | مرتجعات المبيعات | List/Edit قياسي | — |
| 10 | التحصيلات | **عرض مخصص** فوق شاشة السندات (غير قياسي) | فلترة تلقائية: قبض + عميل |
| 11 | عقود المبيعات | List/Edit قياسي | زر "إصدار فاتورة الآن" (قاعدة 21) |
| 12 | إعدادات دورة المبيعات | **شاشة إعدادات** (غير قياسي) | اختيار `CycleType` كـPreset يملّي الأعلام الخمسة تلقائيًا، مع إمكانية تخصيص كل علم يدويًا بعدها (قسم 2.6) |

---

## 6. نقاط التكامل مع موديولات أخرى

| الموديول | الاتجاه | التفاصيل |
|---|---|---|
| **الحسابات العامة** (`01-Module-Accounting.md`) | ⬅️➡️ ثنائي | الفواتير/المرتجعات تنشئ قيودها عبر `IPostingService`؛ التحصيلات واجهة فوق `Voucher`؛ **تقارير أعمار ديون العملاء وكشف حساب عميل (موثّقة سابقًا كـ"غير منفَّذة") تصبح قابلة للتنفيذ الآن** بوجود `Customer` |
| **المخازن والتصنيع** (`02-Module-Inventory-Manufacturing.md`) | ⬅️ داخل | أوامر التسليم والمرتجعات تؤثر على `StockBalance` مباشرة |
| **نقاط البيع والورديات** (`05-Module-POS-Shifts.md`) | ➡️ خارج | يستهلك `PriceList` و`Discount` و`LoyaltyProgramSettings` من هذا الموديول |
| **الامتثال الضريبي** (`06-Module-ETA-Compliance.md`) | ➡️ خارج | كل فاتورة مبيعات مُرحَّلة تُرسَل إلزاميًا (قاعدة 27) |
| **التقارير ولوحة التحكم** (`09-Module-Reports-Dashboard.md`) | ➡️ خارج | يوفر بيانات تقارير المبيعات والعملاء والولاء |

---

## 7. الصلاحيات الخاصة بالموديول

بالإضافة للصلاحيات القياسية (قسم 6.1 من Overview):

| الصلاحية | ملاحظة |
|---|---|
| إنشاء/تعديل عميل | يشمل تعديل `CreditLimit` — قد تُقيَّد لدور مالي |
| **`OverrideCreditLimit`** — تجاوز حد الائتمان عند فاتورة آجلة | صلاحية استثنائية (قاعدة 1) |
| ترحيل فاتورة مبيعات | منفصلة عن "تعديل" |
| ترحيل مرتجع مبيعات | — |
| اعتماد عرض سعر لتحويله لأمر بيع | قد تكون ضمنية عبر حالة `Accepted` |
| **إدارة قوائم الأسعار والخصومات** | صلاحية محدودة — تغييرها يؤثر على كل نقاط البيع |
| **إدارة برنامج الولاء** (الشرائح والمعدلات) | صلاحية إدارية |
| استبدال نقاط ولاء نيابة عن عميل | — |
| **إدارة عقود المبيعات وإصدار فواتير يدوية منها** | — |

---

## 8. التقارير

| # | التقرير | نوعه |
|---|---|---|
| 1 | ربحية المنتج | ثابت |
| 2 | الأصناف الأكثر مبيعًا | ثابت |
| 3 | مبيعات حسب العميل | ثابت |
| 4 | مبيعات حسب الفرع | ثابت |
| 5 | أعمار ديون العملاء | ثابت — يكمّل التقرير الموثّق سابقًا في `01-Module-Accounting.md` |
| 6 | كشف حساب عميل | **تفاعلي** — Dropdown لاختيار العميل، يكمّل التقرير الموثّق سابقًا في `01-Module-Accounting.md` |
| 7 | تقرير نقاط الولاء | ثابت — نقاط ممنوحة/مستبدلة/منتهية خلال فترة |
| 8 | عقود مبيعات قاربت على الانتهاء | ثابت — خلال N يوم (نفس نمط تقرير مشابه في `04-Module-Purchasing.md`) |
| 9 | عروض أسعار منتهية الصلاحية أو غير محوَّلة | ثابت |
