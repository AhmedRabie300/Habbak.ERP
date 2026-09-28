using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetPostedSalesInvoicesList;

public sealed class PostedSalesInvoiceDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required decimal TotalAmount { get; init; }
}

/// <summary>Feeds screen #8's "convert to delivery order" picker for the InvoiceWithIssue cycle —
/// only Posted invoices are offered (قاعدة 31's second branch).</summary>
public sealed record GetPostedSalesInvoicesListQuery : IRequest<IReadOnlyList<PostedSalesInvoiceDto>>;

public sealed class GetPostedSalesInvoicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPostedSalesInvoicesListQuery, IReadOnlyList<PostedSalesInvoiceDto>>
{
    public async Task<IReadOnlyList<PostedSalesInvoiceDto>> Handle(GetPostedSalesInvoicesListQuery request, CancellationToken cancellationToken)
    {
        return await db.SalesInvoices
            .AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Posted)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new PostedSalesInvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerId = i.CustomerId,
                CustomerNameAr = i.Customer!.NameAr,
                TotalAmount = i.TotalAmount
            })
            .ToListAsync(cancellationToken);
    }
}
