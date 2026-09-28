using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Queries.GetGoodsReceiptById;

public sealed record GetGoodsReceiptByIdQuery(long Id) : IRequest<GoodsReceiptDetailDto>;

public sealed class GetGoodsReceiptByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetGoodsReceiptByIdQuery, GoodsReceiptDetailDto>
{
    public async Task<GoodsReceiptDetailDto> Handle(GetGoodsReceiptByIdQuery request, CancellationToken cancellationToken)
    {
        var receipt = await db.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.Warehouse)
            .Include(r => r.Supplier)
            .Include(r => r.PurchaseOrder)
            .Include(r => r.PurchaseInvoice)
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GoodsReceipt), request.Id);

        return new GoodsReceiptDetailDto
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            BranchId = receipt.BranchId,
            WarehouseId = receipt.WarehouseId,
            WarehouseCode = receipt.Warehouse!.Code,
            SupplierId = receipt.SupplierId,
            SupplierCode = receipt.Supplier!.Code,
            SupplierNameAr = receipt.Supplier!.NameAr,
            PurchaseOrderId = receipt.PurchaseOrderId,
            PurchaseOrderNumber = receipt.PurchaseOrder?.OrderNumber,
            PurchaseInvoiceId = receipt.PurchaseInvoiceId,
            PurchaseInvoiceNumber = receipt.PurchaseInvoice?.InvoiceNumber,
            Status = receipt.Status.ToString(),
            Notes = receipt.Notes,
            RowVersion = Convert.ToBase64String(receipt.RowVersion),
            Lines = receipt.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new GoodsReceiptLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    AcceptedQuantity = l.AcceptedQuantity,
                    RejectedQuantity = l.RejectedQuantity,
                    RejectedReason = l.RejectedReason,
                    RejectedWarehouseId = l.RejectedWarehouseId,
                    UnitCost = l.UnitCost,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    BaseQuantity = l.BaseQuantity,
                    BaseUnitCost = l.BaseUnitCost,
                    ExpectedQuantity = l.ExpectedQuantity,
                    VarianceQuantity = l.VarianceQuantity,
                    VarianceReason = l.VarianceReason,
                    BatchNumber = l.BatchNumber,
                    ExpiryDate = l.ExpiryDate,
                    QualityCheckStatus = l.QualityCheckStatus.ToString(),
                    QualityCheckNotes = l.QualityCheckNotes
                })
                .ToList()
        };
    }
}
