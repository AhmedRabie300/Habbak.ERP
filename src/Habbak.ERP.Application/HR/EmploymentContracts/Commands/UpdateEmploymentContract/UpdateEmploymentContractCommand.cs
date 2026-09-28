using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.UpdateEmploymentContract;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — corrects the contract's own business data.
/// Status is deliberately not editable here: its lifecycle moves only through Create (initial),
/// RenewEmploymentContractCommand (Active -&gt; Expired, new contract created), and
/// TerminateEmploymentContractCommand (-&gt; Terminated) — not a free-form field on Update.
/// </summary>
public sealed record UpdateEmploymentContractCommand(
    long Id, ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
    decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay) : IRequest;

public sealed class UpdateEmploymentContractCommandValidator : AbstractValidator<UpdateEmploymentContractCommand>
{
    public UpdateEmploymentContractCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.BasicSalary).GreaterThan(0);
        RuleFor(x => x.InsurableWage).GreaterThan(0);
        RuleFor(x => x.WorkingHoursPerDay).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("تاريخ نهاية العقد لازم يكون بعد تاريخ البداية.");
    }
}

public sealed class UpdateEmploymentContractCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmploymentContractCommand>
{
    public async Task Handle(UpdateEmploymentContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.EmploymentContracts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.Id);

        contract.ContractType = request.ContractType;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.ProbationEndDate = request.ProbationEndDate;
        contract.BasicSalary = request.BasicSalary;
        contract.InsurableWage = request.InsurableWage;
        contract.WorkingHoursPerDay = request.WorkingHoursPerDay;

        await db.SaveChangesAsync(cancellationToken);
    }
}
