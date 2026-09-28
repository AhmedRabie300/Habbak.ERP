using Habbak.ERP.Application.Accounting.Accounts.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountsList;

/// <summary>Lookup list for dropdowns — not paginated (accounts trees are small enough per company).</summary>
public sealed record GetAccountsListQuery(string? Search = null, bool? PostableOnly = null)
    : IRequest<IReadOnlyList<AccountOptionDto>>;

public sealed class GetAccountsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountsListQuery, IReadOnlyList<AccountOptionDto>>
{
    public async Task<IReadOnlyList<AccountOptionDto>> Handle(GetAccountsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Accounts.AsNoTracking().Where(a => a.IsActive);

        if (request.PostableOnly == true)
        {
            query = query.Where(a => a.IsPostable);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a => a.Code.Contains(term) || a.NameAr.Contains(term) || a.NameEn.Contains(term));
        }

        return await query
            .OrderBy(a => a.Code)
            .Select(a => new AccountOptionDto
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                AccountType = a.AccountType.ToString(),
                Nature = a.Nature.ToString(),
                IsPostable = a.IsPostable
            })
            .ToListAsync(cancellationToken);
    }
}
