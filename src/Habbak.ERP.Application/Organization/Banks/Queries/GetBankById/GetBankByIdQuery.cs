using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Banks.Dtos;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Banks.Queries.GetBankById;

public sealed record GetBankByIdQuery(long Id) : IRequest<BankDto>;

public sealed class GetBankByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBankByIdQuery, BankDto>
{
    public async Task<BankDto> Handle(GetBankByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Banks
            .AsNoTracking()
            .Where(b => b.Id == request.Id)
            .Select(b => new BankDto { Id = b.Id, Code = b.Code, NameAr = b.NameAr, NameEn = b.NameEn, IsActive = b.IsActive, SwiftCode = b.SwiftCode, Address = b.Address, CountryId = b.CountryId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Bank), request.Id);
    }
}
