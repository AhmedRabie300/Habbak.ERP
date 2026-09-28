using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Organization.Companies.Commands.CreateCompany;
using Habbak.ERP.Application.Organization.Companies.Commands.DeleteCompany;
using Habbak.ERP.Application.Organization.Companies.Commands.UpdateCompany;
using Habbak.ERP.Application.Organization.Companies.Queries.GetCompaniesList;
using Habbak.ERP.Application.Organization.Companies.Queries.GetCurrentCompany;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Organization;

/// <summary>/organization/companies — the tenant root (Company.cs doc).</summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_COMPANIES", LookupReads = true)]
[Route("api/v1/organization/companies")]
public class CompaniesController(ISender mediator) : ControllerBase
{
    public sealed record CreateCompanyRequest(string Code, string NameAr, string NameEn, string? CommercialRegister, string? TaxCard, long BaseCurrencyId);
    public sealed record UpdateCompanyRequest(string NameAr, string NameEn, string? CommercialRegister, string? TaxCard, long BaseCurrencyId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCompaniesListQuery(), cancellationToken));

    /// <summary>Resolves the logged-in session's own company — e.g. for the Branches screen's
    /// read-only "Company" field (session decision: Branches stays filtered to the current company).</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCurrentCompanyQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCompanyCommand(
            request.Code, request.NameAr, request.NameEn, request.CommercialRegister, request.TaxCard, request.BaseCurrencyId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCompanyCommand(
            id, request.NameAr, request.NameEn, request.CommercialRegister, request.TaxCard, request.BaseCurrencyId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCompanyCommand(id), cancellationToken);
        return NoContent();
    }
}
