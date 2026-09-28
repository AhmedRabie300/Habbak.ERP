using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.Customers.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Customers.Queries.GetCustomerById;

public sealed record GetCustomerByIdQuery(long Id) : IRequest<CustomerDetailDto>;

public sealed class GetCustomerByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCustomerByIdQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        return new CustomerDetailDto
        {
            Id = customer.Id,
            Code = customer.Code,
            NameAr = customer.NameAr,
            NameEn = customer.NameEn,
            BranchId = customer.BranchId,
            CustomerType = customer.CustomerType.ToString(),
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            CreditLimit = customer.CreditLimit,
            PaymentTermDays = customer.PaymentTermDays,
            ReceivableAccountId = customer.ReceivableAccountId,
            LoyaltyTierId = customer.LoyaltyTierId,
            LoyaltyPointsBalance = customer.LoyaltyPointsBalance,
            IsActive = customer.IsActive
        };
    }
}
