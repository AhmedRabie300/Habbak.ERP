using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.Suppliers.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Suppliers.Queries.GetSupplierById;

public sealed record GetSupplierByIdQuery(long Id) : IRequest<SupplierDetailDto>;

public sealed class GetSupplierByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSupplierByIdQuery, SupplierDetailDto>
{
    public async Task<SupplierDetailDto> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        return new SupplierDetailDto
        {
            Id = supplier.Id,
            Code = supplier.Code,
            NameAr = supplier.NameAr,
            NameEn = supplier.NameEn,
            TaxNumber = supplier.TaxNumber,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            PaymentTerms = supplier.PaymentTerms.ToString(),
            CreditLimit = supplier.CreditLimit,
            CurrencyCode = supplier.CurrencyCode,
            DefaultWarehouseId = supplier.DefaultWarehouseId,
            PayableAccountId = supplier.PayableAccountId,
            ExpenseAccountId = supplier.ExpenseAccountId,
            IsActive = supplier.IsActive
        };
    }
}
