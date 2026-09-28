using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Banks.Commands.CreateBank;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. System-wide (Bank.cs doc) — no CompanyId, unique on Code alone. CountryId is optional (Bank.cs).</summary>
public sealed record CreateBankCommand(string? Code, string NameAr, string NameEn, string? SwiftCode, string? Address, long? CountryId) : IRequest<long>;

public sealed class CreateBankCommandValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SwiftCode).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class CreateBankCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateBankCommand, long>
{
    public async Task<long> Handle(CreateBankCommand request, CancellationToken cancellationToken)
    {
        if (request.CountryId is not null && !await db.Countries.AnyAsync(c => c.Id == request.CountryId, cancellationToken))
        {
            throw new NotFoundException(nameof(Country), request.CountryId.Value);
        }

        var code = await codeGenerator.ResolveCodeAsync("SETTINGS_BANKS", request.Code, cancellationToken);

        var codeExists = await db.Banks.AnyAsync(b => b.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("ORG-BANK-CODE-EXISTS", "يوجد بنك آخر بنفس الكود بالفعل.");
        }

        var bank = new Bank
        {
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            SwiftCode = request.SwiftCode,
            Address = request.Address,
            CountryId = request.CountryId,
            IsActive = true
        };

        db.Banks.Add(bank);
        await db.SaveChangesAsync(cancellationToken);

        return bank.Id;
    }
}
