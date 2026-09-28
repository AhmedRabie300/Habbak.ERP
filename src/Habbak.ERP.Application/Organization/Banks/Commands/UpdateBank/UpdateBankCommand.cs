using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Banks.Commands.UpdateBank;

public sealed record UpdateBankCommand(long Id, string NameAr, string NameEn, string? SwiftCode, string? Address, long? CountryId, bool IsActive) : IRequest;

public sealed class UpdateBankCommandValidator : AbstractValidator<UpdateBankCommand>
{
    public UpdateBankCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SwiftCode).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpdateBankCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBankCommand>
{
    public async Task Handle(UpdateBankCommand request, CancellationToken cancellationToken)
    {
        var bank = await db.Banks.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Bank), request.Id);

        if (request.CountryId is not null && !await db.Countries.AnyAsync(c => c.Id == request.CountryId, cancellationToken))
        {
            throw new NotFoundException(nameof(Country), request.CountryId.Value);
        }

        bank.NameAr = request.NameAr;
        bank.NameEn = request.NameEn;
        bank.SwiftCode = request.SwiftCode;
        bank.Address = request.Address;
        bank.CountryId = request.CountryId;
        bank.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
