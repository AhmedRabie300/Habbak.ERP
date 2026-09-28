using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Payroll;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Payroll;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — every Payroll controller in one
/// file, same convention as AttendanceControllers.cs/HrEmploymentControllers.cs. Screens registered
/// in ScreenCodeCatalog.cs (coded: PAY_SALARY_COMPONENTS/PAY_SALARY_STRUCTURES/PAY_PAYROLL_RUNS) or
/// ScreenSeedData.cs ExtraScreens (the rest), and MenuItemSeedData.cs.
/// </summary>
[ApiController]
[Authorize]
[Screen("PAY_SALARY_COMPONENTS", LookupReads = true)]
[Route("api/v1/payroll/salary-components")]
public class SalaryComponentsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(
        string? Code, string NameAr, string NameEn, SalaryComponentType ComponentType, CalculationMethod CalculationMethod,
        bool IsTaxable, bool IsInsurable, bool IsRecurring, SalaryComponentSource SourceType, CompanyAccountRole? AccountRole);

    public sealed record UpdateRequest(
        string NameAr, string NameEn, bool IsActive, SalaryComponentType ComponentType, CalculationMethod CalculationMethod,
        bool IsTaxable, bool IsInsurable, bool IsRecurring, SalaryComponentSource SourceType, CompanyAccountRole? AccountRole);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetSalaryComponentsListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalaryComponentCommand(
            request.Code, request.NameAr, request.NameEn, request.ComponentType, request.CalculationMethod,
            request.IsTaxable, request.IsInsurable, request.IsRecurring, request.SourceType, request.AccountRole), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalaryComponentCommand(
            id, request.NameAr, request.NameEn, request.IsActive, request.ComponentType, request.CalculationMethod,
            request.IsTaxable, request.IsInsurable, request.IsRecurring, request.SourceType, request.AccountRole), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("PAY_SALARY_STRUCTURES", LookupReads = true)]
[Route("api/v1/payroll/salary-structures")]
public class SalaryStructuresController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(string? Code, string NameAr, string NameEn, IReadOnlyList<SalaryStructureLineInput> Lines);
    public sealed record UpdateRequest(string NameAr, string NameEn, bool IsActive, IReadOnlyList<SalaryStructureLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetSalaryStructuresListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalaryStructureCommand(request.Code, request.NameAr, request.NameEn, request.Lines), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalaryStructureCommand(id, request.NameAr, request.NameEn, request.IsActive, request.Lines), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_SALARY_CHANGES")]
[Route("api/v1/payroll/employee-salaries")]
public class EmployeeSalaryController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long EmployeeId, long SalaryComponentId, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    public sealed record UpdateRequest(decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long employeeId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeSalariesQuery(employeeId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeSalaryCommand(request.EmployeeId, request.SalaryComponentId, request.Amount, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeSalaryCommand(id, request.Amount, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeSalaryCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("PAY_PERIODS")]
[Route("api/v1/payroll/periods")]
public class PayrollPeriodsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(int Year, int Month, DateOnly StartDate, DateOnly EndDate, DateOnly CutoffDate);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetPayrollPeriodsQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePayrollPeriodCommand(request.Year, request.Month, request.StartDate, request.EndDate, request.CutoffDate), cancellationToken);
        return Ok(new { id });
    }
}

[ApiController]
[Authorize]
[Screen("PAY_PAYROLL_RUNS")]
[Route("api/v1/payroll/runs")]
public class PayrollRunsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long PayrollPeriodId, PayrollRunType RunType, long? ReferenceId);
    public sealed record ReverseRequest(string Reason);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetPayrollRunsQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) => Ok(await mediator.Send(new GetPayrollRunByIdQuery(id), cancellationToken));

    /// <summary>Idempotent (rule 30) — returns the existing run for the period/type/reference if one
    /// already exists, never a duplicate.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CreatePayrollRunCommand(request.PayrollPeriodId, request.RunType, request.ReferenceId), cancellationToken));

    [HttpPost("{id:long}/calculate")]
    public async Task<IActionResult> Calculate(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CalculatePayrollRunCommand(id), cancellationToken));

    /// <summary>Calculated → PendingApproval (or Approved directly, rule 4). Approval itself happens
    /// on the generic "بانتظار اعتمادي" screen (ApprovalActionCommands), not here — no dedicated
    /// Approve endpoint on this controller (module doc §5: "مفيش شاشة اعتماد خاصة بـ HR").</summary>
    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitPayrollRunForApprovalCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostPayrollRunCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/pay")]
    public async Task<IActionResult> Pay(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PayPayrollRunCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, [FromBody] ReverseRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReversePayrollRunCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("PAY_PAYSLIPS")]
[Route("api/v1/payroll/payslips")]
public class PayslipsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? payrollRunId, [FromQuery] long? employeeId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPayslipsQuery(payrollRunId, employeeId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) => Ok(await mediator.Send(new GetPayslipByIdQuery(id), cancellationToken));
}

[ApiController]
[Authorize]
[Screen("PAY_TIPS_DISTRIBUTION")]
[Route("api/v1/payroll/tips-distribution")]
public class TipsDistributionController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long? BranchId, long PayrollPeriodId, decimal TotalAmount, TipsDistributionMethod Method, IReadOnlyDictionary<long, decimal> Weights);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetTipsDistributionsQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) => Ok(await mediator.Send(new GetTipsDistributionByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateTipsDistributionCommand(request.BranchId, request.PayrollPeriodId, request.TotalAmount, request.Method, request.Weights), cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitTipsDistributionCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>Screen "تبويب لكل جدول" — one GET for the whole screen (LegalTablesDto, all twelve
/// tables), and a Create/Update pair per table (no shared "table name" route param: each table's
/// fields differ, so a generic endpoint would just push the type-per-table problem into the request
/// body instead of the route).</summary>
[ApiController]
[Authorize]
[Screen("PAY_LEGAL_TABLES")]
[Route("api/v1/payroll/legal-tables")]
public class LegalTablesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetLegalTablesQuery(), cancellationToken));

    public sealed record MinimumWageRequest(decimal Amount, string? Sector, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("minimum-wages")]
    public async Task<IActionResult> CreateMinimumWage([FromBody] MinimumWageRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateMinimumWageCommand(request.Amount, request.Sector, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("minimum-wages/{id:long}")]
    public async Task<IActionResult> UpdateMinimumWage(long id, [FromBody] MinimumWageRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateMinimumWageCommand(id, request.Amount, request.Sector, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record SocialInsuranceRateRequest(decimal EmployeeRate, decimal EmployerRate, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("social-insurance-rates")]
    public async Task<IActionResult> CreateSocialInsuranceRate([FromBody] SocialInsuranceRateRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateSocialInsuranceRateCommand(request.EmployeeRate, request.EmployerRate, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("social-insurance-rates/{id:long}")]
    public async Task<IActionResult> UpdateSocialInsuranceRate(long id, [FromBody] SocialInsuranceRateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSocialInsuranceRateCommand(id, request.EmployeeRate, request.EmployerRate, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record InsurableWageLimitRequest(decimal MinWage, decimal MaxWage, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("insurable-wage-limits")]
    public async Task<IActionResult> CreateInsurableWageLimit([FromBody] InsurableWageLimitRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateInsurableWageLimitCommand(request.MinWage, request.MaxWage, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("insurable-wage-limits/{id:long}")]
    public async Task<IActionResult> UpdateInsurableWageLimit(long id, [FromBody] InsurableWageLimitRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateInsurableWageLimitCommand(id, request.MinWage, request.MaxWage, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record TaxBracketSetRequest(decimal PersonalExemption, string? DisqualificationRulesDescription, DateOnly EffectiveFrom, DateOnly? EffectiveTo, IReadOnlyList<TaxBracketInput> Brackets);

    [HttpPost("tax-bracket-sets")]
    public async Task<IActionResult> CreateTaxBracketSet([FromBody] TaxBracketSetRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreatePayrollTaxBracketSetCommand(request.PersonalExemption, request.DisqualificationRulesDescription, request.EffectiveFrom, request.EffectiveTo, request.Brackets), cancellationToken) });

    [HttpPut("tax-bracket-sets/{id:long}")]
    public async Task<IActionResult> UpdateTaxBracketSet(long id, [FromBody] TaxBracketSetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePayrollTaxBracketSetCommand(id, request.PersonalExemption, request.DisqualificationRulesDescription, request.EffectiveFrom, request.EffectiveTo, request.Brackets), cancellationToken);
        return NoContent();
    }

    public sealed record MartyrsFundRateRequest(decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("martyrs-fund-rates")]
    public async Task<IActionResult> CreateMartyrsFundRate([FromBody] MartyrsFundRateRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateMartyrsFundRateCommand(request.Rate, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("martyrs-fund-rates/{id:long}")]
    public async Task<IActionResult> UpdateMartyrsFundRate(long id, [FromBody] MartyrsFundRateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateMartyrsFundRateCommand(id, request.Rate, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record OvertimeRateCreateRequest(OvertimeType OvertimeType, decimal Multiplier, bool IsLegalMinimum, bool GrantsSubstituteDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    public sealed record OvertimeRateUpdateRequest(decimal Multiplier, bool IsLegalMinimum, bool GrantsSubstituteDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("overtime-rates")]
    public async Task<IActionResult> CreateOvertimeRate([FromBody] OvertimeRateCreateRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateOvertimeRateCommand(request.OvertimeType, request.Multiplier, request.IsLegalMinimum, request.GrantsSubstituteDay, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("overtime-rates/{id:long}")]
    public async Task<IActionResult> UpdateOvertimeRate(long id, [FromBody] OvertimeRateUpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateOvertimeRateCommand(id, request.Multiplier, request.IsLegalMinimum, request.GrantsSubstituteDay, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record LeaveEntitlementRuleCreateRequest(long LeaveTypeId, int? MinServiceYears, int? MinAge, decimal Days, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    public sealed record LeaveEntitlementRuleUpdateRequest(decimal Days, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("leave-entitlement-rules")]
    public async Task<IActionResult> CreateLeaveEntitlementRule([FromBody] LeaveEntitlementRuleCreateRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateLeaveEntitlementRuleCommand(request.LeaveTypeId, request.MinServiceYears, request.MinAge, request.Days, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("leave-entitlement-rules/{id:long}")]
    public async Task<IActionResult> UpdateLeaveEntitlementRule(long id, [FromBody] LeaveEntitlementRuleUpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateLeaveEntitlementRuleCommand(id, request.Days, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record PenaltyDeductionCapRequest(decimal MaxDaysPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("penalty-deduction-caps")]
    public async Task<IActionResult> CreatePenaltyDeductionCap([FromBody] PenaltyDeductionCapRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreatePenaltyDeductionCapCommand(request.MaxDaysPerMonth, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("penalty-deduction-caps/{id:long}")]
    public async Task<IActionResult> UpdatePenaltyDeductionCap(long id, [FromBody] PenaltyDeductionCapRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePenaltyDeductionCapCommand(id, request.MaxDaysPerMonth, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record NoticePeriodRuleCreateRequest(int MinServiceYears, int NoticeDays, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    public sealed record NoticePeriodRuleUpdateRequest(int NoticeDays, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("notice-period-rules")]
    public async Task<IActionResult> CreateNoticePeriodRule([FromBody] NoticePeriodRuleCreateRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateNoticePeriodRuleCommand(request.MinServiceYears, request.NoticeDays, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("notice-period-rules/{id:long}")]
    public async Task<IActionResult> UpdateNoticePeriodRule(long id, [FromBody] NoticePeriodRuleUpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateNoticePeriodRuleCommand(id, request.NoticeDays, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record EndOfServicePolicyRequest(EndOfServicePolicyType PolicyType, string? FormulaDescription, int? MinServiceYears, decimal? DaysPerYear, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("end-of-service-policies")]
    public async Task<IActionResult> CreateEndOfServicePolicy([FromBody] EndOfServicePolicyRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateEndOfServicePolicyCommand(request.PolicyType, request.FormulaDescription, request.MinServiceYears, request.DaysPerYear, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("end-of-service-policies/{id:long}")]
    public async Task<IActionResult> UpdateEndOfServicePolicy(long id, [FromBody] EndOfServicePolicyRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEndOfServicePolicyCommand(id, request.PolicyType, request.FormulaDescription, request.MinServiceYears, request.DaysPerYear, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }

    public sealed record OvertimeLimitRuleRequest(int? MaxMinutesPerDay, int? MaxMinutesPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    [HttpPost("overtime-limit-rules")]
    public async Task<IActionResult> CreateOvertimeLimitRule([FromBody] OvertimeLimitRuleRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateOvertimeLimitRuleCommand(request.MaxMinutesPerDay, request.MaxMinutesPerMonth, request.EffectiveFrom, request.EffectiveTo), cancellationToken) });

    [HttpPut("overtime-limit-rules/{id:long}")]
    public async Task<IActionResult> UpdateOvertimeLimitRule(long id, [FromBody] OvertimeLimitRuleRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateOvertimeLimitRuleCommand(id, request.MaxMinutesPerDay, request.MaxMinutesPerMonth, request.EffectiveFrom, request.EffectiveTo), cancellationToken);
        return NoContent();
    }
}
