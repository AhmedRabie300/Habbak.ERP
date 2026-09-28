using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Queries.GetDeliveryOrderById;

public sealed record GetDeliveryOrderByIdQuery(long Id) : IRequest<DeliveryOrderDetailDto>;

public sealed class GetDeliveryOrderByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDeliveryOrderByIdQuery, DeliveryOrderDetailDto>
{
    public async Task<DeliveryOrderDetailDto> Handle(GetDeliveryOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .AsNoTracking()
            .Include(d => d.Customer)
            .Include(d => d.Warehouse)
            .Include(d => d.SourceOrder)
            .Include(d => d.SourceInvoice)
            .Include(d => d.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliveryOrder), request.Id);

        return new DeliveryOrderDetailDto
        {
            Id = deliveryOrder.Id,
            DeliveryNumber = deliveryOrder.DeliveryNumber,
            DeliveryDate = deliveryOrder.DeliveryDate,
            BranchId = deliveryOrder.BranchId,
            CustomerId = deliveryOrder.CustomerId,
            CustomerNameAr = deliveryOrder.Customer!.NameAr,
            WarehouseId = deliveryOrder.WarehouseId,
            WarehouseNameAr = deliveryOrder.Warehouse!.NameAr,
            SourceOrderId = deliveryOrder.SourceOrderId,
            SourceOrderNumber = deliveryOrder.SourceOrder?.OrderNumber,
            SourceInvoiceId = deliveryOrder.SourceInvoiceId,
            SourceInvoiceNumber = deliveryOrder.SourceInvoice?.InvoiceNumber,
            Status = deliveryOrder.Status.ToString(),
            RowVersion = Convert.ToBase64String(deliveryOrder.RowVersion),
            Lines = deliveryOrder.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new DeliveryOrderLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    BatchNumber = l.BatchNumber
                })
                .ToList()
        };
    }
}
