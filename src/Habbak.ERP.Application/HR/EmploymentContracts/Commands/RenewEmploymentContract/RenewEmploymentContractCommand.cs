using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.CreateEmploymentContract;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.RenewEmploymentContract;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 (EmploymentContract.cs doc, Batch B3: "التجديد =
/// عقد جديد مربوط بالقديم"). One SaveChangesAsync closes the previous contract (Status -&gt; Expired)
/// and creates its successor together — EF Core's own implicit transaction covers both, same
/// reasoning as TerminateEmployeeCommand (Batch B5). EmployeeId/BranchId are inherited from the
/// previous contract, not asked of the caller — a renewal is for the same employee, obviously.
/// </summary>
public sealed record RenewEmploymentContractCommand(
    long PreviousContractId, ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
    decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay) : IRequest<long>;

public sealed class RenewEmploymentContractCommandValidator : AbstractValidator<RenewEmploymentContractCommand>
{
    public RenewEmploymentContractCommandValidator()
    {
        RuleFor(x => x.PreviousContractId).GreaterThan(0);
        RuleFor(x => x.BasicSalary).GreaterThan(0);
        RuleFor(x => x.InsurableWage).GreaterThan(0);
        RuleFor(x => x.WorkingHoursPerDay).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("تاريخ نهاية العقد لازم يكون بعد تاريخ البداية.");
    }
}

public sealed class RenewEmploymentContractCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<RenewEmploymentContractCommand, long>
{
    public async Task<long> Handle(RenewEmploymentContractCommand request, CancellationToken cancellationToken)
    {
        var previousContract = await db.EmploymentContracts.FirstOrDefaultAsync(c => c.Id == request.PreviousContractId, cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.PreviousContractId);

        var alreadyRenewed = await db.EmploymentContracts.AnyAsync(c => c.PreviousContractId == request.PreviousContractId, cancellationToken);
        if (alreadyRenewed)
        {
            throw new BusinessRuleException("HR-CONTRACT-ALREADY-RENEWED", "العقد ده اتجدّد بالفعل من قبل.");
        }

        previousContract.Status = EmploymentContractStatus.Expired;

        var newContract = new EmploymentContract
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = previousContract.BranchId,
            EmployeeId = previousContract.EmployeeId,
            ContractType = request.ContractType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ProbationEndDate = request.ProbationEndDate,
            BasicSalary = request.BasicSalary,
            InsurableWage = request.InsurableWage,
            WorkingHoursPerDay = request.WorkingHoursPerDay,
            PreviousContractId = previousContract.Id,
            Status = await CreateEmploymentContractCommandHandler.MandatoryDocumentsCompleteAsync(
                db, currentCompanyContext, previousContract.EmployeeId, cancellationToken)
                ? EmploymentContractStatus.Active
                : EmploymentContractStatus.Draft
        };

        db.EmploymentContracts.Add(newContract);
        await db.SaveChangesAsync(cancellationToken);

        return newContract.Id;
    }
}
