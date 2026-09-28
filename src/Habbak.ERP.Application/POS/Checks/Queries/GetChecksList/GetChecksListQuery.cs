using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Checks.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Queries.GetChecksList;

/// <summary>يخدم شاشتي #9 (الشيكات المفتوحة، Status=Open) و#7 (الفواتير المعلّقة، Status=Held) —
/// نفس الشكل، فلتر الحالة بس هو الفرق.</summary>
public sealed record GetChecksListQuery(long? POSTerminalId, CheckStatus Status) : IRequest<IReadOnlyList<CheckListItemDto>>;

public sealed class GetChecksListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetChecksListQuery, IReadOnlyList<CheckListItemDto>>
{
    public async Task<IReadOnlyList<CheckListItemDto>> Handle(GetChecksListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Checks.AsNoTracking().Include(c => c.Table).Include(c => c.Lines)
            .Where(c => c.Status == request.Status);

        if (request.POSTerminalId is { } terminalId)
        {
            query = query.Where(c => c.POSTerminalId == terminalId);
        }

        var checks = await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(cancellationToken);

        return checks.Select(c => new CheckListItemDto
        {
            Id = c.Id,
            CheckCode = c.CheckCode,
            TableId = c.TableId,
            TableCode = c.Table?.Code,
            OrderType = c.OrderType.ToString(),
            Status = c.Status.ToString(),
            LineCount = c.Lines.Count,
            Total = c.Lines.Sum(l => l.Quantity * l.UnitPrice - l.DiscountAmount),
            OpenedAtUtc = c.CreatedAtUtc
        }).ToList();
    }
}
