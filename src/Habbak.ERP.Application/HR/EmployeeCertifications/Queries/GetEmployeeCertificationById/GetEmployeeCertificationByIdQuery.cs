using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeCertifications.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Queries.GetEmployeeCertificationById;

public sealed record GetEmployeeCertificationByIdQuery(long Id) : IRequest<EmployeeCertificationDto>;

public sealed class GetEmployeeCertificationByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmployeeCertificationByIdQuery, EmployeeCertificationDto>
{
    public async Task<EmployeeCertificationDto> Handle(GetEmployeeCertificationByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeCertifications
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new EmployeeCertificationDto
            {
                Id = c.Id, EmployeeId = c.EmployeeId, BranchId = c.BranchId, NameAr = c.NameAr, NameEn = c.NameEn,
                Issuer = c.Issuer, IssueDate = c.IssueDate, ExpiryDate = c.ExpiryDate, CertificateNumber = c.CertificateNumber
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeCertification), request.Id);
    }
}
