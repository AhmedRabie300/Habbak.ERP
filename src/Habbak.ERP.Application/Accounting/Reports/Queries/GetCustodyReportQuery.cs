using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>My Remarks/Remarks2.md, remark 3.10, report 7 — "تقرير العهد": a static (no
/// parameters) listing of every custody register together with its settlement history, wrapping
/// CustodyRegister/CustodySettlement rather than adding new domain state.</summary>
public sealed record GetCustodyReportQuery : IRequest<IReadOnlyList<CustodyReportLineDto>>;

public sealed class GetCustodyReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustodyReportQuery, IReadOnlyList<CustodyReportLineDto>>
{
    public async Task<IReadOnlyList<CustodyReportLineDto>> Handle(GetCustodyReportQuery request, CancellationToken cancellationToken)
    {
        var registers = await db.CustodyRegisters
            .AsNoTracking()
            .Include(c => c.Settlements).ThenInclude(s => s.Lines).ThenInclude(l => l.Account)
            .OrderByDescending(c => c.IssueDate)
            .ToListAsync(cancellationToken);

        return registers
            .Select(c => new CustodyReportLineDto
            {
                Id = c.Id,
                EmployeeId = c.EmployeeId,
                BranchId = c.BranchId,
                Amount = c.Amount,
                IssueDate = c.IssueDate,
                Status = c.Status.ToString(),
                TotalSettled = c.Settlements.Sum(s => s.Lines.Sum(l => l.Amount)),
                Settlements = c.Settlements
                    .OrderBy(s => s.SettlementDate)
                    .Select(s => new CustodyReportSettlementDto
                    {
                        SettlementDate = s.SettlementDate,
                        TotalAmount = s.Lines.Sum(l => l.Amount),
                        Lines = s.Lines
                            .Select(l => new CustodyReportSettlementLineDto
                            {
                                AccountId = l.AccountId,
                                AccountCode = l.Account.Code,
                                AccountName = l.Account.NameAr,
                                Amount = l.Amount,
                                Description = l.Description
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }
}
