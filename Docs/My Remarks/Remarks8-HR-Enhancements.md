# Remarks8 — HR Enhancements (8 ملاحظات من مراجعة المستخدم)

**التاريخ**: 2026-09-27 (الملاحظات) → 2026-09-28 (تنفيذ Items 5-8، اعتماد Phase 3B رسميًا، تنفيذ Items 1-4 في Phase 3C [جانب الـCloud] وبانتظار Local Verification)
**المصدر**: مراجعة المستخدم بعد Phase 3 Research Pass

## جدول الملاحظات

| # | الموديول | الشاشة | الملاحظة | القرار |
|---|---|---|---|---|
| 1 | HR | الموظفين → تاب العقود | العقد لازم يحتوي على بنود (بدل سكن، انتقالات، إلخ) مش بس الراتب الأساسي | ✅ Phase 3C — نُفذ (Cloud)، بانتظار Local Verification |
| 2 | HR | الموظفين → تاب العقود | تاب العقود لازم يكون فيه مرفقات | ✅ Phase 3C — نُفذ (Cloud)، بانتظار Local Verification |
| 3 | HR | الموظفين → تاب الشهادات | تاب الشهادات لازم يكون فيه مرفقات | ✅ Phase 3C — نُفذ (Cloud)، بانتظار Local Verification |
| 4 | HR | التعيين | العقد في Hiring Wizard لازم يحتوي على بنود | ✅ Phase 3C — نُفذ (Cloud)، بانتظار Local Verification |
| 5 | HR | جداول الورديات | إمكانية تعيين جدول الوردية لفترة أكتر من أسبوع (إضافة حقل "إلى تاريخ") | ✅ Phase 3 Amendment — نُفذ |
| 6 | HR | جداول الورديات | إمكانية تحديد أكتر من يوم إجازة أسبوعية | ✅ Phase 3 Amendment — نُفذ |
| 7 | HR | جداول الورديات | شاشة جديدة لتوليد جدول الورديات على مستوى الهيكل التنظيمي + استثناء موظفين + أكتر من يوم راحة | ✅ Phase 3 Amendment — نُفذ |
| 8 | HR | عطلة جديدة | إضافة حقل "إلى تاريخ" (عيد الفطر من... إلى...) | ✅ Phase 3 Amendment — نُفذ |

---

## Items 5-8 — Phase 3 Amendments (نُفذت فعليًا في نفس Phase 3، مش Phase منفصلة)

### Item 5: ShiftSchedule — StartDate + EndDate

**الحالي قبل التعديل**: `WorkDate` (يوم واحد بس).
**اتنفذ**:
- `ShiftSchedule.WorkDate` (rename) → `StartDate`، وإضافة `EndDate` (`DateOnly?`، `null` = يوم واحد).
- Migration `Phase3Amendments` (RenameColumn — البيانات القديمة اتحافظ عليها، مش Drop+Add).
- التحقق من عدم تداخل الفترات بقى مسؤولية `CreateShiftScheduleCommandHandler`/`UpdateShiftScheduleCommandHandler` (`HR-SHIFT-SCHEDULE-OVERLAPS`) بدل فهرس فريد بالـDB (فهرس فريد على يوم واحد مش منطقي مع فترات متراكبة).
- كل استعلام "هل اليوم ده كذا؟" (RecomputeDailyAttendance، LeaveDaysCalculator) بقى بيستخدم `ShiftScheduleLookup` (Range Containment) بدل مساواة يوم بيوم مباشرة.
- `GenerateWeeklyScheduleCommand` (الموجود من قبل، لموظف واحد) فضل بسيط عمدًا — صف مستقل لكل يوم (`EndDate = null`)، الـ Range Row الموحّدة استخدامها الأساسي في الإدخال اليدوي (شاشة `HR_SHIFT_SCHEDULES` مباشرة) مش التوليد الآلي.

### Item 6: Multiple Weekly Rest Days

**اتنفذ**: كيان جديد `EmployeeWeeklyRestDays` (`Id`, `CompanyId`, `EmployeeId`, `WeeklyRestDaysMask` — Bit Mask فوق `DayOfWeek` (bit 0 = الأحد ... bit 6 = السبت)، `EffectiveFrom`, `EffectiveTo?`).

**قرار تصميم مهم**: `EmployeeWeeklyRestDays` مصدرها الوحيد هو **المولّد الجماعي** (Item 7) — بيتسجّل/يتحدّث كمرجع افتراضي للمرة الجاية بس. `Attendance`/`LeaveDaysCalculator` **مبيقروش منه مباشرة أبدًا** — الحقيقة المُجسَّدة تفضل دايمًا صفوف `ShiftSchedule` الفعلية (الأمندمنت رقم 5). السبب: صف `ShiftSchedule` واحد لازم يحمل قيمة موحّدة (`IsRestDay`) على طول فترته، فمفيش طريقة تمثيل نمط "الجمعة راحة، الباقي شغل" جوه صف واحد — المولّد الجماعي هو اللي بيحوّل النمط الأسبوعي لصفوف يومية فعلية (صف مستقل لكل يوم، `EndDate = null`).

### Item 7: Bulk Shift Schedule Generator

**شاشة جديدة**: `HR_SHIFT_SCHEDULE_GENERATOR` (Controller مستقل `ShiftScheduleGeneratorController`، نفس بادئة المسار `/api/v1/hr/shift-schedules/...` بس صلاحية منفصلة عن `HR_SHIFT_SCHEDULES`).

**اتنفذ**:
- `GenerateBulkShiftSchedulesCommand(OrgUnitId, FromDate, ToDate, WorkShiftDefinitionId?, WeeklyRestDaysMask, Exceptions?)`.
- `PreviewBulkShiftSchedulesQuery` — نفس منطق التوليد بالظبط (`BulkScheduleResolver` مشترك) بدون أي كتابة، عشان المعاينة تطابق التوليد الفعلي بعدها.
- حل الموظفين: `OrgUnit` المختار + كل الوحدات التابعة له (BFS فوق `ParentId`) — موظفين نشطين (`EmployeeStatus.Active`) بس.
- الاستثناءات (`BulkScheduleExceptionInput`): موظف بعينه ياخد `WorkShiftDefinitionId`/`WeeklyRestDaysMask` مختلفين عن الإعداد الافتراضي للمجموعة.
- Idempotent: أي يوم له صف `ShiftSchedule` بالفعل (حتى من مصدر تاني) بيتخطّى، مش بيتكرر.
- `POST /api/v1/hr/shift-schedules/generate-bulk` (توليد فعلي) + `POST /api/v1/hr/shift-schedules/preview-bulk` (معاينة، `[ScreenAction(View)]`).
- Frontend: `BulkShiftScheduleGeneratorPage.tsx` — اختيار OrgUnit (قائمة مسطّحة بمسافات بادئة حسب العمق، مش شجرة تفاعلية كاملة — تبسيط متعمَّد لضيق الوقت)، Period، Work Shift افتراضي، 7 Checkboxes لأيام الراحة الأسبوعية، قسم استثناءات قابل للتكرار، معاينة، وتوليد.

### Item 8: Holiday — StartDate + EndDate

**اتنفذ**:
- `Holiday.Date` (rename) → `StartDate`، وإضافة `EndDate` (`DateOnly?`).
- كل فحص "هل اليوم ده عطلة؟" بقى بيستخدم `HolidayLookup` (Range Containment/Expansion) بدل مساواة يوم بيوم — مُستخدَم في `RecomputeDailyAttendanceCommandHandler` و`LeaveDaysCalculator`.

### التنفيذ التقني المشترك (الأربع بنود)

- **Migration واحدة**: `Phase3Amendments` (rename + add columns + جدول جديد) — اتطبّقت Backup → Trial DB → Real DB، وDown() اتأكد إنها شغّالة (Round-trip كامل على Trial DB قبل ما تتطبّق على `HabbakErp`).
- **Tests**: 6 اختبارات تكامل جديدة (Range Expansion لـ ShiftSchedule، رفض التداخل، Holiday Range في Attendance، المولّد الجماعي مع الاستثناءات، Idempotency المولّد الجماعي) — كل الـ 20 اختبار في `AttendanceAndLeaveTests.cs` ناجحة.
- **Docs**: هذا الملف + `Phase-3-Final.md` (تفصيل كامل في §Amendments).

---

## Items 1-4 — Phase 3C (بعد Phase 3B — Phase 3B اعتُمدت رسميًا 2026-09-28. الجانب الخاص بالـCloud من Phase 3C منجز بالكامل [3C.2 → 3C.6]، تفاصيل التنفيذ الفعلي في `Docs/Implementation/Phase-3C-Final.md`؛ الكيان/الحلول أدناه كانت المقترح الأصلي وطابقت التنفيذ الفعلي بدون انحراف)

### Item 1 & 4: EmploymentContractLine

**كيان مقترح**:
```csharp
public class EmploymentContractLine
{
    long Id;
    long EmploymentContractId;
    string NameAr;      // "بدل سكن"
    string NameEn;
    decimal Amount;
    ContractLineType Type;   // Earning / Deduction
    bool IsTaxable;
    bool IsInsurable;
    int Order;
    // Auditable
}
```

العلاقة: `EmploymentContract.Lines` (1:N).

**UI**:
- تاب "عقود" → Line Editor لكل عقد.
- Hiring Wizard → Line Editor في خطوة "عقد".

**Migration**: جدول `EmploymentContractLines`.

### Item 2: Contract Attachments

**التنفيذ المقترح**: `EmploymentContract.AttachmentId?` (FK → `Attachments`) — نفس نمط `EmployeeDocument.AttachmentId` تمامًا، مباشر بدون Polymorphic. **UI**: زر رفع في Contract Tab.

### Item 3: Certification Attachments

نفس الحل: `EmployeeCertification.AttachmentId?`. **UI**: زر رفع في Certification Tab.
