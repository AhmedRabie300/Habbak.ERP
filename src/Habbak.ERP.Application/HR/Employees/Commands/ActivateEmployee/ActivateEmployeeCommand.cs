using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.ActivateEmployee;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — Draft -&gt; Active. No salary-structure gate
/// (risk 5 in the plan — deferred, that module doesn't exist yet). Checks only what Batch B1/B3
/// actually built: an Active EmploymentContract, and every EmployeeDocumentType this company marked
/// IsMandatory has a matching EmployeeDocument row for this employee.
/// </summary>
public sealed record ActivateEmployeeCommand(long EmployeeId) : IRequest;

public sealed class ActivateEmployeeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<ActivateEmployeeCommand>
{
    public async Task Handle(ActivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.EmployeeId], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (employee.Status != EmployeeStatus.Draft)
        {
            throw new BusinessRuleException("HR-EMPLOYEE-NOT-DRAFT", "لا يمكن تفعيل موظف إلا وهو في حالة مسودة.");
        }

        var hasActiveContract = await db.EmploymentContracts.AnyAsync(
            c => c.EmployeeId == request.EmployeeId && c.Status == EmploymentContractStatus.Active, cancellationToken);
        if (!hasActiveContract)
        {
            throw new BusinessRuleException("HR-EMPLOYEE-NO-ACTIVE-CONTRACT", "لا يوجد عقد عمل ساري لهذا الموظف.");
        }

        var mandatoryDocumentTypeIds = await db.EmployeeDocumentTypes
            .Where(t => t.CompanyId == currentCompanyContext.CompanyId && t.IsMandatory)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (mandatoryDocumentTypeIds.Count > 0)
        {
            var providedDocumentTypeIds = await db.EmployeeDocuments
                .Where(d => d.EmployeeId == request.EmployeeId && mandatoryDocumentTypeIds.Contains(d.EmployeeDocumentTypeId))
                .Select(d => d.EmployeeDocumentTypeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (mandatoryDocumentTypeIds.Except(providedDocumentTypeIds).Any())
            {
                throw new BusinessRuleException("HR-EMPLOYEE-MISSING-MANDATORY-DOCUMENTS", "لسه فيه مستندات إلزامية ناقصة لهذا الموظف.");
            }
        }

        employee.Status = EmployeeStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
    }
}
