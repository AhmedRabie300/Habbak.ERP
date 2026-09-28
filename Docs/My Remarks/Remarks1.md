# خطة العمل للجلسة القادمة

---

## 1. الأولوية الأولى: تنفيذ شاشة تحويلات الخزائن/البنوك (TreasuryTransfer)

### 1.1 ما هو المطلوب؟

شاشة List/Edit لإدارة عمليات التحويل بين الخزائن والحسابات البنكية، متسقة مع بقية شاشات النظام.

### 1.2 الموجود حالياً

| العنصر | الحالة |
|--------|--------|
| كيان `TreasuryTransfer` في الـ Domain | ✅ موجود |
| قاعدة البيانات (جدول `TreasuryTransfers`) | ✅ موجود (من التقرير: 17 جدول تم نقلها) |
| شاشة List/Edit | ❌ غير موجودة |
| API Endpoints (GetList, GetById, Create, Update, Delete) | ❌ غير موجودة |

### 1.3 المطلوب تنفيذه

#### 1.3.1 الباك إند (Backend)

| الخطوة | المطلوب | الملفات المتوقعة |
|--------|---------|-------------------|
| 1 | إضافة `TreasuryTransfer` كـ `AuditableEntity` في الـ Domain (إن لم تكن موجودة) | `Domain/Accounting/TreasuryTransfer.cs` |
| 2 | إضافة `DbContext` configuration | `Infrastructure/Data/Configurations/TreasuryTransferConfiguration.cs` |
| 3 | إنشاء Migration جديدة | `dotnet ef migrations add AddTreasuryTransfer` |
| 4 | إنشاء DTOs | `Application/Accounting/TreasuryTransfer/Dtos/TreasuryTransferDto.cs` |
| 5 | إنشاء Commands/Queries | `Application/Accounting/TreasuryTransfer/Commands/CreateTreasuryTransferCommand.cs` <br> `Application/Accounting/TreasuryTransfer/Commands/UpdateTreasuryTransferCommand.cs` <br> `Application/Accounting/TreasuryTransfer/Queries/GetTreasuryTransfersQuery.cs` <br> `Application/Accounting/TreasuryTransfer/Queries/GetTreasuryTransferByIdQuery.cs` |
| 6 | إنشاء Validators (FluentValidation) | لكل Command ملف Validator خاص به |
| 7 | إنشاء Controller | `API/Controllers/TreasuryTransfersController.cs` |
| 8 | التأكد من التكامل مع `IPostingService` (قيد التحويل) | تحديث `PostingService` إن لزم |

#### 1.3.2 الفرونت إند (Frontend)

| الخطوة | المطلوب | الملفات المتوقعة |
|--------|---------|-------------------|
| 1 | إنشاء API Hooks | `frontend/src/features/accounting/treasury-transfers/api.ts` |
| 2 | إنشاء شاشة List | `frontend/src/features/accounting/treasury-transfers/pages/TreasuryTransfersListPage.tsx` |
| 3 | إنشاء شاشة Edit | `frontend/src/features/accounting/treasury-transfers/pages/TreasuryTransferEditPage.tsx` |
| 4 | إضافة المسار (Route) | تحديث `frontend/src/router.tsx` |
| 5 | إضافة عنصر القائمة الجانبية | تحديث `frontend/src/components/layout/Sidebar.tsx` |
| 6 | إضافة الترجمة (i18n) | تحديث ملفات الترجمة (AR/EN) |

### 1.4 القيود والشروط

| الشرط | الوصف |
|--------|-------|
| **عملة التحويل** | لا يسمح بالتحويل بين حسابين بعملتين مختلفتين — يجب أن تكون العملة واحدة |
| **المبلغ** | أكبر من صفر (قاعدة 17 من موديول الحسابات) |
| **المصدر والمستلم** | لا يمكن التحويل من وإلى نفس الحساب |
| **الرصيد** | يجب أن يكون الرصيد في الحساب المصدر كافياً (يُتحقق في الخدمة، وليس فقط في الـ Command) |
| **القيد المحاسبي** | يتم عبر `IPostingService` داخل نفس الـ Transaction (Atomicity) |

### 1.5 الأولوية

🔴 **عالية** — هذه هي النقطة الوحيدة المفقودة في موديول الحسابات العامة حالياً

---

## 2. تأجيل سلاسل الموافقات (Approval Workflow)

### 2.1 القرار

**يتم تأجيل تنفيذ سلاسل الموافقات (Approval Workflow) إلى ما بعد اكتمال كل الموديولات الأساسية (1-10).**

### 2.2 المبرر

| السبب | التوضيح |
|-------|----------|
| **كل موديول بيستخدمها** | سلاسل الموافقات مش مقتصرة على موديول واحد — هتستخدم في المشتريات، المبيعات، المرتبات، العهد، إلخ. |
| **معرفة المتطلبات الكاملة** | بنّاءها دلوقتي هيكون مبني على تخمين لاحتياجات الموديولات التانية. |
| **تجنب إعادة العمل** | لما الموديولات التانية تتبنى، ممكن تظهر متطلبات جديدة للـ Approval (مثال: مراتب تحتاج موافقة المدير المالي، مشتريات تحتاج موافقة المدير العام) — الانتظار يوفّر وقت إعادة التطوير. |

### 2.3 ما هو موجود حالياً

| العنصر | الحالة |
|--------|--------|
| مواصفة سلاسل الموافقات في `00-Project-Overview.md` | ✅ موجودة (قسم 12) |
| محرك `IApprovalWorkflowService` | ✅ موجود في الـ Overview (قسم 12) |
| كيانات `ApprovalInstance`, `ApprovalAction`, إلخ | ⚠️ موجودة في المواصفة لكن لم يتم التحقق من تنفيذها في الكود |
| التكامل مع `IPostingService` | ⚠️ موثق في المواصفة لكن لم يتم التحقق من تنفيذه |

### 2.4 المطلوب الآن

| الخطوة | المطلوب |
|--------|---------|
| 1 | **لا شيء** — القرار هو **التأجيل** وليس البناء |
| 2 | توثيق القرار في `00-Project-Overview.md` (قسم 29 — قرارات معلّقة) |

**النص المقترح للإضافة في `00-Project-Overview.md` (قسم 29):**

> - [x] **تأجيل تنفيذ سلاسل الموافقات (`ApprovalWorkflow`, `ApprovalInstance`)** إلى ما بعد اكتمال كل الموديولات الأساسية (1-10) — القرار مؤجل، سيُعاد النظر فيه بعد بناء الموديولات كاملةً لتحديد المتطلبات الفعلية.

### 2.5 الأولوية

🟢 **منخفضة حالياً** — سيُعاد تقييمها بعد إنجاز كل الموديولات

---

## 3. جدول المقترح للجلسة القادمة

| # | الخطوة | التقدير الزمني |
|---|--------|----------------|
| 1 | تنفيذ `TreasuryTransfer` — الباك إند (كيانات + Migration + Commands/Queries + Controller) | 30-45 دقيقة |
| 2 | تنفيذ `TreasuryTransfer` — الفرونت إند (List + Edit + Sidebar) | 30-45 دقيقة |
| 3 | اختبار العملية كاملة (إنشاء، تعديل، حذف، قيد محاسبي) | 15 دقيقة |
| 4 | توثيق القرار في الـ Overview وتحديث التقرير | 10 دقائق |
| **الإجمالي** | | **~1.5 - 2 ساعة** |

---

## 4. الخلاصة

| البند | القرار |
|-------|--------|
| **TreasuryTransfer** | ✅ **يُنفَّذ حالياً** — أولوية عالية |
| **سلاسل الموافقات** | ⏳ **يُؤجَّل** — بعد اكتمال كل الموديولات (1-10) |

---

**تاريخ الخطة:** 2026-09-08
**المُعد:** فريق تحليل النظام