using Habbak.ERP.Application.Accounting.PaymentMethods.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.PaymentMethods.Queries.GetPaymentMethodsList;

/// <summary>Not a paginated GetList screen: a company's payment-method count is always small.</summary>
public sealed record GetPaymentMethodsListQuery : IRequest<IReadOnlyList<PaymentMethodDto>>;

public sealed class GetPaymentMethodsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentMethodsListQuery, IReadOnlyList<PaymentMethodDto>>
{
    public async Task<IReadOnlyList<PaymentMethodDto>> Handle(GetPaymentMethodsListQuery request, CancellationToken cancellationToken)
    {
        return await db.PaymentMethods
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new PaymentMethodDto { Id = p.Id, Code = p.Code, NameAr = p.NameAr, NameEn = p.NameEn, IsActive = p.IsActive })
            .ToListAsync(cancellationToken);
    }
}
