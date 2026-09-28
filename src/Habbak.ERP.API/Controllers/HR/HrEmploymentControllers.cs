using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.CreateEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.DeleteEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.SetEmployeeCertificationAttachment;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.UpdateEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Queries.GetEmployeeCertificationById;
using Habbak.ERP.Application.HR.EmployeeCertifications.Queries.GetEmployeeCertificationsList;
using Habbak.ERP.Application.HR.EmployeeDocuments.Commands.CreateEmployeeDocument;
using Habbak.ERP.Application.HR.EmployeeDocuments.Commands.DeleteEmployeeDocument;
using Habbak.ERP.Application.HR.EmployeeDocuments.Commands.UpdateEmployeeDocument;
using Habbak.ERP.Application.HR.EmployeeDocuments.Queries.GetEmployeeDocumentById;
using Habbak.ERP.Application.HR.EmployeeDocuments.Queries.GetEmployeeDocumentsList;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.CreateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.DeleteEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.RenewEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.SetEmploymentContractAttachment;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.TerminateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.UpdateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Dtos;
using Habbak.ERP.Application.HR.EmploymentContracts.Queries.GetEmploymentContractById;
using Habbak.ERP.Application.HR.EmploymentContracts.Queries.GetEmploymentContractsList;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — the 3 Employee "followers" (Batch B3's
/// entities). All 3 nested under their employee (/hr/employees/{employeeId}/...), the clearer-for-UI
/// choice the plan itself picked over a flat ?employeeId= query filter — matching Purchasing/Suppliers'
/// own detail-plus-tabs shape, where a supplier's related records live under its own id. Same single
/// screen code (HR_EMPLOYEES) as EmployeesController (Batch B5) for all 3: this is all employee data,
/// not three separate permissions to manage.
/// </summary>
[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[Route("api/v1/hr/employees/{employeeId:long}/contracts")]
public class EmploymentContractsController(ISender mediator) : ControllerBase
{
    public sealed record CreateEmploymentContractRequest(
        ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
        decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay,
        IReadOnlyList<ContractLineInput>? Lines = null);

    public sealed record UpdateEmploymentContractRequest(
        ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
        decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay,
        IReadOnlyList<ContractLineInput>? Lines = null);

    public sealed record RenewEmploymentContractRequest(
        ContractType ContractType, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
        decimal BasicSalary, decimal InsurableWage, int WorkingHoursPerDay,
        IReadOnlyList<ContractLineInput>? Lines = null);

    public sealed record SetEmploymentContractAttachmentRequest(long? AttachmentId);

    [HttpGet]
    public async Task<IActionResult> GetList(long employeeId, [FromQuery] ListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmploymentContractsListQuery { EmployeeId = employeeId, Search = query.Search, Page = query.Page, PageSize = query.PageSize, SortBy = query.SortBy, SortDir = query.SortDir }, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long employeeId, long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmploymentContractByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(long employeeId, [FromBody] CreateEmploymentContractRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmploymentContractCommand(
            employeeId, request.ContractType, request.StartDate, request.EndDate, request.ProbationEndDate,
            request.BasicSalary, request.InsurableWage, request.WorkingHoursPerDay, request.Lines), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long employeeId, long id, [FromBody] UpdateEmploymentContractRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmploymentContractCommand(
            id, request.ContractType, request.StartDate, request.EndDate, request.ProbationEndDate,
            request.BasicSalary, request.InsurableWage, request.WorkingHoursPerDay, request.Lines), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long employeeId, long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmploymentContractCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/renew")]
    public async Task<IActionResult> Renew(long employeeId, long id, [FromBody] RenewEmploymentContractRequest request, CancellationToken cancellationToken)
    {
        var newId = await mediator.Send(new RenewEmploymentContractCommand(
            id, request.ContractType, request.StartDate, request.EndDate, request.ProbationEndDate,
            request.BasicSalary, request.InsurableWage, request.WorkingHoursPerDay, request.Lines), cancellationToken);
        return Ok(new { id = newId });
    }

    [HttpPost("{id:long}/terminate")]
    public async Task<IActionResult> Terminate(long employeeId, long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new TerminateEmploymentContractCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/attachment")]
    public async Task<IActionResult> SetAttachment(long employeeId, long id, [FromBody] SetEmploymentContractAttachmentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetEmploymentContractAttachmentCommand(id, request.AttachmentId), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[Route("api/v1/hr/employees/{employeeId:long}/documents")]
public class EmployeeDocumentsController(ISender mediator) : ControllerBase
{
    public sealed record CreateEmployeeDocumentRequest(long EmployeeDocumentTypeId, DateOnly IssueDate, DateOnly? ExpiryDate, long AttachmentId, string? DocumentNumber);
    public sealed record UpdateEmployeeDocumentRequest(long EmployeeDocumentTypeId, DateOnly IssueDate, DateOnly? ExpiryDate, long AttachmentId, string? DocumentNumber);

    [HttpGet]
    public async Task<IActionResult> GetList(long employeeId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeDocumentsListQuery(employeeId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long employeeId, long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeDocumentByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(long employeeId, [FromBody] CreateEmployeeDocumentRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeDocumentCommand(
            employeeId, request.EmployeeDocumentTypeId, request.IssueDate, request.ExpiryDate, request.AttachmentId, request.DocumentNumber), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long employeeId, long id, [FromBody] UpdateEmployeeDocumentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeDocumentCommand(
            id, request.EmployeeDocumentTypeId, request.IssueDate, request.ExpiryDate, request.AttachmentId, request.DocumentNumber), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long employeeId, long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeDocumentCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[Route("api/v1/hr/employees/{employeeId:long}/certifications")]
public class EmployeeCertificationsController(ISender mediator) : ControllerBase
{
    public sealed record CreateEmployeeCertificationRequest(string NameAr, string NameEn, string Issuer, DateOnly IssueDate, DateOnly? ExpiryDate, string? CertificateNumber);
    public sealed record UpdateEmployeeCertificationRequest(string NameAr, string NameEn, string Issuer, DateOnly IssueDate, DateOnly? ExpiryDate, string? CertificateNumber);
    public sealed record SetEmployeeCertificationAttachmentRequest(long? AttachmentId);

    [HttpGet]
    public async Task<IActionResult> GetList(long employeeId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeCertificationsListQuery(employeeId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long employeeId, long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeCertificationByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(long employeeId, [FromBody] CreateEmployeeCertificationRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeCertificationCommand(
            employeeId, request.NameAr, request.NameEn, request.Issuer, request.IssueDate, request.ExpiryDate, request.CertificateNumber), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long employeeId, long id, [FromBody] UpdateEmployeeCertificationRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeCertificationCommand(
            id, request.NameAr, request.NameEn, request.Issuer, request.IssueDate, request.ExpiryDate, request.CertificateNumber), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long employeeId, long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeCertificationCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/attachment")]
    public async Task<IActionResult> SetAttachment(long employeeId, long id, [FromBody] SetEmployeeCertificationAttachmentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetEmployeeCertificationAttachmentCommand(id, request.AttachmentId), cancellationToken);
        return NoContent();
    }
}
