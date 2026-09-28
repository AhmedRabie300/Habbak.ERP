using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeCertifications.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Queries.GetEmployeeCertificationsList;

/// <summary>Not paginated: one employee's own certification list is always small.</summary>
public sealed record GetEmployeeCertificationsListQuery(long EmployeeId) : IRequest<IReadOnlyList<EmployeeCertificationDto>>;

public sealed class GetEmployeeCertificationsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeCertificationsListQuery, IReadOnlyList<EmployeeCertificationDto>>
{
    public async Task<IReadOnlyList<EmployeeCertificationDto>> Handle(GetEmployeeCertificationsListQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeCertifications
            .AsNoTracking()
            .Where(c => c.EmployeeId == request.EmployeeId)
            .OrderByDescending(c => c.IssueDate)
            .Select(c => new EmployeeCertificationDto
            {
                Id = c.Id, EmployeeId = c.EmployeeId, BranchId = c.BranchId, NameAr = c.NameAr, NameEn = c.NameEn,
                Issuer = c.Issuer, IssueDate = c.IssueDate, ExpiryDate = c.ExpiryDate, CertificateNumber = c.CertificateNumber
            })
            .ToListAsync(cancellationToken);
    }
}
