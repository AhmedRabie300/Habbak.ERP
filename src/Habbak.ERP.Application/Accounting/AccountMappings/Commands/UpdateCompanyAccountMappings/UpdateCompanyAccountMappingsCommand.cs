using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountMappings.Commands.UpdateCompanyAccountMappings;

/// <param name="AccountId">Null clears the role.</param>
public sealed record AccountMappingInput(CompanyAccountRole Role, long? AccountId);

/// <summary>
/// Sets or clears the given roles and leaves every other role exactly as it was.
///
/// Partial on purpose. The other settings endpoints in this codebase replace their whole row, and
/// that has already reset unrelated switches once (BranchPOSSettings during the 2026-09-13 review).
/// With fourteen accounts a finance manager may fill in over several sittings, a request that only
/// names two roles must not wipe the other twelve.
/// </summary>
public sealed record UpdateCompanyAccountMappingsCommand(IReadOnlyList<AccountMappingInput> Mappings) : IRequest;

public sealed class UpdateCompanyAccountMappingsCommandValidator : AbstractValidator<UpdateCompanyAccountMappingsCommand>
{
    public UpdateCompanyAccountMappingsCommandValidator()
    {
        RuleFor(x => x.Mappings).NotEmpty();

        RuleForEach(x => x.Mappings).ChildRules(m => m.RuleFor(x => x.Role).IsInEnum());

        RuleFor(x => x.Mappings)
            .Must(ms => ms.Select(m => m.Role).Distinct().Count() == ms.Count)
            .WithMessage("كل دور محاسبي يظهر مرة واحدة بس في الطلب.");
    }
}

public sealed class UpdateCompanyAccountMappingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateCompanyAccountMappingsCommand>
{
    public async Task Handle(UpdateCompanyAccountMappingsCommand request, CancellationToken cancellationToken)
    {
        var accountIds = request.Mappings
            .Where(m => m.AccountId.HasValue)
            .Select(m => m.AccountId!.Value)
            .Distinct()
            .ToList();

        // The global query filter scopes this to accounts the current company can see (its own plus
        // shared ones), so another company's account reads as not found rather than leaking.
        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var input in request.Mappings.Where(m => m.AccountId.HasValue))
        {
            Validate(input.Role, input.AccountId!.Value, accounts);
        }

        var roles = request.Mappings.Select(m => m.Role).ToList();
        var existing = await db.CompanyAccountMappings
            .Where(m => roles.Contains(m.Role))
            .ToDictionaryAsync(m => m.Role, cancellationToken);

        foreach (var input in request.Mappings)
        {
            existing.TryGetValue(input.Role, out var row);

            if (input.AccountId is not { } accountId)
            {
                if (row is not null)
                {
                    db.CompanyAccountMappings.Remove(row);
                }
                continue;
            }

            if (row is null)
            {
                db.CompanyAccountMappings.Add(new CompanyAccountMapping
                {
                    CompanyId = currentCompanyContext.CompanyId,
                    Role = input.Role,
                    AccountId = accountId
                });
            }
            else
            {
                row.AccountId = accountId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(CompanyAccountRole role, long accountId, IReadOnlyDictionary<long, Account> accounts)
    {
        if (!accounts.TryGetValue(accountId, out var account))
        {
            throw new BusinessRuleException("ACC-MAPPING-ACCOUNT-NOT-FOUND", $"الحساب رقم {accountId} غير موجود.");
        }

        if (!account.IsPostable)
        {
            throw new BusinessRuleException(
                "ACC-MAPPING-ACCOUNT-NOT-POSTABLE",
                $"الحساب {account.Code} حساب تجميعي — لازم تختار حساب فرعي يقبل الترحيل.");
        }

        if (!account.IsActive)
        {
            throw new BusinessRuleException("ACC-MAPPING-ACCOUNT-INACTIVE", $"الحساب {account.Code} غير نشط.");
        }

        var expected = role.ExpectedAccountType();
        if (account.AccountType != expected)
        {
            throw new BusinessRuleException(
                "ACC-MAPPING-ACCOUNT-TYPE-MISMATCH",
                $"الحساب {account.Code} من نوع {TypeName(account.AccountType)} — والدور ده محتاج حساب من نوع {TypeName(expected)}.");
        }
    }

    private static string TypeName(AccountType type) => type switch
    {
        AccountType.Asset => "أصول",
        AccountType.Liability => "خصوم",
        AccountType.Equity => "حقوق ملكية",
        AccountType.Revenue => "إيرادات",
        AccountType.Expense => "مصروفات",
        _ => type.ToString()
    };
}
