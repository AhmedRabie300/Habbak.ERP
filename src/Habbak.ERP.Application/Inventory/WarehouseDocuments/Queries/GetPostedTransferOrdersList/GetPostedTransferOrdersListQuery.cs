using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetPostedTransferOrdersList;

public sealed class PostedTransferOrderDto
{
    public required long Id { get; init; }
    public required string DocumentNumber { get; init; }
    public required string SourceWarehouseCode { get; init; }
    public required string CustodyOfficerCode { get; init; }
}

/// <summary>Feeds the TransferReceipt create screen's order picker (screen #11) — any Posted
/// TransferOrder is selectable; nothing here excludes an order already received against, since a
/// single order can legitimately be received in more than one partial receipt.</summary>
public sealed record GetPostedTransferOrdersListQuery : IRequest<IReadOnlyList<PostedTransferOrderDto>>;

public sealed class GetPostedTransferOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPostedTransferOrdersListQuery, IReadOnlyList<PostedTransferOrderDto>>
{
    public async Task<IReadOnlyList<PostedTransferOrderDto>> Handle(GetPostedTransferOrdersListQuery request, CancellationToken cancellationToken)
    {
        return await db.WarehouseDocuments
            .AsNoTracking()
            .Where(d => d.DocumentType == WarehouseDocumentType.TransferOrder && d.Status == WarehouseDocumentStatus.Posted)
            .OrderByDescending(d => d.DocumentNumber)
            .Select(d => new PostedTransferOrderDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                SourceWarehouseCode = d.SourceWarehouse!.Code,
                CustodyOfficerCode = d.CustodyOfficer!.Code
            })
            .ToListAsync(cancellationToken);
    }
}
