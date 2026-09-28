using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — one combined query for the whole
// PAY_LEGAL_TABLES screen ("تبويب لكل جدول" — one page load, tabs switch client-side), instead of
// twelve separate list endpoints for what's ultimately one settings screen.

public sealed record MinimumWageDto(long Id, decimal Amount, string? Sector, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record SocialInsuranceRateDto(long Id, decimal EmployeeRate, decimal EmployerRate, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record InsurableWageLimitDto(long Id, decimal MinWage, decimal MaxWage, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record TaxBracketDto(long Id, decimal FromAmount, decimal? ToAmount, decimal Rate, int Order);
public sealed record PayrollTaxBracketSetDto(
    long Id, decimal PersonalExemption, string? DisqualificationRulesDescription, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<TaxBracketDto> Brackets);
public sealed record MartyrsFundRateDto(long Id, decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record OvertimeRateDto(long Id, OvertimeType OvertimeType, decimal Multiplier, bool IsLegalMinimum, bool GrantsSubstituteDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record LeaveEntitlementRuleDto(long Id, long LeaveTypeId, int? MinServiceYears, int? MinAge, decimal Days, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record PenaltyDeductionCapDto(long Id, decimal MaxDaysPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record NoticePeriodRuleDto(long Id, int MinServiceYears, int NoticeDays, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record EndOfServicePolicyDto(
    long Id, EndOfServicePolicyType PolicyType, string? FormulaDescription, int? MinServiceYears, decimal? DaysPerYear,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record OvertimeLimitRuleDto(long Id, int? MaxMinutesPerDay, int? MaxMinutesPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record LegalTablesDto(
    IReadOnlyList<MinimumWageDto> MinimumWages,
    IReadOnlyList<SocialInsuranceRateDto> SocialInsuranceRates,
    IReadOnlyList<InsurableWageLimitDto> InsurableWageLimits,
    IReadOnlyList<PayrollTaxBracketSetDto> PayrollTaxBracketSets,
    IReadOnlyList<MartyrsFundRateDto> MartyrsFundRates,
    IReadOnlyList<OvertimeRateDto> OvertimeRates,
    IReadOnlyList<LeaveEntitlementRuleDto> LeaveEntitlementRules,
    IReadOnlyList<PenaltyDeductionCapDto> PenaltyDeductionCaps,
    IReadOnlyList<NoticePeriodRuleDto> NoticePeriodRules,
    IReadOnlyList<EndOfServicePolicyDto> EndOfServicePolicies,
    IReadOnlyList<OvertimeLimitRuleDto> OvertimeLimitRules);

public sealed record GetLegalTablesQuery : IRequest<LegalTablesDto>;

public sealed class GetLegalTablesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLegalTablesQuery, LegalTablesDto>
{
    public async Task<LegalTablesDto> Handle(GetLegalTablesQuery request, CancellationToken cancellationToken) =>
        new(
            await db.MinimumWages.AsNoTracking().OrderByDescending(m => m.EffectiveFrom)
                .Select(m => new MinimumWageDto(m.Id, m.Amount, m.Sector, m.EffectiveFrom, m.EffectiveTo)).ToListAsync(cancellationToken),
            await db.SocialInsuranceRates.AsNoTracking().OrderByDescending(r => r.EffectiveFrom)
                .Select(r => new SocialInsuranceRateDto(r.Id, r.EmployeeRate, r.EmployerRate, r.EffectiveFrom, r.EffectiveTo)).ToListAsync(cancellationToken),
            await db.InsurableWageLimits.AsNoTracking().OrderByDescending(l => l.EffectiveFrom)
                .Select(l => new InsurableWageLimitDto(l.Id, l.MinWage, l.MaxWage, l.EffectiveFrom, l.EffectiveTo)).ToListAsync(cancellationToken),
            await db.PayrollTaxBracketSets.AsNoTracking().Include(s => s.Brackets).OrderByDescending(s => s.EffectiveFrom)
                .Select(s => new PayrollTaxBracketSetDto(
                    s.Id, s.PersonalExemption, s.DisqualificationRulesDescription, s.EffectiveFrom, s.EffectiveTo,
                    s.Brackets.OrderBy(b => b.Order).Select(b => new TaxBracketDto(b.Id, b.FromAmount, b.ToAmount, b.Rate, b.Order)).ToList()))
                .ToListAsync(cancellationToken),
            await db.MartyrsFundRates.AsNoTracking().OrderByDescending(r => r.EffectiveFrom)
                .Select(r => new MartyrsFundRateDto(r.Id, r.Rate, r.EffectiveFrom, r.EffectiveTo)).ToListAsync(cancellationToken),
            await db.OvertimeRates.AsNoTracking().OrderBy(r => r.OvertimeType).ThenByDescending(r => r.EffectiveFrom)
                .Select(r => new OvertimeRateDto(r.Id, r.OvertimeType, r.Multiplier, r.IsLegalMinimum, r.GrantsSubstituteDay, r.EffectiveFrom, r.EffectiveTo))
                .ToListAsync(cancellationToken),
            await db.LeaveEntitlementRules.AsNoTracking().OrderBy(r => r.LeaveTypeId).ThenByDescending(r => r.EffectiveFrom)
                .Select(r => new LeaveEntitlementRuleDto(r.Id, r.LeaveTypeId, r.MinServiceYears, r.MinAge, r.Days, r.EffectiveFrom, r.EffectiveTo))
                .ToListAsync(cancellationToken),
            await db.PenaltyDeductionCaps.AsNoTracking().OrderByDescending(c => c.EffectiveFrom)
                .Select(c => new PenaltyDeductionCapDto(c.Id, c.MaxDaysPerMonth, c.EffectiveFrom, c.EffectiveTo)).ToListAsync(cancellationToken),
            await db.NoticePeriodRules.AsNoTracking().OrderBy(r => r.MinServiceYears).ThenByDescending(r => r.EffectiveFrom)
                .Select(r => new NoticePeriodRuleDto(r.Id, r.MinServiceYears, r.NoticeDays, r.EffectiveFrom, r.EffectiveTo))
                .ToListAsync(cancellationToken),
            await db.EndOfServicePolicies.AsNoTracking().OrderByDescending(p => p.EffectiveFrom)
                .Select(p => new EndOfServicePolicyDto(p.Id, p.PolicyType, p.FormulaDescription, p.MinServiceYears, p.DaysPerYear, p.EffectiveFrom, p.EffectiveTo))
                .ToListAsync(cancellationToken),
            await db.OvertimeLimitRules.AsNoTracking().OrderByDescending(r => r.EffectiveFrom)
                .Select(r => new OvertimeLimitRuleDto(r.Id, r.MaxMinutesPerDay, r.MaxMinutesPerMonth, r.EffectiveFrom, r.EffectiveTo)).ToListAsync(cancellationToken));
}
