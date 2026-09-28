using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.WasteRecords.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WasteRecords.Queries.GetWasteRecordById;

public sealed record GetWasteRecordByIdQuery(long Id) : IRequest<WasteRecordDetailDto>;

public sealed class GetWasteRecordByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetWasteRecordByIdQuery, WasteRecordDetailDto>
{
    public async Task<WasteRecordDetailDto> Handle(GetWasteRecordByIdQuery request, CancellationToken cancellationToken)
    {
        var wasteRecord = await db.WasteRecords
            .AsNoTracking()
            .Include(w => w.Warehouse)
            .Include(w => w.Item)
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WasteRecord), request.Id);

        return new WasteRecordDetailDto
        {
            Id = wasteRecord.Id,
            WarehouseId = wasteRecord.WarehouseId,
            WarehouseCode = wasteRecord.Warehouse!.Code,
            ItemId = wasteRecord.ItemId,
            ItemCode = wasteRecord.Item!.Code,
            ItemNameAr = wasteRecord.Item!.NameAr,
            Quantity = wasteRecord.Quantity,
            WasteDate = wasteRecord.WasteDate,
            Reason = wasteRecord.Reason,
            SourceDocumentType = wasteRecord.SourceDocumentType,
            SourceDocumentId = wasteRecord.SourceDocumentId,
            RowVersion = Convert.ToBase64String(wasteRecord.RowVersion)
        };
    }
}
