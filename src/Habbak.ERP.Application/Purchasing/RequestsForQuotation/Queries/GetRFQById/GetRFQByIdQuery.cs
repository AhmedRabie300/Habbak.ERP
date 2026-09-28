using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Queries.GetRFQById;

public sealed record GetRFQByIdQuery(long Id) : IRequest<RFQDetailDto>;

public sealed class GetRFQByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRFQByIdQuery, RFQDetailDto>
{
    public async Task<RFQDetailDto> Handle(GetRFQByIdQuery request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation
            .AsNoTracking()
            .Include(r => r.PurchaseRequest)
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .Include(r => r.Suppliers).ThenInclude(s => s.Supplier)
            .Include(r => r.Suppliers).ThenInclude(s => s.Quotes)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new RFQDetailDto
        {
            Id = rfq.Id,
            RFQNumber = rfq.RFQNumber,
            RFQDate = rfq.RFQDate,
            BranchId = rfq.BranchId,
            PurchaseRequestId = rfq.PurchaseRequestId,
            PurchaseRequestNumber = rfq.PurchaseRequest?.RequestNumber,
            Status = rfq.Status.ToString(),
            RequiredDate = rfq.RequiredDate,
            Notes = rfq.Notes,
            RowVersion = Convert.ToBase64String(rfq.RowVersion),
            Lines = rfq.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new RFQLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code
                })
                .ToList(),
            Suppliers = rfq.Suppliers
                .Select(s => new RFQSupplierDto
                {
                    Id = s.Id,
                    SupplierId = s.SupplierId,
                    SupplierCode = s.Supplier!.Code,
                    SupplierNameAr = s.Supplier!.NameAr,
                    Status = s.Status.ToString(),
                    ResponseDate = s.ResponseDate,
                    Quotes = s.Quotes
                        .Select(q => new RFQSupplierQuoteDto
                        {
                            Id = q.Id,
                            RFQLineId = q.RFQLineId,
                            UnitPrice = q.UnitPrice,
                            DiscountPercentage = q.DiscountPercentage,
                            DeliveryDays = q.DeliveryDays,
                            ValidUntil = q.ValidUntil,
                            IsExpired = q.ValidUntil < today,
                            IsSelected = q.IsSelected,
                            Notes = q.Notes
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
