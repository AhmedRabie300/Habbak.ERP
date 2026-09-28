using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentsList;

/// <summary>GetList — DocumentType is required so the Stock In screen and the Stock Out screen
/// each see only their own rows despite sharing one underlying table (mirrors GetVouchersListQuery).</summary>
public sealed class GetWarehouseDocumentsListQuery : ListQuery, IRequest<PagedResult<WarehouseDocumentListItemDto>>
{
    public required WarehouseDocumentType DocumentType { get; init; }
}

public sealed class GetWarehouseDocumentsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWarehouseDocumentsListQuery, PagedResult<WarehouseDocumentListItemDto>>
{
    public async Task<PagedResult<WarehouseDocumentListItemDto>> Handle(
        GetWarehouseDocumentsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.WarehouseDocuments.AsNoTracking().Where(d => d.DocumentType == request.DocumentType);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<WarehouseDocumentStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            // My Remarks/Remarks2.md, remark 2.5 — search must reach every grid column, including
            // line-level ones (item, quantity, unit cost, total) that live on WarehouseDocumentLine
            // rather than the master row itself.
            decimal? numericTerm = decimal.TryParse(term, out var parsedNumber) ? parsedNumber : null;

            query = query.Where(d =>
                d.DocumentNumber.Contains(term) ||
                (d.SourceWarehouse != null && d.SourceWarehouse.Code.Contains(term)) ||
                (d.DestinationWarehouse != null && d.DestinationWarehouse.Code.Contains(term)) ||
                matchingStatuses.Contains(d.Status) ||
                (dateTerm != null && d.DocumentDate == dateTerm) ||
                d.Lines.Any(l =>
                    l.Item.Code.Contains(term) ||
                    l.Item.NameAr.Contains(term) ||
                    l.Item.NameEn.Contains(term) ||
                    (numericTerm != null && (l.Quantity == numericTerm || l.UnitCost == numericTerm || l.Quantity * l.UnitCost == numericTerm))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "documentDate" => descending ? query.OrderByDescending(d => d.DocumentDate) : query.OrderBy(d => d.DocumentDate),
            "status" => descending ? query.OrderByDescending(d => d.Status) : query.OrderBy(d => d.Status),
            _ => descending ? query.OrderByDescending(d => d.DocumentNumber) : query.OrderBy(d => d.DocumentNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new WarehouseDocumentListItemDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                DocumentDate = d.DocumentDate,
                SourceWarehouseCode = d.SourceWarehouse != null ? d.SourceWarehouse.Code : null,
                DestinationWarehouseCode = d.DestinationWarehouse != null ? d.DestinationWarehouse.Code : null,
                CustodyOfficerCode = d.CustodyOfficer != null ? d.CustodyOfficer.Code : null,
                LineCount = d.Lines.Count,
                Status = d.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<WarehouseDocumentListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
