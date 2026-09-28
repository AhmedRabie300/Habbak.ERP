using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmploymentContracts.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.CreateEmploymentContract;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6. EmploymentContract is not ILookupEntity — no
/// Code, so no ICodeGenerator here (unlike the Batch B4 lookups). BranchId is not a request field: it
/// always mirrors the employee's own BranchId (IBranchScopedEntity consistency — a contract can't sit
/// in a different branch than the employee it's for), resolved from the Employee row, not asked of
/// the caller. Initial Status mirrors ActivateEmployeeCommand's own mandatory-document check (Batch
/// B5) — Active if every EmployeeDocumentType this company marked IsMandatory already has a matching
/// EmployeeDocument for this employee, Draft otherwise; there is no separate "activate a contract"
/// command in this batch, so Create is the only place that decision gets made (besides Renew).
/// </summary>
public sealed record CreateEmploymentContractCommand(
    long EmployeeId, ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
    decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay,
    IReadOnlyList<ContractLineInput>? Lines = null) : IRequest<long>;

public sealed class CreateEmploymentContractCommandValidator : AbstractValidator<CreateEmploymentContractCommand>
{
    public CreateEmploymentContractCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.BasicSalary).GreaterThan(0);
        RuleFor(x => x.InsurableWage).GreaterThan(0);
        RuleFor(x => x.WorkingHoursPerDay).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("تاريخ نهاية العقد لازم يكون بعد تاريخ البداية.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.NameAr).NotEmpty().MaximumLength(200);
            line.RuleFor(l => l.NameEn).NotEmpty().MaximumLength(200);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

public sealed class CreateEmploymentContractCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateEmploymentContractCommand, long>
{
    public async Task<long> Handle(CreateEmploymentContractCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var hasActiveContract = await db.EmploymentContracts.AnyAsync(
            c => c.EmployeeId == request.EmployeeId && c.Status == EmploymentContractStatus.Active, cancellationToken);
        if (hasActiveContract)
        {
            throw new BusinessRuleException("HR-CONTRACT-ALREADY-ACTIVE", "يوجد عقد ساري بالفعل لهذا الموظف — استخدم التجديد بدلًا من إنشاء عقد جديد.");
        }

        var contract = new EmploymentContract
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            ContractType = request.ContractType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ProbationEndDate = request.ProbationEndDate,
            BasicSalary = request.BasicSalary,
            InsurableWage = request.InsurableWage,
            WorkingHoursPerDay = request.WorkingHoursPerDay,
            Status = await MandatoryDocumentsCompleteAsync(db, currentCompanyContext, request.EmployeeId, cancellationToken)
                ? EmploymentContractStatus.Active
                : EmploymentContractStatus.Draft
        };

        foreach (var line in request.Lines ?? [])
        {
            contract.Lines.Add(line.ToEntity());
        }

        db.EmploymentContracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return contract.Id;
    }

    internal static async Task<bool> MandatoryDocumentsCompleteAsync(
        IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, long employeeId, CancellationToken cancellationToken)
    {
        var mandatoryDocumentTypeIds = await db.EmployeeDocumentTypes
            .Where(t => t.CompanyId == currentCompanyContext.CompanyId && t.IsMandatory)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (mandatoryDocumentTypeIds.Count == 0)
        {
            return true;
        }

        var providedDocumentTypeIds = await db.EmployeeDocuments
            .Where(d => d.EmployeeId == employeeId && mandatoryDocumentTypeIds.Contains(d.EmployeeDocumentTypeId))
            .Select(d => d.EmployeeDocumentTypeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return !mandatoryDocumentTypeIds.Except(providedDocumentTypeIds).Any();
    }
}
