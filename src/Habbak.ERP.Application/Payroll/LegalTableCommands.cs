using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — Create/Update for the twelve legal
// tables (screen PAY_LEGAL_TABLES, "تبويب لكل جدول"). No Delete: a legal table row is a historical
// fact ("the rate was X from date A to date B"), not a record to remove — closing a row's applicable
// period is done by setting EffectiveTo via Update, same convention as every other dated row in the
// project. Each Create/Update narrows to the row's own "key" (Phase-4-Research.md §1.4's own framing)
// before calling LegalTableRules.EnsureNoOverlap; most tables have no extra key beyond CompanyId.

// ------------------------------------------------------------------ MinimumWage

public sealed record CreateMinimumWageCommand(decimal Amount, string? Sector, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateMinimumWageCommandValidator : AbstractValidator<CreateMinimumWageCommand>
{
    public CreateMinimumWageCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Sector).MaximumLength(100);
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo is not null);
    }
}

public sealed class CreateMinimumWageCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<CreateMinimumWageCommand, long>
{
    public async Task<long> Handle(CreateMinimumWageCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.MinimumWages.Where(m => m.CompanyId == current.CompanyId && m.Sector == request.Sector).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new MinimumWage
        {
            CompanyId = current.CompanyId, Amount = request.Amount, Sector = request.Sector,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.MinimumWages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateMinimumWageCommand(long Id, decimal Amount, string? Sector, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateMinimumWageCommandValidator : AbstractValidator<UpdateMinimumWageCommand>
{
    public UpdateMinimumWageCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo is not null);
    }
}

public sealed class UpdateMinimumWageCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<UpdateMinimumWageCommand>
{
    public async Task Handle(UpdateMinimumWageCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.MinimumWages.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MinimumWage), request.Id);

        var sameKey = await db.MinimumWages.Where(m => m.CompanyId == current.CompanyId && m.Sector == request.Sector).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.Amount = request.Amount;
        entity.Sector = request.Sector;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ SocialInsuranceRate

public sealed record CreateSocialInsuranceRateCommand(decimal EmployeeRate, decimal EmployerRate, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateSocialInsuranceRateCommandValidator : AbstractValidator<CreateSocialInsuranceRateCommand>
{
    public CreateSocialInsuranceRateCommandValidator()
    {
        RuleFor(x => x.EmployeeRate).InclusiveBetween(0, 1);
        RuleFor(x => x.EmployerRate).InclusiveBetween(0, 1);
    }
}

public sealed class CreateSocialInsuranceRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateSocialInsuranceRateCommand, long>
{
    public async Task<long> Handle(CreateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.SocialInsuranceRates.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new SocialInsuranceRate
        {
            CompanyId = current.CompanyId, EmployeeRate = request.EmployeeRate, EmployerRate = request.EmployerRate,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.SocialInsuranceRates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateSocialInsuranceRateCommand(long Id, decimal EmployeeRate, decimal EmployerRate, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateSocialInsuranceRateCommandValidator : AbstractValidator<UpdateSocialInsuranceRateCommand>
{
    public UpdateSocialInsuranceRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EmployeeRate).InclusiveBetween(0, 1);
        RuleFor(x => x.EmployerRate).InclusiveBetween(0, 1);
    }
}

public sealed class UpdateSocialInsuranceRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateSocialInsuranceRateCommand>
{
    public async Task Handle(UpdateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.SocialInsuranceRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SocialInsuranceRate), request.Id);

        var sameKey = await db.SocialInsuranceRates.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.EmployeeRate = request.EmployeeRate;
        entity.EmployerRate = request.EmployerRate;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ InsurableWageLimit

public sealed record CreateInsurableWageLimitCommand(decimal MinWage, decimal MaxWage, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateInsurableWageLimitCommandValidator : AbstractValidator<CreateInsurableWageLimitCommand>
{
    public CreateInsurableWageLimitCommandValidator()
    {
        RuleFor(x => x.MinWage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxWage).GreaterThan(x => x.MinWage);
    }
}

public sealed class CreateInsurableWageLimitCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateInsurableWageLimitCommand, long>
{
    public async Task<long> Handle(CreateInsurableWageLimitCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.InsurableWageLimits.Where(l => l.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new InsurableWageLimit
        {
            CompanyId = current.CompanyId, MinWage = request.MinWage, MaxWage = request.MaxWage,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.InsurableWageLimits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateInsurableWageLimitCommand(long Id, decimal MinWage, decimal MaxWage, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateInsurableWageLimitCommandValidator : AbstractValidator<UpdateInsurableWageLimitCommand>
{
    public UpdateInsurableWageLimitCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MinWage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxWage).GreaterThan(x => x.MinWage);
    }
}

public sealed class UpdateInsurableWageLimitCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateInsurableWageLimitCommand>
{
    public async Task Handle(UpdateInsurableWageLimitCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.InsurableWageLimits.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InsurableWageLimit), request.Id);

        var sameKey = await db.InsurableWageLimits.Where(l => l.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.MinWage = request.MinWage;
        entity.MaxWage = request.MaxWage;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ PayrollTaxBracketSet (+ Brackets, Replace-All)

public sealed record TaxBracketInput(decimal FromAmount, decimal? ToAmount, decimal Rate, int Order);

public sealed record CreatePayrollTaxBracketSetCommand(
    decimal PersonalExemption, string? DisqualificationRulesDescription, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<TaxBracketInput> Brackets) : IRequest<long>;

public sealed class CreatePayrollTaxBracketSetCommandValidator : AbstractValidator<CreatePayrollTaxBracketSetCommand>
{
    public CreatePayrollTaxBracketSetCommandValidator()
    {
        RuleFor(x => x.PersonalExemption).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Brackets).NotEmpty();
        RuleForEach(x => x.Brackets).ChildRules(b =>
        {
            b.RuleFor(x => x.FromAmount).GreaterThanOrEqualTo(0);
            b.RuleFor(x => x.Rate).InclusiveBetween(0, 1);
        });
    }
}

public sealed class CreatePayrollTaxBracketSetCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreatePayrollTaxBracketSetCommand, long>
{
    public async Task<long> Handle(CreatePayrollTaxBracketSetCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.PayrollTaxBracketSets.Where(s => s.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new PayrollTaxBracketSet
        {
            CompanyId = current.CompanyId, PersonalExemption = request.PersonalExemption,
            DisqualificationRulesDescription = request.DisqualificationRulesDescription,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            Brackets = request.Brackets.Select(b => new PayrollTaxBracket { FromAmount = b.FromAmount, ToAmount = b.ToAmount, Rate = b.Rate, Order = b.Order }).ToList()
        };
        db.PayrollTaxBracketSets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdatePayrollTaxBracketSetCommand(
    long Id, decimal PersonalExemption, string? DisqualificationRulesDescription, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<TaxBracketInput> Brackets) : IRequest;

public sealed class UpdatePayrollTaxBracketSetCommandValidator : AbstractValidator<UpdatePayrollTaxBracketSetCommand>
{
    public UpdatePayrollTaxBracketSetCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.PersonalExemption).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Brackets).NotEmpty();
    }
}

public sealed class UpdatePayrollTaxBracketSetCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdatePayrollTaxBracketSetCommand>
{
    public async Task Handle(UpdatePayrollTaxBracketSetCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.PayrollTaxBracketSets.Include(s => s.Brackets).FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollTaxBracketSet), request.Id);

        var sameKey = await db.PayrollTaxBracketSets.Where(s => s.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.PersonalExemption = request.PersonalExemption;
        entity.DisqualificationRulesDescription = request.DisqualificationRulesDescription;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;

        entity.Brackets.Clear();
        foreach (var bracket in request.Brackets)
        {
            entity.Brackets.Add(new PayrollTaxBracket { FromAmount = bracket.FromAmount, ToAmount = bracket.ToAmount, Rate = bracket.Rate, Order = bracket.Order });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ MartyrsFundRate

public sealed record CreateMartyrsFundRateCommand(decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateMartyrsFundRateCommandValidator : AbstractValidator<CreateMartyrsFundRateCommand>
{
    public CreateMartyrsFundRateCommandValidator() => RuleFor(x => x.Rate).InclusiveBetween(0, 1);
}

public sealed class CreateMartyrsFundRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<CreateMartyrsFundRateCommand, long>
{
    public async Task<long> Handle(CreateMartyrsFundRateCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.MartyrsFundRates.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new MartyrsFundRate { CompanyId = current.CompanyId, Rate = request.Rate, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
        db.MartyrsFundRates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateMartyrsFundRateCommand(long Id, decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateMartyrsFundRateCommandValidator : AbstractValidator<UpdateMartyrsFundRateCommand>
{
    public UpdateMartyrsFundRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Rate).InclusiveBetween(0, 1);
    }
}

public sealed class UpdateMartyrsFundRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<UpdateMartyrsFundRateCommand>
{
    public async Task Handle(UpdateMartyrsFundRateCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.MartyrsFundRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MartyrsFundRate), request.Id);

        var sameKey = await db.MartyrsFundRates.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.Rate = request.Rate;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ OvertimeRate

public sealed record CreateOvertimeRateCommand(
    Habbak.ERP.Domain.Attendance.OvertimeType OvertimeType, decimal Multiplier, bool IsLegalMinimum, bool GrantsSubstituteDay,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateOvertimeRateCommandValidator : AbstractValidator<CreateOvertimeRateCommand>
{
    public CreateOvertimeRateCommandValidator() => RuleFor(x => x.Multiplier).GreaterThanOrEqualTo(1);
}

public sealed class CreateOvertimeRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<CreateOvertimeRateCommand, long>
{
    public async Task<long> Handle(CreateOvertimeRateCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.OvertimeRates
            .Where(r => r.CompanyId == current.CompanyId && r.OvertimeType == request.OvertimeType).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new OvertimeRate
        {
            CompanyId = current.CompanyId, OvertimeType = request.OvertimeType, Multiplier = request.Multiplier,
            IsLegalMinimum = request.IsLegalMinimum, GrantsSubstituteDay = request.GrantsSubstituteDay,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.OvertimeRates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateOvertimeRateCommand(
    long Id, decimal Multiplier, bool IsLegalMinimum, bool GrantsSubstituteDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateOvertimeRateCommandValidator : AbstractValidator<UpdateOvertimeRateCommand>
{
    public UpdateOvertimeRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Multiplier).GreaterThanOrEqualTo(1);
    }
}

public sealed class UpdateOvertimeRateCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<UpdateOvertimeRateCommand>
{
    public async Task Handle(UpdateOvertimeRateCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.OvertimeRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRate), request.Id);

        var sameKey = await db.OvertimeRates
            .Where(r => r.CompanyId == current.CompanyId && r.OvertimeType == entity.OvertimeType).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.Multiplier = request.Multiplier;
        entity.IsLegalMinimum = request.IsLegalMinimum;
        entity.GrantsSubstituteDay = request.GrantsSubstituteDay;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ LeaveEntitlementRule

public sealed record CreateLeaveEntitlementRuleCommand(
    long LeaveTypeId, int? MinServiceYears, int? MinAge, decimal Days, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateLeaveEntitlementRuleCommandValidator : AbstractValidator<CreateLeaveEntitlementRuleCommand>
{
    public CreateLeaveEntitlementRuleCommandValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.Days).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateLeaveEntitlementRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateLeaveEntitlementRuleCommand, long>
{
    public async Task<long> Handle(CreateLeaveEntitlementRuleCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.LeaveEntitlementRules
            .Where(r => r.CompanyId == current.CompanyId && r.LeaveTypeId == request.LeaveTypeId
                        && r.MinServiceYears == request.MinServiceYears && r.MinAge == request.MinAge)
            .ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new LeaveEntitlementRule
        {
            CompanyId = current.CompanyId, LeaveTypeId = request.LeaveTypeId, MinServiceYears = request.MinServiceYears,
            MinAge = request.MinAge, Days = request.Days, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.LeaveEntitlementRules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateLeaveEntitlementRuleCommand(long Id, decimal Days, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateLeaveEntitlementRuleCommandValidator : AbstractValidator<UpdateLeaveEntitlementRuleCommand>
{
    public UpdateLeaveEntitlementRuleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Days).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateLeaveEntitlementRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateLeaveEntitlementRuleCommand>
{
    public async Task Handle(UpdateLeaveEntitlementRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.LeaveEntitlementRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveEntitlementRule), request.Id);

        var sameKey = await db.LeaveEntitlementRules
            .Where(r => r.CompanyId == current.CompanyId && r.LeaveTypeId == entity.LeaveTypeId
                        && r.MinServiceYears == entity.MinServiceYears && r.MinAge == entity.MinAge)
            .ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.Days = request.Days;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ PenaltyDeductionCap

public sealed record CreatePenaltyDeductionCapCommand(decimal MaxDaysPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreatePenaltyDeductionCapCommandValidator : AbstractValidator<CreatePenaltyDeductionCapCommand>
{
    public CreatePenaltyDeductionCapCommandValidator() => RuleFor(x => x.MaxDaysPerMonth).GreaterThan(0);
}

public sealed class CreatePenaltyDeductionCapCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreatePenaltyDeductionCapCommand, long>
{
    public async Task<long> Handle(CreatePenaltyDeductionCapCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.PenaltyDeductionCaps.Where(c => c.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new PenaltyDeductionCap { CompanyId = current.CompanyId, MaxDaysPerMonth = request.MaxDaysPerMonth, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
        db.PenaltyDeductionCaps.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdatePenaltyDeductionCapCommand(long Id, decimal MaxDaysPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdatePenaltyDeductionCapCommandValidator : AbstractValidator<UpdatePenaltyDeductionCapCommand>
{
    public UpdatePenaltyDeductionCapCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MaxDaysPerMonth).GreaterThan(0);
    }
}

public sealed class UpdatePenaltyDeductionCapCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdatePenaltyDeductionCapCommand>
{
    public async Task Handle(UpdatePenaltyDeductionCapCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.PenaltyDeductionCaps.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PenaltyDeductionCap), request.Id);

        var sameKey = await db.PenaltyDeductionCaps.Where(c => c.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.MaxDaysPerMonth = request.MaxDaysPerMonth;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ NoticePeriodRule

public sealed record CreateNoticePeriodRuleCommand(int MinServiceYears, int NoticeDays, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateNoticePeriodRuleCommandValidator : AbstractValidator<CreateNoticePeriodRuleCommand>
{
    public CreateNoticePeriodRuleCommandValidator()
    {
        RuleFor(x => x.MinServiceYears).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NoticeDays).GreaterThan(0);
    }
}

public sealed class CreateNoticePeriodRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateNoticePeriodRuleCommand, long>
{
    public async Task<long> Handle(CreateNoticePeriodRuleCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.NoticePeriodRules
            .Where(r => r.CompanyId == current.CompanyId && r.MinServiceYears == request.MinServiceYears).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new NoticePeriodRule
        {
            CompanyId = current.CompanyId, MinServiceYears = request.MinServiceYears, NoticeDays = request.NoticeDays,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.NoticePeriodRules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateNoticePeriodRuleCommand(long Id, int NoticeDays, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateNoticePeriodRuleCommandValidator : AbstractValidator<UpdateNoticePeriodRuleCommand>
{
    public UpdateNoticePeriodRuleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NoticeDays).GreaterThan(0);
    }
}

public sealed class UpdateNoticePeriodRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateNoticePeriodRuleCommand>
{
    public async Task Handle(UpdateNoticePeriodRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.NoticePeriodRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NoticePeriodRule), request.Id);

        var sameKey = await db.NoticePeriodRules
            .Where(r => r.CompanyId == current.CompanyId && r.MinServiceYears == entity.MinServiceYears).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.NoticeDays = request.NoticeDays;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ EndOfServicePolicy

public sealed record CreateEndOfServicePolicyCommand(
    EndOfServicePolicyType PolicyType, string? FormulaDescription, int? MinServiceYears, decimal? DaysPerYear,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateEndOfServicePolicyCommandValidator : AbstractValidator<CreateEndOfServicePolicyCommand>
{
    public CreateEndOfServicePolicyCommandValidator() => RuleFor(x => x.FormulaDescription).MaximumLength(1000);
}

public sealed class CreateEndOfServicePolicyCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateEndOfServicePolicyCommand, long>
{
    public async Task<long> Handle(CreateEndOfServicePolicyCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.EndOfServicePolicies.Where(p => p.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new EndOfServicePolicy
        {
            CompanyId = current.CompanyId, PolicyType = request.PolicyType, FormulaDescription = request.FormulaDescription,
            MinServiceYears = request.MinServiceYears, DaysPerYear = request.DaysPerYear,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.EndOfServicePolicies.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateEndOfServicePolicyCommand(
    long Id, EndOfServicePolicyType PolicyType, string? FormulaDescription, int? MinServiceYears, decimal? DaysPerYear,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateEndOfServicePolicyCommandValidator : AbstractValidator<UpdateEndOfServicePolicyCommand>
{
    public UpdateEndOfServicePolicyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FormulaDescription).MaximumLength(1000);
    }
}

public sealed class UpdateEndOfServicePolicyCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateEndOfServicePolicyCommand>
{
    public async Task Handle(UpdateEndOfServicePolicyCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EndOfServicePolicies.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EndOfServicePolicy), request.Id);

        var sameKey = await db.EndOfServicePolicies.Where(p => p.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.PolicyType = request.PolicyType;
        entity.FormulaDescription = request.FormulaDescription;
        entity.MinServiceYears = request.MinServiceYears;
        entity.DaysPerYear = request.DaysPerYear;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ OvertimeLimitRule

public sealed record CreateOvertimeLimitRuleCommand(int? MaxMinutesPerDay, int? MaxMinutesPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateOvertimeLimitRuleCommandValidator : AbstractValidator<CreateOvertimeLimitRuleCommand>
{
    public CreateOvertimeLimitRuleCommandValidator()
    {
        RuleFor(x => x.MaxMinutesPerDay).GreaterThan(0).When(x => x.MaxMinutesPerDay is not null);
        RuleFor(x => x.MaxMinutesPerMonth).GreaterThan(0).When(x => x.MaxMinutesPerMonth is not null);
    }
}

public sealed class CreateOvertimeLimitRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateOvertimeLimitRuleCommand, long>
{
    public async Task<long> Handle(CreateOvertimeLimitRuleCommand request, CancellationToken cancellationToken)
    {
        var sameKey = await db.OvertimeLimitRules.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new OvertimeLimitRule
        {
            CompanyId = current.CompanyId, MaxMinutesPerDay = request.MaxMinutesPerDay, MaxMinutesPerMonth = request.MaxMinutesPerMonth,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.OvertimeLimitRules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateOvertimeLimitRuleCommand(long Id, int? MaxMinutesPerDay, int? MaxMinutesPerMonth, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateOvertimeLimitRuleCommandValidator : AbstractValidator<UpdateOvertimeLimitRuleCommand>
{
    public UpdateOvertimeLimitRuleCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class UpdateOvertimeLimitRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<UpdateOvertimeLimitRuleCommand>
{
    public async Task Handle(UpdateOvertimeLimitRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.OvertimeLimitRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeLimitRule), request.Id);

        var sameKey = await db.OvertimeLimitRules.Where(r => r.CompanyId == current.CompanyId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.MaxMinutesPerDay = request.MaxMinutesPerDay;
        entity.MaxMinutesPerMonth = request.MaxMinutesPerMonth;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}
