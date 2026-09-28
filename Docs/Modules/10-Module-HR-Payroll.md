# موديول شئون العاملين والمرتبات (HR & Payroll)

> الملف ده ماشي على القالب الموحّد في `Docs/Modules/00-Project-Overview.md` (قسم 27). وهو التحويل الرسمي للتحليل الأولي المعتمد `docs/ERP-Review/12-HR-Payroll-Analysis.md`. أي مصطلح عام مرجعه `00-Project-Overview.md`، ومش هيتشرح هنا تاني. الكيانات المرجعية بتنفّذ `ILookupEntity` (`Code`، و`NameAr`، و`NameEn`، و`IsActive`)، وكل كيان ليه جدول وشاشة مستقلين (`Docs/Analysis/00-System-Wide-Corrections-02.md`). الهرمية بتتعمل بـ `ParentId`. وكل كيان بيمر بالاعتماد بيحمل `Rejected` في الحالة بتاعته، ومعاه `ApprovalInstanceId` (قسم 12.4، قاعدة 6).

> **الأولوية: 🔴** (`docs/ERP-Review/00-Decisions.md`). الأولوية دي بتلغي 🟡 القديمة في قسم 28 من الـ Overview.
> **الموديول ده بيعتمد على بنيتين لسه مش مبنيتين في الكود**: محرك سلاسل الموافقات (قسم 12، والموجود دلوقتي `NullApprovalWorkflowService` بس)، وبنية الإشعارات العامة (قسم 12.7). ترتيب البناء في ملحق ب.

---

## 1. الهدف من الموديول

إدارة دورة حياة الموظف كاملة، من قرار التعيين لحد التسوية النهائية: البيانات، والهيكل التنظيمي، والعقود، والحضور، والإجازات، والإضافي، والرواتب، وضريبة كسب العمل، والتأمينات الاجتماعية، والسلف، والجزاءات، والمكافآت، وإنهاء الخدمة، وتوزيع البقشيش. ده غير **بوابة خدمة ذاتية** للموظف والمدير وأدمن HR.

**الموديول ده هو الكيان المرجعي اللي 7 موديولات بتشاور عليه**: العهد (`CustodyRegister`، و`CustodyOfficer`)، والفنيين (`TechnicianId`)، والموظف كطرف في السندات (`CounterpartyType.Employee`)، والمندوبين (`SALES_REP`)، والبقشيش (`TipsPayable`)، والمعتمِدين في محرك الموافقات (`DirectManager`، و`JobGrade`، و`SpecificEmployee`).

**برّه النطاق (قرار)**: التوظيف (Recruitment) (الـ Overview، قسم 5.1)، والتدريب، وتقييم الأداء الكامل، والتخطيط الوظيفي، والتأمين الطبي الخاص. النطاق بيبدأ من **"قرار تعيين معتمد"**. **داخل النطاق كمرحلة لاحقة**: ربط أجهزة البصمة، وملف التحويل البنكي.

---

## 2. الكيانات والجداول

> **المسارات**: `src/Habbak.ERP.Domain/{HR,Attendance,Payroll,SelfService}/`، ونفس التقسيم في `Application`، و`Infrastructure/Persistence/Configurations/`. كل الكيانات `AuditableEntity` + `ICompanyScopedEntity`، والتشغيلي منها `IBranchScopedEntity`. الأعمدة المشتركة (`Id`، و`PublicId`، و`CompanyId`، و`RowVersion`، وحقول التدقيق) مش مكررة في الجداول تحت.

### 2.1 الموظف والهيكل التنظيمي

#### `Employee` (موظف) — `ILookupEntity`
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `Code` / `NameAr` / `NameEn` / `IsActive` | — | ✅ | الكود من `ICodeGenerator` |
| `BranchId` | `long` | ✅ | فرع العمل. بيحدد مركز التكلفة الافتراضي |
| `OrgUnitId` | `long` | ✅ | → `OrgUnit` |
| `JobPositionId` / `JobGradeId` | `long` | ✅ | |
| `ManagerId` | `long?` | — | → `Employee`. **المصدر اللي `ApprovalApproverType.DirectManager` بيتحل منه** (قسم 12.3). ده مرجع علاقة إدارية، مش هرمية للكيان، **فمش `ParentId`** |
| `UserId` | `long?` | — | → `User`. **الربط الرسمي** (قرار 1): **فريد جوه الشركة** (فهرس فريد مفلتر على `CompanyId + UserId`) |
| `HireDate` | `DateOnly` | ✅ | |
| `EmploymentType` | enum | ✅ | `FullTime` / `PartTime` / `Temporary` |
| `Status` | enum | ✅ | قسم 4.1 |
| `CostCenterDimensionValueId` | `long?` | — | بيتجاوز مركز تكلفة الفرع (موظفي الإدارة) |
| `TerminationDate` | `DateOnly?` | — | بيتملى عند إنهاء الخدمة |

#### `EmployeePersonalData` (1:1 مع `Employee`): جدول منفصل بصلاحية مستقلة
| الحقل | النوع | إلزامي | الوصف |
|---|---|---|---|
| `EmployeeId` | `long` | ✅ | فريد |
| `NationalIdEncrypted` | `string` | ✅ | **تشفير عمود** (قسم 6.2) |
| `NationalIdHash` | `string` | ✅ | **بصمة HMAC عشان التفرّد والبحث** (فهرس فريد على `CompanyId + NationalIdHash`) |
| `NationalIdLast4` | `string(4)` | ✅ | للعرض المخفي |
| `BankIbanEncrypted` / `BankIbanLast4` / `BankName` | `string` | — | مشفّر + آخر 4 |
| `BankId` | `long?` | — | → `Bank` (موديول التنظيم) — البنك المُصدر لحساب الراتب |
| `BirthDate` / `Gender` / `MaritalStatus` | — | ✅ | `Gender`/`MaritalStatus` Enum (قسم 2.6)، مش كيان مرجعي |
| `NationalityId` | `long?` | — | → `Nationality` (موديول التنظيم) |
| `CityId` | `long?` | — | → `City` (موديول التنظيم) — مدينة السكن |
| `MilitaryStatusId` | `long?` | — | → `MilitaryStatus` (كيان مرجعي HR — أدّى الخدمة، معفى، مؤجل...) |
| `QualificationTypeId` | `long?` | — | → `QualificationType` (كيان مرجعي HR — المؤهل الدراسي) |
| `Address` / `PhoneNumber` / `PersonalEmail` | `string` | — | |
| `EmergencyContactName` / `EmergencyContactPhone` | `string` | — | |
| `EmergencyContactRelationshipTypeId` | `long?` | — | → `RelationshipType` (كيان مرجعي HR) |

#### كيانات الهيكل والمستندات
| الكيان | الحقول الأساسية (غير المشتركة) | ملاحظات |
|---|---|---|
| `OrgUnit` — `ILookupEntity` | **`ParentId`**، و`BranchId?`، و`ManagerEmployeeId?`، و`CostCenterDimensionValueId?` | فرع أو قسم إداري (مالية، مشتريات، محمصة) |
| `JobPosition` — `ILookupEntity` | `OrgUnitId?`، و`DefaultJobGradeId?`، و`DefaultSalaryStructureId?` | كاشير، باريستا، مشرف وردية، فني |
| `JobGrade` — `ILookupEntity` | `Level` (`int`)، و`MinSalary?`، و`MaxSalary?` | بيغذّي `ApprovalApproverType.JobGrade` |
| `EmploymentContract` | `EmployeeId`، و`ContractType` (`FixedTerm`/`Indefinite`)، و`StartDate`، و`EndDate?`، و`ProbationEndDate?`، و`BasicSalary`، و`InsurableWage`، و`WorkingHoursPerDay`، و`Status`، و`PreviousContractId?` | التجديد = عقد جديد مربوط بالقديم |
| `EmployeeDocumentType` — `ILookupEntity` | `RequiresExpiry`، و`IsMandatory`، و`ExpiryAlertDays` | بطاقة، و**شهادة صحية**، وفيش، ومؤهل |
| `EmployeeDocument` | `EmployeeId`، و`EmployeeDocumentTypeId`، و`IssueDate`، و`ExpiryDate?`، ومرفق | المرفق بياخد صلاحية ملف الموظف (قسم 8.5) |
| `EmployeeCertification` | `EmployeeId`، و`NameAr`، و`NameEn`، و`Issuer`، و`IssueDate`، و`ExpiryDate?` | باريستا، وسلامة غذاء |

#### كيانات مرجعية جديدة (Lookups) — قرار محسوم (`Docs/Implementation/HR-Core-Plan.md §1.1`، Batch B1)

> **مفيش جدول Lookup عام** — كل عنصر جدول مستقل بيلتزم `ILookupEntity` (`Code`/`NameAr`/`NameEn`/`IsActive`)، نفس مبدأ باقي الكيانات المرجعية في النظام (`00-System-Wide-Corrections-02.md`).

**من موديول التنظيم (Organization) — `src/Habbak.ERP.Domain/Organization/`، مُستخدَمة من HR بس مش مملوكة له**:

| الكيان | الحقول الإضافية | العلاقات |
|---|---|---|
| `Country` | `IsoCode?` | — |
| `City` | — | `CountryId` → `Country` |
| `Nationality` | — | `CountryId?` → `Country` |
| `Bank` | `SwiftCode?`، و`Address?` | `CountryId?` → `Country` |

**من موديول HR — `src/Habbak.ERP.Domain/HR/`**:

| الكيان | الحقول الإضافية | الاستخدام |
|---|---|---|
| `RelationshipType` | — | `EmployeePersonalData.EmergencyContactRelationshipTypeId` (قسم 2.1) |
| `MilitaryStatus` | — | `EmployeePersonalData.MilitaryStatusId` (قسم 2.1) |
| `QualificationType` | — | `EmployeePersonalData.QualificationTypeId` (قسم 2.1) |
| `InsuranceOffice` | `Code?`، و`Address?` | مكتب التأمينات — بيتستخدم في `EmployeeSocialInsurance` (موديول الرواتب، مرحلة لاحقة) |
| `TerminationReason` | — | بيتستخدم في `EmployeeEndOfService` (موديول الرواتب، مرحلة لاحقة) |

### 2.2 الحضور والإجازات

| الكيان | الحقول الأساسية | ملاحظات |
|---|---|---|
| `WorkShiftDefinition` — `ILookupEntity` | `StartTime`، و`EndTime`، و`BreakMinutes`، و`IsNightShift` | **مختلف تمامًا عن `POS.Shift`** (وردية الكاشير المالية) |
| `ShiftSchedule` | `EmployeeId`، و`BranchId`، و`WorkDate`، و`WorkShiftDefinitionId?`، و`IsRestDay` | جدول الأسبوع لكل فرع |
| `TimeEntry` | `EmployeeId`، و`BranchId`، و`EntryType` (`In`/`Out`)، و`TimestampUtc`، و`Source` (`Manual`/`SelfService`/`Kiosk`/`POSShift`/`Device`)، و`POSShiftId?`، و`DeviceId?`، و`Status` (`Suggested`/`Accepted`/`Dismissed`)، و`IsCorrection`، و`CorrectsTimeEntryId?` | **مصدر الحقيقة الوحيد** (قرار 4). **مابيتعدّلش**: التصحيح بصف جديد |
| `Attendance` | `EmployeeId`، و`BranchId`، و`WorkDate`، و`ShiftScheduleId?`، و`FirstInUtc`، و`LastOutUtc`، و`WorkedMinutes`، و`LateMinutes`، و`EarlyLeaveMinutes`، و`OvertimeMinutes`، و`Status` (`Present`/`Absent`/`Leave`/`Holiday`/`RestDay`)، و`IsApproved` | **ملخص يومي محسوب**، وهو **اللي بيدخل الرواتب** |
| `LeaveType` — `ILookupEntity` | **`AccrualMethod`** (`Monthly`/`Annual`/`Hourly`/`None`)، و**`AnnualDays`** (الـ Fallback الافتراضي: القيمة الفعلية بتيجي من `LeaveEntitlementRule` المؤرّخ حسب سنين الخدمة أو السن، ولو مفيش Rule مطابق بيستخدم `AnnualDays`)، و**`MaxCarryOver`**، و`IsPaid`، و`PaidPercentage`، و`DeductFromLeaveTypeId?`، و`RequiresDocument`، و`MaxDaysPerRequest?`، و`GenderRestriction?`، و`MaxTimesInService?`، و`IsCashableOnTermination` | سنوية، وعارضة، ومرضية، ووضع، وحج، وبدون أجر |
| `LeaveBalance` | `EmployeeId`، و`LeaveTypeId`، و`Year`، و**`AccruedThisYear`**، و**`CarriedOver`**، و**`Used`**، و`Pending`، و**`Available`** (محسوب) | صف لكل موظف ونوع وسنة |
| `LeaveBalanceHistory` | `LeaveBalanceId`، و`MovementType` (`Accrual`/`Usage`/`Reversal`/`CarryOver`/`Expiry`/`CashOut`/`Adjustment`)، و`Days`، و`EffectiveDate`، و`SourceType` + `SourceId`، و`Reason` | **سجل كل حركة**. الرصيد = مجموعه دايمًا |
| `LeaveRequest` | `EmployeeId`، و`LeaveTypeId`، و`StartDate`، و`EndDate`، و`Days`، و`Reason`، و`Status`، و`ApprovalInstanceId` | |
| `Holiday` — `ILookupEntity` | `Date`، و`Year`، و`IsNational`، و`BranchId?` | إدخال سنوي |
| `OvertimeRequest` | `EmployeeId`، و`WorkDate`، و`PlannedMinutes`، و`OvertimeType`، و`Reason`، و`Status`، و`ApprovalInstanceId`، و`ActualMinutes?` | موافقة **مسبقة** |

### 2.3 الرواتب

| الكيان | الحقول الأساسية | ملاحظات |
|---|---|---|
| `SalaryComponent` — `ILookupEntity` | `ComponentType` (`Earning`/`Deduction`)، و`CalculationMethod` (`Fixed`/`PercentOfBasic`/`Hourly`/`Formula`)، و`IsTaxable`، و`IsInsurable`، و`IsRecurring`، و**`SourceType`** (`SalaryComponentSource`: `Fixed`/`FromOvertime`/`FromTips`/`FromCommissions`/`FromAdvances`/`FromPenalties`، بيحدد المبلغ بيتجاب منين لو `IsRecurring = false`)، و`AccountRole?` | أساسي، وبدل انتقال، وبدل وجبة، وحافز، وعمولة، وبقشيش. **`SourceType` ده غير `PayrollLine.SourceType`**: الأول بيوصف نوع مصدر البند، والتاني بيشاور على السجل المصدر نفسه |
| `SalaryStructure` — `ILookupEntity` + `SalaryStructureLine` | السطر: `SalaryComponentId`، و`Amount` أو `Percentage` | قالب بيتنسخ عند التعيين |
| `EmployeeSalary` | `EmployeeId`، و`SalaryComponentId`، و`Amount`، و`EffectiveFrom`، و`EffectiveTo?` | **مؤرّخ**، فبيغني عن جدول تاريخ منفصل |
| `PayrollPeriod` | `Year`، و`Month`، و`StartDate`، و`EndDate`، و`CutoffDate`، و`Status` (`Open`/`Locked`/`Closed`) | فهرس فريد على `CompanyId + Year + Month` |
| `PayrollRun` | `PayrollPeriodId`، و**`RunType`**، و**`IdempotencyKey`** (`Guid`)، و`ReferenceId?`، و`Status`، و`ApprovalInstanceId`، و`JournalEntryId?`، و`PaymentJournalEntryId?`، و`EmployeeCount`، و`TotalGross`، و`TotalDeductions`، و`TotalNet`، و`TotalEmployerCost` | قاعدة 30 |
| `PayrollLine` | `PayrollRunId`، و`EmployeeId`، و`BranchId`، و**`CostCenterDimensionValueId`**، و`SalaryComponentId`، و`Amount`، و`Quantity?`، و**`RateSnapshot`** (JSON)، و`SourceType` + `SourceId?` | سطر لكل بند لكل موظف. **بيحتفظ باللقطة** (قاعدة 29) |
| `Payslip` | `PayrollRunId`، و`EmployeeId`، و`Gross`، و`TotalDeductions`، و`Net`، و`IssuedAtUtc`، و`PdfPath?`، و`ViewedAtUtc?` | القسيمة المجمّدة |
| `EmployeeAdvance` + `AdvanceInstallment` | `EmployeeId`، و`Amount`، و`InstallmentCount`، و`StartPayrollPeriodId`، و`Status`، و`ApprovalInstanceId`، و`DisbursementJournalEntryId?`، و`IdempotencyKey` · القسط: `PayrollPeriodId`، و`Amount`، و`PayrollLineId?`، و`Status` | |
| `PenaltyType` / `BonusType` / `DeductionType` — `ILookupEntity` | | جداول مستقلة |
| `EmployeePenalty` | `EmployeeId`، و`PenaltyTypeId`، و`IncidentDate`، و`InvestigationNotes`، و`DeductionDays?` أو `Amount?`، و`PayrollPeriodId?`، و`Status`، و`ApprovalInstanceId` | سقف قانوني (قاعدة 36) |
| `EmployeeBonus` | `EmployeeId`، و`BonusTypeId`، و`Amount`، و`PayrollPeriodId`، و`Status`، و`ApprovalInstanceId` | |
| `EmployeeDeduction` | `EmployeeId`، و`DeductionTypeId`، و`Amount`، و`PayrollPeriodId`، و`Reason`، و`SourceType` + `SourceId?` | عجز عهدة أو تلفيات بعد تحقيق |
| `EmployeeEndOfService` | `EmployeeId`، و**`TerminationType`**، و`NoticeDate`، و`LastWorkingDate`، و`Reason`، و`LeaveBalanceDays`، و`LeaveCashOut`، و`NoticePay`، و`Compensation`، و`ContractualGratuity`، و`OutstandingAdvances`، و`OutstandingCustody`، و`NetSettlement`، و`BeneficiaryType` (`Employee`/`Heirs`)، و`Status`، و`ApprovalInstanceId`، و`FinalPayrollRunId?`، و`Form6SubmittedAt?` | المكوّنات صريحة (قاعدة 41). **مفيش كيان حساب منفصل** |
| `EndOfServiceHeir` | `EmployeeEndOfServiceId`، و`NameAr`، و`Relation`، و`SharePercent`، و`NationalIdEncrypted`/`Hash`/`Last4` | حالة الوفاة |
| `EmployeeSocialInsurance` | `EmployeeId`، و`InsuranceNumber`، و`RegistrationDate?`، و`Form1SubmittedAt?`، و`InsurableWage`، و`Status` (`NotRegistered`/`Registered`/`Terminated`) | |
| `EmployeeTaxProfile` | `EmployeeId`، و`IsExempt`، و`ExemptionReason?`، و`YtdTaxableIncome`، و`YtdTaxWithheld`، و`TaxYear` | تراكمي للتسوية السنوية |
| `TipsDistribution` + `TipsDistributionLine` | `BranchId`، و`PayrollPeriodId`، و`TotalAmount`، و`Method` (`Equal`/`ByDays`/`ByHours`/`ByPoints`)، و`Status`، و`ApprovalInstanceId` · السطر: `EmployeeId`، و`Share`، و`Amount` | بيصفّي `TipsPayable` |
| `EndOfServicePolicy` | `PolicyType` (`None`/`Contractual`)، و`FormulaDescription`، و`MinServiceYears`، و`DaysPerYear`، و`EffectiveFrom` | **⏸️ Pending Legal** (ملحق أ) |
| `HrSettings` (صف لكل شركة) | `MonthBasis` (`ActualDays`/`Thirty`)، و`DefaultCutoffDay`، و`MaxAdvanceInstallmentPercent`، و`KioskSessionSeconds`، و`SelfServiceClockInRequiresLocation`، و`CompanyDefaultApproverUserId` | `MonthBasis` **⏸️ Pending Company** |

### 2.4 الجداول القانونية المؤرّخة (الـ Overview، قسم 10)

> **مفيش ولا نسبة قانونية ثابتة في الكود.** كل جدول مستقل (مش lookup عام)، وكل صف فيه `EffectiveFrom` / `EffectiveTo?`. **كل الأرقام: للتأكيد مع المستشار قبل الإدخال.**

| الجدول | الحقول |
|---|---|
| `MinimumWage` | `Amount`، و`Sector?` |
| `SocialInsuranceRate` | `EmployeeRate`، و`EmployerRate` |
| `InsurableWageLimit` | `MinWage`، و`MaxWage` (بيتحدثوا كل يناير) |
| `PayrollTaxBracketSet` + `PayrollTaxBracket` | `PersonalExemption`، وقواعد الحرمان من الشرائح · الشريحة: `FromAmount`، و`ToAmount?`، و`Rate` |
| `MartyrsFundRate` | `Rate` |
| `OvertimeRate` | `OvertimeType` (`Day`/`Night`/`RestDay`/`PublicHoliday`)، و`Multiplier`، و**`IsLegalMinimum`**، و`GrantsSubstituteDay` |
| `LeaveEntitlementRule` | `LeaveTypeId`، و`MinServiceYears?`، و`MinAge?`، و`Days`. **مصدر الحقيقة لعدد أيام الإجازة حسب شروط الخدمة أو السن، وبيتقدّم على `LeaveType.AnnualDays`** |
| `PenaltyDeductionCap` | `MaxDaysPerMonth` |
| `NoticePeriodRule` | `MinServiceYears`، و`NoticeDays` |

**القيم الابتدائية لـ `OvertimeRate`** (قرار 2، الحد الأدنى القانوني):

| النوع | المعامل | ملاحظة |
|---|---|---|
| نهار | **1.35×** (+35%) | |
| ليل | **1.70×** (+70%) | |
| جمعة أو يوم راحة | **2×** | |
| عيد أو عطلة رسمية | **2×** + يوم بديل | `GrantsSubstituteDay = true` |

### 2.5 الخدمة الذاتية

| الكيان | الحقول الأساسية | ملاحظات |
|---|---|---|
| **مفيش `RequestApproval`** | — | كل اعتماد على المحرك المركزي (قسم 12) |
| `EmployeeRequest` | `EmployeeId`، و`RequestType` (`Leave`/`Advance`/`Certificate`/`DataUpdate`/`Overtime`/`CustodySettlement`/`Resignation`)، و`RequestedByUserId`، و`Status`، و**`ApprovalInstanceId`**، و`DetailEntityType` + `DetailEntityId`، و`SubmittedAtUtc` | رأس موحّد لـ "طلباتي". **هو والكيان التفصيلي بيتحدّثوا في نفس الـ Transaction** |
| `CertificateRequest` | `EmployeeRequestId`، و`CertificateType` (`SalaryDetails`/`Experience`/`ToWhomItMayConcern`)، و`AddressedTo`، و`Language`، و`PdfPath?` | |
| `EmployeeSelfUpdate` | `EmployeeRequestId`، و`FieldName`، و`OldValueMasked`، و`NewValueEncrypted`، و`IsPII` | **مابيتطبّقش إلا بعد الاعتماد** |
| `KioskDevice` | `BranchId`، و`Code`، و`NameAr`، و`NameEn`، و`IsActive`، و`LastSeenAtUtc` | تسجيل التابلت المشترك (قرار 5) |
| `EmployeeKioskPin` | `EmployeeId`، و`PinHash`، و`FailedAttempts`، و`LockedUntilUtc?` | **هاش بس، مفيش PIN صريح** |
| ~~`EmployeeNotification`~~ | — | **اتشال** (قرار 3). بيستخدم البنية العامة `Notification` + `RecipientUserId` (قسم 6.3) |

### 2.6 الـ Enums الأساسية

```csharp
public enum EmployeeStatus { Draft = 1, Active = 2, OnLeave = 3, Suspended = 4, Terminated = 5 }
public enum TerminationType { Resignation = 1, Dismissal = 2, Retirement = 3, Death = 4, ContractEnd = 5 }
public enum PayrollRunType { Regular = 1, Supplementary = 2, FinalSettlement = 3, AnnualTaxSettlement = 4 }
public enum PayrollRunStatus { Draft = 1, Calculated = 2, PendingApproval = 3, Approved = 4, Posted = 5, Paid = 6, Reversed = 7, Rejected = 8 }
public enum HrRequestStatus { Draft = 1, Pending = 2, Approved = 3, Rejected = 4, Cancelled = 5 } // الطلبات (إجازة، وسلفة، وإضافي…)
public enum TimeEntrySource { Manual = 1, SelfService = 2, Kiosk = 3, POSShift = 4, Device = 5 }
public enum LeaveAccrualMethod { Monthly = 1, Annual = 2, Hourly = 3, None = 4 }
public enum SalaryComponentSource { Fixed = 1, FromOvertime = 2, FromTips = 3, FromCommissions = 4, FromAdvances = 5, FromPenalties = 6 }
public enum Gender { Male = 1, Female = 2 }
public enum MaritalStatus { Single = 1, Married = 2, Divorced = 3, Widowed = 4 }
```

> **`Gender`/`MaritalStatus` قيم ثابتة (Enum)، مش كيانات مرجعية (`ILookupEntity`)** — قرار محسوم (`Docs/Implementation/HR-Core-Plan.md §1.1`، Batch B1): مفيش سيناريو عملي لإضافة قيمة جديدة زي ما بيحصل مع الكيانات المرجعية التانية، فمفيش داعي لجدول مستقل ليهم.

### 2.7 إضافات على كيانات موجودة (Migrations)

| الكيان الموجود | التعديل | ملاحظة |
|---|---|---|
| `User.EmployeeId` (`src/Habbak.ERP.Domain/Settings/Users/User.cs:31`) | **للقراءة بس أثناء الانتقال، ويتشال بعدين** (قرار 1) | المرجع الرسمي `Employee.UserId` |
| `CustodyRegister.EmployeeId` (`src/Habbak.ERP.Domain/Accounting/CustodyRegister.cs:11`, `long` إلزامي) | FK → `Employee` | **تقرير تشخيصي قبل الـ FK** (قاعدة 50) |
| `CustodyOfficer` (`src/Habbak.ERP.Domain/Inventory/CustodyOfficer.cs`) | + `EmployeeId?` FK | المطابقة بالاسم مش مضمونة، فجدول مطابقة يدوي |
| `MaintenanceRequest.TechnicianId` + جدولة الصيانة (`src/Habbak.ERP.Domain/FixedAssets/Maintenance.cs:68,139`) | FK → `Employee` | |
| `CompanyAccountRole` (`src/Habbak.ERP.Domain/Accounting/CompanyAccountMapping.cs`) | أدوار جديدة (قسم 6.2) | `EmployeeReceivable = 6` و`TipsPayable = 16` **موجودين ومستخدمين زي ما هم** |
| `SourceDocumentType` (`src/Habbak.ERP.Domain/Accounting/Enums.cs`) | + `EmployeeAdvance`، و`EndOfService`، و`TipsDistribution` | `Payroll = 5` **موجود** |
| `FieldPermissionCatalog` | تسجيل كل حقول `EmployeePersonalData` والأجر | **شرط لتعقيم `AuditLog` تلقائيًا** (قاعدة 6) |

---

## 3. قواعد العمل (Business Rules)

### 3.1 الموظف والهيكل وحماية البيانات
1. **`Employee.UserId` هو الربط الرسمي** بين الموظف وحساب الدخول: اختياري، و**فريد جوه الشركة**. نفس الـ `User` (على مستوى النظام) ممكن يتربط بموظف في كل شركة من المجموعة. **`User.EmployeeId` مايتكتبش فيه من الموديول ده.**
2. **الموظف مايتفعّلش (`Active`) إلا بعد**: عقد ساري، وكل المستندات `IsMandatory` (ومنها **الشهادة الصحية** لعمال الأغذية)، وهيكل راتب.
3. **`ManagerId` مايعملش حلقة** (الموظف مايبقاش مدير لمديره بشكل مباشر أو غير مباشر)، ونفس الكلام لـ `OrgUnit.ParentId`.
4. **الرقم القومي فريد جوه الشركة** عن طريق `NationalIdHash`، **مش عن طريق النص المشفّر**.
5. **حقول الـ PII** (الرقم القومي، والـ IBAN، وأرقام الورثة) **بتتخزن مشفّرة** (قسم 6.2) **وبتظهر مخفية جزئيًا افتراضيًا** (`••••1234`). **الإظهار الكامل بزرار له صلاحية (`HR_REVEAL_PII`)، وكل إظهار بيتسجّل سطر في `AuditLog`** بغض النظر عن إعداد التدقيق العام للكيان.
6. **كل حقل PII أو أجر لازم يتسجّل في `FieldPermissionCatalog`**: ده اللي بيخلّي `AuditLog.FieldChange` يحوّل قيمه لـ `[REDACTED]` تلقائيًا (`src/Habbak.ERP.Domain/Settings/Audit/AuditLog.cs:37`). **في اختبار آلي بيفشل لو حقل متعلّم PII مش متسجّل في الكتالوج.**
7. **الـ PII ممنوع يظهر في الـ Logging** حتى في مستوى `Debug`. **والتصدير (Excel/PDF/CSV) بيحترم نفس الإخفاء**: الرقم القومي مايطلعش كامل في أي ملف.
8. **مفاتيح التشفير جزء إلزامي من نطاق النسخ الاحتياطي** (قسم 24)، ومعاها اختبار استعادة وخطة تدوير. ضياع المفاتيح = ضياع البيانات المشفّرة نهائيًا.
9. **الاحتفاظ ببيانات الموظف بعد ترك الخدمة: 5 سنين مقترحة، وبعدها أرشفة** (Job منفصل عن أرشفة التدقيق اللي مدتها 7 سنين). **⏸️ Pending Legal**.

### 3.2 الحضور
10. **`TimeEntry` هو مصدر الحقيقة الوحيد للحضور** لكل الموظفين (قرار 4). **مابيتعدّلش ولا بيتمسح**: التصحيح بصف جديد (`IsCorrection = true`، و`CorrectsTimeEntryId`) بصلاحية وسبب.
11. **الوردية بتلقّم**: فتح `POS.Shift` بيعمل `TimeEntry(Source = POSShift, Status = Suggested)` لو مفيش دخول مسجّل للكاشير في اليوم ده (عن طريق `Shift.CashierUserId` → `Employee.UserId`)، وقفلها بيقترح خروج. **المقترح بيتقبل تلقائيًا لو مفيش تعارض، وغير كده بيستنى المشرف.**
12. **الوردية بتراجع**: تقرير تعارض يومي (كاشير فاتح وردية وحضوره `Absent`، أو وردية مفتوحة بعد آخر خروج). ده **كاشف احتيال** مش مجرد تدقيق.
13. **مفيش أي تعديل على `POS.Shift`** ولا على أوامره. التلقيم بيحصل بحدث بعد الحفظ.
14. **`Attendance` (الملخص اليومي) بيتحسب** من `TimeEntry` المقبولة مقارنة بـ `ShiftSchedule` (Job ليلي + عند الطلب)، **وهو بس اللي بيدخل الرواتب**.
15. **مفيش `TimeEntry` بتاريخ في `PayrollPeriod` مقفولة (`Locked`)**. الإدخال المتأخر بيتحسب كتسوية في الشهر اللي بعده (قاعدة 33).
16. **التسجيل من الخدمة الذاتية بالموقع** مسموح بس لو `HrSettings.SelfServiceClockInRequiresLocation` متسمح بيه للفرع، والموقع جوه نطاق الفرع.

### 3.3 الإضافي
17. **الإضافي محتاج `OvertimeRequest` معتمد مسبقًا** عن طريق المحرك المركزي. والإضافي الفعلي من غير طلب معتمد **بيظهر استثناء للمشرف ومابيتصرفش تلقائيًا**.
18. **المعامل بييجي من `OvertimeRate` الساري في تاريخ العمل** (قرار 2). سياسة الشركة ممكن تكون أعلى بصف تاني، **لكن مفيش معامل أقل من صف `IsLegalMinimum` لنفس النوع** (تحقق عند الحفظ).
19. **العمل في عيد رسمي بيدّي يوم بديل** (`GrantsSubstituteDay`) كحركة `Accrual` في رصيد إجازة بديلة.
20. **حد أقصى يومي وشهري للإضافي** (جدول مؤرّخ، للتأكيد). والتجاوز بيضيف خطوة اعتماد.
21. **كل نوع إضافي سطر `PayrollLine` مستقل.**

### 3.4 الإجازات
22. **الطلب مايتقدّمش لو `Days > Available − Pending`** (إلا لأنواع `AccrualMethod = None` زي المرضية، اللي بتتحسب بالحدث مع مستند).
23. **الاستحقاق الشهري (الافتراضي)**: `AnnualDays ÷ 12` أول كل شهر، بـ Job **Idempotent لكل موظف وشهر**. و`AnnualDays` الفعلي من `LeaveEntitlementRule` حسب سنين الخدمة أو السن، والتغيير بيحصل تلقائيًا في الشهر اللي الشرط اتحقق فيه.
24. **الترحيل**: بحد `MaxCarryOver`، والزيادة بحركة `Expiry` **بسبب إلزامي**. الرصيد اللي انتهى لأن صاحب العمل رفض الإجازة ممكن يبقى مستحق نقدًا (للتأكيد).
25. **الإجازة العارضة بتتخصم من السنوية** (`DeductFromLeaveTypeId`) بحدها (للتأكيد).
26. **كل تغيير في الرصيد = صف في `LeaveBalanceHistory`**، و`LeaveBalance` مابيتعدّلش مباشرة أبدًا.
27. **إلغاء إجازة معتمدة قبل بدايتها** بيعمل حركة `Reversal`. **وبعد ما بدأت، محتاج اعتماد.**

### 3.5 الرواتب
28. **الحساب**: الأساسي + البدلات + الإضافي + البقشيش − الغياب − الجزاءات (بالسقف) − أقساط السلف − تأمينات العامل − الضريبة − صندوق الشهداء = الصافي. **كل `SalaryComponent` معاه `SourceType` بيحدد مصدر المبلغ لو `IsRecurring = false`** (إضافي من `Attendance`/`OvertimeRequest`، وبقشيش من `TipsDistribution`، وعمولات من فواتير المبيعات المحصّلة، وأقساط من `AdvanceInstallment`، وجزاءات من `EmployeePenalty`). والبنود المتكررة (`Fixed`) بتيجي من `EmployeeSalary`.
29. **كل `PayrollLine` بيحتفظ بـ `RateSnapshot`** للنسبة أو المعامل أو الشريحة اللي اتطبقت فعلًا. **إعادة طباعة قسيمة قديمة بتطلع زي ما هي بالظبط** حتى لو الجداول اتغيّرت. **وصف مؤرّخ استُخدم في تشغيل مرحّل ماينفعش يتعدّل**.
30. **🔴 Idempotency (إلزامي)**: `PayrollRun.IdempotencyKey = PayrollRunKeys.For(CompanyId, Year, Month, RunType, ReferenceId?)`: **`Guid` محسوب** (نفس نمط `DepreciationRunKeys.For` في `src/Habbak.ERP.Application/FixedAssets/DepreciationCommands.cs:98`) **+ فهرس فريد مفلتر بيستثني `Reversed` و`Rejected`** + التقاط `DbUpdateException` لو حصل سباق (`PAY-RUN-EXISTS`).
   - **تشغيل `Regular` واحد بس لكل شركة وشهر.**
   - `Supplementary` و`FinalSettlement` بيضيفوا `ReferenceId` (رقم الموظف أو الطلب) عشان يبقوا **متعددين ومش متكررين**.
   - `AnnualTaxSettlement` واحد بس لكل سنة.
31. **الحماية على 3 مستويات**: مفتاح التشغيل (30) + مفتاح الترحيل (`PostingKeys.For(CompanyId, "PayrollRun.Post", run.Id)`) + **مفتاح الصرف** (`"PayrollRun.Pay"`). **حماية الحساب من غير الصرف مش كفاية.**
32. **تشغيل واحد للشركة كلها**، و**كل سطر عليه `BranchId` + `CostCenterDimensionValueId`** بتوع الموظف وقت التشغيل.
33. **الـ Cutoff**: `PayrollPeriod.CutoffDate` (آخر يوم افتراضيًا). بعده الفترة `Locked`. **وأي إدخال متأخر (حضور، أو إجازة، أو زيادة بأثر رجعي) بيتحسب سطر تسوية في الشهر اللي بعده** (`SourceType = PriorPeriodAdjustment`). **مفيش إعادة حساب لتشغيل مرحّل.**
34. **النسبة والتناسب**: الموظف الجديد، واللي مشي، والإجازة بدون أجر كلهم بالمعادلة `الأجر × (أيام الاستحقاق ÷ أساس الشهر)`، و**أساس الشهر من `HrSettings.MonthBasis`** (**⏸️ Pending Company**: أيام فعلية ولا 30).
35. **مراجعة الاستثناءات قبل الاعتماد (إلزامي)**: صافي بالسالب، أو صافي تحت `MinimumWage` الساري لموظف دوام كامل، أو تغيّر أكتر من X% عن الشهر اللي فات، أو موظف `Active` من غير تسجيل تأميني.
36. **سقف الخصم الشهري** (جزاءات + خصومات) من `PenaltyDeductionCap`. والزيادة **بتترحّل للشهر اللي بعده** مش بتتلغي.
37. **الضريبة بتتحسب سنويًا وبتتقسّط شهريًا** (`EmployeeTaxProfile` التراكمي)، و**التسوية آخر السنة بتشغيل `AnnualTaxSettlement`**.
38. **القسيمة (`Payslip`) مابتظهرش في الخدمة الذاتية إلا بعد ما التشغيل يبقى `Posted`.**
39. **البقشيش**: `TipsDistribution` معتمد بيتحوّل لسطور `PayrollLine` (بند بقشيش)، وقيد الاستحقاق بيقفل `TipsPayable` بمقداره.
40. **الصرف في الإصدار الأول: سند صرف + كشف صرف** (✅ محسوم). ملف التحويل البنكي مرحلة لاحقة.

### 3.6 إنهاء الخدمة والسلف والجزاءات
41. **مستحقات نهاية الخدمة مكوّنات صريحة** (`LeaveCashOut`، و`NoticePay`، و`Compensation`، و`ContractualGratuity`)، **وكل مكوّن بيتحسب بقاعدة نوعه** (`TerminationType`). و**`ContractualGratuity` مابيتحسبش إلا لو `EndOfServicePolicy.PolicyType = Contractual`** (**⏸️ Pending Legal**: القطاع الخاص في مصر مافيهوش مكافأة نهاية خدمة قانونية عامة، وده للتأكيد).
42. **الحساب بيطلع من تشغيل `FinalSettlement`**، مش حساب منفصل.
43. **الإنهاء مايتعتمدش نهائيًا طول ما في عهدة مفتوحة** (`CustodyRegister` بحالة `Open`) **أو أصل عهدته على الموظف** (`FixedAsset.CustodyOfficerId` → `CustodyOfficer.EmployeeId`)، إلا لو اتسوّت أو اتحوّلت لـ `EmployeeDeduction` في التسوية.
44. **اعتماد الإنهاء بيعمل في نفس الـ Transaction**: `Employee.Status = Terminated` + `User` → `Suspended` (لو مربوط) + إلغاء كل `UserScope` في الشركة دي + إلغاء `ShiftSchedule` المستقبلية.
45. **في حالة الوفاة**: `BeneficiaryType = Heirs`، والطرف الدائن في القيد **`HeirsPayable`** بتوزيع `EndOfServiceHeir`.
46. **السلفة**: مفيش سلفة جديدة ولسه في سلفة مفتوحة (إلا بصلاحية)، والقسط ≤ `MaxAdvanceInstallmentPercent` من الصافي، ومفيش سلفة في فترة الاختبار (إعداد).
47. **الجزاء محتاج `InvestigationNotes` قبل الاعتماد** (تحقيق قبل الجزاء).
48. **تغيير الحساب البنكي من الخدمة الذاتية**: اعتماد HR + تأكيد بـ 2FA من الموظف + **إشعار على القناة القديمة** (الإيميل أو الموبايل المسجّل قبل التغيير).

### 3.7 التأمينات والتكامل
49. **موظف `Active` من غير `EmployeeSocialInsurance.Status = Registered`** بعد المهلة القانونية (للتأكيد) بيظهر تنبيه امتثال يومي. **وتغيير الأجر التأميني بيولّد تذكير استمارة 2.**
50. **Migration الحقول الحرة**: قبل تحويل `CustodyRegister.EmployeeId` و`TechnicianId` لـ FK، **تقرير تشخيصي** (زي Remarks7): اللي بيتطابق بيقين بيتربط، والباقي بيتعلّم للمراجعة اليدوية، **ومفيش حاجة بتتمسح**.
51. **مفيش قيد بيتعمل مباشر**: كل قيد عن طريق `IPostingService.PostAsync` بقالب، **وكل سطر عليه `CostCenterId`** (قسم 6.2).
52. **كل الاعتمادات عن طريق المحرك المركزي** (قسم 12)، ومفيش أي آلية اعتماد موازية. **ولحد ما المحرك يتبني، شاشات الطلبات بتفضل مقفولة** (ملحق ب).

---

## 4. دورة حياة المستند / الحالات

> **قواعد المحرك اللي بتنطبق على كل الدورات دي** (قسم 12.4): الرفض نهائي (2)، و**حارس الاعتماد الذاتي** (7): موظف HR بيطلب سلفة لنفسه مايقدرش يعتمد خطوة HR بتاعتها، و**`DirectManager` بيتحل وقت التنفيذ**، ولو `ManagerId = null` بيروح لـ `CompanyDefaultApproverUserId` (8)، و`JobGrade` من غير أي موظف = تنبيه إعداد (9)، و**الإسناد اليدوي** لمعتمد تاني (10). **ورفض السلسلة بيتعكس على حالة الكيان نفسه في نفس الـ Transaction** (6).

### 4.1 الموظف (`Employee`)
```
Draft → [التعيين: سلسلة HR (+ مالية لو JobGrade.Level ≥ X)] → Active ⇄ OnLeave (تلقائي من الإجازة)
                                                               ⇄ Suspended (إداري بسبب)
Active → [إنهاء الخدمة، 4.6] → Terminated
Draft → Rejected (رفض التعيين)
```

### 4.2 طلب الإجازة (`LeaveRequest`) / الإضافي (`OvertimeRequest`)
```
Draft → Pending [السلسلة: DirectManager] → Approved → (الإجازة: حركة Usage | الإضافي: ActualMinutes من Attendance)
                                        ↘ Rejected (نهائي، والطلب الجديد سلسلة جديدة)
Pending/Approved (قبل البداية) → Cancelled (حركة Reversal)
```

### 4.3 تشغيل الرواتب (`PayrollRun`)
```
Draft (Idempotent) → Calculated → [مراجعة الاستثناءات] → PendingApproval [HR ثم مالية]
   → Approved → Posted (قيد الاستحقاق) → Paid (قيد الصرف) → [PayrollPeriod: Closed]
PendingApproval → Rejected  |  Calculated → Draft (إعادة حساب قبل الاعتماد بس)
Posted → Reversed (قيد عكسي بصلاحية عالية، ويتاح بعده تشغيل جديد بنفس المفتاح)
```

### 4.4 السلفة (`EmployeeAdvance`)
```
Draft → Pending [1: DirectManager · 2: HR · (+3: مالية لو ≥ الحد)] → Approved → Disbursed (قيد الصرف)
      → Repaying (أقساط آلية في كل تشغيل) → Settled
Pending → Rejected
```

### 4.5 الجزاء، والمكافأة، وتعديل الراتب، والشهادة، وتحديث البيانات
`Draft → Pending [السلسلة حسب مصفوفة 4.8] → Approved → (تطبيق: سطر رواتب / EmployeeSalary جديد / PDF / تحديث Employee) · Pending → Rejected`

### 4.6 إنهاء الخدمة (`EmployeeEndOfService`)
```
Draft (الخدمة الذاتية للاستقالة / HR للباقي) → Pending [DirectManager ثم HR ثم مالية، دايمًا]
   → Approved → FinalSettlement run (Calculated) → فحص العهد والأصول (قاعدة 43)
   → Settled (قيد التسوية + استمارة 6 + إيقاف الحساب) → Paid → شهادة خبرة
Pending → Rejected
```

| النوع | المستحقات | الإجراءات | ✔ للتأكيد |
|---|---|---|---|
| استقالة | بدل رصيد الإجازات + أجر آخر فترة + تعاقدي (لو السياسة `Contractual`) − السلف والعهد | إخطار مسبق · استمارة 6 | مدة الإخطار |
| إنهاء من الشركة | زي الاستقالة + بدل مهلة الإخطار + تعويض لو الفصل من غير سبب مشروع | تحقيق ومستندات · استمارة 6 | شروط الفصل ومعادلة التعويض |
| معاش | بدل الإجازات + التعاقدي. **المعاش نفسه من هيئة التأمينات** | استمارة 6 + ملف المعاش | |
| وفاة | للمستحقين (`HeirsPayable`): المستحق + بدل الإجازات + منحة الوفاة | شهادة وفاة + إعلام وراثة · استمارة 6 | منحة الوفاة |
| نهاية عقد | بدل الإجازات + التعاقدي | من غير إخطار لو العقد انتهى طبيعي · استمارة 6 | التجديد الضمني |

### 4.7 توزيع البقشيش (`TipsDistribution`)
`Draft → Pending [1: مدير الفرع · 2: مالية] → Approved → Included (في تشغيل الرواتب) · Pending → Rejected`

### 4.8 مصفوفة سلاسل الاعتماد

> المعتمِدين من نوع `ApprovalStepApprover.ApproverType`. والخطوات المتتالية = `StepOrder` مختلف (مفيش `Sequential`). **الحدود المالية بتتنفّذ بـ `ApprovalWorkflowAssignment` بشرط تفعيل** (سلسلة للأقل من الحد، وسلسلة للحد وأكتر)، ومش في الكود. **وقيم الحدود قرار إداري**.

| # | الطلب | الخطوات | الحد | `Screen.Code` |
|---|---|---|---|---|
| 1 | إجازة | 1: `DirectManager` | — | `HR_LEAVE_REQUESTS` |
| 2 | سلفة | 1: `DirectManager` · 2: `Role`=HR (`AnyOne`) · +3: `Role`=مالية لو ≥ الحد | 5000 مقترح | `HR_ADVANCES` |
| 3 | تحديث بيانات | 1: HR · (PII: 1: `DirectManager`، و2: HR) | — | `HR_SELF_UPDATES` |
| 4 | إنهاء خدمة | 1: `DirectManager` · 2: HR · 3: مالية | دايمًا | `HR_END_OF_SERVICE` |
| 5 | إضافي | 1: `DirectManager` (+ خطوة لو فوق الحد الشهري) | — | `HR_OVERTIME` |
| 6 | جزاء | 1: `DirectManager` · 2: HR | — | `HR_PENALTIES` |
| 7 | مكافأة | 1: `DirectManager` · 2: HR · 3: مالية · +4: `JobGrade`=مدير عام لو > الحد | 5000 مقترح | `HR_BONUSES` |
| 8 | تعديل راتب | 1: `DirectManager` · 2: HR · 3: مالية | دايمًا | `HR_SALARY_CHANGES` |
| 9 | شهادة | 1: HR | — | `HR_CERTIFICATES` |
| 10 | تسوية عهدة | 1: `DirectManager` · 2: مالية | — | `ACC_CUSTODY_SETTLEMENTS` |
| 11 | تشغيل رواتب | 1: HR · 2: مالية | دايمًا | `PAY_PAYROLL_RUNS` |
| 12 | تعيين | 1: HR · +2: مالية لو الدرجة ≥ X | — | `HR_HIRING` |
| 13 | توزيع بقشيش | 1: مدير الفرع · 2: مالية | — | `PAY_TIPS_DISTRIBUTION` |

**حالات لازم يبقى ليها اختبار صريح**: مدير فرع بيطلب سلفة لنفسه (`DirectManager` بيتحل لمديره هو، والحارس بيمنعه يعتمد لنفسه) · `ManagerId = null` بيروح للمعتمد الاحتياطي · المدير في إجازة يعني إسناد يدوي · `JobGrade` من غير أي موظف يعني تنبيه.

---

## 5. الشاشات المطلوبة

كل الشاشات ماشية على نمط List/Edit القياسي (`00-Frontend-Specs.md` الأقسام 4-7) ومعاها `ActionBar` الموحّد، إلا اللي مكتوب قصادها غير كده. **الاعتمادات كلها في شاشة "بانتظار اعتمادي" الموحّدة** (`00-Frontend-Specs.md` قسم 18)، **ومفيش شاشة اعتماد خاصة بـ HR**.

### 5.1 شاشات الإدارة (التطبيق الرئيسي)

| # | الشاشة | `Screen.Code` | النوع | ملاحظات |
|---|---|---|---|---|
| 1 | الموظفين | `HR_EMPLOYEES` | List/Edit | تبويبات: بيانات، وشخصية (PII مخفية)، وعقود، ومستندات، وراتب، وتأمينات، وإجازات |
| 2 | الهيكل التنظيمي | `HR_ORG_UNITS` | **شجرة** | `ParentId` |
| 3 | الوظائف / الدرجات | `HR_JOB_POSITIONS` / `HR_JOB_GRADES` | List/Edit | lookup |
| 4 | أنواع المستندات | `HR_DOCUMENT_TYPES` | List/Edit | lookup |
| 5 | التعيين | `HR_HIRING` | **معالج (Wizard)** | موظف ← عقد ← مستندات ← راتب ← تأمينات ← مستخدم |
| 6 | ورديات العمل / الجداول | `HR_WORK_SHIFTS` / `HR_SHIFT_SCHEDULES` | List/Edit + **شبكة أسبوعية** | جدول الفرع |
| 7 | تسجيلات الحضور | `HR_TIME_ENTRIES` | List | المقترح من POS + تصحيح بسبب |
| 8 | الحضور اليومي | `HR_ATTENDANCE` | List + استثناءات | تأخير، وغياب، وإضافي من غير طلب |
| 9 | تعارضات الوردية مع الحضور | `HR_ATTENDANCE_CONFLICTS` | **تقرير تفاعلي** | قاعدة 12 |
| 10 | أنواع الإجازات / العطلات | `HR_LEAVE_TYPES` / `HR_HOLIDAYS` | List/Edit | |
| 11 | طلبات الإجازات / الإضافي | `HR_LEAVE_REQUESTS` / `HR_OVERTIME` | List/Edit | |
| 12 | أرصدة الإجازات | `HR_LEAVE_BALANCES` | List + سجل حركة | تعديل يدوي بسبب وصلاحية |
| 13 | بنود وهياكل الرواتب | `PAY_SALARY_COMPONENTS` / `PAY_SALARY_STRUCTURES` | List/Edit | |
| 14 | تعديلات الرواتب | `HR_SALARY_CHANGES` | List/Edit | مؤرّخة |
| 15 | الفترات | `PAY_PERIODS` | List | فتح، وقفل، وإقفال |
| 16 | تشغيلات الرواتب | `PAY_PAYROLL_RUNS` | **شاشة معالجة** | حساب، واستثناءات، ومقارنة بالشهر اللي فات، واعتماد، وترحيل، وصرف |
| 17 | السلف / الجزاءات / المكافآت / الخصومات | `HR_ADVANCES` / `HR_PENALTIES` / `HR_BONUSES` / `HR_DEDUCTIONS` | List/Edit | |
| 18 | توزيع البقشيش | `PAY_TIPS_DISTRIBUTION` | List/Edit | الرصيد من `TipsPayable` لكل فرع |
| 19 | إنهاء الخدمة | `HR_END_OF_SERVICE` | List/Edit + حساب | + الورثة |
| 20 | الشهادات | `HR_CERTIFICATES` | List | قوالب PDF |
| 21 | الجداول القانونية | `PAY_LEGAL_TABLES` | **تبويب لكل جدول** | ممنوع تعديل صف مستخدم في تشغيل مرحّل |
| 22 | إعدادات HR | `HR_SETTINGS` | Edit | + `EndOfServicePolicy` |
| 23 | لوحة الامتثال | `HR_COMPLIANCE` | **لوحة** | من غير تأمينات، ومستندات منتهية، وعقود قربت تخلص، وتحت الحد الأدنى، وإضافي فوق الحد |
| 24 | أجهزة الكشك | `HR_KIOSK_DEVICES` | List/Edit | |
| 25 | تدقيق إظهار الـ PII | `HR_PII_REVEAL_LOG` | List | من `AuditLog` |

### 5.2 بوابة الخدمة الذاتية: PWA بمدخل `/ess` جوه نفس مشروع React

| # | الشاشة | `Screen.Code` | النطاق | ملاحظات |
|---|---|---|---|---|
| 26 | الرئيسية | `ESS_HOME` | Self | الرصيد، وآخر قسيمة، وطلبات مستنية |
| 27 | قسيمتي | `ESS_PAYSLIPS` | Self | + PDF، بعد `Posted` بس |
| 28 | إجازاتي | `ESS_LEAVE` | Self | الرصيد لكل نوع + سجل الحركة + طلب |
| 29 | حضوري | `ESS_ATTENDANCE` | Self | + تسجيل دخول وخروج (لو مسموح) |
| 30 | طلباتي | `ESS_REQUESTS` | Self | سلفة، وشهادة، وتحديث بيانات، واستقالة. **ومكان الطلب في السلسلة** |
| 31 | ملفي / مستنداتي | `ESS_PROFILE` / `ESS_DOCUMENTS` | Self | PII مخفية دايمًا (**مفيش "إظهار" في البوابة**) |
| 32 | لوحة فريقي | `ESS_TEAM` | Team | حاضر، وغايب، وفي إجازة النهارده، وطلبات مستنية |
| 33 | حضور وإجازات الفريق | `ESS_TEAM_ATTENDANCE` / `ESS_TEAM_LEAVE` | Team | تقويم إجازات |
| 34 | الإشعارات | (البنية العامة) | Self | `Notification` |
| 35 | **وضع الكشك** | `ESS_KIOSK` | Self (بالـ PIN) | قرار 5، والتفاصيل تحت |

**وضع كشك الفرع (قرار 5)**: تابلت مشترك متسجّل (`KioskDevice`) في الفرع. **الدخول برقم الموظف + PIN** (هاش في `EmployeeKioskPin`، وقفل بعد محاولات فاشلة). **الجلسة بتخلص بعد دقيقة من غير نشاط** (`HrSettings.KioskSessionSeconds`)، وبعد كل عملية تسجيل. **ومفيش أي تخزين محلي لأي بيانات**: لا كاش، ولا IndexedDB، ولا localStorage للبيانات. الـ Service Worker بيخزّن الأصول الثابتة (JS/CSS) بس. **الشاشات المتاحة في الكشك**: تسجيل الحضور، وقسيمتي، وإجازاتي، وطلب إجازة. **ومفيش تحديث بيانات ولا سلف من الكشك.**

**أسباب اختيار PWA**: نفس الكود ونفس `ui-kit` ونفس الدخول (JWT + 2FA). الـ `frontend/package.json` مافيهوش PWA دلوقتي، والإضافة صغيرة. إشعارات Push بتشتغل على أندرويد، وعلى iOS 16.4+ لما التطبيق يتثبّت. **والكشك هو نفس الـ PWA بوضع مختلف.**

---

## 6. نقاط التكامل مع موديولات أخرى

### 6.1 الجدول

| الموديول | الاتجاه | التفاصيل |
|---|---|---|
| **الإعدادات والصلاحيات** (`07-Module-Settings-Permissions.md`) | ⬅️➡️ | `Employee.UserId` (قرار 1) · إنهاء الخدمة بيوقف `User` ويلغي `UserScope` (قاعدة 44) · **نطاق بيانات جديد `Self`/`Team`** (قسم 7) · دور نظامي جديد `EMPLOYEE_SELF` · `FieldPermissionCatalog` لحقول PII · **محرك الموافقات** (قسم 12) بيقرا `Employee.ManagerId` و`JobGradeId` |
| **الحسابات** (`01-Module-Accounting.md`) | ➡️ | 7 قوالب ترحيل (6.2) · `CounterpartyType.Employee` بيشتغل ويقفل `ACC-COUNTERPARTY-RESOLUTION-PENDING` · `CustodyRegister.EmployeeId` → FK · سداد التأمينات والضريبة بسند صرف عادي |
| **المخازن** (`02-Module-Inventory-Manufacturing.md`) | ⬅️ | `CustodyOfficer.EmployeeId` → FK. عهدة التحويلات على موظف حقيقي |
| **المشتريات** (`03-Module-Purchasing.md`) | ⬅️ | مقدّم الطلب والمعتمد: `CreatedBy` → `User` → `Employee` · `DirectManager` في سلاسل الشراء |
| **المبيعات** (`04-Module-Sales.md`) | ⬅️➡️ | `SALES_REP` → `Employee`: عمولة كبند راتب من **الفواتير المحصّلة** (سياسة، للتأكيد) + تقرير أداء |
| **نقاط البيع** (`05-Module-POS-Shifts.md`) | ⬅️ | **تلقيم ومراجعة الحضور من غير أي تعديل على `POS.Shift`** (قرار 4) · البقشيش من `TipsPayable` لـ `TipsDistribution` · أداء الكاشير على مستوى الموظف · عجز الوردية الثابت ممكن يبقى `EmployeeDeduction` **بعد تحقيق بس** |
| **الأصول والصيانة** (`08-Module-Maintenance-FixedAssets.md`) | ⬅️ | `TechnicianId` → FK · تكلفة عمالة الصيانة الداخلية · عهدة الأصل (`FixedAsset.CustodyOfficerId`) بتدخل في فحص الإنهاء (قاعدة 43) |
| **التقارير ولوحة التحكم** (`09-Module-Reports-Dashboard.md`) | ➡️ | تكلفة العمالة ونسبتها من المبيعات لكل فرع |
| **الامتثال الضريبي** (`06-Module-ETA-Compliance.md`) | ➡️ | تقرير ضريبة المرتبات بشكل المنظومة الإلكترونية (للتأكيد) |

### 6.2 التكامل المحاسبي (كل القوالب عن طريق `IPostingService`، والمدير المالي هو اللي بيربط الحسابات)

**`CostCenterId` إلزامي على كل سطر**: مركز تكلفة فرع الموظف (عن طريق `BranchDimensionSync` الموجود) أو `Employee`/`OrgUnit.CostCenterDimensionValueId` لموظفي الإدارة. **القيد بيتجمّع لكل مركز تكلفة**، وده اللي بيطلّع تكلفة العمالة في قائمة الدخل لكل فرع.

**الأدوار الجديدة في `CompanyAccountRole`**: `SalariesExpense`، و`SalariesPayable`، و`SocialInsuranceExpense`، و`SocialInsurancePayable` (حساب واحد للحصتين)، و`PayrollTaxPayable`، و`MartyrsFundPayable`، و`LeaveExpense`، و`LeaveProvision`، و`EOSExpense`، و`EOSProvision`، و`HeirsPayable`. **وبنستخدم الموجود زي ما هو**: `EmployeeReceivable = 6` (سلف الموظفين: **أصل مدين، مش التزام**)، و`TipsPayable = 16`.

| القالب | المدين | الدائن | `SourceDocumentType` |
|---|---|---|---|
| استحقاق الرواتب | `SalariesExpense`، و`SocialInsuranceExpense`، و`TipsPayable` | `SalariesPayable`، و`SocialInsurancePayable`، و`PayrollTaxPayable`، و`MartyrsFundPayable`، و`EmployeeReceivable` (أقساط) | `Payroll` (موجود) |
| صرف الرواتب | `SalariesPayable` | `Cash` / `Bank` | `Payroll` |
| صرف سلفة | `EmployeeReceivable` (طرف: موظف) | `Cash` / `Bank` | `EmployeeAdvance` (جديد) |
| مخصص الإجازات الشهري | `LeaveExpense` | `LeaveProvision` | `Payroll` |
| مخصص نهاية الخدمة الشهري | `EOSExpense` | `EOSProvision` | `Payroll`. **بس لو `EndOfServicePolicy ≠ None`** |
| تسوية نهاية الخدمة | `SalariesPayable`، و`LeaveProvision`، و`EOSProvision` | `SalariesPayable` أو `HeirsPayable`، و`EmployeeReceivable`، وحساب العهدة | `EndOfService` (جديد) |
| توزيع البقشيش | (داخل قيد الاستحقاق) | | `TipsDistribution` (جديد، مرجعي) |

**طريقة المخصصات**: **إعادة قياس شهرية** (المستحق المتراكم في آخر الشهر − رصيد المخصص = قيد الفرق)، لأنها بتتعامل مع زيادات الأجر ومع حدود السنين تلقائيًا. **ولو السياسة `None`، مخصص نهاية الخدمة يبقى غلط محاسبيًا، والمخصص المطلوب هو مخصص الإجازات بس** (للتأكيد مع المحاسب القانوني: معيار مزايا العاملين).

### 6.3 البنية التحتية العابرة: الإشعارات (قرار 3)

الموديول ده **أول مستهلك** لبنية الإشعارات العامة (الـ Overview، قسم 12.7)، ومش صاحبها: كيان `Notification` (`RecipientUserId`، و`Type`، و`TitleAr`/`TitleEn`، و`BodyAr`/`BodyEn`، و`RelatedEntityType` + `RelatedEntityId`، و`IsRead`، و`CreatedAtUtc`) + قنوات (In-App دلوقتي، وPush للـ PWA، وإيميل بعدين). **الموظف اللي مالوش `UserId` مابيوصلوش إشعار** (بيشوف في الكشك بس). **وأحداث HR**: قسيمة جديدة، واعتماد أو رفض طلب، ومستند قرب ينتهي، وطلب مستني اعتمادي.

---

## 7. الصلاحيات الخاصة بالموديول

بالإضافة للصلاحيات القياسية (الـ Overview، قسم 6.1):

### 7.1 نطاق البيانات (إضافة على نموذج الصلاحيات الحالي)

| النطاق | المعنى | الفلتر |
|---|---|---|
| `Self` | بياناتي بس | `EmployeeId == current.EmployeeId` (Claim بيتضاف وقت الدخول للشركة الحالية) |
| `Team` | أنا ومرؤوسيني (مباشرين وغير مباشرين) | شجرة `ManagerId` (مخزّنة مؤقتًا، وبتتحدث عند التغيير) |
| `Branch` / `Company` | الموجود | `UserScope` |

**كل Endpoint في الخدمة الذاتية ليه اختبار API صريح**: موظف أ بيطلب بيانات موظف ب = `404`، ومدير بيطلب موظف برّه فريقه = `404`.

### 7.2 الأدوار والصلاحيات الخاصة

| الصلاحية | ملاحظة |
|---|---|
| **دور `EMPLOYEE_SELF`** (نظامي، بيتولّد لكل شركة) | شاشات `ESS_*` بنطاق `Self` بس، ومفيش أي شاشة تشغيلية |
| **تبويب "فريقي"** | تلقائي لأي موظف ليه مرؤوسين، **ومش دور** |
| **`HR_REVEAL_PII`**: إظهار الرقم القومي أو الـ IBAN كامل | زرار بصلاحية، **وكل استخدام بيتسجّل** |
| عرض الأجر وبنوده | صلاحية حقل (`MaskFieldsAttribute` الموجود) |
| تصحيح `TimeEntry` | بسبب إلزامي |
| تعديل رصيد إجازة يدويًا | صلاحية عالية + سبب |
| إدارة الجداول القانونية | صلاحية عالية (HR + مالية) |
| عكس تشغيل رواتب مرحّل | **صلاحية استثنائية** |
| سلفة تانية مع وجود سلفة مفتوحة | صلاحية استثنائية |
| إدارة أجهزة الكشك وإعادة ضبط الـ PIN | إداري فرع |

---

## 8. التقارير

| # | التقرير | المتلقي | نوعه | ملاحظة |
|---|---|---|---|---|
| 1 | كشف الرواتب (Payroll Register) | المدير المالي | ثابت | فلتر فرع ومركز تكلفة، والتصدير بالإخفاء |
| 2 | قسيمة الراتب | الموظف | PDF | عربي وإنجليزي |
| 3 | ضريبة المرتبات (بشكل النموذج) | الضرايب | ثابت | ✔ شكل المنظومة |
| 4 | اشتراكات التأمينات الشهرية | التأمينات | ثابت | |
| 5 | أرصدة وأعمار الإجازات | HR | ثابت | + رصيد هينتهي + مخصص |
| 6 | الحضور والغياب الشهري | HR | ثابت | |
| 7 | السلف المفتوحة | المالية | ثابت | **لازم يساوي رصيد `EmployeeReceivable`** (اختبار تطابق) |
| 8 | تكلفة العمالة لكل فرع | الإدارة | ثابت | |
| 9 | **نسبة العمالة من المبيعات (Labor Cost %)** | الإدارة | ثابت | أهم مؤشر F&B بعد تكلفة الأكل |
| 10 | مخصص نهاية الخدمة والإجازات | المالية | ثابت | |
| 11 | سجل الموظفين | HR | ثابت | بالإخفاء |
| 12 | الشهادات (خبرة، ومفردات) | HR / الموظف | قالب PDF | |
| 13 | قسيمتي | الموظف | خدمة ذاتية | |
| 14 | رصيد إجازاتي | الموظف | خدمة ذاتية | |
| 15 | حضوري | الموظف | خدمة ذاتية | |
| 16 | حضور الفريق | المدير | خدمة ذاتية | نطاق Team |
| 17 | تقويم إجازات الفريق | المدير | خدمة ذاتية | نطاق Team |

**تقارير رقابة وامتثال (من غير ما تتحسب في الـ 17)**: تعارض الحضور مع الوردية · من غير تسجيل تأميني · مستندات منتهية · تحت الحد الأدنى للأجور · تدقيق إظهار الـ PII · توزيع البقشيش لكل فرع · دوران العمالة لكل فرع.

---

## ملحق أ — القرارات

### أ.1 معتمدة (مطبّقة في الملف)

| # | القرار | التطبيق |
|---|---|---|
| 1 | الربط الرسمي `Employee.UserId` (فريد جوه الشركة). `User.EmployeeId` للقراءة بس أثناء الانتقال، وبعدين يتشال | قسم 2.1، وقاعدة 1، وقسم 2.7 |
| 2 | الإضافي: الحد الأدنى القانوني 1.35× نهار، و1.70× ليل، و2× جمعة أو عطلة، و2× عيد + يوم بديل. سياسة الشركة ممكن تبقى أعلى عن طريق `OvertimeRate` المؤرّخ. ومفيش نسبة ثابتة في الكود | قسم 2.4، والقواعد 18-19 |
| 3 | الإشعارات بنية عامة (`Notification` + `RecipientUserId`)، و`EmployeeNotification` اتشال | الأقسام 2.5 و6.3 |
| 4 | `TimeEntry` هو مصدر الحقيقة الوحيد، والوردية بتلقّم وبتراجع، ومفيش تعديل على `POS.Shift`، و`Attendance` هو اللي بيدخل الرواتب | القواعد 10-14 |
| 5 | كشك الفرع: تابلت مشترك + PIN + جلسة دقيقة، ومفيش تخزين محلي، وجزء من PWA `/ess` | قسم 5.2 |
| 6 | الصرف في الإصدار الأول: سند + كشف | قاعدة 40 |

### أ.2 معلّقة (Pending Consultant / Company Policy)

| # | القرار | الحالة | الأثر لحد ما يتحسم |
|---|---|---|---|
| 2 | **سياسة نهاية الخدمة** (`None` / `Contractual`) | ⏸️ **Pending Legal**: حسب لائحة الشركة | `EndOfServicePolicy` افتراضيًا `None`، ومفيش مخصص نهاية خدمة (قاعدة 41، و6.2) |
| 4 | **أساس الشهر** (أيام فعلية / 30) | ⏸️ **Pending Company**: حسب ممارسة الشركة | `HrSettings.MonthBasis` إلزامي عند إعداد الشركة، ومفيش افتراضي صامت (قاعدة 34) |
| 9 | **مدة الاحتفاظ ببيانات الموظف بعد ترك الخدمة** (5 سنين مقترحة) | ⏸️ **Pending Legal** (الـ Overview، قسم 29) | Job الأرشفة بيتبني، وبيفضل متوقف لحد ما المدة تتحسم (قاعدة 9) |
| — | **كل الأرقام القانونية** (الشرائح، ونسب التأمينات، والحدود، وسقف الجزاءات، ومهل الإخطار والتسجيل) | ⏸️ Pending Legal | بيانات في الجداول المؤرّخة، **ومابتوقفش التطوير** |
| — | حدود مبالغ سلاسل الاعتماد (5000 المقترحة) | ⏸️ Pending Company | بيانات في `ApprovalWorkflowAssignment` |

## ملحق ب — التناقضات الموثّقة وترتيب البناء

### ب.1 التناقضات (من `12-HR-Payroll-Analysis.md` قسم 13.3)

| # | المستند | بيقول | الواقع / المعتمد |
|---|---|---|---|
| 1 | الـ Overview قسم 12 + طلب التحليل | محرك الموافقات موجود | **تصميم بس**: الكود `src/Habbak.ERP.Infrastructure/Services/NullApprovalWorkflowService.cs`، ومفيش `Screen` ولا `ApprovalWorkflow` ولا `ApprovalInstance`، و`PostingTemplate.ScreenCode` لسه نص. **السبب**: قرار "تأجيل تنفيذ الموافقات لحد ما الموديولات الأساسية تكمل" (الـ Overview قسم 29، واتدمج في النسخة المعتمدة يوم 2026-09-26 مع تحديث توقيت البناء) |
| 2 | `docs/ERP-Review/11-Recommendations-Roadmap.md` القرار 3 | المحرك الكامل بعد المرتبات (المرحلة 3) | **المحرك في المرحلة 2 من الموديول ده**: بعد HR Core وقبل الطلبات (تبعية دائرية: `DirectManager` و`JobGrade` محتاجين `Employee`) |
| 3 | `11` القرار 5 | HR 44-60 يوم | **165-230 يوم** (ب.2) |
| 4 | الـ Overview قسم 28 | HR 🟡 | **🔴** (`00-Decisions.md`) |
| 5 | طلب التحليل | دور `EmployeeAdvances` | `EmployeeReceivable = 6` **موجود ومستخدم** |
| 6 | طلب التحليل 6.5 | "مكافأة" في كل أنواع الإنهاء | مش قانونية بالضرورة، وسياسة لكل شركة (⏸️) |
| 7 | طلب التحليل 6.2.1 | الإضافي 1.5×، و2×، و2×، و3× | **قرار 2: 1.35×، و1.70×، و2×، و2× + يوم بديل** |
| 8 | طلب التحليل 6.4 | المفتاح (الشركة + السنة + الشهر) | **+ `RunType` + `ReferenceId`** (قاعدة 30) |
| 9 | طلب التحليل | `docs/ERP-Review/00-System-Overview-UPDATED.md` | موجود في `Docs/Reports-UPDATED/`، ومافيهوش ذكر لـ HR |
| 10 | 3 نسخ من `00-Project-Overview.md` | — | **المعتمدة `Docs/Modules/`**، والنسختين التانيين متعلّمين OUTDATED (2026-09-26) |
| 11 | `docs/ERP-Review/10-Compliance-Review.md` قسم 9 | التدقيق 5 سنين كحد أدنى | الـ Overview قسم 18: **7 سنين** (متسق) |

### ب.2 ترتيب البناء والجهد (مرجعي، وخطة التنفيذ بتتكتب منفصلة)

| المرحلة | المحتوى | يعتمد على | الجهد (يوم) |
|---|---|---|---|
| 0 | نطاق `Self`/`Team`، وإخفاء جزئي + إظهار مدقَّق، وتشفير + HMAC، وتسجيل PII في الكتالوج، ومفاتيح التشفير | — | 8-12 |
| 1 | HR Core + Migrations الحقول الحرة + الموظف كطرف في السندات | 0 | 24-35 |
| 2 | **محرك الموافقات v1** (بيخدم النظام كله) | 1 | 15-25 |
| 3 | الحضور والإجازات + تلقيم POS | 2 | 20-30 |
| 4 | الرواتب + الجداول المؤرّخة + البقشيش + التسوية السنوية | 3 | 32-45 |
| 5 | إنهاء الخدمة والسلف والجزاءات والمكافآت | 4 | 15-20 |
| 6 | الخدمة الذاتية (PWA + الكشك) | 0 + 2 (**بالتوازي مع 3-5**) | 25-35 |
| 7 | التقارير (مع كل مرحلة) | — | 15-20 |
| — | دعم التشغيل الموازي للرواتب (شهرين مع المحاسب الحالي قبل الصرف من النظام) | 4 | 8-10 |
| | **الإجمالي** | | **~165-230** (من غير المحرك المشترك: ~150-205) |

## ملحق ج — Alignment with 00-Project-Overview.md

| قسم الـ Overview | الموضوع | التطبيق في الملف ده |
|---|---|---|
| 6.2 | حماية البيانات الشخصية (PII) | تشفير عمود + بصمة HMAC + إخفاء جزئي + إظهار مدقَّق + استبعاد من الـ Logging (2.1، والقواعد 5-8) |
| 10 | الجداول القانونية المؤرّخة | 9 جداول في 2.4، و`RateSnapshot` على كل سطر راتب (قاعدة 29) |
| 11 | محرك الترحيل | كل القيود عن طريق `IPostingService` بقوالب، و`CostCenterId` على كل سطر (6.2، وقاعدة 51) |
| 12 | محرك الموافقات | 13 سلسلة على المحرك المركزي، ومفيش كيان اعتماد موازي، وحارس الاعتماد الذاتي (4.8) |
| 14 | Idempotency | مفتاح التشغيل والترحيل والصرف (القواعد 30-31)، والاستحقاق الشهري للإجازات (قاعدة 23) |
| 18 | سجل التدقيق (7 سنين) | تعقيم الـ PII عن طريق `FieldPermissionCatalog` (قاعدة 6)، وأرشفة بيانات الموظف منفصلة (قاعدة 9) |
| 24 | النسخ الاحتياطي والتعافي | مفاتيح التشفير جزء من النسخ الاحتياطي (قاعدة 8، وD.1، وD.2) |
| 26 | معايير التسمية | `ParentId` للهرمية، و`<Entity>Id` للمفاتيح الأجنبية، و`IdempotencyKey` نوعه `Guid`، و`DateOnly` لتواريخ المستندات، و`ILookupEntity` للمرجعي |
| 27 | القالب الموحّد | الأقسام الثمانية + الملاحق |
| 28 | خريطة الموديولات | الموديول 10 بأولوية 🔴، والإشعارات ومحرك الموافقات في 28.3 كبنية تحتية عابرة |
| 29 | القرارات المعلّقة | مدة الاحتفاظ ببيانات الموظف ⏸️ (أ.2)، وRPO/RTO (D.2)، ونقل المرفقات (D.3) |

## ملحق د — Appendix D: قرارات مفتوحة قبل خطة التنفيذ

> البنود دي لازم تتحسم **قبل** ما تتكتب خطة التنفيذ. وهي مكمّلة لبنود "Pending" في ملحق أ.2.

### D.1 — مكان حفظ مفاتيح التشفير ونسختها الاحتياطية
- **القرار المطلوب**: فين تتخزن مفاتيح التشفير اللي بتحمي حقول الـ PII (الرقم القومي، والـ IBAN، وأرقام الورثة).
- **الوضع الحالي**: مفاتيح Data Protection (الخاصة بالتحقق بخطوتين 2FA) محفوظة كملفات على القرص (`DataProtection:KeysPath`، في `src/Habbak.ERP.API/Auth/DataProtectionSecretProtector.cs`).
- **الخيارات**:

| الخيار | ملاحظة |
|---|---|
| Azure Key Vault | خدمة مُدارة، بس برّه بيئة الاستضافة المحسومة (Contabo VPS) |
| **HashiCorp Vault** | بيتنصب ذاتيًا ومناسب لـ VPS |
| SQL Server (مشفّر بمفتاح رئيسي) | المفتاح والبيانات في نفس المكان، فالحماية ضعيفة لو القاعدة اتسربت |
| ملفات على القرص + نسخة احتياطية | ده الوضع الحالي. أبسط حل، لكنه أضعفهم |

- **التوصية**: **HashiCorp Vault** مع Contabo VPS. **وشرط أساسي**: نسخة احتياطية من المفاتيح (أو من مفاتيح فك الختم Unseal Keys) **تتحفظ برّه الـ VPS**. لو الـ Vault والقاعدة الاتنين على نفس السيرفر من غير نسخة برّه، ضياع السيرفر معناه ضياع البيانات المشفّرة نهائيًا.
- **الأولوية**: 🔴 **قبل ما يتبني أي حقل PII حقيقي** (المرحلة 0 في ملحق ب.2).
- **المرجع**: `docs/ERP-Review/12-HR-Payroll-Analysis.md` قسم 5.5 + قسم 12 (خطر د) · قاعدة 8 في الملف ده.

### D.2 — أرقام RPO/RTO
- **القرار المطلوب**: تأكيد الأرقام المقترحة.
- **المقترح**: **RPO من 15 لـ 30 دقيقة**، و**RTO 4 ساعات** في ساعات العمل.
- **التوصية**: الأرقام دي **مقبولة للإصدار الأول**، وتتراجع **بعد 3 شهور تشغيل** على بيانات فعلية. واختبار الاستعادة الشهري لازم يشمل مفاتيح التشفير (D.1).
- **المرجع**: `Docs/Modules/00-Project-Overview.md` قسم 24 وقسم 29.

### D.3 — نقل المرفقات (من SQL Server للـ VPS)
- **القرار المطلوب**: إمتى يتنقلوا.
- **التوصية**: **مؤجّل**. مش حرج لـ HR، لأن مستندات الموظفين بتستخدم نفس آلية المرفقات الحالية زي ما هي.
- **الشرط**: يتنفّذ **قبل ما حجم المرفقات يكبر** (عدد الفروع والموظفين بيزوّد مستندات HR بشكل ملحوظ)، عشان الـ Migration يفضل صغير.
- **المرجع**: `Docs/Modules/00-Project-Overview.md` قسم 8.5.

### D.4 — ملخص البنود المفتوحة

| # | البند | المصدر | النوع |
|---|---|---|---|
| 1 | مكان مفاتيح التشفير | D.1 | تقني، 🔴 قبل الـ PII |
| 2 | أرقام RPO/RTO | D.2 | تشغيلي |
| 3 | توقيت نقل المرفقات | D.3 | تقني، مؤجّل |
| 4 | سياسة نهاية الخدمة | أ.2 | ⏸️ Pending Legal |
| 5 | أساس الشهر | أ.2 | ⏸️ Pending Company |
| 6 | مدة الاحتفاظ ببيانات الموظف | أ.2 | ⏸️ Pending Legal |
| 7 | الأرقام القانونية للجداول المؤرّخة | أ.2 | ⏸️ Pending Legal (بيانات، ومابتوقفش التطوير) |
| 8 | حدود مبالغ سلاسل الاعتماد | أ.2 | ⏸️ Pending Company (بيانات) |

---

## ملحق هـ — Implementation Status: Phase 1.1 (2026-09-27)

> المرحلة 1.1 (الكيانات الأساسية) و1.1b (إظهار PII) اتنفّذوا بالكامل عبر 6 Batches (B1-B6)، موثّقين بالتفصيل في `Docs/Implementation/HR-Core-Plan.md` (قسم "Phase 1.1 — Final Status") و`docs/Implementation/Phase-1.1-Final-Report.md`. القسم ده بيربط كل جزء من المواصفة (§2-§8 فوق) بحالته الفعلية.

| قسم المواصفة | الحالة | ملاحظة |
|---|---|---|
| §2.1 (الكيانات والجداول) | ✅ متطابق مع الكود | كل الحقول المذكورة اتبنت زي ما هي — الاستثناء الوحيد: `EmploymentContract`/`EmployeeDocument`/`EmployeeCertification` عندهم `BranchId` خاص بيهم (مش موروث)، بس بيتاخد تلقائيًا من `Employee.BranchId` وقت الإنشاء (تفصيل في Batch B6) |
| §3 (قواعد العمل) — قاعدة 1 (User↔Employee فريد) | ✅ منفّذة | فهرس فريد مفلتر `(CompanyId, UserId)` + `AssignUserToEmployeeCommand`/`HR-USER-ALREADY-LINKED` |
| §3 — قاعدة 3 (منع حلقة في التسلسل الإداري) | ✅ منفّذة | `OrgUnitHierarchy.WouldCreateCycle` — نفس المنطق لـ`Employee.ManagerId` و`OrgUnit.ParentId` |
| §3 — قاعدة 2 (شروط التفعيل) | ✅ منفّذة جزئيًا (بالتصميم) | `ActivateEmployeeCommand`: عقد ساري + مستندات إلزامية. **من غير** هيكل راتب (مؤجل لمرحلة الرواتب، موثّق كقرار من الأول) |
| §3 — قاعدة 44 (إنهاء الخدمة) | ✅ منفّذة بالكامل | `TerminateEmployeeCommand` (B5، وُسّعت في Phase 1.4): إيقاف User + سحب الجلسات النشطة (`RevokeSessionsAsync`) + إلغاء Scopes + منع لو عهدة نقدية (`CustodyRegister`) **أو عهدة أصول** (`CustodyOfficer`→`FixedAsset.CustodyOfficerId`، قاعدة 43) مفتوحة |
| §5.5 / إظهار PII | ✅ منفّذة (1.1b) | `HrPiiController.Reveal` + `HR_REVEAL_PII` + `AuditLog` إلزامي بدون تخزين القيمة نفسها |
| §5 (الشاشات) | ⏳ Backend بس | كل الشاشات المذكورة عندها API كامل (Controllers + Screen Permissions) — **الـ Frontend لسه معمول** (مرحلة 1.5، خارج B1-B7) |
| ملحق د.1 (مكان مفاتيح التشفير) | ✅ اتحسم ونُفّذ | HashiCorp Vault (مش الخيار الاحتياطي) — Vault-Setup.md كامل، Spike نجح من أول مرة (Phase 0.6) |

**خلاصة**: Backend الخاص بالمرحلة 1.1/1.1b **جاهز بالكامل وتحت اختبار Regression مستمر** (171 اختبار Integration + 260 اختبار API وقت هذا التحديث، كلهم بيعدّوا). الفجوة الوحيدة المفتوحة موثّقة صراحة في `Known-Issues.md` ومش بتمنع الانتقال للمرحلة 1.2.
