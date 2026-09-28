using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Queries.GetEmployeeDocumentTypeById;

public sealed record GetEmployeeDocumentTypeByIdQuery(long Id) : IRequest<EmployeeDocumentTypeDto>;

public sealed class GetEmployeeDocumentTypeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmployeeDocumentTypeByIdQuery, EmployeeDocumentTypeDto>
{
    public async Task<EmployeeDocumentTypeDto> Handle(GetEmployeeDocumentTypeByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeDocumentTypes
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new EmployeeDocumentTypeDto { Id = t.Id, Code = t.Code, NameAr = t.NameAr, NameEn = t.NameEn, IsActive = t.IsActive, RequiresExpiry = t.RequiresExpiry, IsMandatory = t.IsMandatory, ExpiryAlertDays = t.ExpiryAlertDays })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocumentType), request.Id);
    }
}
