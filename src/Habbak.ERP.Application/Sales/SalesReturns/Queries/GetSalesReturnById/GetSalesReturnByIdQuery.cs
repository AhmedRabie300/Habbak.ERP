using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Queries.GetSalesReturnById;

public sealed record GetSalesReturnByIdQuery(long Id) : IRequest<SalesReturnDetailDto>;

public sealed class GetSalesReturnByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesReturnByIdQuery, SalesReturnDetailDto>
{
    public async Task<SalesReturnDetailDto> Handle(GetSalesReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var salesReturn = await db.SalesReturns
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Warehouse)
            .Include(r => r.SourceInvoice)
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesReturn), request.Id);

        return new SalesReturnDetailDto
        {
            Id = salesReturn.Id,
            ReturnNumber = salesReturn.ReturnNumber,
            ReturnDate = salesReturn.ReturnDate,
            BranchId = salesReturn.BranchId,
            CustomerId = salesReturn.CustomerId,
            CustomerNameAr = salesReturn.Customer!.NameAr,
            WarehouseId = salesReturn.WarehouseId,
            WarehouseNameAr = salesReturn.Warehouse!.NameAr,
            SourceInvoiceId = salesReturn.SourceInvoiceId,
            SourceInvoiceNumber = salesReturn.SourceInvoice?.InvoiceNumber,
            Reason = salesReturn.Reason,
            Status = salesReturn.Status.ToString(),
            RowVersion = Convert.ToBase64String(salesReturn.RowVersion),
            Lines = salesReturn.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SalesReturnLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    BatchNumber = l.BatchNumber
                })
                .ToList()
        };
    }
}
