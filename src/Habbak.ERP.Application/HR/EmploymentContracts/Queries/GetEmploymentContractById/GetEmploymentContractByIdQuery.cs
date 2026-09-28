using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmploymentContracts.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Queries.GetEmploymentContractById;

public sealed record GetEmploymentContractByIdQuery(long Id) : IRequest<EmploymentContractDto>;

public sealed class GetEmploymentContractByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmploymentContractByIdQuery, EmploymentContractDto>
{
    public async Task<EmploymentContractDto> Handle(GetEmploymentContractByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.EmploymentContracts
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new EmploymentContractDto
            {
                Id = c.Id, EmployeeId = c.EmployeeId, BranchId = c.BranchId, ContractType = c.ContractType,
                StartDate = c.StartDate, EndDate = c.EndDate, ProbationEndDate = c.ProbationEndDate,
                BasicSalary = c.BasicSalary, InsurableWage = c.InsurableWage, WorkingHoursPerDay = c.WorkingHoursPerDay,
                Status = c.Status, PreviousContractId = c.PreviousContractId, ApprovalInstanceId = c.ApprovalInstanceId,
                AttachmentId = c.AttachmentId,
                Lines = c.Lines.OrderBy(l => l.Order).ThenBy(l => l.Id).Select(l => new ContractLineDto
                {
                    Id = l.Id, NameAr = l.NameAr, NameEn = l.NameEn, Amount = l.Amount,
                    Type = l.Type, IsTaxable = l.IsTaxable, IsInsurable = l.IsInsurable, Order = l.Order
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.Id);
    }
}
