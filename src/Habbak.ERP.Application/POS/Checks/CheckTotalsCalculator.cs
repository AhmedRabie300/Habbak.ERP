using Habbak.ERP.Domain.POS;

namespace Habbak.ERP.Application.POS.Checks;

/// <summary>
/// Service charge, VAT and the figure the cashier actually collects — shared between the cart
/// preview (GetCheckByIdQuery) and the charge itself (CompleteCheckPaymentCommand), for the same
/// reason ManualDiscountCalculator and LoyaltyRedemptionCalculator are shared: two copies of this
/// arithmetic drift, and the moment they do every payment is rejected with
/// POS-PAYMENT-AMOUNT-MISMATCH.
///
/// Until this existed only the payment command knew these figures, so GET /pos/checks/{id} reported
/// a total that was never the amount due. The frontend compensated by recomputing them itself; any
/// other consumer of the API silently got it wrong.
///
/// VAT is charged on the net of the lines after discounts only — not on the service charge and not
/// on the tip — matching Coffee_ERP_Full_System_Mockup.html's own `tax = afterDiscount * 0.14`.
/// </summary>
internal static class CheckTotalsCalculator
{
    public readonly record struct Result(decimal ServiceChargeAmount, decimal TaxAmount, decimal Total);

    public static Result Calculate(decimal netAfterDiscount, BranchPOSSettings settings, decimal tipAmount = 0m)
    {
        var serviceChargeAmount = settings.ServiceChargeEnabled
            ? Math.Round(netAfterDiscount * settings.ServiceChargeRate / 100m, 2)
            : 0m;

        var taxAmount = settings.VatEnabled
            ? Math.Round(netAfterDiscount * settings.VatRate / 100m, 2)
            : 0m;

        var total = Math.Round(netAfterDiscount + serviceChargeAmount + tipAmount + taxAmount, 2);

        return new Result(serviceChargeAmount, taxAmount, total);
    }
}
