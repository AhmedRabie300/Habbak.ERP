using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnById;

public sealed record GetPurchaseReturnByIdQuery(long Id) : IRequest<PurchaseReturnDetailDto>;

public sealed class GetPurchaseReturnByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseReturnByIdQuery, PurchaseReturnDetailDto>
{
    public async Task<PurchaseReturnDetailDto> Handle(GetPurchaseReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await db.PurchaseReturns
            .AsNoTracking()
            .Include(r => r.Supplier)
            .Include(r => r.PurchaseInvoice)
            .Include(r => r.Warehouse)
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseReturn), request.Id);

        return new PurchaseReturnDetailDto
        {
            Id = purchaseReturn.Id,
            ReturnNumber = purchaseReturn.ReturnNumber,
            ReturnDate = purchaseReturn.ReturnDate,
            BranchId = purchaseReturn.BranchId,
            SupplierId = purchaseReturn.SupplierId,
            SupplierCode = purchaseReturn.Supplier!.Code,
            SupplierNameAr = purchaseReturn.Supplier!.NameAr,
            PurchaseInvoiceId = purchaseReturn.PurchaseInvoiceId,
            PurchaseInvoiceNumber = purchaseReturn.PurchaseInvoice?.InvoiceNumber,
            WarehouseId = purchaseReturn.WarehouseId,
            WarehouseCode = purchaseReturn.Warehouse!.Code,
            Reason = purchaseReturn.Reason.ToString(),
            Status = purchaseReturn.Status.ToString(),
            Notes = purchaseReturn.Notes,
            RowVersion = Convert.ToBase64String(purchaseReturn.RowVersion),
            Lines = purchaseReturn.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new PurchaseReturnLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    BaseQuantity = l.BaseQuantity,
                    BaseUnitCost = l.BaseUnitCost,
                    BatchNumber = l.BatchNumber
                })
                .ToList()
        };
    }
}
