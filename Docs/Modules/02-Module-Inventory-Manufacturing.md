# موديول المخازن والتصنيع (Inventory & Manufacturing)

> هذا الملف يتبع القالب الموحّد المحدد في `00-Project-Overview.md` (قسم 27)، ويطبّق كل الدروس المستفادة من مراجعات موديول الحسابات العامة (`01-Module-Accounting.md`) — تسمية `ParentId` الموحّدة، نمط `Type+Id` للمراجع المرنة، انعكاس حالة الرفض في الكيان نفسه، `BranchId` كعمود مباشر Nullable، وربط القيود المحاسبية الناتجة بمركز تكلفة دايمًا. أي مصطلح عام (RowVersion، IAuditableEntity، Idempotency...) مرجعه `00-Project-Overview.md` ولا يُعاد شرحه هنا.

---

## 1. الهدف من الموديول

الموديول ده بيغطي **دورة حياة المخزون الكاملة**: تعريف الأصناف والوحدات والمخازن، كل حركات الإضافة والصرف والتحويل بين المخازن والفروع، طلبات توريد الفروع واعتمادها، دورة الجرد الكاملة (كامل/جزئي/مفاجئ/دائري)، وموديول **التصنيع** (الوصفات متعددة المستويات، أوامر الإنتاج، الهالك). **أي حركة مخزون ليها أثر مالي بتتفاعل مع الحسابات عبر `IPostingService` (`00-Project-Overview.md` قسم 11)**، وأي مستند يحتاج اعتماد بيمر بمحرك سلاسل الموافقات (قسم 12) بنفس القواعد المُطبَّقة فعليًا في موديول الحسابات.

---

## 2. الكيانات والجداول

### 2.1 الأصناف والوحدات والمخازن

#### `UnitOfMeasure` (وحدة القياس)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | قياسي |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | مثال: `KG`, `كجم`, `Kilogram` |
| `IsActive` | `bool` | ✅ | — |

#### `ItemGroup` (مجموعة الأصناف — هرمية)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `ParentId` | `long?` | ❌ | هرمية المجموعات |
| `IsActive` | `bool` | ✅ | — |

#### `POSCategory` (تصنيف نقطة البيع)
**منفصل تمامًا عن `ItemGroup`** — `ItemGroup` لتصنيف المخزون والتقارير الداخلية، و`POSCategory` لتبويبات شاشة الكاشير فقط (مثال: قهوة / بن مطحون / عصائر / مياه / إضافات).
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `DisplayOrder` | `int` | ✅ | ترتيب ظهور التبويب في شاشة البيع |
| `IsActive` | `bool` | ✅ | — |

#### `Item` (الصنف)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | قياسي |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `ItemGroupId` | `long?` | ❌ | تصنيف المخزون الداخلي |
| `POSCategoryId` | `long?` | ❌ | تصنيف تبويب الكاشير — `null` للأصناف غير المباعة مباشرة (خامات) |
| `ItemType` | `enum` | ✅ | `RawMaterial` (خامة) / `SemiFinished` (نصف مصنع — ناتج وصفة ومكوّن في وصفة أخرى) / `FinishedGood` (تام الصنع) / `Consumable` (مستهلكات غير مخزنية بالضرورة) |
| `BaseUnitOfMeasureId` | `long` | ✅ | **وحدة القياس الأساسية** — كل الكميات في `StockBalance`/`StockTransaction` مُخزَّنة بهذه الوحدة حصريًا (قاعدة 28) |
| `IsSoldByWeight` | `bool` | ✅ (افتراضي `false`) | يتطلب تكامل الميزان وقت البيع (`00-Project-Overview.md` قسم 5.1/5.4) |
| `IsTracked` | `bool` | ✅ (افتراضي `false`) | يتطلب رقم دفعة (`BatchNumber`) إلزاميًا على أي حركة (قاعدة 8) — للأصناف القابلة للتلف أو محدودة الصلاحية |
| `ShelfLifeDays` | `int?` | ❌ | عمر الصلاحية بالأيام من تاريخ الإنتاج/الاستلام — يغذّي تقرير الأصناف قريبة الانتهاء |
| `StandardCost` | `decimal?` | ❌ | التكلفة المعيارية المستخدمة في تقارير انحراف التكلفة (قاعدة 16) — بعملة/وحدة `BaseUnitOfMeasureId` دايمًا |
| `AverageCost` | `decimal` | ✅ (افتراضي `0`) | **التكلفة الفعلية الحية** (متوسط مرجّح) — تُحدَّث تلقائيًا مع كل حركة استلام (قاعدة 32)، وهي المصدر الوحيد لتكلفة البضاعة المباعة الفعلية (COGS) — مختلفة عن `StandardCost` (تقديرية/معيارية للمقارنة بس) |
| `LastCostUpdateAt` | `DateTime?` | ❌ | آخر لحظة اتحدّثت فيها `AverageCost` |
| `IsActive` | `bool` | ✅ | — |

#### `ItemUnitConversion` (تحويل وحدات القياس لكل صنف)
**يسمح بإدخال/شراء/تحضير الصنف بوحدة مختلفة عن وحدته الأساسية** (مثال: شراء خامة بالشكارة/الكرتونة، تخزينها بالكيلو) — التخزين الفعلي في `StockBalance` يفضل بالوحدة الأساسية دايمًا (قاعدة 28)، والتحويل يحصل عند الإدخال فقط.
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `ItemId` | — | ✅ | — |
| `AlternateUnitOfMeasureId` | `long` | ✅ | الوحدة البديلة المسموح إدخال الكمية بيها لهذا الصنف |
| `ConversionFactor` | `decimal` | ✅ | **عدد الوحدات الأساسية (`BaseUnitOfMeasureId`) في الوحدة البديلة الواحدة** — مثال: `ConversionFactor = 50` يعني كل شكارة واحدة = 50 كجم (وليس العكس). حساب التحويل: `الكمية بالوحدة الأساسية = الكمية المُدخَلة بالوحدة البديلة × ConversionFactor` (قاعدة 32) |

#### `ItemWarehouseSettings` (إعدادات الصنف لكل مخزن)
**الحد الأدنى/الأقصى مختلف لكل مخزن، مش قيمة عامة واحدة للصنف (قاعدة 21).**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `ItemId`, `WarehouseId` | — | ✅ | قيد فريد مركّب `(ItemId, WarehouseId)` |
| `MinStockLevel` / `MaxStockLevel` / `ReorderPoint` | `decimal?` | ❌ | **3 من "الحدود الخمسة"** — الاثنان الباقيان في `BranchItemLimit` (قسم 2.4) — الفصل مقصود لأن دول حدود على **مستوى المخزون الفعلي**، والاثنان الآخران على **مستوى حجم طلب التوريد** نفسه (قاعدة 29) |

#### `Warehouse` (المخزن)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | قياسي |
| `BranchId` | `long?` | ❌ | **عمود مباشر Nullable** — `null` للمخازن المركزية (`WarehouseType = Main`)، ومحدد لمخازن الفروع (قاعدة 24) |
| `Code` / `NameAr` / `NameEn` | `string` | ✅ | — |
| `WarehouseType` | `enum` | ✅ | `Main` (رئيسي) / `BranchMaterials` (خامات فرع) / `Production` (تحضير/تصنيع) / `DamagedReturns` (تالف ومرتجعات) |
| `IsActive` | `bool` | ✅ | — |

### 2.2 الأرصدة والحركة المخزنية

#### `StockBalance` (رصيد الصنف بالمخزن)
**كيان حساس للتزامن (Concurrency-Critical) — بيتحدّث من مصادر متعددة في نفس اللحظة (بيع POS، تحويل، جرد).**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `ItemId`, `WarehouseId`, `CompanyId` | — | ✅ | قيد فريد مركّب `(ItemId, WarehouseId)` |
| `QuantityOnHand` | `decimal` | ✅ | لا يُقبل بالسالب (قاعدة 1) |
| `RowVersion` | `byte[]` | ✅ | **Optimistic Concurrency إلزامي** (`00-Project-Overview.md` قسم 13) — أهم استخدام له في النظام كله عمليًا |

#### `StockTransaction` (سجل الحركة التاريخي)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId`, `WarehouseId`, `ItemId` | — | ✅ | — |
| `TransactionType` | `enum` | ✅ | داخل: `Purchase`, `TransferIn`, `ProductionReceipt`, `AdjustmentIn`, `OpeningBalance` — خارج: `POSSale`, `TransferOut`, `ProductionIssue`, `AdjustmentOut`, `Waste` |
| `Quantity` | `decimal` | ✅ | قيمة موجبة دايمًا — الاتجاه محدد من `TransactionType` |
| `UnitCost` | `decimal` | ✅ | للتقييم (`inventoryValuationReport`) — **إلزامي وممنوع يكون صفر لصنف قيمته الفعلية أكبر من صفر (قاعدة 41)** |
| `TransactionDate` | `DateOnly` | ✅ | — |
| `ExpiryDate` | `DateOnly?` | ⚠️ | **إلزامي لو `Item.IsTracked = true` وحركة داخلة** (`Purchase`, `TransferIn`, `ProductionReceipt`) — تُحسَب افتراضيًا كـ `TransactionDate + Item.ShelfLifeDays`، **لكن قابلة للتعديل يدويًا** لو التاريخ الفعلي المطبوع على البضاعة مختلف (قاعدة 32) |
| `SourceDocumentType` / `SourceDocumentId` | `string` / `long?` | ❌ | مرجع مرن (Polymorphic) — **قيم `SourceDocumentType` الثابتة**: `WarehouseDocument`, `InventoryCount`, `ProductionOrder`, `POSSale` |
| `BatchNumber` | `string?` | ⚠️ | إلزامي لو `Item.IsTracked = true` (قاعدة 8) — **رقم تعريفي للدفعة فقط، لا يحتوي على تاريخ مشفَّر؛ تاريخ الصلاحية مصدره حصريًا حقل `ExpiryDate`** |
| `JournalEntryId` | `long?` | ❌ | القيد المحاسبي الناتج عبر `IPostingService` |

### 2.3 المستندات المخزنية الموحّدة

#### `WarehouseDocument` (مستند مخزني)
**نمط موحّد لكل أنواع مستندات الحركة — نفس فلسفة `Voucher` في موديول الحسابات (`VoucherType` واحد بدل جداول منفصلة).**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `BranchId` | `long?` | ❌ | عمود مباشر Nullable (قاعدة 24) |
| `DocumentType` | `enum` | ✅ | `StockIn` (إذن إضافة) / `StockOut` (إذن صرف) / `TransferOrder` (أمر تحويل) / `TransferReceipt` (استلام تحويل) / `InventoryAdjustment` (تسوية جرد) / `ProductionIssue` (صرف إنتاج) / `ProductionReceipt` (استلام إنتاج) |
| `DocumentNumber` / `DocumentDate` | — | ✅ | — |
| `SourceWarehouseId` | `long?` | ⚠️ | إلزامي لـ `StockOut`, `TransferOrder`, `ProductionIssue` |
| `DestinationWarehouseId` | `long?` | ⚠️ | إلزامي لـ `StockIn`, `TransferReceipt`, `ProductionReceipt` |
| `RelatedWarehouseDocumentId` | `long?` | ⚠️ | **مرجع ذاتي — إلزامي لـ `TransferReceipt` فقط**، بيشاور على `TransferOrder.Id` اللي طلع منه الاستلام ده (قاعدة 38) — ضمان تتبع كامل للتحويل من الطلب للاستلام |
| `CustodyOfficerId` | `long?` | ⚠️ | **إلزامي لـ `TransferOrder`**، **ومنسوخ تلقائيًا لـ `TransferReceipt` المرتبط عبر `RelatedWarehouseDocumentId`** (قاعدة 34) — نفس المسؤول يفضل مسؤولًا حتى اكتمال الاستلام |
| `Status` | `enum` | ✅ | `Draft` / `Posted` / `Rejected` / `Cancelled` — **`Rejected` نهائي وتلقائي عند رفض سلسلة الموافقات؛ لا يُعدَّل المستند المرفوض ويُعاد إرساله، أي محاولة جديدة تتطلب مستندًا جديدًا بالكامل (قاعدة 20، متسق مع `00-Project-Overview.md` قسم 12.3)** |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `JournalEntryId` | `long?` | ❌ | — |
| `Notes` | `string?` | ❌ | — |

#### `CustodyOfficer` (مسؤول عهدة نقل مخزني)
**مفهوم مختلف تمامًا عن `CustodyRegister` في موديول الحسابات** (اللي بيدير العُهد النقدية) — هنا المسؤولية عن **بضاعة فعلية** أثناء النقل، لغرض المساءلة عن أي عجز يحصل في الطريق.
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `EmployeeId` | `long` | ✅ | مرجع لموظف من `10-Module-HR-Payroll.md` |
| `BranchId` | `long?` | ❌ | الفرع الأساسي للمسؤول، لو محدد |
| `IsActive` | `bool` | ✅ | — |

#### `WarehouseDocumentLine` (بند المستند)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `WarehouseDocumentId`, `LineNumber`, `ItemId` | — | ✅ | — |
| `Quantity` / `UnitCost` | `decimal` | ✅ | — |
| `BatchNumber` | `string?` | ⚠️ | إلزامي لو `Item.IsTracked = true` |
| `ExpiryDate` | `DateOnly?` | ⚠️ | إلزامي لو `Item.IsTracked = true` — نفس منطق `StockTransaction.ExpiryDate` أعلاه، يُنسخ لسجل الحركة الناتج عند الترحيل |
| `ExpectedQuantity` | `decimal?` | ❌ | **تُستخدم حصريًا في `TransferReceipt`** — الكمية المُرسَلة أصلًا من `TransferOrder` |
| `VarianceQuantity` | `decimal?` | ❌ | محسوبة (`Quantity - ExpectedQuantity`) — تُعرض لحظيًا وقت الاستلام (قاعدة 7) |

### 2.4 طلبات توريد الفروع

#### `BranchRequest` (طلب توريد فرع)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `BranchId` | — | ✅ | فرع الطلب |
| `RequestNumber` / `RequestDate` / `RequestedByUserId` | — | ✅ | — |
| `Status` | `enum` | ✅ | `Draft` / `PendingApproval` / `Approved` / `Rejected` / `PartiallyFulfilled` / `Fulfilled` / `Cancelled` |
| `ApprovalInstanceId` | `long?` | ❌ | — |

#### `BranchRequestLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `BranchRequestId`, `ItemId` | — | ✅ | — |
| `RequestedQuantity` | `decimal` | ✅ | يُتحقَّق مقابل `BranchItemLimit` (قاعدة 4) |
| `ApprovedQuantity` | `decimal?` | ❌ | قد تقل عن المطلوب دون رفض كامل الطلب |

#### `BranchItemLimit` (حد الصنف للفرع)
**2 من "الحدود الخمسة" (قاعدة 29) — على مستوى حجم طلب التوريد الواحد، بمعزل تام عن حدود المخزون الفعلي في `ItemWarehouseSettings`.**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `BranchId`, `ItemId` | — | ✅ | قيد فريد مركّب |
| `MinRequestQuantity` | `decimal?` | ❌ | أقل كمية مسموح بطلبها في الطلب الواحد — لو محدد ومفيش طلب أعلى منه، يُرفض الطلب |
| `MaxRequestQuantity` | `decimal` | ✅ | أقصى كمية مسموح بطلبها في الطلب الواحد |

### 2.5 الجرد

#### `InventoryCount` (عملية جرد)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `WarehouseId` | — | ✅ | — |
| `CountNumber` / `CountDate` | — | ✅ | — |
| `CountType` | `enum` | ✅ | `Full` (كامل) / `Partial` (جزئي) / `Surprise` (مفاجئ) / `Cyclic` (دائري) |
| `Status` | `enum` | ✅ | `Draft` / `InProgress` / `PendingSettlement` / `Settled` / `Closed` / `Rejected` / `Cancelled` |
| `ApprovalInstanceId` | `long?` | ❌ | مطلوبة عادةً لاعتماد الإقفال |

#### `InventoryCountLine`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `InventoryCountId`, `ItemId` | — | ✅ | — |
| `SystemQuantity` | `decimal` | ✅ | رصيد `StockBalance` وقت بدء الجرد (Snapshot) |
| `CountedQuantity` | `decimal?` | ❌ | فارغة حتى يُعد الصنف فعليًا |
| `VarianceQuantity` | `decimal?` | ❌ | محسوبة |
| `SettlementDecision` | `enum` | ✅ | `Pending` / `Approved` / `Rejected` — قرار مستقل **لكل سطر على حدة** (قاعدة 10) |
| `SettlementReason` | `string?` | ⚠️ | إلزامي لو `VarianceQuantity ≠ 0` |

### 2.6 الوصفات والتصنيع

#### `Recipe` (وصفة / BOM)
**تدعم الإصدارات (Versioning) — تعديل وصفة معتمدة لا يُعدِّل السجل الموجود، بل ينشئ إصدارًا جديدًا (قاعدة 31)، بنفس فلسفة أسعار الصرف التاريخية (`00-Project-Overview.md` قسم 10).**
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId` | — | ✅ | — |
| `RecipeFamilyCode` | `string` | ✅ | كود ثابت مشترك بين كل إصدارات نفس الوصفة (مختلف عن `Id` الذي يتغيّر لكل إصدار) |
| `VersionNumber` | `int` | ✅ | يبدأ من `1` ويزيد تلقائيًا مع كل إصدار جديد لنفس `RecipeFamilyCode` |
| `PreviousVersionId` | `long?` | ❌ | مرجع ذاتي للإصدار السابق مباشرة |
| `IsCurrentVersion` | `bool` | ✅ | **إصدار واحد فقط لكل `RecipeFamilyCode` يحمل `true`** — يتحدّث تلقائيًا عند اعتماد إصدار جديد |
| `OutputItemId` | `long` | ✅ | الصنف الناتج — لازم يكون `ItemType ∈ {SemiFinished, FinishedGood}`، وثابت عبر كل إصدارات نفس الوصفة |
| `OutputQuantity` | `decimal` | ✅ | الكمية القياسية الناتجة من دورة تحضير واحدة |
| `WastePercentage` | `decimal` | ✅ (افتراضي `0`) | نسبة هالك قياسية متوقعة |
| `Status` | `enum` | ✅ | `Draft` / `PendingApproval` / `Approved` / `Rejected` |
| `ApprovalInstanceId` | `long?` | ❌ | — |
| `EffectiveFromDate` | `DateOnly` | ✅ | — |

#### `RecipeLine` (مكوّن الوصفة)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `RecipeId`, `ComponentItemId`, `Quantity` | — | ✅ | **`ComponentItemId` ممكن يكون هو نفسه `OutputItemId` لوصفة تانية** — دعم وصفات متعددة المستويات (قاعدة 13) |

#### `ProductionOrder` (أمر إنتاج)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `PublicId`, `CompanyId`, `WarehouseId` | — | ✅ | مخزن التحضير/التنفيذ |
| `RecipeId` | `long` | ✅ | لازم `Recipe.Status = Approved` — **يربط بإصدار محدد (`Recipe.Id`) وليس بالوصفة كمفهوم عام**، فيفضل تاريخيًا صحيحًا حتى بعد اعتماد إصدارات أحدث لاحقًا (قاعدة 31) |
| `PlannedQuantity` | `decimal` | ✅ | — |
| `ActualQuantity` | `decimal?` | ❌ | تتحدد عند الاكتمال — تحدد معامل القياس (قاعدة 15) |
| `Status` | `enum` | ✅ | `Pending` / `InProgress` / `Completed` / `Cancelled` |
| `StartDate` / `EndDate` | `DateOnly?` | ⚠️ | `EndDate` إلزامية عند `Completed` |
| `ExecutedByUserId` | `long` | ✅ | — |
| `StandardCost` / `ActualCost` | `decimal` / `decimal?` | ✅ / ❌ | للمقارنة في تقرير انحراف التكلفة (قاعدة 16) |

#### `WasteRecord` (سجل هالك)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId`, `WarehouseId`, `ItemId` | — | ✅ | — |
| `Quantity` / `WasteDate` / `Reason` | — | ✅ | — |
| `SourceDocumentType` / `SourceDocumentId` | `string` / `long?` | ❌ | **قيم ثابتة**: `ProductionOrder`, `POSRealtimeLoss`, `Manual` |
| `ApprovalInstanceId` | `long?` | ⚠️ | إلزامية لو القيمة المالية للهالك تتجاوز حد الإعدادات (قاعدة 19) |

### 2.7 إعدادات النماذج والحدود

#### `ProductionSalesModeSetting` (إعداد نموذج الإنتاج والبيع)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `ScopeType` | `enum` | ✅ | `Company` / `Branch` / `POS` / `Item` — ترتيب الأولوية من الأخص للأعم (قاعدة 17) |
| `ScopeId` | `long?` | ⚠️ | `null` فقط لو `ScopeType = Company` |
| `Mode` | `enum` | ✅ | `RealTime` (لحظي) / `Stocked` (مخزني) |

#### `ShortagePolicy` (سياسة تجاوز الحدود والنقص)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `AllowOverrideOnShortage` | `bool` | ✅ | — |
| `RequiresApprovalForOverride` | `bool` | ✅ | — |

#### `InventorySettings` (إعدادات عامة للمخزون على مستوى الشركة)
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Id`, `CompanyId` | — | ✅ | — |
| `SlowMovingThresholdDays` | `int` | ✅ (افتراضي `90`) | عدد الأيام بدون أي `StockTransaction` لاعتبار الصنف راكدًا (قاعدة 37) — قابل للتخصيص لكل شركة، لا يُثبَّت في الكود (`00-Project-Overview.md` قسم 23) |

---

## 3. قواعد العمل (Business Rules)

1. **لا يُقبل أي حركة صرف تخفض `StockBalance.QuantityOnHand` لأقل من صفر** — يُرفض الحفظ فورًا، فيما عدا استثناء صريح بصلاحية خاصة حسب `ShortagePolicy`.
2. **أي تعديل على `StockBalance` يتم داخل Transaction صريحة مع تحديث `RowVersion` إلزاميًا** (`00-Project-Overview.md` قسم 13) — هذا الكيان الأكثر عرضة للتزامن العالي في النظام كله (بيع POS، تحويل، جرد، إنتاج، كلهم بيلمسوه بالتوازي).
3. **`WarehouseDocument` بجميع أنواعه (فيما عدا الآلي) يمر بـ `Draft` → `Posted`**، والترحيل ينشئ `StockTransaction` + القيد المحاسبي عبر `IPostingService` **معًا داخل نفس الـ Transaction** (Atomicity).
4. **`BranchRequestLine.RequestedQuantity` لا يجوز أن تتجاوز `BranchItemLimit.MaxRequestQuantity`** للصنف/الفرع المعنيين — يُرفض الحفظ، إلا بصلاحية تجاوز خاصة حسب `ShortagePolicy.RequiresApprovalForOverride`.
5. **الفرع لا يطلب توريد من فرع آخر مباشرة** (`00-Project-Overview.md` قسم 2) — `BranchRequest` موجّه دايمًا لمخزن `WarehouseType = Main` فقط.
6. **اعتماد `BranchRequest` (كليًا أو جزئيًا) ينشئ `WarehouseDocument` من نوع `TransferOrder` تلقائيًا** — الطلب المعتمد جزئيًا (`PartiallyFulfilled`) يفضل مفتوحًا لباقي الكمية غير المُلبّاة.
7. **عند `TransferReceipt`، أي `VarianceQuantity ≠ 0` يُسجَّل صراحة ولا يُقفَل المستند بدون توثيق السبب** — يُعرض الفرق لحظيًا وقت إدخال الكمية المستلمة فعليًا.
8. **الأصناف `IsTracked = true` لا تقبل ترحيل أي حركة بدون `BatchNumber`** — إلزامية على مستوى `StockTransaction` و`WarehouseDocumentLine` معًا.
9. **`InventoryCount` من نوع `Full` يُنشئ `InventoryCountLine` تلقائيًا لكل الأصناف النشطة في المخزن المعني وقت الإنشاء** — أنواع `Partial`/`Cyclic` تسمح باختيار أصناف محددة يدويًا فقط.
10. **تسوية فروقات الجرد تتم سطر بسطر عبر `SettlementDecision`** — قبول فرق معيّن لا يعني قبول باقي الفروق تلقائيًا في نفس عملية الجرد.
11. **إقفال `InventoryCount` (`Status = Closed`) ينشئ تلقائيًا `WarehouseDocument` من نوع `InventoryAdjustment` للفروق المعتمدة (`SettlementDecision = Approved`) فقط**، مع القيد المحاسبي المرافق — الفروق المرفوضة لا تُسوَّى ماليًا وتحتاج متابعة يدوية منفصلة خارج دورة الجرد. **رفض سطر واحد أو أكتر (`SettlementDecision = Rejected`) لا يمنع إقفال باقي الجرد** — السطر المرفوض يُستبعد من التسوية المالية فقط، ومش شرط لإتمام الإقفال.
12. **لا يمكن فتح عملية جرد جديدة على مخزن عليه عملية جرد أخرى بحالة `InProgress` أو `PendingSettlement`.**
13. **الوصفات تدعم مكوّنات متعددة المستويات بطبيعتها** — `RecipeLine.ComponentItemId` ممكن يكون هو نفسه `OutputItemId` لوصفة أخرى (مثال: "بن روبوستا محمص" مكوّن في "خلطة هاوس بلند" وهو نفسه ناتج وصفة تحميص منفصلة). **النظام يمنع صراحة أي حلقة دائرية (Circular Reference)** عند الحفظ — وصفة تستخدم نفسها كمكوّن بشكل مباشر أو غير مباشر عبر أي عدد من المستويات تُرفض.
14. **أي وصفة جديدة أو تعديل على وصفة موجودة يمر إلزاميًا بحالة `PendingApproval`** قبل استخدامها فعليًا في أي أمر إنتاج أو بيع لحظي — لا استثناءات، حتى لو كان المعدِّل هو نفسه من أنشأ الوصفة الأصلية.
15. **`ProductionOrder.ActualQuantity` عند الاكتمال يحدد معامل القياس (Scaling Factor = ActualQuantity ÷ Recipe.OutputQuantity)** المُطبَّق على كل `RecipeLine.Quantity` وقت إنشاء `WarehouseDocument` من نوع `ProductionIssue` — مش بالضرورة نفس `PlannedQuantity` الأصلي.
16. **أي فرق بين `StandardCost` و`ActualCost` على `ProductionOrder` المكتمل يُسجَّل كقيد انحراف تكلفة منفصل** عبر `IPostingService` (يغذّي `costVarianceReport`، `01-Module-Accounting.md` قسم 8) — لا يُدمَج صامتًا في تكلفة المخزون الناتج.
17. **نموذج البيع/الإنتاج (`ProductionSalesModeSetting.Mode`) يُحل بترتيب أولوية من الأخص للأعم**: `Item` > `POS` > `Branch` > `Company` — أول إعداد موجود فعليًا بيُطبَّق، وإلا يُستخدم إعداد الشركة الافتراضي.
18. **البيع اللحظي (`Mode = RealTime`) يمنع البيع فورًا لو أي مكوّن من مكوّنات الوصفة أقل من الكمية المطلوبة** (تكامل مباشر مع `05-Module-POS-Shifts.md`) — البيع المخزني (`Stocked`) يتحقق من رصيد الصنف النهائي نفسه فقط، مش مكوّناته.
19. **`WasteRecord` اللي قيمته المالية (`Quantity × Item.StandardCost`) تتجاوز حدًا معينًا في الإعدادات يتطلب المرور بسلسلة موافقات** (`00-Project-Overview.md` قسم 12) قبل ترحيله محاسبيًا.
20. **رفض سلسلة الموافقات لأي `WarehouseDocument`, `BranchRequest`, `Recipe`, أو `InventoryCount` ينعكس فورًا في `Status` الكيان نفسه لقيمة `Rejected`** — نفس النمط القياسي المُلزَم في `00-Project-Overview.md` (قسم 12.3) والمُطبَّق فعليًا في `01-Module-Accounting.md` (قاعدة 26). **`Rejected` حالة نهائية بلا استثناء لأي من الأربعة كيانات**: لا يُعدَّل الكيان المرفوض ويُعاد إرساله لسلسلة الموافقات مرة تانية — أي محاولة جديدة تتطلب إنشاء كيان جديد بالكامل من الصفر (مثال: `BranchRequest` مرفوض يحتاج طلب توريد جديد، مش تعديل المرفوض نفسه).
21. **تقرير "الأصناف تحت الحد الأدنى" يقارن `StockBalance.QuantityOnHand` بـ `ItemWarehouseSettings.MinStockLevel` لكل مخزن على حدة** — لا يوجد حد أدنى عام واحد للصنف عبر كل المخازن.
22. **زر "طلب شراء تلقائي" من تقرير الأصناف تحت الحد الأدنى** (تكامل مع `03-Module-Purchasing.md`) **ينشئ طلب شراء بحالة `Draft` ويحمل `IdempotencyKey` خاص بيه** (`00-Project-Overview.md` قسم 14) — لمنع إنشاء طلبات شراء مكررة لو تم الضغط على الزر أكثر من مرة بالخطأ.
23. **حركات المخزون الناتجة عن عمليات POS اللحظية (`SourceDocumentType = POSSale`) لا تمر بحالة `Draft`** — تُسجَّل مباشرة كـ `Posted` لحظة إتمام البيع، نفس مبدأ القيود الآلية (`01-Module-Accounting.md` قاعدة 4).
24. **`Warehouse.BranchId` عمود مباشر Nullable** — المخازن المركزية (`WarehouseType = Main`) تبقى `null`، ومخازن الفروع (`BranchMaterials`, `Production`) ترتبط بفرع محدد دايمًا — لا يُستخدم أي بُعد تحليلي كبديل عن العمود المباشر (نفس مبدأ `01-Module-Accounting.md` قاعدة 25).
25. **أي `WarehouseDocument` بيولّد قيده المحاسبي يحمل `CostCenterId` مأخوذ من الفرع/المخزن المرتبط** (تكامل مباشر مع نظام الأبعاد في `01-Module-Accounting.md` قسم 2.1) — لا يوجد قيد ناتج عن حركة مخزنية بدون تصنيف تحليلي.
26. **الصنف `IsActive = false` لا يظهر في أي قائمة اختيار جديدة** (شاشة بيع، إنشاء وصفة، إنشاء مستند مخزني) **لكنه يفضل ظاهرًا في التقارير التاريخية والمستندات القديمة** — نفس فلسفة Soft Delete بدون حذف فعلي (`00-Project-Overview.md` قسم 16).
27. **لا يجوز أن يكون `Item.ItemType = RawMaterial` هو نفسه `Recipe.OutputItemId`** — الخامة بحكم تعريفها لا تُنتَج بوصفة داخلية، هي مُشتراة فقط (تكامل مع `03-Module-Purchasing.md`).
28. **`StockBalance` و`StockTransaction` يُخزَّنان دايمًا بوحدة `Item.BaseUnitOfMeasureId` فقط** — أي كمية تُدخَل بوحدة بديلة (عبر `ItemUnitConversion`) تُحوَّل للوحدة الأساسية **وقت التسجيل** قبل أي تأثير على الرصيد؛ لا يوجد رصيد مخزون مُخزَّن بأكتر من وحدة قياس لنفس الصنف.
29. **"الحدود الخمسة" لكل صنف/فرع موزَّعة على كيانين بمعزل تام عن بعض**: 3 حدود مخزون فعلي (`ItemWarehouseSettings.MinStockLevel`/`MaxStockLevel`/`ReorderPoint`) + حدّان لحجم طلب التوريد الواحد (`BranchItemLimit.MinRequestQuantity`/`MaxRequestQuantity`) — تحديث أحدهما لا يغيّر الآخر تلقائيًا، وهما يُفحصان في سياقين مختلفين تمامًا (تقارير المخزون مقابل التحقق اللحظي وقت إنشاء الطلب).
30. **أي `WarehouseDocument` من نوع `TransferOrder` لا يُقبل ترحيله بدون `CustodyOfficerId` محدد** — المسؤولية المادية عن البضاعة أثناء النقل بين مخزنين إلزامية، وليست اختيارية. باقي أنواع المستندات لا تتطلب هذا الحقل.
31. **تعديل وصفة بحالة `Approved` لا يُعدِّل نفس السجل أبدًا** — أي تعديل ينشئ سجل `Recipe` جديد بنفس `RecipeFamilyCode` و`VersionNumber` أعلى بواحد، بحالة `Draft`، ومرجعه `PreviousVersionId` للإصدار السابق. اعتماد الإصدار الجديد **يحدّث `IsCurrentVersion` تلقائيًا**: `true` للجديد، `false` للقديم — لكن **السجل القديم يفضل موجودًا كما هو بدون حذف أو تعديل**، وتفضل كل أوامر الإنتاج التاريخية المرتبطة بيه (`ProductionOrder.RecipeId`) تشاور على الإصدار الصحيح اللي كان ساري وقتها بالظبط.
32. **`ItemUnitConversion.ConversionFactor` يعبّر دايمًا عن عدد الوحدات الأساسية في الوحدة البديلة الواحدة** (مثال: `50` يعني شكارة واحدة = 50 كجم) — أي حساب تحويل في النظام لازم يتبع نفس الاتجاه: `الكمية بالوحدة الأساسية = الكمية المُدخَلة × ConversionFactor`، بدون استثناء أو عكس في أي شاشة.
33. **صلاحية `OverrideShortage` اسم موحّد لتجاوز أي حد نقص أو حجم طلب في الموديول** — تغطي سيناريوهين معًا: تجاوز رصيد المخزون السالب (قاعدة 1) وتجاوز حدود حجم الطلب (`BranchItemLimit`، قاعدة 4) — صلاحية واحدة، مش صلاحيتين منفصلتين، لتبسيط إدارة الأدوار.
34. **`WarehouseDocument.CustodyOfficerId` يُنسَخ تلقائيًا من `TransferOrder` إلى `TransferReceipt` المرتبط عبر `RelatedWarehouseDocumentId`** وقت إنشاء الاستلام — نفس المسؤول يفضل مسؤولًا عن البضاعة حتى لحظة تأكيد الاستلام الفعلي، ولا يُطلب اختياره من جديد.
35. **`ProductionOrder.ActualCost` يُحسَب حصريًا عند اكتمال الأمر (`Status = Completed`)**، بناءً على تكلفة المكوّنات المصروفة فعليًا (`WarehouseDocument` من نوع `ProductionIssue`) + قيمة الهالك الفعلي المسجَّل — **وليس أثناء التنفيذ (`InProgress`)**؛ أي عرض للتكلفة قبل الاكتمال يكون تقديريًا (`StandardCost` مُقاسًا بنسبة الإنجاز) ولا يُعتَد به كقيمة نهائية.
36. **تقرير "الأصناف الراكدة" يعتمد حصريًا على `InventorySettings.SlowMovingThresholdDays`** (افتراضي 90 يوم) — الصنف يُعتبر راكدًا لو مفيش عليه أي `StockTransaction` (داخل أو خارج) خلال المدة دي، بغض النظر عن رصيده الحالي.
37. **أي `WarehouseDocument` من نوع `TransferReceipt` لا يُقبل ترحيله بدون `RelatedWarehouseDocumentId` صحيح يشاور على `TransferOrder` موجود فعليًا وبحالة `Posted`** — استلام تحويل بدون ربطه بأمر تحويل صادر لا معنى له ويُرفض.
38. **`Item.POSCategoryId = null` يعني الصنف لا يظهر أبدًا في شاشة البيع بنقاط البيع** (`05-Module-POS-Shifts.md`) — يشمل ده الخامات والمستهلكات ونصف المصنَّع اللي مالهاش تصنيف بيع مباشر؛ الصنف يفضل مستخدَم بالكامل في المخزون والوصفات، بس مش قابل للبيع المباشر من الكاشير.
39. **`Item.AverageCost` يُحدَّث تلقائيًا بمعادلة المتوسط المرجّح القياسية عند كل حركة استلام** (`StockIn`, `TransferReceipt`, `ProductionReceipt`, `OpeningBalance`):
    ```
    NewAverageCost = (OldQuantity × OldAverageCost + ReceivedQuantity × ReceivedUnitCost) ÷ (OldQuantity + ReceivedQuantity)
    ```
    التحديث ده بيحصل داخل نفس الـ Transaction الخاصة بترحيل `WarehouseDocument`/`StockTransaction` نفسها (قاعدة 3) — مش خطوة منفصلة لاحقة.
40. **نقل التكلفة بين المخازن (Transfer) يحافظ على التكلفة الأصلية، ولا يُعاد حسابها بشكل مستقل في المخزن الوجهة**:
    - عند `TransferOut`: `StockTransaction.UnitCost = Item.AverageCost` **في المخزن المصدر لحظة الصرف بالظبط** (Snapshot).
    - عند `TransferReceipt` المقابل: `StockTransaction.UnitCost` **نفس القيمة المصروفة بالظبط من `TransferOut`** — لا تُقرَأ `Item.AverageCost` من جديد ولا تُحسَب بشكل مستقل وقت الاستلام.
    - `Item.AverageCost` في المخزن الوجهة **بعد الاستلام** يُحدَّث بنفس معادلة قاعدة 39 (باستخدام التكلفة المنقولة كـ `ReceivedUnitCost`) — لأن `AverageCost` قيمة **لكل مخزن**، مش قيمة عامة واحدة للصنف عبر كل المخازن (`ItemWarehouseSettings` نفس المبدأ، قاعدة 21).
41. **`StockTransaction.UnitCost` إلزامي وغير قابل لأن يكون صفرًا لأي حركة داخلة أو خارجة** — أي محاولة ترحيل حركة مخزنية بدون `UnitCost` صريح (أو بقيمة صفر لصنف قيمته الفعلية أكبر من صفر) تُرفض عند الحفظ. هذه القاعدة تمنع فقدان التكلفة الصامت بين المخازن أو عند التصنيع (كان هذا سبب فجوة حقيقية اكتُشفت أثناء مراجعة `00-Posting-Engine-Architecture.md`).
42. **`SalesInvoiceLine.UnitCost` و`POSInvoiceLine.UnitCost`** (في `04-Module-Sales.md` و`05-Module-POS-Shifts.md`) **تُحفَظان كـ Snapshot من `Item.AverageCost` في المخزن المصدر لحظة إنشاء السطر بالضبط** — قيمة تاريخية ثابتة لا تتغيّر لو `AverageCost` اتغيّر لاحقًا، وهي المصدر الوحيد اللي محرك الترحيل (`00-Posting-Engine-Architecture.md` قسم 4.1، `SumLineQuantityTimesUnitCost`) بيعتمد عليه لحساب تكلفة البضاعة المباعة — المحرك نفسه لا يعيد حساب أي تكلفة.

---

## 4. دورة حياة المستند / الحالات (Workflow / State Machine)

### 4.1 المستند المخزني (`WarehouseDocument`)
```
مسودة (Draft) → [سلسلة موافقات إن وُجدت] → مرحّل (Posted، ينشئ StockTransaction + القيد معًا)
              ↘ مرفوض (Rejected — نهائي، قاعدة 20)
```
المستندات الآلية (`InventoryAdjustment` الناتجة عن إقفال جرد، `ProductionIssue`/`ProductionReceipt` الناتجة عن اكتمال أمر إنتاج) **تُنشأ مباشرة في حالة `Posted`** — لا تمر بـ `Draft`.

### 4.2 طلب توريد الفرع (`BranchRequest`)
```
مسودة → بانتظار الاعتماد (PendingApproval) → معتمد (Approved) أو معتمد جزئيًا (PartiallyFulfilled) → منفَّذ بالكامل (Fulfilled)
                                            ↘ مرفوض (Rejected — نهائي)
```

### 4.3 عملية الجرد (`InventoryCount`)
```
مسودة → قيد التنفيذ (InProgress، إدخال الكميات المعدودة) → بانتظار التسوية (PendingSettlement)
       → تمت التسوية (Settled، كل السطور اتحسم قبولًا أو رفضًا) → مُقفلة (Closed، القيود الآلية اتولّدت)
                                                                 ↘ مرفوضة (Rejected)
```

### 4.4 الوصفة (`Recipe`)
```
مسودة → بانتظار الاعتماد → معتمدة (Approved، قابلة للاستخدام في أوامر إنتاج/بيع لحظي)
                          ↘ مرفوضة (Rejected — نهائي، تعديل جديد = وصفة جديدة تبدأ من مسودة)
```

### 4.5 أمر الإنتاج (`ProductionOrder`)
```
معلّق (Pending) → قيد التنفيذ (InProgress) → مكتمل (Completed، يولّد ProductionIssue + ProductionReceipt + قيد انحراف التكلفة)
                                          ↘ ملغى (Cancelled)
```

---

## 5. الشاشات المطلوبة

كل الشاشات تتبع نمط List/Edit القياسي (`00-Frontend-Specs.md` قسم 4-7) وتستخدم `ActionBar` الموحّد (`00-System-Wide-Corrections-01.md` قسم 1) ما لم يُذكر خلاف ذلك، وتسمياتها وأعمدتها مصدرها `MenuItem`/`FieldLabel` الديناميكيان (نفس الملف، قسم 3-4).

| # | الشاشة | النوع | ملاحظات خاصة |
|---|---|---|---|
| 1 | الأصناف | List/Edit — **6 أقسام في شاشة التعديل** | بيانات أساسية (تشمل المجموعة وتصنيف POS ووحدات التحويل)، التسعير والتكلفة، إعدادات المخزون لكل فرع (`ItemWarehouseSettings`)، الوصفة/التصنيع، الربط الضريبي ETA، الحالة |
| 2 | مجموعات الأصناف | List/Edit قياسي | هرمية |
| 3 | تصنيفات نقطة البيع | List/Edit قياسي | — |
| 4 | وحدات القياس | List/Edit قياسي | — |
| 5 | المخازن | List/Edit قياسي | — |
| 6 | مسؤولو العهد | List/Edit قياسي | ربط موظف بصلاحية مسؤولية نقل مخزني (تكامل `10-Module-HR-Payroll.md`) |
| 7 | أرصدة افتتاحية | List/Edit قياسي | ينشئ `StockTransaction` نوع `OpeningBalance` |
| 8 | إذن إضافة | List/Edit قياسي (`WarehouseDocument`) | — |
| 9 | إذن صرف | List/Edit قياسي (`WarehouseDocument`) | — |
| 10 | أمر تحويل | List/Edit قياسي (`WarehouseDocument`) | يتطلب اختيار `CustodyOfficerId` (قاعدة 30) |
| 11 | استلام تحويل | **شاشة تفاعلية** (غير قياسي) | حساب `VarianceQuantity` لحظيًا أثناء إدخال الكمية المستلمة |
| 12 | طلب توريد فرع | **شاشة تفاعلية** (غير قياسي) | تحقق لحظي من `BranchItemLimit` (الحد الأدنى والأقصى معًا) أثناء الإدخال |
| 13 | اعتماد طلبات التوريد | **شاشة تفاعلية** (غير قياسي) | نفس بيانات `BranchRequest`، بزاوية اعتماد/رفض لكل سطر |
| 14 | دورة الجرد | **شاشة تفاعلية متعددة المراحل** (غير قياسي) | إنشاء → عدادات (تكامل قارئ باركود) → تسوية تفاعلية (قبول/رفض لكل فرق) → إقفال |
| 15 | تسوية جرد مستقلة | List/Edit قياسي (`WarehouseDocument` نوع `InventoryAdjustment` يدوي، خارج دورة جرد كاملة) | — |
| 16 | تعريف وصفة | List/Edit مع **جدول مكوّنات** | كل مكوّن ممكن يكون ناتج وصفة تانية (قاعدة 13)؛ التعديل بعد الاعتماد ينشئ إصدارًا جديدًا (قاعدة 31) |
| 17 | اعتماد وصفة | **شاشة تفاعلية** (غير قياسي) | عرض المكوّنات + التكلفة المحسوبة + اعتماد/رفض |
| 18 | متابعة الهالك | List/Edit قياسي | — |
| 19 | أمر إنتاج | List/Edit قياسي | زر "إكمال الأمر" يفتح إدخال `ActualQuantity` |
| 20 | صرف واستلام الإنتاج | List قياسي (عرض فقط — تُنشأ آليًا من إكمال أمر الإنتاج، قاعدة الآلية في 4.1) | — |
| 21 | إعدادات نماذج الإنتاج والبيع | **شاشة إعدادات تفاعلية** (غير قياسي) | مصفوفة الأولوية (شركة/فرع/نقطة بيع/صنف) |
| 22 | سياسة تجاوز الحدود والنقص | **شاشة إعدادات** (غير قياسي) | — |
| 23 | إعدادات المخزون العامة | **شاشة إعدادات** (غير قياسي) | حد أيام الركود (`InventorySettings.SlowMovingThresholdDays`) |

---

## 6. نقاط التكامل مع موديولات أخرى

| الموديول | الاتجاه | التفاصيل |
|---|---|---|
| **الحسابات العامة** (`01-Module-Accounting.md`) | ➡️ خارج | كل حركة مخزون ذات أثر مالي (استلام، صرف، تسوية جرد، إنتاج، هالك) تنشئ قيدها عبر `IPostingService`، حاملة `CostCenterId` (قاعدة 25) |
| **المشتريات** (`03-Module-Purchasing.md`) | ⬅️➡️ ثنائي | استلام المشتريات ينشئ `WarehouseDocument` نوع `StockIn`؛ نقص المخزون ينشئ طلب شراء تلقائي (قاعدة 22) |
| **المبيعات** (`04-Module-Sales.md`) | ⬅️ داخل | فاتورة البيع تخصم `StockBalance` مباشرة (منتج جاهز) أو عبر مكوّنات الوصفة (منتج مُصنَّع) |
| **نقاط البيع والورديات** (`05-Module-POS-Shifts.md`) | ⬅️➡️ ثنائي | البيع اللحظي يستهلك `RecipeLine` مباشرة (قاعدة 18)؛ نموذج البيع (`ProductionSalesModeSetting`) يحدد سلوك نقطة البيع |
| **الامتثال الضريبي** (`06-Module-ETA-Compliance.md`) | ➡️ خارج | ربط `Item` بكود GS1/EGS |
| **الصيانة والأصول الثابتة** (`08-Module-Maintenance-FixedAssets.md`) | ⬅️➡️ ثنائي | قطع غيار الصيانة ممكن تكون أصناف مخزون (`ItemType = Consumable`) |
| **شئون العاملين والمرتبات** (`10-Module-HR-Payroll.md`) | ⬅️ داخل | `CustodyOfficer.EmployeeId` مرجع مباشر لموظف مُسجَّل (قاعدة 30) |
| **التقارير ولوحة التحكم** (`09-Module-Reports-Dashboard.md`) | ➡️ خارج | يوفر كل بيانات تقارير المخزون والتصنيع (قسم 8) |

---

## 7. الصلاحيات الخاصة بالموديول

بالإضافة للصلاحيات القياسية (قسم 6.1 من Overview):

| الصلاحية | ملاحظة |
|---|---|
| إنشاء/تعديل صنف | يشمل تعديل `StandardCost` — قد تُقيَّد لدور مالي/محاسبي |
| **ترحيل مستند مخزني** | منفصلة عن "تعديل" |
| **اعتماد طلب توريد فرع** | مستوى إداري أعلى من الإنشاء |
| **`OverrideShortage`** — تجاوز نقص المخزون أو حدود حجم طلب التوريد | صلاحية استثنائية موحّدة (قاعدة 33) |
| **إنشاء/فتح عملية جرد** | — |
| **اعتماد تسوية فروقات الجرد** (سطر بسطر) | — |
| **إقفال عملية جرد** | صلاحية عالية — تولّد قيودًا محاسبية آلية |
| **إنشاء/تعديل وصفة** | — |
| **اعتماد وصفة** | منفصلة تمامًا عن الإنشاء (قاعدة 14) |
| **إكمال أمر إنتاج** (إدخال `ActualQuantity`) | — |
| **اعتماد هالك يتجاوز الحد المالي** | — |
| **إدارة إعدادات نماذج الإنتاج والبيع وسياسة التجاوز** | صلاحية إعدادات عامة، مستوى إداري |
| **إدارة مسؤولي العهد** (تعيين موظف كمسؤول نقل مخزني) | صلاحية إدارية — منفصلة عن إنشاء أمر التحويل نفسه |

---

## 8. التقارير

| # | التقرير | نوعه |
|---|---|---|
| 1 | رصيد الأصناف بالمخازن | ثابت |
| 2 | كارت الصنف | **تفاعلي** — Dropdown لاختيار الصنف، رصيد متحرك (وارد/منصرف) |
| 3 | حركة صنف | ثابت |
| 4 | أرصدة المخازن الإجمالية | ثابت |
| 5 | تقييم المخزون | ثابت |
| 6 | الأصناف تحت الحد الأدنى | ثابت — بزر "طلب توريد فرع" و"طلب شراء تلقائي" لكل صف (قاعدة 22) |
| 7 | الأصناف الراكدة | ثابت — معيار الركود من `InventorySettings.SlowMovingThresholdDays` (قاعدة 36) |
| 8 | الأصناف قريبة الانتهاء | ثابت — يعتمد على `Item.ShelfLifeDays` + `BatchNumber` |
| 9 | تقرير الباتشات | ثابت |
| 10 | تقرير فروقات الجرد | ثابت |
| 11 | طلبات التوريد المعلقة | ثابت |
| 12 | تحليل ABC للمخزون | ثابت |
| 13 | تكلفة الوصفة | ثابت |
| 14 | مقارنة التكلفة المعيارية بالفعلية | ثابت |
| 15 | ملخص أوامر الإنتاج | ثابت |
| 16 | الهالك اليومي | ثابت |
| 17 | استهلاك الخامات | ثابت — إجمالي كمية كل خامة اتصرفت (إنتاج + بيع لحظي) خلال فترة، بمعزل عن ربطها بأمر إنتاج محدد |
