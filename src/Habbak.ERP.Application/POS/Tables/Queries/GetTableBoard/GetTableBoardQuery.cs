using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Tables.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Queries.GetTableBoard;

/// <summary>شاشة الطرابيزات التفاعلية (screen #8) — حالة كل طرابيزة حيًا مع ملخص الشيك المرتبط
/// (Open أو Held) لو موجود، عشان الواجهة تقرر تسترجع الشيك ولا تفتح واحد جديد (قاعدة 7).</summary>
public sealed record GetTableBoardQuery(long BranchId) : IRequest<IReadOnlyList<TableBoardItemDto>>;

public sealed class GetTableBoardQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTableBoardQuery, IReadOnlyList<TableBoardItemDto>>
{
    public async Task<IReadOnlyList<TableBoardItemDto>> Handle(GetTableBoardQuery request, CancellationToken cancellationToken)
    {
        var tables = await db.Tables
            .AsNoTracking()
            .Where(t => t.BranchId == request.BranchId && t.IsActive)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);

        var tableIds = tables.Select(t => t.Id).ToList();

        var openChecks = await db.Checks
            .AsNoTracking()
            .Where(c => c.TableId != null && tableIds.Contains(c.TableId!.Value)
                && (c.Status == CheckStatus.Open || c.Status == CheckStatus.Held))
            .Select(c => new
            {
                c.Id,
                c.TableId,
                c.CheckCode,
                Status = c.Status.ToString(),
                Total = c.Lines.Sum(l => (decimal?)(l.Quantity * l.UnitPrice - l.DiscountAmount)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var checksByTable = openChecks.ToDictionary(c => c.TableId!.Value);

        return tables.Select(t =>
        {
            checksByTable.TryGetValue(t.Id, out var check);
            return new TableBoardItemDto
            {
                Id = t.Id,
                Code = t.Code,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                Status = t.Status.ToString(),
                OpenCheckId = check?.Id,
                OpenCheckCode = check?.CheckCode,
                OpenCheckStatus = check?.Status,
                OpenCheckTotal = check?.Total
            };
        }).ToList();
    }
}
