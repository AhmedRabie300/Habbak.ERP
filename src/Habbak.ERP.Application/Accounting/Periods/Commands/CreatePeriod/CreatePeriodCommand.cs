using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Periods.Commands.CreatePeriod;

public sealed record CreatePeriodCommand(DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<long>;

public sealed class CreatePeriodCommandValidator : AbstractValidator<CreatePeriodCommand>
{
    public CreatePeriodCommandValidator()
    {
        RuleFor(x => x.PeriodEnd).GreaterThan(x => x.PeriodStart);
    }
}

public sealed class CreatePeriodCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreatePeriodCommand, long>
{
    public async Task<long> Handle(CreatePeriodCommand request, CancellationToken cancellationToken)
    {
        var overlaps = await db.AccountingPeriods.AnyAsync(
            p => p.CompanyId == currentCompanyContext.CompanyId
                && p.PeriodStart <= request.PeriodEnd && p.PeriodEnd >= request.PeriodStart,
            cancellationToken);

        if (overlaps)
        {
            throw new BusinessRuleException("ACC-PERIOD-OVERLAP", "الفترة الجديدة تتداخل مع فترة موجودة بالفعل.");
        }

        var period = new AccountingPeriod
        {
            CompanyId = currentCompanyContext.CompanyId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = AccountingPeriodStatus.Open
        };

        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync(cancellationToken);

        return period.Id;
    }
}
