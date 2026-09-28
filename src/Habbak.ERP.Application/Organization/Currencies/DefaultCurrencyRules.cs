using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Currencies;

/// <summary>
/// Exactly one default currency (Remarks3, item 1): the one every new record starts with. Making a
/// currency the default takes the flag off the previous one; the default itself cannot be switched
/// off, deactivated or deleted — another currency has to become the default first.
/// </summary>
public static class DefaultCurrencyRules
{
    /// <summary>
    /// Moves the default to <paramref name="currency"/>. Two saves in one transaction: the old
    /// default is cleared before the new one is set, or the unique index would refuse the swap.
    /// </summary>
    public static async Task MakeDefaultAsync(IApplicationDbContext db, Currency currency, CancellationToken cancellationToken)
    {
        if (!currency.IsActive)
        {
            throw new BusinessRuleException("ORG-CURRENCY-DEFAULT-INACTIVE", "العملة الافتراضية لازم تكون فعّالة.");
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        foreach (var other in await db.Currencies.Where(c => c.IsDefault && c.Id != currency.Id).ToListAsync(cancellationToken))
        {
            other.IsDefault = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        currency.IsDefault = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static void EnsureNotRemovingDefault(Currency currency, bool keepDefault, bool keepActive)
    {
        if (currency.IsDefault && !keepDefault)
        {
            throw new BusinessRuleException(
                "ORG-CURRENCY-DEFAULT-REQUIRED", "لازم يكون فيه عملة افتراضية — اختار عملة تانية كافتراضية الأول وبعدين الغي دي.");
        }

        if (currency.IsDefault && !keepActive)
        {
            throw new BusinessRuleException("ORG-CURRENCY-DEFAULT-INACTIVE", "مينفعش توقف العملة الافتراضية — اختار عملة تانية كافتراضية الأول.");
        }
    }
}
