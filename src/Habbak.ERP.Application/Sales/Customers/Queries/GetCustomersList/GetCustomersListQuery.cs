using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.Customers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Customers.Queries.GetCustomersList;

/// <summary>Not a paginated GetList screen, same reasoning as Suppliers/Warehouses/Items — reference
/// data the rest of the module (quotes, orders, invoices) picks from in dropdowns.</summary>
public sealed record GetCustomersListQuery : IRequest<IReadOnlyList<CustomerListItemDto>>;

public sealed class GetCustomersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustomersListQuery, IReadOnlyList<CustomerListItemDto>>
{
    public async Task<IReadOnlyList<CustomerListItemDto>> Handle(GetCustomersListQuery request, CancellationToken cancellationToken)
    {
        return await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                CustomerType = c.CustomerType.ToString(),
                CreditLimit = c.CreditLimit,
                LoyaltyPointsBalance = c.LoyaltyPointsBalance,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
