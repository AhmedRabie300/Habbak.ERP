using Habbak.ERP.Application.Accounting.Dimensions.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Queries.GetAccountDimensionLinks;

public sealed record GetAccountDimensionLinksQuery(long AccountId) : IRequest<IReadOnlyList<AccountDimensionLinkDto>>;

public sealed class GetAccountDimensionLinksQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountDimensionLinksQuery, IReadOnlyList<AccountDimensionLinkDto>>
{
    public async Task<IReadOnlyList<AccountDimensionLinkDto>> Handle(GetAccountDimensionLinksQuery request, CancellationToken cancellationToken)
    {
        return await db.AccountDimensionLinks
            .AsNoTracking()
            .Where(l => l.AccountId == request.AccountId)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new AccountDimensionLinkDto
            {
                Id = l.Id,
                AccountId = l.AccountId,
                CostCenterDimensionId = l.CostCenterDimensionId,
                DimensionNameAr = l.CostCenterDimension.NameAr,
                DimensionNameEn = l.CostCenterDimension.NameEn,
                DisplayOrder = l.DisplayOrder,
                IsMandatory = l.IsMandatory
            })
            .ToListAsync(cancellationToken);
    }
}
