using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.InsuranceOffices.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.InsuranceOffices.Queries.GetInsuranceOfficeById;

public sealed record GetInsuranceOfficeByIdQuery(long Id) : IRequest<InsuranceOfficeDto>;

public sealed class GetInsuranceOfficeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInsuranceOfficeByIdQuery, InsuranceOfficeDto>
{
    public async Task<InsuranceOfficeDto> Handle(GetInsuranceOfficeByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.InsuranceOffices
            .AsNoTracking()
            .Where(o => o.Id == request.Id)
            .Select(o => new InsuranceOfficeDto { Id = o.Id, Code = o.Code, NameAr = o.NameAr, NameEn = o.NameEn, IsActive = o.IsActive, OfficialCode = o.OfficialCode, Address = o.Address })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(InsuranceOffice), request.Id);
    }
}
