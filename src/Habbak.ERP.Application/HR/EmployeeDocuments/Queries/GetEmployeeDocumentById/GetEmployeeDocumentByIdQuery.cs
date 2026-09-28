using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeDocuments.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocuments.Queries.GetEmployeeDocumentById;

public sealed record GetEmployeeDocumentByIdQuery(long Id) : IRequest<EmployeeDocumentDto>;

public sealed class GetEmployeeDocumentByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmployeeDocumentByIdQuery, EmployeeDocumentDto>
{
    public async Task<EmployeeDocumentDto> Handle(GetEmployeeDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.Id == request.Id)
            .Select(d => new EmployeeDocumentDto
            {
                Id = d.Id, EmployeeId = d.EmployeeId, BranchId = d.BranchId, EmployeeDocumentTypeId = d.EmployeeDocumentTypeId,
                IssueDate = d.IssueDate, ExpiryDate = d.ExpiryDate, AttachmentId = d.AttachmentId, DocumentNumber = d.DocumentNumber
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocument), request.Id);
    }
}
