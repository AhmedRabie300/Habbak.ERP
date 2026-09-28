using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.BankReconciliations.Commands.CreateBankReconciliationRun;

public sealed record CreateBankReconciliationRunCommand(long BankAccountId, DateOnly PeriodFrom, DateOnly PeriodTo)
    : IRequest<long>;

public sealed class CreateBankReconciliationRunCommandValidator : AbstractValidator<CreateBankReconciliationRunCommand>
{
    public CreateBankReconciliationRunCommandValidator()
    {
        RuleFor(x => x.BankAccountId).GreaterThan(0);
        RuleFor(x => x.PeriodTo).GreaterThanOrEqualTo(x => x.PeriodFrom);
    }
}

public sealed class CreateBankReconciliationRunCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateBankReconciliationRunCommand, long>
{
    public async Task<long> Handle(CreateBankReconciliationRunCommand request, CancellationToken cancellationToken)
    {
        var run = new BankReconciliationRun
        {
            BankAccountId = request.BankAccountId,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Status = BankReconciliationStatus.InProgress
        };

        db.BankReconciliationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        return run.Id;
    }
}
