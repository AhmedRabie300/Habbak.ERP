using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeDocuments.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocuments.Queries.GetEmployeeDocumentsList;

/// <summary>Not paginated: one employee's own document list is always small (unlike GetEmploymentContractsListQuery, which the plan explicitly asked to paginate).</summary>
public sealed record GetEmployeeDocumentsListQuery(long EmployeeId) : IRequest<IReadOnlyList<EmployeeDocumentDto>>;

public sealed class GetEmployeeDocumentsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeDocumentsListQuery, IReadOnlyList<EmployeeDocumentDto>>
{
    public async Task<IReadOnlyList<EmployeeDocumentDto>> Handle(GetEmployeeDocumentsListQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.EmployeeId == request.EmployeeId)
            .OrderByDescending(d => d.IssueDate)
            .Select(d => new EmployeeDocumentDto
            {
                Id = d.Id, EmployeeId = d.EmployeeId, BranchId = d.BranchId, EmployeeDocumentTypeId = d.EmployeeDocumentTypeId,
                IssueDate = d.IssueDate, ExpiryDate = d.ExpiryDate, AttachmentId = d.AttachmentId, DocumentNumber = d.DocumentNumber
            })
            .ToListAsync(cancellationToken);
    }
}
