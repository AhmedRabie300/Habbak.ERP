using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.PaymentMethodConfigs.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.PaymentMethodConfigs.Queries.GetPOSPaymentMethodConfigsList;

public sealed record GetPOSPaymentMethodConfigsListQuery(long POSTerminalId) : IRequest<IReadOnlyList<POSPaymentMethodConfigDto>>;

public sealed class GetPOSPaymentMethodConfigsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPOSPaymentMethodConfigsListQuery, IReadOnlyList<POSPaymentMethodConfigDto>>
{
    public async Task<IReadOnlyList<POSPaymentMethodConfigDto>> Handle(GetPOSPaymentMethodConfigsListQuery request, CancellationToken cancellationToken)
    {
        return await db.POSPaymentMethodConfigs
            .AsNoTracking()
            .Include(c => c.PaymentMethod)
            .Include(c => c.LinkedTreasuryAccount)
            .Where(c => c.POSTerminalId == request.POSTerminalId)
            .Select(c => new POSPaymentMethodConfigDto
            {
                Id = c.Id,
                POSTerminalId = c.POSTerminalId,
                PaymentMethodId = c.PaymentMethodId,
                PaymentMethodNameAr = c.PaymentMethod!.NameAr,
                PaymentMethodNameEn = c.PaymentMethod!.NameEn,
                IsEnabled = c.IsEnabled,
                LinkedTreasuryAccountId = c.LinkedTreasuryAccountId,
                LinkedTreasuryAccountNameAr = c.LinkedTreasuryAccount!.NameAr
            })
            .ToListAsync(cancellationToken);
    }
}
