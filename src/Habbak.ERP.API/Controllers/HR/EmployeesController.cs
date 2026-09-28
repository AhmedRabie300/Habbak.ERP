using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.HR.Employees.Commands.ActivateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.AssignUserToEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.CreateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.CreateEmployeePersonalData;
using Habbak.ERP.Application.HR.Employees.Commands.DeleteEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.SetEmployeeManager;
using Habbak.ERP.Application.HR.Employees.Commands.TerminateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployeePersonalData;
using Habbak.ERP.Application.HR.Employees.Queries.GetEmployeeById;
using Habbak.ERP.Application.HR.Employees.Queries.GetEmployeesList;
using Habbak.ERP.Application.HR.Employees.Queries.GetMyEmployeeProfile;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5. No LookupReads here (unlike the 8 lookups,
/// Batch B4) — Employee master data is not as harmless to expose broadly as Country/JobGrade;
/// dropdown access goes through the separate EmployeesLookupController instead, the same split
/// Users/UsersLookupController already uses (Settings/SecurityControllers.cs). [MaskFields] hides the
/// [PiiField] properties of EmployeePersonalData (Address/PhoneNumber/PersonalEmail/EmergencyContact*/
/// BirthDate) from viewers without the field's own View permission — pre-existing Phase 0.4 machinery,
/// not new here. NationalIdEncrypted/BankIbanEncrypted are never serialized by this controller at all
/// (EmployeePersonalDataDto only carries *Last4) — the only path to their plaintext is HrPiiController.
/// </summary>
[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[MaskFields("EmployeePersonalData")]
[Route("api/v1/hr/employees")]
public class EmployeesController(ISender mediator) : ControllerBase
{
    public sealed record CreateEmployeeRequest(
        string? Code, string NameAr, string NameEn, long BranchId, long OrgUnitId, long JobPositionId, long JobGradeId,
        long? ManagerId, long? UserId, DateOnly HireDate, EmploymentType EmploymentType, long? CostCenterDimensionValueId);

    public sealed record UpdateEmployeeRequest(
        string NameAr, string NameEn, long BranchId, long OrgUnitId, long JobPositionId, long JobGradeId,
        long? ManagerId, long? UserId, DateOnly HireDate, EmploymentType EmploymentType, long? CostCenterDimensionValueId, bool IsActive);

    public sealed record CreateEmployeePersonalDataRequest(
        string NationalId, string? BankIban, string? BankName, long? BankId, long? NationalityId, long? CityId,
        long? MilitaryStatusId, long? QualificationTypeId, DateOnly BirthDate, Gender Gender, MaritalStatus MaritalStatus,
        string? Address, string? PhoneNumber, string? PersonalEmail, string? EmergencyContactName, string? EmergencyContactPhone,
        long? EmergencyContactRelationshipTypeId);

    public sealed record UpdateEmployeePersonalDataRequest(
        string NationalId, string? BankIban, string? BankName, long? BankId, long? NationalityId, long? CityId,
        long? MilitaryStatusId, long? QualificationTypeId, DateOnly BirthDate, Gender Gender, MaritalStatus MaritalStatus,
        string? Address, string? PhoneNumber, string? PersonalEmail, string? EmergencyContactName, string? EmergencyContactPhone,
        long? EmergencyContactRelationshipTypeId);

    public sealed record AssignUserRequest(long UserId);
    public sealed record SetManagerRequest(long? ManagerId);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeesListQuery { Search = query.Search, Page = query.Page, PageSize = query.PageSize, SortBy = query.SortBy, SortDir = query.SortDir }, cancellationToken));

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMyEmployeeProfileQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeCommand(
            request.Code, request.NameAr, request.NameEn, request.BranchId, request.OrgUnitId, request.JobPositionId, request.JobGradeId,
            request.ManagerId, request.UserId, request.HireDate, request.EmploymentType, request.CostCenterDimensionValueId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeCommand(
            id, request.NameAr, request.NameEn, request.BranchId, request.OrgUnitId, request.JobPositionId, request.JobGradeId,
            request.ManagerId, request.UserId, request.HireDate, request.EmploymentType, request.CostCenterDimensionValueId, request.IsActive),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/personal-data")]
    public async Task<IActionResult> CreatePersonalData(long id, [FromBody] CreateEmployeePersonalDataRequest request, CancellationToken cancellationToken)
    {
        var personalDataId = await mediator.Send(new CreateEmployeePersonalDataCommand(
            id, request.NationalId, request.BankIban, request.BankName, request.BankId, request.NationalityId, request.CityId,
            request.MilitaryStatusId, request.QualificationTypeId, request.BirthDate, request.Gender, request.MaritalStatus,
            request.Address, request.PhoneNumber, request.PersonalEmail, request.EmergencyContactName, request.EmergencyContactPhone,
            request.EmergencyContactRelationshipTypeId), cancellationToken);
        return Ok(new { id = personalDataId });
    }

    [HttpPut("{id:long}/personal-data")]
    public async Task<IActionResult> UpdatePersonalData(long id, [FromBody] UpdateEmployeePersonalDataRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeePersonalDataCommand(
            id, request.NationalId, request.BankIban, request.BankName, request.BankId, request.NationalityId, request.CityId,
            request.MilitaryStatusId, request.QualificationTypeId, request.BirthDate, request.Gender, request.MaritalStatus,
            request.Address, request.PhoneNumber, request.PersonalEmail, request.EmergencyContactName, request.EmergencyContactPhone,
            request.EmergencyContactRelationshipTypeId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ActivateEmployeeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/terminate")]
    public async Task<IActionResult> Terminate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new TerminateEmployeeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/assign-user")]
    public async Task<IActionResult> AssignUser(long id, [FromBody] AssignUserRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AssignUserToEmployeeCommand(id, request.UserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/set-manager")]
    public async Task<IActionResult> SetManager(long id, [FromBody] SetManagerRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetEmployeeManagerCommand(id, request.ManagerId), cancellationToken);
        return NoContent();
    }
}

/// <summary>/hr/employees-lookup — names of the company's employees, for any screen that picks one (same split as Users/UsersLookupController).</summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/hr/employees-lookup")]
public class EmployeesLookupController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new Application.HR.Employees.Queries.GetEmployeesLookup.GetEmployeesLookupQuery(), cancellationToken));
}
