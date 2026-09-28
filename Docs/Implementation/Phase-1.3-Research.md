# Phase 1.3 — Research Pass (الموظف كطرف في السندات)

**التاريخ**: 2026-09-27
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md § Phase 1.3` · `Docs/Implementation/HR-Core-Plan.md §1.3`

---

## 1. فحص الكود الفعلي

| البند | الملف | الحالة الفعلية المتحقق منها |
|---|---|---|
| `CounterpartyType.Employee` | `src/Habbak.ERP.Domain/Accounting/Enums.cs:68-74` | موجود فعلًا: `Customer=1, Supplier=2, Employee=3, Other=4`. **مفيش أي تعديل Enum مطلوب** — مطابق للخطة بالحرف. |
| النقطة المعطّلة | `src/Habbak.ERP.Infrastructure/Services/CounterpartyAccountResolver.cs:47-50` | مطابق تمامًا لما ورد في الخطة (وصفت "حواليّ سطر 46-50"): بعد فرعين صريحين لـ`Supplier`/`Customer`، أي نوع تاني (يعني `Employee` أو أي قيمة مستقبلية) بيقع على `throw new BusinessRuleException("ACC-COUNTERPARTY-RESOLUTION-PENDING", ...)` برسالة عامة بتذكر "شئون العاملين" صراحة. |
| `CompanyAccountRole.EmployeeReceivable` | `src/Habbak.ERP.Domain/Accounting/CompanyAccountMapping.cs:40` | موجود فعلًا (`= 6`)، ومربوط في `CompanyAccountRoleExtensions` (سطر 71) بـ`AccountType.Asset` — يعني أي محاولة ربطه بحساب من نوع تاني هترفض من فحص التطابق الموجود بالفعل (نفس آلية باقي الأدوار). **الحساب بيتسجّل عن طريق شاشة `CompanyAccountMapping` الموجودة فعلًا** (`UpdateCompanyAccountMappingsCommand.cs`) — مفيش شاشة جديدة مطلوبة. |
| الـ Validation على `CounterpartyId` | `src/Habbak.ERP.Application/Accounting/Vouchers/Commands/CreateVoucher/CreateVoucherCommand.cs:40-43` | **مكتشفة إضافية غير مذكورة صراحة في الخطة**: الـ Validator الحالي بيعامل `Employee` **زي `Customer`/`Supplier` بالظبط** من الأول (`.When(x => x.CounterpartyType is CounterpartyType.Customer or CounterpartyType.Supplier or CounterpartyType.Employee)`) — يعني `CounterpartyId` بقى إلزامي لسند بطرف موظف من غير أي تعديل مطلوب هنا. الاحتمال الأرجح: الـ Enum والـ Validation اتبنوا مع بعض وقت ما `CounterpartyType.Employee` اتضاف كـ Placeholder، قبل ما الـ Resolver نفسه يتبنى. |
| نقطة التكامل مع `IPostingService` | `src/Habbak.ERP.Application/Accounting/Vouchers/Commands/PostVoucher/PostVoucherCommand.cs:43-46` | `PostVoucherCommandHandler.Handle` بينادي `counterpartyAccountResolver.ResolveAccountIdAsync(voucher.CounterpartyType, voucher.CounterpartyId!.Value, ...)` لأي نوع غير `Other`، والنتيجة بتتحط كـ`offsetAccountId` في سطر الـ`PostingRequest` التاني مباشرة. **الموظف مالوش أي منطق تخصيص إضافي بعد الترحيل** (خلافًا لـ`Supplier`/`Customer` اللي ليهم منطق تطبيق على فاتورة/توزيع سطور 100-186 من نفس الملف) — يعني **فرع `Employee` في الـ Resolver وحده كافي، صفر تعديل على `PostVoucherCommand.cs` نفسه**، مطابق تمامًا لما تقوله الخطة ("بدون تعديل الواجهة"). |

**نتيجة**: التوصيف في `HR-Core-Plan.md §1.3` (والمنقول في Master Plan) **مطابق للكود بنسبة كاملة** — مفيش أي انحراف حقيقي، ومفيش أي تغيير حصل في الكود من وقت كتابة الخطة يستدعي تصحيح.

---

## 2. ملاحظة جانبية (خارج نطاق 1.3 — للعلم بس)

`NotImplementedCounterpartyAccountResolver.cs` (نفس المجلد) هو نسخة أقدم/عامة من نفس الفكرة — كانت الـ Fallback الوحيدة قبل ما `Customer`/`Supplier` ياخدوا منطقهم الفعلي في `CounterpartyAccountResolver.cs`. **مش مسجّلة في DI خالص** (`DependencyInjection.cs:48` بيسجّل `CounterpartyAccountResolver` الحقيقي بس) — الاستخدام الوحيد المتبقي ليها هو اختبار واحد في `VoucherAndJournalEntryCommandTests.cs:171` بيبنيها يدويًا عشان يتأكد إن `PostVoucherCommandHandler` بيتعامل صح مع أي فشل تحليل عام (مش لاختبار `Employee` تحديدًا). **مفيش طلب لمسها في Phase 1.3** ولا في أي خطة حالية — تتسجل هنا كملاحظة سياق بس.

---

## 3. Migration

**لا يوجد** — مطابق للخطة. الحساب المحاسبي بيتسجّل عن طريق شاشة `CompanyAccountMapping` الموجودة فعلًا (صف جديد بـ`Role = EmployeeReceivable`)، مفيش أي عمود/جدول جديد.

---

## 4. الملفات المتأثرة (مؤكدة من الكود)

| الغرض | الملف | نوع التغيير |
|---|---|---|
| فرع `Employee` في الـ Resolver | `src/Habbak.ERP.Infrastructure/Services/CounterpartyAccountResolver.cs` | تعديل — إضافة فرع `if (counterpartyType == CounterpartyType.Employee)` بنفس نمط `Supplier`/`Customer` تمامًا: `db.CompanyAccountMappings.Where(m => m.Role == CompanyAccountRole.EmployeeReceivable).Select(m => m.AccountId).FirstOrDefaultAsync(...)`، ولو `null`/مفيش صف → `BusinessRuleException("ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED", ...)` بنفس أسلوب رسائل الفرعين التانيين. |
| Tests | ملف جديد: `src/Habbak.ERP.IntegrationTests/HR/EmployeeCounterpartyPostingTests.cs` (مقترح — نفس مكان اختبارات HR التانية زي `HrFreeFieldMigrationTests.cs`، بدل الإضافة لملف `VoucherAndJournalEntryCommandTests.cs` الموجود أصلًا كبير وعام) | جديد — 2 اختبار على الأقل: (1) موظف حقيقي (Phase 1.1) + `CompanyAccountMapping(Role=EmployeeReceivable)` مربوط = ترحيل ناجح عن طريق `PostVoucherCommandHandler` الحقيقي (نفس نمط بناء الـ Handler يدويًا الموجود في `VoucherAndJournalEntryCommandTests.cs`، لكن بـ`CounterpartyAccountResolver` الحقيقي مش `NotImplementedCounterpartyAccountResolver`)، و(2) نفس السيناريو من غير Mapping = `ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED` صريح. |
| Docs | `Docs/Implementation/HR-Core-Plan.md §1.3` (تحديث حالة التنفيذ) + `Docs/Implementation/Phase-1.3-Final.md` (جديد، بعد STEP 2/3) | تعديل/جديد |

**لا تعديل مطلوب** على: `Enums.cs` (Employee موجود)، `CreateVoucherCommand.cs`/`UpdateVoucherCommand.cs` (الـ Validation شامل Employee بالفعل)، `PostVoucherCommand.cs` (التكامل عام بالفعل)، `CompanyAccountMapping.cs`/الـ Configuration (الدور والتحقق من نوع الحساب موجودين).

---

## 5. الاختبارات المطلوبة (تفصيل، زي ما طلبت الخطة بالحرف)

1. **إيجابي**: سند (Receipt أو Payment) بطرف `CounterpartyType.Employee` + `CounterpartyId` = موظف حقيقي (مُنشأ بنفس نمط Phase 1.1/1.2 الاختباري) + صف `CompanyAccountMapping(Role = EmployeeReceivable)` مربوط بحساب Asset حقيقي = الترحيل ينجح عن طريق `IPostingService.PostAsync` (القيد يتوازن، `Voucher.Status = Posted`)، **من غير أي تعديل على `PostVoucherCommand`/الواجهة**.
2. **سلبي**: نفس السيناريو بالظبط من غير أي صف `CompanyAccountMapping` لـ`EmployeeReceivable` = `BusinessRuleException` بكود `ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED` (مش `ACC-COUNTERPARTY-RESOLUTION-PENDING` العام).

Regression الكامل (Integration + API) بعد كل Batch، زي كل مرة.

---

## 6. الأسئلة المفتوحة قبل الموافقة على التنفيذ

**مفيش أسئلة حقيقية** — الكود مطابق للخطة 100%، القرار المعماري (حساب تحكم واحد بدل حساب فرعي لكل موظف) محسوم ومسجّل من قبل، ومفيش بيانات إنتاج تتأثر (لا Migration أصلًا). القرار الوحيد اللي اتخذته بنفسي (مش محتاج توقف): مكان ملف الاختبار الجديد (`IntegrationTests/HR/` بدل الإضافة لملف `VoucherAndJournalEntryCommandTests.cs` العام) — قرار تنظيمي بسيط مش معماري، هعدّل لو فيه اعتراض.

---

## الخلاصة

**جاهز للتنفيذ فورًا** — أبسط وأقل مخاطرة Phase لحد دلوقتي في الخطة: تعديل حقيقي واحد بس (فرع جديد في ملف موجود، ~8 أسطر، بنفس نمط الفرعين المجاورين له بالحرف)، صفر Migration، صفر تعديل على أي Application/API/Frontend، صفر بيانات إنتاج معرّضة للخطر. **جاهز للتنفيذ بمجرد "موافق".**
