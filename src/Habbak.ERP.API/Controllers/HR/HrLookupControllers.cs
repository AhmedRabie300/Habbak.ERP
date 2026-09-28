using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.CreateEmployeeDocumentType;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.DeleteEmployeeDocumentType;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.UpdateEmployeeDocumentType;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Queries.GetEmployeeDocumentTypeById;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Queries.GetEmployeeDocumentTypesList;
using Habbak.ERP.Application.HR.InsuranceOffices.Commands.CreateInsuranceOffice;
using Habbak.ERP.Application.HR.InsuranceOffices.Commands.DeleteInsuranceOffice;
using Habbak.ERP.Application.HR.InsuranceOffices.Commands.UpdateInsuranceOffice;
using Habbak.ERP.Application.HR.InsuranceOffices.Queries.GetInsuranceOfficeById;
using Habbak.ERP.Application.HR.InsuranceOffices.Queries.GetInsuranceOfficesList;
using Habbak.ERP.Application.HR.JobGrades.Commands.CreateJobGrade;
using Habbak.ERP.Application.HR.JobGrades.Commands.DeleteJobGrade;
using Habbak.ERP.Application.HR.JobGrades.Commands.UpdateJobGrade;
using Habbak.ERP.Application.HR.JobGrades.Queries.GetJobGradeById;
using Habbak.ERP.Application.HR.JobGrades.Queries.GetJobGradesList;
using Habbak.ERP.Application.HR.JobPositions.Commands.CreateJobPosition;
using Habbak.ERP.Application.HR.JobPositions.Commands.DeleteJobPosition;
using Habbak.ERP.Application.HR.JobPositions.Commands.UpdateJobPosition;
using Habbak.ERP.Application.HR.JobPositions.Queries.GetJobPositionById;
using Habbak.ERP.Application.HR.JobPositions.Queries.GetJobPositionsList;
using Habbak.ERP.Application.HR.OrgUnits.Commands.CreateOrgUnit;
using Habbak.ERP.Application.HR.OrgUnits.Commands.DeleteOrgUnit;
using Habbak.ERP.Application.HR.OrgUnits.Commands.UpdateOrgUnit;
using Habbak.ERP.Application.HR.OrgUnits.Queries.GetOrgUnitById;
using Habbak.ERP.Application.HR.OrgUnits.Queries.GetOrgUnitsList;
using Habbak.ERP.Application.Organization.Banks.Commands.CreateBank;
using Habbak.ERP.Application.Organization.Banks.Commands.DeleteBank;
using Habbak.ERP.Application.Organization.Banks.Commands.UpdateBank;
using Habbak.ERP.Application.Organization.Banks.Queries.GetBankById;
using Habbak.ERP.Application.Organization.Banks.Queries.GetBanksList;
using Habbak.ERP.Application.Organization.Cities.Commands.CreateCity;
using Habbak.ERP.Application.Organization.Cities.Commands.DeleteCity;
using Habbak.ERP.Application.Organization.Cities.Commands.UpdateCity;
using Habbak.ERP.Application.Organization.Cities.Queries.GetCityById;
using Habbak.ERP.Application.Organization.Cities.Queries.GetCitiesList;
using Habbak.ERP.Application.Organization.Countries.Commands.CreateCountry;
using Habbak.ERP.Application.Organization.Countries.Commands.DeleteCountry;
using Habbak.ERP.Application.Organization.Countries.Commands.UpdateCountry;
using Habbak.ERP.Application.Organization.Countries.Queries.GetCountryById;
using Habbak.ERP.Application.Organization.Countries.Queries.GetCountriesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4 — the 8 "Hybrid: full screen" lookups (the
/// other 5 of the 13 from Batch B1 are Seed Only, no Commands/Controllers). One file for all 8, same
/// grouping convention as Controllers/Settings/SecurityControllers.cs. Routes follow each entity's
/// own Domain module (Organization vs HR), not this file's location — same as
/// CurrenciesController living under /organization/currencies despite its SETTINGS_CURRENCIES screen
/// code. No separate "/lookup" dropdown endpoint: every GetList query here is already unpaginated and
/// LookupReads = true lets any signed-in user read it for a dropdown, matching Currencies/ItemGroups/
/// UnitsOfMeasure/Suppliers — none of which has a dedicated lookup endpoint either.
/// </summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_COUNTRIES", LookupReads = true)]
[Route("api/v1/organization/countries")]
public class CountriesController(ISender mediator) : ControllerBase
{
    public sealed record CreateCountryRequest(string? Code, string NameAr, string NameEn, string? IsoCode);
    public sealed record UpdateCountryRequest(string NameAr, string NameEn, string? IsoCode, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCountriesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCountryByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCountryRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCountryCommand(request.Code, request.NameAr, request.NameEn, request.IsoCode), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCountryRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCountryCommand(id, request.NameAr, request.NameEn, request.IsoCode, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCountryCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("SETTINGS_CITIES", LookupReads = true)]
[Route("api/v1/organization/cities")]
public class CitiesController(ISender mediator) : ControllerBase
{
    public sealed record CreateCityRequest(string? Code, string NameAr, string NameEn, long CountryId);
    public sealed record UpdateCityRequest(string NameAr, string NameEn, long CountryId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCitiesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCityByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCityRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCityCommand(request.Code, request.NameAr, request.NameEn, request.CountryId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCityRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCityCommand(id, request.NameAr, request.NameEn, request.CountryId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCityCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("SETTINGS_BANKS", LookupReads = true)]
[Route("api/v1/organization/banks")]
public class BanksController(ISender mediator) : ControllerBase
{
    public sealed record CreateBankRequest(string? Code, string NameAr, string NameEn, string? SwiftCode, string? Address, long? CountryId);
    public sealed record UpdateBankRequest(string NameAr, string NameEn, string? SwiftCode, string? Address, long? CountryId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBanksListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBankByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBankRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateBankCommand(request.Code, request.NameAr, request.NameEn, request.SwiftCode, request.Address, request.CountryId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateBankRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBankCommand(id, request.NameAr, request.NameEn, request.SwiftCode, request.Address, request.CountryId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBankCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_JOB_GRADES", LookupReads = true)]
[Route("api/v1/hr/job-grades")]
public class JobGradesController(ISender mediator) : ControllerBase
{
    public sealed record CreateJobGradeRequest(string? Code, string NameAr, string NameEn, int Level, decimal? MinSalary, decimal? MaxSalary);
    public sealed record UpdateJobGradeRequest(string NameAr, string NameEn, int Level, decimal? MinSalary, decimal? MaxSalary, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetJobGradesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetJobGradeByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobGradeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateJobGradeCommand(request.Code, request.NameAr, request.NameEn, request.Level, request.MinSalary, request.MaxSalary), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateJobGradeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateJobGradeCommand(id, request.NameAr, request.NameEn, request.Level, request.MinSalary, request.MaxSalary, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteJobGradeCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_JOB_POSITIONS", LookupReads = true)]
[Route("api/v1/hr/job-positions")]
public class JobPositionsController(ISender mediator) : ControllerBase
{
    public sealed record CreateJobPositionRequest(string? Code, string NameAr, string NameEn, long? OrgUnitId, long? DefaultJobGradeId);
    public sealed record UpdateJobPositionRequest(string NameAr, string NameEn, long? OrgUnitId, long? DefaultJobGradeId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetJobPositionsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetJobPositionByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobPositionRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateJobPositionCommand(request.Code, request.NameAr, request.NameEn, request.OrgUnitId, request.DefaultJobGradeId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateJobPositionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateJobPositionCommand(id, request.NameAr, request.NameEn, request.OrgUnitId, request.DefaultJobGradeId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteJobPositionCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_ORG_UNITS", LookupReads = true)]
[Route("api/v1/hr/org-units")]
public class OrgUnitsController(ISender mediator) : ControllerBase
{
    public sealed record CreateOrgUnitRequest(string? Code, string NameAr, string NameEn, long? ParentId, long? BranchId, long? ManagerEmployeeId, long? CostCenterDimensionValueId);
    public sealed record UpdateOrgUnitRequest(string NameAr, string NameEn, long? ParentId, long? BranchId, long? ManagerEmployeeId, long? CostCenterDimensionValueId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOrgUnitsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOrgUnitByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrgUnitRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateOrgUnitCommand(request.Code, request.NameAr, request.NameEn, request.ParentId, request.BranchId, request.ManagerEmployeeId, request.CostCenterDimensionValueId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateOrgUnitRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateOrgUnitCommand(id, request.NameAr, request.NameEn, request.ParentId, request.BranchId, request.ManagerEmployeeId, request.CostCenterDimensionValueId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteOrgUnitCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>Screen code is HR_DOCUMENT_TYPES (registered in Batch B1), not HR_EMPLOYEE_DOCUMENT_TYPES.</summary>
[ApiController]
[Authorize]
[Screen("HR_DOCUMENT_TYPES", LookupReads = true)]
[Route("api/v1/hr/employee-document-types")]
public class EmployeeDocumentTypesController(ISender mediator) : ControllerBase
{
    public sealed record CreateEmployeeDocumentTypeRequest(string? Code, string NameAr, string NameEn, bool RequiresExpiry, bool IsMandatory, int? ExpiryAlertDays);
    public sealed record UpdateEmployeeDocumentTypeRequest(string NameAr, string NameEn, bool RequiresExpiry, bool IsMandatory, int? ExpiryAlertDays, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeDocumentTypesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeDocumentTypeByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeDocumentTypeCommand(request.Code, request.NameAr, request.NameEn, request.RequiresExpiry, request.IsMandatory, request.ExpiryAlertDays), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateEmployeeDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeDocumentTypeCommand(id, request.NameAr, request.NameEn, request.RequiresExpiry, request.IsMandatory, request.ExpiryAlertDays, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeDocumentTypeCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_INSURANCE_OFFICES", LookupReads = true)]
[Route("api/v1/hr/insurance-offices")]
public class InsuranceOfficesController(ISender mediator) : ControllerBase
{
    public sealed record CreateInsuranceOfficeRequest(string? Code, string NameAr, string NameEn, string? OfficialCode, string? Address);
    public sealed record UpdateInsuranceOfficeRequest(string NameAr, string NameEn, string? OfficialCode, string? Address, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInsuranceOfficesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInsuranceOfficeByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInsuranceOfficeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateInsuranceOfficeCommand(request.Code, request.NameAr, request.NameEn, request.OfficialCode, request.Address), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateInsuranceOfficeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateInsuranceOfficeCommand(id, request.NameAr, request.NameEn, request.OfficialCode, request.Address, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteInsuranceOfficeCommand(id), cancellationToken);
        return NoContent();
    }
}
