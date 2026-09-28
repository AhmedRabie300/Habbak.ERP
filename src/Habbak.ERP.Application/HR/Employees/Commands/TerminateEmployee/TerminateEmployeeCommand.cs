using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Users;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.TerminateEmployee;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — resolves open question 4 (§8, "termination
/// blocked on an open custody"): rejects with HR-EMPLOYEE-OPEN-CUSTODY rather than closing it (a
/// custody needs its own settlement flow, not a side effect of terminating someone). Any still-Active
/// EmploymentContract is auto-closed (Status -&gt; Terminated, EndDate -&gt; today) rather than blocking,
/// since a departing employee's contract ending IS the termination, not a precondition for it. All of
/// this — Employee, its EmploymentContracts, the linked User, its UserScopes, its future
/// ShiftAssignments — is one SaveChangesAsync call, so EF Core's own implicit transaction covers it;
/// no explicit BeginTransactionAsync needed (that API is for commands that need two separate saves).
///
/// Phase 1.4 (HR-MASTER-PLAN.md §Phase 1.4) widened the open-custody check to also cover assets:
/// Employee -&gt; CustodyOfficer.EmployeeId -&gt; FixedAsset.CustodyOfficerId (rule 43), a chain Phase 1.2
/// completed. Draft/Disposed/WrittenOff assets don't count — Draft was never actually acquired yet,
/// and Disposed/WrittenOff no longer represent anything the employee is still accountable for. Same
/// error code as the cash-custody case (HR-EMPLOYEE-OPEN-CUSTODY) since this is the same rule widened,
/// not a separate one — the message just names what's open.
/// </summary>
public sealed record TerminateEmployeeCommand(long EmployeeId) : IRequest;

public sealed class TerminateEmployeeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<TerminateEmployeeCommand>
{
    public async Task Handle(TerminateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.EmployeeId], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (employee.Status == EmployeeStatus.Terminated)
        {
            throw new BusinessRuleException("HR-EMPLOYEE-ALREADY-TERMINATED", "الموظف ده متعيّن إنهاء خدمته بالفعل.");
        }

        var openCustodyRegisterIds = await db.CustodyRegisters
            .Where(c => c.EmployeeId == request.EmployeeId && c.Status == CustodyStatus.Open)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var custodyOfficerIds = await db.CustodyOfficers
            .Where(o => o.EmployeeId == request.EmployeeId)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var custodyAssetNumbers = custodyOfficerIds.Count == 0
            ? []
            : await db.FixedAssets
                .Where(a => a.CustodyOfficerId != null && custodyOfficerIds.Contains(a.CustodyOfficerId.Value)
                    && a.Status != FixedAssetStatus.Draft && a.Status != FixedAssetStatus.Disposed && a.Status != FixedAssetStatus.WrittenOff)
                .Select(a => a.AssetNumber)
                .ToListAsync(cancellationToken);

        if (openCustodyRegisterIds.Count > 0 || custodyAssetNumbers.Count > 0)
        {
            var reasons = new List<string>();
            if (openCustodyRegisterIds.Count > 0)
            {
                reasons.Add($"عهدة نقدية مفتوحة ({FormatList(openCustodyRegisterIds.Select(id => $"CustodyRegister #{id}"))})");
            }
            if (custodyAssetNumbers.Count > 0)
            {
                reasons.Add($"عهدة على أصول ({FormatList(custodyAssetNumbers)})");
            }

            throw new BusinessRuleException("HR-EMPLOYEE-OPEN-CUSTODY", $"لا يمكن إنهاء خدمة موظف لسه معاه {string.Join(" و", reasons)}.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var openContracts = await db.EmploymentContracts
            .Where(c => c.EmployeeId == request.EmployeeId && c.Status == EmploymentContractStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var contract in openContracts)
        {
            contract.Status = EmploymentContractStatus.Terminated;
            contract.EndDate ??= today;
        }

        employee.Status = EmployeeStatus.Terminated;
        employee.TerminationDate = today;

        if (employee.UserId is not null)
        {
            var user = await db.Users.FindAsync([employee.UserId.Value], cancellationToken);
            if (user is not null)
            {
                user.Status = UserStatus.Suspended;
                await UserRules.RevokeSessionsAsync(db, user.Id, "EmployeeTerminated", cancellationToken);
            }

            var scopes = await db.UserScopes
                .Where(s => s.UserId == employee.UserId && s.CompanyId == currentCompanyContext.CompanyId)
                .ToListAsync(cancellationToken);
            foreach (var scope in scopes)
            {
                scope.IsActive = false;
            }

            var futureShiftAssignments = await db.ShiftAssignments
                .Where(a => a.UserId == employee.UserId && a.AssignedDate >= today)
                .ToListAsync(cancellationToken);
            db.ShiftAssignments.RemoveRange(futureShiftAssignments);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>First 5 items, "و{N} أخرى" appended for the rest — a termination-blocked message names what's open, not every single row.</summary>
    private static string FormatList(IEnumerable<string> items)
    {
        var list = items.ToList();
        var shown = string.Join(", ", list.Take(5));
        return list.Count > 5 ? $"{shown} و{list.Count - 5} أخرى" : shown;
    }
}
