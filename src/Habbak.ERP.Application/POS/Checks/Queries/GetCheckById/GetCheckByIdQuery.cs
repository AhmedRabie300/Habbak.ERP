using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Checks.Dtos;
using Habbak.ERP.Application.POS.Checks;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Queries.GetCheckById;

public sealed record GetCheckByIdQuery(long Id) : IRequest<CheckDetailDto>;

public sealed class GetCheckByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCheckByIdQuery, CheckDetailDto>
{
    public async Task<CheckDetailDto> Handle(GetCheckByIdQuery request, CancellationToken cancellationToken)
    {
        var check = await db.Checks
            .AsNoTracking()
            .Include(c => c.Table)
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.Id);

        var itemIds = check.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.NameAr })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var lines = check.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new CheckLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ItemId = l.ItemId,
                ItemCode = items.TryGetValue(l.ItemId, out var item) ? item.Code : "",
                ItemNameAr = items.TryGetValue(l.ItemId, out var item2) ? item2.NameAr : "",
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.Quantity * l.UnitPrice - l.DiscountAmount,
                IsPriceManuallyOverridden = l.IsPriceManuallyOverridden,
                Note = l.Note,
                SentToKitchenAt = l.SentToKitchenAt
            })
            .ToList();

        var subtotal = check.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var discountTotal = check.Lines.Sum(l => l.DiscountAmount);
        var netAfterLineDiscount = subtotal - discountTotal;
        var manualDiscountAmount = ManualDiscountCalculator.Calculate(
            check.ManualDiscountType, check.ManualDiscountValue, netAfterLineDiscount);
        var remainingAfterManualDiscount = netAfterLineDiscount - manualDiscountAmount;

        string? customerNameAr = null;
        decimal? customerLoyaltyPointsBalance = null;
        if (check.CustomerId is { } customerId)
        {
            var customer = await db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.NameAr, c.LoyaltyPointsBalance })
                .FirstOrDefaultAsync(cancellationToken);
            customerNameAr = customer?.NameAr;
            customerLoyaltyPointsBalance = customer?.LoyaltyPointsBalance;
        }

        var loyaltyProgramSettings = await db.LoyaltyProgramSettingsRows.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var loyaltyDiscountAmount = LoyaltyRedemptionCalculator.Calculate(
            check.LoyaltyPointsToRedeem, loyaltyProgramSettings?.PointsRedemptionValue ?? 0m, remainingAfterManualDiscount);

        var netAfterDiscount = remainingAfterManualDiscount - loyaltyDiscountAmount;

        // Same defaulting as CompleteCheckPaymentCommand: a branch with no settings row charges
        // neither service charge nor VAT, so the preview must say so too.
        var posSettings = await db.BranchPOSSettingsRows.AsNoTracking()
            .FirstOrDefaultAsync(s => s.BranchId == check.BranchId, cancellationToken)
            ?? new BranchPOSSettings { BranchId = check.BranchId };

        var totals = CheckTotalsCalculator.Calculate(netAfterDiscount, posSettings);

        return new CheckDetailDto
        {
            Id = check.Id,
            RowVersion = Convert.ToBase64String(check.RowVersion),
            POSTerminalId = check.POSTerminalId,
            ShiftId = check.ShiftId,
            CheckCode = check.CheckCode,
            TableId = check.TableId,
            TableCode = check.Table?.Code,
            OrderType = check.OrderType.ToString(),
            Status = check.Status.ToString(),
            CustomerId = check.CustomerId,
            CustomerNameAr = customerNameAr,
            CustomerLoyaltyPointsBalance = customerLoyaltyPointsBalance,
            MergedIntoCheckId = check.MergedIntoCheckId,
            Subtotal = subtotal,
            DiscountTotal = discountTotal,
            ManualDiscountType = check.ManualDiscountType?.ToString(),
            ManualDiscountValue = check.ManualDiscountValue,
            ManualDiscountReason = check.ManualDiscountReason,
            ManualDiscountAmount = manualDiscountAmount,
            LoyaltyPointsToRedeem = check.LoyaltyPointsToRedeem,
            LoyaltyDiscountAmount = loyaltyDiscountAmount,
            Total = netAfterDiscount,
            ServiceChargeAmount = totals.ServiceChargeAmount,
            TaxAmount = totals.TaxAmount,
            PayableTotal = totals.Total,
            Lines = lines
        };
    }
}
