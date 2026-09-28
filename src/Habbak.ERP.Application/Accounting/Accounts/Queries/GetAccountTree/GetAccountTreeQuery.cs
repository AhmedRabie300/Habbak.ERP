using Habbak.ERP.Application.Accounting.Accounts.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountTree;

/// <summary>
/// Returns the full flat account list (a company's chart is always small); the client builds
/// the tree from ParentId (01-Module-Accounting.md, section 5, screen 1).
/// </summary>
public sealed record GetAccountTreeQuery : IRequest<IReadOnlyList<AccountTreeNodeDto>>;

public sealed class GetAccountTreeQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountTreeQuery, IReadOnlyList<AccountTreeNodeDto>>
{
    public async Task<IReadOnlyList<AccountTreeNodeDto>> Handle(GetAccountTreeQuery request, CancellationToken cancellationToken)
    {
        return await db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new AccountTreeNodeDto
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                ParentId = a.ParentId,
                Level = a.Level,
                AccountType = a.AccountType.ToString(),
                Nature = a.Nature.ToString(),
                IsPostable = a.IsPostable,
                IsActive = a.IsActive,
                IsSharedAcrossCompanies = a.IsSharedAcrossCompanies
            })
            .ToListAsync(cancellationToken);
    }
}
