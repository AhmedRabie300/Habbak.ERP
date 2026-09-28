using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Queries.GetSalesOrderById;

public sealed record GetSalesOrderByIdQuery(long Id) : IRequest<SalesOrderDetailDto>;

public sealed class GetSalesOrderByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesOrderByIdQuery, SalesOrderDetailDto>
{
    public async Task<SalesOrderDetailDto> Handle(GetSalesOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.SourceQuote)
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.Id);

        return new SalesOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            BranchId = order.BranchId,
            CustomerId = order.CustomerId,
            CustomerNameAr = order.Customer!.NameAr,
            SourceQuoteId = order.SourceQuoteId,
            SourceQuoteNumber = order.SourceQuote?.QuoteNumber,
            Subtotal = order.Subtotal,
            Status = order.Status.ToString(),
            RowVersion = Convert.ToBase64String(order.RowVersion),
            Lines = order.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SalesOrderLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    DeliveredQuantity = l.DeliveredQuantity
                })
                .ToList()
        };
    }
}
