using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentById;

public sealed record GetWarehouseDocumentByIdQuery(long Id) : IRequest<WarehouseDocumentDetailDto>;

public sealed class GetWarehouseDocumentByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWarehouseDocumentByIdQuery, WarehouseDocumentDetailDto>
{
    public async Task<WarehouseDocumentDetailDto> Handle(GetWarehouseDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await db.WarehouseDocuments
            .AsNoTracking()
            .Include(d => d.Lines).ThenInclude(l => l.Item)
            .Include(d => d.Lines).ThenInclude(l => l.Unit)
            .Include(d => d.RelatedWarehouseDocument)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseDocument), request.Id);

        return new WarehouseDocumentDetailDto
        {
            Id = document.Id,
            DocumentType = document.DocumentType.ToString(),
            BranchId = document.BranchId,
            DocumentNumber = document.DocumentNumber,
            DocumentDate = document.DocumentDate,
            SourceWarehouseId = document.SourceWarehouseId,
            DestinationWarehouseId = document.DestinationWarehouseId,
            CustodyOfficerId = document.CustodyOfficerId,
            RelatedWarehouseDocumentId = document.RelatedWarehouseDocumentId,
            RelatedWarehouseDocumentNumber = document.RelatedWarehouseDocument?.DocumentNumber,
            Status = document.Status.ToString(),
            Notes = document.Notes,
            RowVersion = Convert.ToBase64String(document.RowVersion),
            Lines = document.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new WarehouseDocumentLineDto
                {
                    Id = l.Id,
                    LineNumber = l.LineNumber,
                    ItemId = l.ItemId,
                    ItemCode = l.Item.Code,
                    ItemNameAr = l.Item.NameAr,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit != null ? l.Unit.Code : null,
                    UnitNameAr = l.Unit != null ? l.Unit.NameAr : null,
                    UnitFactor = l.UnitFactor,
                    BatchNumber = l.BatchNumber,
                    ExpiryDate = l.ExpiryDate,
                    ExpectedQuantity = l.ExpectedQuantity,
                    VarianceQuantity = l.VarianceQuantity
                })
                .ToList()
        };
    }
}
