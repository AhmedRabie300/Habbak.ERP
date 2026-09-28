using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;

public sealed record GetPurchaseOrderByIdQuery(long Id) : IRequest<PurchaseOrderDetailDto>;

public sealed class GetPurchaseOrderByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailDto>
{
    public async Task<PurchaseOrderDetailDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.PurchaseRequest)
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .Include(o => o.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        return new PurchaseOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            BranchId = order.BranchId,
            SupplierId = order.SupplierId,
            SupplierCode = order.Supplier!.Code,
            SupplierNameAr = order.Supplier!.NameAr,
            PurchaseRequestId = order.PurchaseRequestId,
            PurchaseRequestNumber = order.PurchaseRequest?.RequestNumber,
            CurrencyCode = order.CurrencyCode,
            ExchangeRate = order.ExchangeRate,
            PaymentTerms = order.PaymentTerms.ToString(),
            DeliveryTerms = order.DeliveryTerms?.ToString(),
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            DeliveryAddress = order.DeliveryAddress,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            DiscountReason = order.DiscountReason,
            Notes = order.Notes,
            RowVersion = Convert.ToBase64String(order.RowVersion),
            Lines = order.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new PurchaseOrderLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    ReceivedQuantity = l.ReceivedQuantity,
                    UnitPrice = l.UnitPrice,
                    TotalPrice = l.TotalPrice,
                    DiscountAmount = l.DiscountAmount,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    BaseQuantity = l.BaseQuantity,
                    BaseUnitCost = l.BaseUnitCost,
                    ExpectedDeliveryDate = l.ExpectedDeliveryDate,
                    Weight = l.Weight,
                    PurchaseRequestLineId = l.PurchaseRequestLineId
                })
                .ToList()
        };
    }
}
