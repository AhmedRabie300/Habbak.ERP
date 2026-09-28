using Habbak.ERP.Application.Accounting.AccountMappings.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountMappings.Queries.GetCompanyAccountMappings;

/// <summary>Every <see cref="CompanyAccountRole"/>, in enum order, with its mapping if one exists.</summary>
public sealed record GetCompanyAccountMappingsQuery : IRequest<IReadOnlyList<CompanyAccountMappingDto>>;

public sealed class GetCompanyAccountMappingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCompanyAccountMappingsQuery, IReadOnlyList<CompanyAccountMappingDto>>
{
    public async Task<IReadOnlyList<CompanyAccountMappingDto>> Handle(
        GetCompanyAccountMappingsQuery request, CancellationToken cancellationToken)
    {
        var mapped = await db.CompanyAccountMappings
            .AsNoTracking()
            .Select(m => new { m.Role, m.AccountId, m.Account!.Code, m.Account.NameAr })
            .ToDictionaryAsync(m => m.Role, cancellationToken);

        return Enum.GetValues<CompanyAccountRole>()
            .Select(role =>
            {
                mapped.TryGetValue(role, out var mapping);
                return new CompanyAccountMappingDto
                {
                    Role = role.ToString(),
                    ExpectedAccountType = role.ExpectedAccountType().ToString(),
                    AccountId = mapping?.AccountId,
                    AccountCode = mapping?.Code,
                    AccountNameAr = mapping?.NameAr
                };
            })
            .ToList();
    }
}
