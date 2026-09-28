using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Accounts.Commands.UpdateAccount;

public sealed record UpdateAccountCommand(
    long Id, string RowVersion, string NameAr, string NameEn, AccountType AccountType, AccountNature Nature,
    bool IsPostable, string? CurrencyCode, bool IsActive) : IRequest;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateAccountCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAccountCommand>
{
    public async Task Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.Id);

        var changesSensitiveField = account.AccountType != request.AccountType
            || account.Nature != request.Nature
            || (account.IsPostable && !request.IsPostable);

        if (changesSensitiveField)
        {
            var hasPostedLines = await db.JournalEntryLines
                .AnyAsync(l => l.AccountId == account.Id && l.JournalEntry.Status == JournalEntryStatus.Posted, cancellationToken);

            // Rule 20: changing AccountType/Nature, or IsPostable true->false, after the account
            // already has posted movements requires an approval workflow — not yet built
            // (00-Project-Overview.md, section 12), so the change is refused outright for now
            // rather than silently allowed or fake-approved.
            if (hasPostedLines)
            {
                throw new BusinessRuleException(
                    "ACC-R20-APPROVAL-REQUIRED",
                    "هذا التعديل يمس حساب له حركات مرحّلة بالفعل ويتطلب سلسلة موافقات إدارية — غير متاح حاليًا (المحرك لسه مبنيش).");
            }
        }

        db.Entry(account).Property(nameof(Account.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        account.NameAr = request.NameAr;
        account.NameEn = request.NameEn;
        account.AccountType = request.AccountType;
        account.Nature = request.Nature;
        account.IsPostable = request.IsPostable;
        account.CurrencyCode = request.CurrencyCode;
        account.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
