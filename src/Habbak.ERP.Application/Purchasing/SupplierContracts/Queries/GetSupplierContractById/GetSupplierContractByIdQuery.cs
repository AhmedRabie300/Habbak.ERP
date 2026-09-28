using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Queries.GetSupplierContractById;

public sealed record GetSupplierContractByIdQuery(long Id) : IRequest<SupplierContractDetailDto>;

public sealed class GetSupplierContractByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierContractByIdQuery, SupplierContractDetailDto>
{
    public async Task<SupplierContractDetailDto> Handle(GetSupplierContractByIdQuery request, CancellationToken cancellationToken)
    {
        var contract = await db.SupplierContracts
            .AsNoTracking()
            .Include(c => c.Supplier)
            .Include(c => c.Items).ThenInclude(i => i.Item)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierContract), request.Id);

        return new SupplierContractDetailDto
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            SupplierId = contract.SupplierId,
            SupplierCode = contract.Supplier!.Code,
            SupplierNameAr = contract.Supplier!.NameAr,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            AutoRenew = contract.AutoRenew,
            Status = contract.Status.ToString(),
            Notes = contract.Notes,
            RowVersion = Convert.ToBase64String(contract.RowVersion),
            Items = contract.Items
                .Select(i => new ContractItemDto
                {
                    Id = i.Id,
                    ItemId = i.ItemId,
                    ItemCode = i.Item!.Code,
                    ItemNameAr = i.Item!.NameAr,
                    UnitPrice = i.UnitPrice,
                    MinQuantity = i.MinQuantity,
                    MaxQuantity = i.MaxQuantity,
                    DiscountPercentage = i.DiscountPercentage
                })
                .ToList()
        };
    }
}
