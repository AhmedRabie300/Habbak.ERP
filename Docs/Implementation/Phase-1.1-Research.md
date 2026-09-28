# Research Pass — Phase 1.1 (Core HR Entities)

> مرجعي: `Docs/Implementation/HR-Core-Plan.md §1.1` (المصدر المعتمد لنطاق الـ 9 كيانات)، `Docs/Modules/10-Module-HR-Payroll.md §2.1`، `Docs/Modules/00-Project-Overview.md` (6.2، 8.5، 11، 12، 14، 18، 26، 27). **كل الأنماط تحت اتفحصت في الكود الفعلي، مش افتراض من المواصفة.**

---

## 1. ملخص تنفيذي

الكود بيدعم كل الأدوات اللي محتاجينها (Phase 0 كامل: `EncryptedStringConverter`، `PiiSecretProtector` Keyed "HR.PII"، `IPiiHasher`، `PiiFieldAttribute`، `IEmployeeScopedEntity`، `DataScope`، `ICurrentCompanyContext.EmployeeId`) — كلهم موجودين ومُختبَرين فعليًا، مش خطة نظرية. **أهم اكتشاف**: خطة `HR-Core-Plan.md §1.1` بتحدد نمط "ملف واحد لكل مجموعة كيان" (`UserCommands.cs`) للـ Commands، لكن **كل كود اتكتب في آخر يومين (2026-09-25/26) بيستخدم نمط مختلف تمامًا**: فولدر منفصل لكل Command (`Commands/{CreateX}/CreateXCommand.cs`). ده تعارض مباشر محتاج قرار قبل 1.1.3. اكتشاف تاني مهم: **مفيش فحص حلقات (Cycle Detection) حقيقي في الكود خالص** — بس فحص سطحي "الأب = نفسي" — فاختبار "OrgUnit.ParentId ما بيعملش حلقة" اللي الخطة طالباه هيتبني من الصفر. وأخيرًا: آلية تسجيل `ScreenCode` الجديدة اتأكدت — ملف Seed يدوي ثابت (`MenuItemSeedData.cs`)، مش Reflection.

---

## 2. الأنماط المكتشفة

### 2.1 كيانات Lookup بسيطة
**المسار**: `src/Habbak.ERP.Domain/Inventory/POSCategory.cs` + `Configurations/Inventory/POSCategoryConfiguration.cs`.
```csharp
// الحقول: CompanyId, Code, NameAr, NameEn, DisplayOrder(int), IsActive
builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
```
**التوصية لـ 1.1**: `JobPosition`، و`JobGrade`، و`EmployeeDocumentType` تتبع نفس الشكل بالظبط (فهرس فريد مفلتر على `CompanyId + Code`).

### 2.2 كيانات هرمية (ParentId)
**المسار**: `Domain/Accounting/Account.cs` (`ParentId (long?)` + `Level (int)`)، و`ItemGroupConfiguration.cs:17-20`:
```csharp
builder.HasOne(g => g.Parent).WithMany().HasForeignKey(g => g.ParentId).OnDelete(DeleteBehavior.Restrict);
```
**اكتشاف مهم**: `Account` نفسه **مايسمحش بتغيير `ParentId` بعد الإنشاء خالص** (`UpdateAccountCommand` ما بياخدش `ParentId`)، فمفيش فحص حلقة فيه أصلًا. `ItemGroup` هو الوحيد اللي فيه أي فحص، وهو سطحي: `if (request.ParentId == request.Id) throw ...`. **مفيش أي فحص عميق (Walk الأسلاف) في المشروع كله.**
**التوصية لـ 1.1**: `OrgUnit.ParentId` يقبل تعديل (عكس `Account`)، فمحتاج فحص حلقة **حقيقي** (Walk للأعلى لحد الجذر أو `null`) — كود جديد بالكامل، مش نسخ من مكان موجود.

### 2.3 كيان رئيسي + Sub-entities
**اكتشاف مهم**: **مفيش نمط "1:1 بمفتاح أساسي مشترك" في المشروع خالص** (اتفحص كل `WithOne` في الكود، صفر نتائج من النوع ده). أقرب نمط فعلي هو `BranchPOSSettings` (صف مستقل + فهرس فريد مفلتر على FK):
```csharp
builder.HasIndex(s => s.BranchId).IsUnique().HasFilter("[IsDeleted] = 0");
```
**التوصية لـ 1.1**: `EmployeePersonalData` (1:1 مع `Employee`) تتبع نفس نمط `BranchPOSSettings`: عمود `EmployeeId (long)` + فهرس فريد مفلتر، مش Shared Primary Key.
باقي الكيانات (`EmploymentContract`، `EmployeeDocument`، `EmployeeCertification`) هي 1:N عادية — نفس نمط `SupplierContract`/`SupplierPriceHistory` (جداول مستقلة بـ FK عادي، مش Collection مُضمّنة).

### 2.4 كيان بيمر بسلسلة اعتماد
**المسار**: `PurchaseOrder` (`PurchaseOrderStatus` فيه `Rejected = 9`)، و`Shift` (`ShiftStatus` فيه `Rejected = 4`، معلّق بتعليق صريح "unreachable in this build; no real approval engine attached yet"). `ApprovalInstanceId` في الاتنين `long?`.
**اكتشاف**: `NullApprovalWorkflowService` بيتنادى من **مكان واحد بس في المشروع كله**: `PostingService.cs:228` (محرك الترحيل) — **مش** من الـ Command Handlers بتاعة `PurchaseOrder` مباشرة (صفر نتائج بحث). يعني الحقل موجود على الكيان بس مالوش أي استهلاك فعلي دلوقتي.
**التوصية لـ 1.1**: نفس النمط بالظبط — `Employee`/`EmploymentContract` تحمل `ApprovalInstanceId (long?)` و`Rejected` في الـ Enum، بس **من غير أي استدعاء فعلي لخدمة الموافقات** (زي `PurchaseOrder` بالظبط). هذا متسق مع قرار الخطة الأصلي (خطر 6: التعيين بدون سلسلة اعتماد حقيقية دلوقتي).

### 2.5 Commands/Queries/CodeGenerator
**⚠️ التعارض الأهم**: `UserCommands.cs` (ملف مجمّع) هو النمط **القديم**. كل حاجة اتلمست فعليًا في آخر يومين (`Purchasing/PurchaseOrders/Commands/{Create,Update,Cancel,Reject}PurchaseOrder/`، و`Accounting/Vouchers/Commands/CreateVoucher/`، و`Accounting/TreasuryTransfers/`) بتستخدم **فولدر منفصل لكل Command**. خطة `HR-Core-Plan.md §1.1` بتحدد صراحة "نفس نمط `UserCommands.cs`" — وده **يتعارض مع الاتجاه الفعلي للكود الحالي**.
**`ICodeGenerator`** (`Application/Common/Interfaces/ICodeGenerator.cs:9-22`):
```csharp
Task<string> ResolveCodeAsync(string screenCode, string? manualCode, CancellationToken cancellationToken = default);
```
استدعاء حقيقي: `codeGenerator.ResolveCodeAsync("INVENTORY_ITEM_GROUPS", request.Code, cancellationToken)`.
**MediatR + FluentValidation مؤكدين 100%** في كل ملف اتفحص — مفيش استثناء.

### 2.6 Controllers + تسجيل ScreenCode
**المسار**: `AccountsController.cs:19-23`:
```csharp
[ApiController][Authorize][Screen("ACCOUNTING_CHART_OF_ACCOUNTS", LookupReads = true)]
[Route("api/v1/accounting/accounts")]
```
**اكتشاف حاسم (كان معلّق كخطر 8 في الخطة)**: آلية تسجيل `ScreenCode` الجديدة اتأكدت — `Infrastructure/Persistence/Seeding/MenuItemSeedData.cs`، **ملف Seed يدوي ثابت** (`Leaf(group, "CODE", nameAr, nameEn, order, route)`)، **مش Reflection ولا DB Table**. أي شاشة HR جديدة (`HR_EMPLOYEES`... إلخ) لازم تتضاف يدويًا هنا كمجموعة `HR` جديدة.

### 2.7 Migration
أحدث Migration بتنشئ جدول (`PurchasingQaBatch20260921.cs`) بتأكد الشكل القياسي: `Id` Identity، `CompanyId (long?)`، 8 أعمدة تدقيق `AuditableEntity`، `RowVersion` كـ `rowversion`، FKs لـ `Users` بـ `ReferentialAction.Restrict`. **نفس الشكل** المتوقع لجداول HR التسعة.

### 2.8 Tests
أحدث IntegrationTests (`Vault/*`، `Persistence/EncryptedStringConverterTests.cs`) بتستخدم نمط `IAsyncLifetime` + LocalDB حقيقية باسم فريد + `[Fact]` بأسماء جُمَل كاملة. أحدث ApiTests (`PurchasingQaBatch20260921Tests.cs`) بتستخدم `IClassFixture<AccountingApiFactory>` + `CycleSeed.SeedAsync` للتحضير.

### 2.9 Frontend
`purchasing/suppliers/` فيها 4 ملفات بس (`api.ts`، `types.ts`، `SupplierEditPage.tsx`، `SuppliersListPage.tsx`) — **مفيش ملفات Tabs منفصلة**، التبويبات على الأرجح جوه `SupplierEditPage.tsx` نفسه (يتفتح ويتأكد وقت 1.5). **تأكيد نضارة**: بحث `wizard|Stepper|activeStep|currentStep` في الفرونت كله رجّع نتيجة واحدة زائفة بس (تعليق عن زرار +/- في POS) — **صفر Wizard حقيقي**، زي ما كانت الخطة قايلة بالظبط.

---

## 3. الأدوات القابلة لإعادة الاستخدام

| الأداة | المسار | الاستخدام في 1.1 |
|---|---|---|
| `ICodeGenerator` | `Application/Common/Interfaces/ICodeGenerator.cs` | كود كل الكيانات التسعة |
| `ISecretProtector` (Keyed `"HR.PII"`) | `Infrastructure/Security/PiiSecretProtector.cs` | حقن في `EncryptedStringConverter` لـ `EmployeePersonalData` |
| `IPiiHasher`/`HmacPiiHasher` | `Infrastructure/Services/HmacPiiHasher.cs` | `NationalIdHash` عند الحفظ |
| `EncryptedStringConverter` | `Infrastructure/Persistence/Converters/EncryptedStringConverter.cs` | `NationalIdEncrypted`/`BankIbanEncrypted` — **أول استخدام فعلي ليه** |
| `PiiFieldAttribute` | `Domain/Common/PiiFieldAttribute.cs` | كل حقول `EmployeePersonalData` الحساسة |
| `IEmployeeScopedEntity` | `Domain/Common/ICompanyScopedEntity.cs:31-34` | **موجود بالفعل من Phase 0** — يتفعّل على `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` بس |
| `DataScope` enum | `Domain/Common/DataScope.cs` | **موجود بالفعل** — مفيش بناء إضافي |
| `Attachment` (عام) | `Domain/Common/Attachment.cs` | `EntityType="EmployeeDocument"` + `EntityId` — **مفيش جدول مرفقات خاص بـ HR** |
| `ICurrentCompanyContext.EmployeeId` | `Application/Common/Interfaces/ICurrentCompanyContext.cs:19` | **موجود، لسه `null` دايمًا** — 1.1 أول حاجة تملّيه فعليًا (`SessionIssuer.cs` محتاج تعديل صغير يربطه بـ`Employee.UserId`) |
| `CompanyAccountRole.EmployeeReceivable`/`TipsPayable` | `Domain/Accounting/CompanyAccountMapping.cs:33-53` | مش مستخدم في 1.1 (ده لـ 1.3 لاحقًا) |
| `MenuItemSeedData.Build()` | `Infrastructure/Persistence/Seeding/MenuItemSeedData.cs` | تسجيل الـ 7 شاشات HR يدويًا |

---

## 4. الفروق بين الـ Plan والواقع

| البند | الخطة قالت إيه | الواقع الفعلي |
|---|---|---|
| نمط الـ Commands | "نفس نمط `UserCommands.cs`/`DepreciationCommands.cs` — ملف واحد" | **الكود المكتوب فعليًا في آخر 48 ساعة كله فولدر منفصل لكل Command** — تعارض مباشر (قرار مطلوب، قسم 5) |
| فحص حلقة `OrgUnit.ParentId` | مطلوب كاختبار (`§1.1` الاختبارات) | **مفيش أي كود مطابق موجود للنسخ** — `Account` مايتغيّرش أصلًا، و`ItemGroup` سطحي بس. هيتبني من الصفر |
| تسجيل `ScreenCode` (خطر 8 في الخطة، "يتأكد وقت التنفيذ") | معلّق/غير مؤكد | **اتأكد**: `MenuItemSeedData.cs`، إضافة يدوية |
| نمط 1:1 لـ `EmployeePersonalData` | "جدول منفصل" (مجرد) | مفيش سابقة Shared-PK؛ النمط الفعلي القريب هو FK + Unique Index (`BranchPOSSettings`) |
| `IApprovalWorkflowService` كخطوة استهلاك | ضمنية في وصف "سلسلة اعتماد" | بيتنادى من **مكان واحد بس** (`PostingService.cs`)، مش من أي Command Handler — الحقل بيتحط على الكيان بس مايتستخدمش، زي `PurchaseOrder` بالظبط |

---

## 5. القرارات الفرعية المقترحة

| # | القرار | الاقتراح |
|---|---|---|
| 1 | **نمط ملفات الـ Commands**: نتبع الخطة الأصلية (ملف مجمّع) ولا الاتجاه الفعلي الحالي (فولدر لكل Command)؟ | **التوصية: فولدر منفصل لكل Command** — ده اللي كل كود جديد فعليًا بيتبنى بيه (Purchasing، وVouchers، وTreasuryTransfers)، ومتسق مع اتجاه المشروع، مش الأسلوب القديم |
| 2 | **فحص حلقة `OrgUnit.ParentId`**: مستوى العمق؟ | Walk كامل للأعلى لحد الجذر (`while (parent != null)`)، مش فحص سطحي زي `ItemGroup` — لأن الخطة نفسها بتطلب اختبار حلقة حقيقي |
| 3 | **ترتيب بناء الكيانات التسعة**: كل كيان لوحده ولا Batch واحد؟ | Batch واحد للـ Lookups التلاتة (`JobGrade`→`JobPosition`→`OrgUnit`) + `EmployeeDocumentType`، بعدين `Employee` (بيعتمد عليهم)، بعدين الـ 4 الباقيين بالتوازي (كلهم بيعتمدوا على `Employee` بس، مش على بعض) |
| 4 | **إضافة مجموعة `HR` في `MenuItemSeedData.cs`**: تتضاف في 1.1.2 (مع الـ Migration) ولا 1.1.4 (مع الـ Controllers)؟ | **1.1.2** — الـ Migration والـ Seed Data لازم يتزامنوا، وأي Controller يتبني بعدين هيحتاجها موجودة أصلًا عشان `ScreenPermissionFilter` ما يرفضش الطلبات |

---

## 6. ترتيب التنفيذ الأمثل

```
1.1.2 (Domain + EF + Migration + MenuItemSeedData)
   │  يشمل: 9 كيانات + HrConfigurations.cs + EncryptedStringConverter مربوط على
   │  EmployeePersonalData + فحص حلقة OrgUnit + FK جديدة لـ MenuItemSeedData
   ▼
1.1.3 (Commands) ──────────────┬── 1.1.5 (PII Endpoint — 1.1b)
   │  يحتاج 1.1.2 بس            │      يحتاج بس EmployeePersonalData من 1.1.2 —
   ▼                            │      مش محتاج ينتظر 1.1.3/1.1.4 خالص
1.1.4 (Queries + Controllers)   │      (فرصة توازي حقيقية لو مطورين اتنين)
   │  يحتاج 1.1.3 (DTOs)        │
   └──────────────┬─────────────┘
                   ▼
              1.1.6 (Tests)
```

**تبعيات**: 1.1.2 حرجة (كل حاجة بعدها بتعتمد عليها). 1.1.3→1.1.4 تسلسلي (الـ Controllers محتاجة الـ Command/Query DTOs). **1.1.5 مستقل تمامًا** بعد 1.1.2 — أفضل فرصة توازي في المرحلة دي.
**توصية على الاختبارات**: **متأجلش كل الاختبارات لـ 1.1.6** — كل Batch (Lookups، Employee، الأربعة التابعين) ياخد اختباراته Integration فور ما يتبني، و1.1.6 يبقى الـ API Tests الشاملة + التشغيل الكامل للـ Regression بس (نفس أسلوب باقي المراحل اللي فاتت).

---

## 7. المخاطر

| # | الخطر | التخفيف |
|---|---|---|
| 1 | **تعارض نمط الملفات** لو اتجاهل، هيبني 9 كيانات بنمط قديم لازم يتصلح لاحقًا | القرار 1 (قسم 5) — يتحسم قبل 1.1.3 مباشرة |
| 2 | **فحص الحلقة لـ `OrgUnit`** كود جديد بالكامل، مش منسوخ ومُختبَر من قبل | كتابة اختبارات كتير (سلسلة عمق 2، 3، حلقة مباشرة، حلقة غير مباشرة) بدل الاعتماد على نمط موجود |
| 3 | **`MenuItemSeedData.cs` ملف واحد كبير مشترك** — أي Merge Conflict مستقبلي لو مطورين اتنين بيضيفوا شاشات مختلفين بنفس الوقت | إضافة الـ 7 شاشات كـ Batch واحد في نفس الـ Commit، مش تدريجيًا |
| 4 | **`EncryptedStringConverter` أول استخدام فعلي ليه** — الاختبارات السابقة (0.3) كانت على كيان تجريبي بس، مش `EmployeePersonalData` حقيقي | اختبار Integration مخصص يتأكد إن `NationalIdEncrypted`/`BankIbanEncrypted` فعلًا مش نص صريح في العمود لـ `EmployeePersonalData` نفسها، مش بس الكيان التجريبي |

---

## 8. Open Questions

1. **قرار 1 أعلاه** (نمط الملفات) — الأهم، ولازم يتحسم قبل أي سطر Command.
2. الأسئلة المفتوحة من `HR-Core-Plan.md §8` (4، 5، 6) **لسه سارية** ومحتاجة قرار قبل ما نوصل لـ 1.4/1.5 — مش عاجلة لـ 1.1.2 لكن هتقابلنا قريب.
3. هل نضيف `HR` كـ `MenuItem` Group واحد يضم كل الـ 7 شاشات، ولا كل شاشة تحت المجموعة الأقرب ليها (مثلًا `HR_SETTINGS` تحت `SETTINGS`)؟ **التوصية: مجموعة `HR` واحدة مستقلة** — أوضح للمستخدم النهائي ومتسقة مع باقي الموديولات (كل موديول ليه مجموعته الخاصة في القائمة).
