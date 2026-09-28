using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.AccountMappings.Commands.UpdateCompanyAccountMappings;
using Habbak.ERP.Application.Accounting.AccountMappings.Queries.GetCompanyAccountMappings;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/settings/* — company-level accounts the posting engine resolves by role
/// (Docs/Posting-Engine-Implementation-Plan.md, stage 0).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_ACCOUNT_MAPPINGS")]
[Route("api/v1/accounting/settings")]
public class AccountingSettingsController(ISender mediator) : ControllerBase
{
    public sealed record AccountMappingRequest(CompanyAccountRole Role, long? AccountId);
    public sealed record UpdateAccountMappingsRequest(IReadOnlyList<AccountMappingRequest> Mappings);

    [HttpGet("account-mappings")]
    public async Task<IActionResult> GetAccountMappings(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCompanyAccountMappingsQuery(), cancellationToken));

    [HttpPut("account-mappings")]
    public async Task<IActionResult> UpdateAccountMappings([FromBody] UpdateAccountMappingsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new UpdateCompanyAccountMappingsCommand(
                request.Mappings.Select(m => new AccountMappingInput(m.Role, m.AccountId)).ToList()),
            cancellationToken);

        return NoContent();
    }
}
