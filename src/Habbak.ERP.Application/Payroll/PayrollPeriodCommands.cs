using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.4 — minimal PayrollPeriod lifecycle
// (Open at creation) so CreatePayrollRunCommand has a period to run against. Lock/Close transitions
// belong to Sub-Batch 4.5/4.6 (Lock ties to CutoffDate passing, Close ties to the payment posting
// template) — not built here, same "build what the current sub-batch needs" scoping as every prior
// phase.

public sealed record PayrollPeriodDto(long Id, int Year, int Month, DateOnly StartDate, DateOnly EndDate, DateOnly CutoffDate, PayrollPeriodStatus Status);

public sealed record GetPayrollPeriodsQuery : IRequest<IReadOnlyList<PayrollPeriodDto>>;

public sealed class GetPayrollPeriodsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPayrollPeriodsQuery, IReadOnlyList<PayrollPeriodDto>>
{
    public async Task<IReadOnlyList<PayrollPeriodDto>> Handle(GetPayrollPeriodsQuery request, CancellationToken cancellationToken) =>
        await db.PayrollPeriods.AsNoTracking().OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new PayrollPeriodDto(p.Id, p.Year, p.Month, p.StartDate, p.EndDate, p.CutoffDate, p.Status))
            .ToListAsync(cancellationToken);
}

/// <summary>Rule: one PayrollPeriod per company/year/month (unique filtered index,
/// Infrastructure/Persistence/Configurations/Payroll/PayrollEntitiesConfigurations.cs) — a second
/// attempt for the same month is a validation error here, not a silent no-op, since unlike a
/// PayrollRun a period isn't something a retry should transparently return.</summary>
public sealed record CreatePayrollPeriodCommand(int Year, int Month, DateOnly StartDate, DateOnly EndDate, DateOnly CutoffDate) : IRequest<long>;

public sealed class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
    public CreatePayrollPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.CutoffDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public sealed class CreatePayrollPeriodCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreatePayrollPeriodCommand, long>
{
    public async Task<long> Handle(CreatePayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.PayrollPeriods.AnyAsync(
            p => p.CompanyId == current.CompanyId && p.Year == request.Year && p.Month == request.Month, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("PAY-PERIOD-EXISTS", $"فترة رواتب شهر {request.Month}/{request.Year} موجودة بالفعل.");
        }

        var period = new PayrollPeriod
        {
            CompanyId = current.CompanyId,
            Year = request.Year,
            Month = request.Month,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CutoffDate = request.CutoffDate,
            Status = PayrollPeriodStatus.Open
        };

        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync(cancellationToken);
        return period.Id;
    }
}
