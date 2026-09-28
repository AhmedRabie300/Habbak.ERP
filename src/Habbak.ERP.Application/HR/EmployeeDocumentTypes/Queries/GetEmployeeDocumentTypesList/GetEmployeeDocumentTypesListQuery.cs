using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Queries.GetEmployeeDocumentTypesList;

/// <summary>Not a paginated GetList screen: a company's document-type catalog is always small.</summary>
public sealed record GetEmployeeDocumentTypesListQuery : IRequest<IReadOnlyList<EmployeeDocumentTypeDto>>;

public sealed class GetEmployeeDocumentTypesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeDocumentTypesListQuery, IReadOnlyList<EmployeeDocumentTypeDto>>
{
    public async Task<IReadOnlyList<EmployeeDocumentTypeDto>> Handle(GetEmployeeDocumentTypesListQuery request, CancellationToken cancellationToken)
    {
        return await db.EmployeeDocumentTypes
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new EmployeeDocumentTypeDto { Id = t.Id, Code = t.Code, NameAr = t.NameAr, NameEn = t.NameEn, IsActive = t.IsActive, RequiresExpiry = t.RequiresExpiry, IsMandatory = t.IsMandatory, ExpiryAlertDays = t.ExpiryAlertDays })
            .ToListAsync(cancellationToken);
    }
}
