using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.Suppliers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Suppliers.Queries.GetSuppliersList;

/// <summary>Not a paginated GetList screen, same reasoning as Warehouses/Items/CustodyOfficers —
/// this is reference/master data the rest of the module picks from in dropdowns.</summary>
public sealed record GetSuppliersListQuery : IRequest<IReadOnlyList<SupplierListItemDto>>;

public sealed class GetSuppliersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSuppliersListQuery, IReadOnlyList<SupplierListItemDto>>
{
    public async Task<IReadOnlyList<SupplierListItemDto>> Handle(GetSuppliersListQuery request, CancellationToken cancellationToken)
    {
        return await db.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new SupplierListItemDto
            {
                Id = s.Id,
                Code = s.Code,
                NameAr = s.NameAr,
                NameEn = s.NameEn,
                PaymentTerms = s.PaymentTerms.ToString(),
                CurrencyCode = s.CurrencyCode,
                IsActive = s.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
