# Phase 1.4 — Research Pass (إنهاء خدمة إداري مبسّط)

**التاريخ**: 2026-09-27
**المرجع**: `Docs/Implementation/HR-MASTER-PLAN.md § Phase 1.4` · `Docs/Implementation/HR-Core-Plan.md §1.4` · `Docs/Modules/10-Module-HR-Payroll.md` قاعدة 43-44

---

## 1. فحص الكود الفعلي — `TerminateEmployeeCommandHandler`

**الملف**: `src/Habbak.ERP.Application/HR/Employees/Commands/TerminateEmployee/TerminateEmployeeCommand.cs` (موجود بالفعل من Batch B5، مش جديد كما كانت `HR-Core-Plan.md` الأصلية تفترض — مطابق لتصحيح `HR-MASTER-PLAN.md`).

**الحالة الفعلية المتحقق منها سطر بسطر**:

| البند | السطر | الحالة |
|---|---|---|
| رفض لو الموظف متعيّن إنهاء خدمته بالفعل | 32-35 | موجود (`HR-EMPLOYEE-ALREADY-TERMINATED`) |
| فحص عهدة نقدية مفتوحة | 37-42 | موجود — `CustodyRegister` حيث `EmployeeId == request.EmployeeId && Status == CustodyStatus.Open` → `HR-EMPLOYEE-OPEN-CUSTODY`. **ده بالظبط الفحص اللي المفروض "يتوسّع"، مش يتكرر بفحص منفصل** |
| إغلاق العقود النشطة | 46-53 | موجود — `EmploymentContractStatus.Terminated` + `EndDate` |
| تعطيل المستخدم | 58-63 | موجود — `UserStatus.Suspended` |
| **سحب الجلسات النشطة** | 64 | **موجود وشغّال بالفعل** — `UserRules.RevokeSessionsAsync(db, user.Id, "EmployeeTerminated", cancellationToken)`. **ده يصحّح `Known-Issues.md`** اللي لسه مسجّل الثغرة دي كـ"🔴 لسه مفتوحة" — الكود اتصلح فعليًا (فيه اختبار مخصص ليها: `EmployeeBatch5Tests.TerminateEmployee_revokes_the_linked_user_active_refresh_token_session`)، بس التوثيق ما اتحدّثش. **تحديث توثيقي بسيط مطلوب في 1.4.4**، مش كود. |
| تعطيل الـ `UserScope` | 67-73 | موجود — كل Scope بتاعت المستخدم في نفس الشركة |
| إلغاء الورديات المستقبلية | 75-78 | موجود — `ShiftAssignments` بتاريخ `>= today` |
| **فحص عهدة الأصول (`CustodyOfficer`/`FixedAsset`)** | — | **مفيش خالص** — دي بالضبط الفجوة الوحيدة المتبقية، ومطابقة تمامًا لما `HR-MASTER-PLAN.md` بيقوله. |

**نتيجة**: التوصيف في `HR-MASTER-PLAN.md §Phase 1.4` **مطابق للكود بنسبة كاملة** — الفجوة الحقيقية الوحيدة محصورة ودقيقة: فحص عهدة الأصول.

---

## 2. تأكيد اكتمال شرط Phase 1.2 (الفحص المطلوب في 1.4.1 تحديدًا)

| الشرط | الحالة |
|---|---|
| `CustodyOfficer.EmployeeId` (+ FK لـ`Employees`) | ✅ موجود (Phase 1.2، `Phase-1.2-Final.md`) |
| `FixedAsset.CustodyOfficerId` (+ FK لـ`CustodyOfficer`) | ✅ موجود من قبل (موديول 08، `FixedAsset.cs`) — السلسلة `Employee → CustodyOfficer → FixedAsset` **مكتملة الآن بالفعل** بفضل Phase 1.2، من غير أي تعديل إضافي على `FixedAsset` نفسه. |

**قاعدة 43 (`10-Module-HR-Payroll.md:258`)، النص الحرفي**: *"الإنهاء مايتعتمدش نهائيًا طول ما في عهدة مفتوحة (`CustodyRegister` بحالة `Open`) أو أصل عهدته على الموظف (`FixedAsset.CustodyOfficerId` → `CustodyOfficer.EmployeeId`)، إلا لو اتسوّت أو اتحوّلت لـ `EmployeeDeduction` في التسوية."* — جزء "التسوية/`EmployeeDeduction`" برّه نطاق 1.4 (Phase 5 لسه مبنيش) — **نفس نطاق "مبسّط عمدًا" الموثّق أصلًا لهذه المرحلة**.

---

## 3. قرارات تنفيذ صغيرة (مش معمارية، هعتمدها إلا لو فيه اعتراض)

1. **نفس كود الخطأ `HR-EMPLOYEE-OPEN-CUSTODY`** للحالتين (نقدية وأصول) — الخطة نفسها بتقول "**توسيع** فحص العهدة"، مش فحص جديد منفصل، فده امتداد لنفس المفهوم مش ميزة مستقلة. الرسالة النصية بس هتختلف (تسمي الأصل/الكود بتاعه) عشان يبان للمستخدم إيه بالظبط اللي واقف.
2. **استبعاد `FixedAssetStatus.Disposed`/`WrittenOff`** من الفحص — أصل اتباع أو اتشطب مالوش عهدة حقيقية باقية على حد، فمينفعش يمنع الإنهاء. الحالات التانية (`Draft`/`Active`/`InMaintenance`/`Transferred`) كلها بتفتكر إن فيه مسؤولية باقية.
3. **مفيش `confirm=true` تجاوز** — نفس القرار المحسوم بالفعل لحالة `CustodyRegister` (موثّق في XML doc الملف نفسه: "resolves open question 4 (§8)... rejects... rather than closing it") — نفس المنطق يتطبّق على عهدة الأصول لنفس السبب (التسوية محتاجة Flow خاص بيها، مش Side Effect لإنهاء الخدمة).
4. **الاختبار الجديد ينضاف لملف `EmployeeBatch5Tests.cs` الموجود** (قسم `// TerminateEmployee` بالفعل فيه)، مش ملف مستقل جديد — استمرار طبيعي لنفس التغطية، بنفس الـ Helper (`SeedEmployeeAsync`) ونفس نمط `TerminateEmployee_is_rejected_while_an_open_custody_exists` المجاور.

---

## 4. ملاحظة توثيقية جانبية (خارج نطاق 1.4 التنفيذي، تتصلح في 1.4.4)

`Docs/Reports-UPDATED/Known-Issues.md` (بند "🔴 لسه مفتوحة — `TerminateEmployeeCommand` (B5) بيوقف الـ `User` من غير ما يسحب جلساته") و`Docs/Modules/10-Module-HR-Payroll.md:634` (جدول التتبع، صف "§3 — قاعدة 44") **كلاهما بيوصفوا ثغرة مش موجودة في الكود الحالي فعليًا** — الكود بيسحب الجلسات (سطر 64 أعلاه) وله اختبار مخصص ناجح. هذا تحديث توثيقي بسيط (تغيير الحالة لـ"اتصلح")، هيتعمل في 1.4.4 مع باقي التوثيق، مش سبب لتوقف أو Batch منفصل.

---

## 5. الملفات المتأثرة (مؤكدة من الكود)

| الغرض | الملف | نوع التغيير |
|---|---|---|
| توسيع فحص العهدة | `src/Habbak.ERP.Application/HR/Employees/Commands/TerminateEmployee/TerminateEmployeeCommand.cs` | تعديل — إضافة فحص `FixedAsset` حيث `CustodyOfficerId` ينتمي لأي `CustodyOfficer` بـ`EmployeeId == request.EmployeeId` و`Status` مش `Disposed`/`WrittenOff` → نفس `HR-EMPLOYEE-OPEN-CUSTODY` برسالة موسّعة |
| Tests | `src/Habbak.ERP.IntegrationTests/HR/EmployeeBatch5Tests.cs` | تعديل — اختبار جديد بجانب `TerminateEmployee_is_rejected_while_an_open_custody_exists` |
| Docs | `Docs/Implementation/HR-Core-Plan.md §1.4` (حالة "منفّذة بالكامل ضمن نطاقها المبسّط") + `Known-Issues.md` (تصحيح حالة بند الجلسات لـ"اتصلح") + `10-Module-HR-Payroll.md:634` (تحديث نفس الصف) + `Phase-1.4-Final.md` (جديد) | تعديل/جديد |

**لا Migration مطلوبة** — مطابق للخطة (كل الأعمدة/الـ FKs المطلوبة موجودة بالفعل من Phase 1.2 وموديول 08).

---

## 6. الاختبارات المطلوبة

1. **موظف عليه أصل ثابت في عهدته** (عن طريق `CustodyOfficer.EmployeeId` → `FixedAsset.CustodyOfficerId`، `Status = Active`) = الإنهاء **يُرفض** بـ`HR-EMPLOYEE-OPEN-CUSTODY`.
2. **(للتأكيد، مش تكرار)**: أصل بحالة `Disposed`/`WrittenOff` تحت نفس `CustodyOfficer` = **لا يمنع** الإنهاء.
3. الاختبارات الموجودة بالفعل (`TerminateEmployee_is_rejected_while_an_open_custody_exists`, `TerminateEmployee_sets_status_suspends_the_user_and_deactivates_its_scopes_in_one_save`, `TerminateEmployee_revokes_the_linked_user_active_refresh_token_session`) لازم تفضل عادية بعد التعديل (Regression).

---

## الخلاصة

**جاهز للتنفيذ فورًا** — فجوة محصورة ودقيقة جدًا (فحص إضافي واحد في Handler موجود بالفعل)، صفر Migration، صفر تعديل على أي Application/API/Frontend تاني. القرارات الصغيرة في قسم 3 مش معمارية ومش هتغيّر بشكل الحل — هعتمدها كما هي إلا لو فيه اعتراض. **جاهز للتنفيذ بمجرد "موافق".**
