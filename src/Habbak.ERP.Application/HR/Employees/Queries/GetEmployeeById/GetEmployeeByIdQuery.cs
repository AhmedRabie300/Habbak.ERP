using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Employees.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Queries.GetEmployeeById;

public sealed record GetEmployeeByIdQuery(long Id) : IRequest<EmployeeDetailDto>;

public sealed class GetEmployeeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        var personalData = await db.EmployeePersonalDataRows.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EmployeeId == request.Id, cancellationToken);

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
