using Habbak.ERP.Application.Accounting.Accounts.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountById;

public sealed record GetAccountByIdQuery(long Id) : IRequest<AccountDetailDto>;

public sealed class GetAccountByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountByIdQuery, AccountDetailDto>
{
    public async Task<AccountDetailDto> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.Id);

        return new AccountDetailDto
        {
            Id = account.Id,
            Code = account.Code,
            NameAr = account.NameAr,
            NameEn = account.NameEn,
            ParentId = account.ParentId,
            Level = account.Level,
            AccountType = account.AccountType.ToString(),
            Nature = account.Nature.ToString(),
            IsPostable = account.IsPostable,
            IsActive = account.IsActive,
            IsSharedAcrossCompanies = account.IsSharedAcrossCompanies,
            CurrencyCode = account.CurrencyCode,
            RowVersion = Convert.ToBase64String(account.RowVersion)
        };
    }
}
