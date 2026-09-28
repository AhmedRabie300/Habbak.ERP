using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Banks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Banks.Queries.GetBanksList;

/// <summary>Not a paginated GetList screen: the bank catalog is always small (Currency precedent).</summary>
public sealed record GetBanksListQuery : IRequest<IReadOnlyList<BankDto>>;

public sealed class GetBanksListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBanksListQuery, IReadOnlyList<BankDto>>
{
    public async Task<IReadOnlyList<BankDto>> Handle(GetBanksListQuery request, CancellationToken cancellationToken)
    {
        return await db.Banks
            .AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => new BankDto { Id = b.Id, Code = b.Code, NameAr = b.NameAr, NameEn = b.NameEn, IsActive = b.IsActive, SwiftCode = b.SwiftCode, Address = b.Address, CountryId = b.CountryId })
            .ToListAsync(cancellationToken);
    }
}
