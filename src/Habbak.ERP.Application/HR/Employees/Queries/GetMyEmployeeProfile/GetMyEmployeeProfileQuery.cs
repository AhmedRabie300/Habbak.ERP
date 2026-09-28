using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Employees.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Queries.GetMyEmployeeProfile;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — the Self data scope (Phase 0.1): the
/// signed-in session's own Employee record, resolved via ICurrentCompanyContext.EmployeeId (the
/// EmployeeId claim, Batch B2) rather than a route parameter — a user can never pass someone else's
/// id to see their profile through this query.
/// </summary>
public sealed record GetMyEmployeeProfileQuery : IRequest<EmployeeDetailDto>;

public sealed class GetMyEmployeeProfileQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetMyEmployeeProfileQuery, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(GetMyEmployeeProfileQuery request, CancellationToken cancellationToken)
    {
        var employeeId = currentCompanyContext.EmployeeId
            ?? throw new BusinessRuleException("HR-NO-LINKED-EMPLOYEE", "حسابك مش مربوط بأي موظف.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        var personalData = await db.EmployeePersonalDataRows.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId, cancellationToken);

        return new EmployeeDetailDto
        {
            Id = employee.Id, Code = employee.Code, NameAr = employee.NameAr, NameEn = employee.NameEn, IsActive = employee.IsActive,
            BranchId = employee.BranchId, OrgUnitId = employee.OrgUnitId, JobPositionId = employee.JobPositionId, JobGradeId = employee.JobGradeId,
            ManagerId = employee.ManagerId, UserId = employee.UserId, HireDate = employee.HireDate, EmploymentType = employee.EmploymentType,
            Status = employee.Status, CostCenterDimensionValueId = employee.CostCenterDimensionValueId, TerminationDate = employee.TerminationDate,
            PersonalData = personalData is null ? null : new EmployeePersonalDataDto
            {
                Id = personalData.Id, NationalIdLast4 = personalData.NationalIdLast4, BankIbanLast4 = personalData.BankIbanLast4,
                BankName = personalData.BankName, BankId = personalData.BankId, NationalityId = personalData.NationalityId,
                CityId = personalData.CityId, MilitaryStatusId = personalData.MilitaryStatusId, QualificationTypeId = personalData.QualificationTypeId,
                BirthDate = personalData.BirthDate, Gender = personalData.Gender, MaritalStatus = personalData.MaritalStatus,
                Address = personalData.Address, PhoneNumber = personalData.PhoneNumber, PersonalEmail = personalData.PersonalEmail,
                EmergencyContactName = personalData.EmergencyContactName, EmergencyContactPhone = personalData.EmergencyContactPhone,
                EmergencyContactRelationshipTypeId = personalData.EmergencyContactRelationshipTypeId
            }
        };
    }
}
