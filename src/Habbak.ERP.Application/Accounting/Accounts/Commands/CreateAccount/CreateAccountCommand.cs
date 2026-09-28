using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Accounts.Commands.CreateAccount;

/// <summary>Creates an account in the Chart of Accounts tree (01-Module-Accounting.md, section 5, screen 1).
/// Code is optional: null/blank when the "ACCOUNTING_CHART_OF_ACCOUNTS" CodingRule is automatic,
/// required otherwise (enforced by ICodeGenerator, not this validator, since that depends on data).</summary>
public sealed record CreateAccountCommand(
    string? Code, string NameAr, string NameEn, long? ParentId, AccountType AccountType, AccountNature Nature,
    bool IsPostable, string? CurrencyCode, bool IsSharedAcrossCompanies) : IRequest<long>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).Length(3).When(x => x.CurrencyCode is not null);
    }
}

public sealed class CreateAccountCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateAccountCommand, long>
{
    public async Task<long> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        // Rule 12 / section 2.1: CompanyId is null only for accounts shared across every company.
        var companyId = request.IsSharedAcrossCompanies ? (long?)null : currentCompanyContext.CompanyId;

        var code = await codeGenerator.ResolveCodeAsync("ACCOUNTING_CHART_OF_ACCOUNTS", request.Code, cancellationToken);

        // The Global Query Filter already scopes visible rows to "my company OR shared", so no
        // need to IgnoreQueryFilters — just distinguish which of those two buckets to check.
        var codeExists = request.IsSharedAcrossCompanies
            ? await db.Accounts.AnyAsync(a => a.Code == code && a.CompanyId == null, cancellationToken)
            : await db.Accounts.AnyAsync(a => a.Code == code && a.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("ACC-ACCOUNT-CODE-EXISTS", "يوجد حساب آخر بنفس الكود بالفعل.");
        }

        int level = 0;
        if (request.ParentId is not null)
        {
            var parent = await db.Accounts.FindAsync([request.ParentId.Value], cancellationToken)
                ?? throw new NotFoundException(nameof(Account), request.ParentId.Value);
            level = parent.Level + 1;
        }

        var account = new Account
        {
            CompanyId = companyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ParentId = request.ParentId,
            Level = level,
            AccountType = request.AccountType,
            Nature = request.Nature,
            IsPostable = request.IsPostable,
            CurrencyCode = request.CurrencyCode,
            IsActive = true,
            IsSharedAcrossCompanies = request.IsSharedAcrossCompanies
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}
