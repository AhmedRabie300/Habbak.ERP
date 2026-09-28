namespace Habbak.ERP.Application.POS.Checks;

/// <summary>مراجعة 2026-09-13، قاعدة 12 — حساب مشترك بين معاينة السلة (GetCheckByIdQuery) وإتمام
/// الدفع (CompleteCheckPaymentCommand)، بنفس مبدأ ManualDiscountCalculator. القيمة تُقصّ عند
/// (الصافي بعد الخصم اليدوي) — الخصمين مع بعض ميقدروش يخلّوا الفاتورة بالسالب، والخصم اليدوي دايمًا
/// يُطبَّق أولًا (ترتيب مُتَّفَق عليه: خصم يدوي ثم نقاط ولاء، الاتنين قبل الخدمة والضريبة).</summary>
internal static class LoyaltyRedemptionCalculator
{
    public static decimal Calculate(decimal? pointsToRedeem, decimal redemptionValuePerPoint, decimal remainingAfterManualDiscount)
    {
        if (pointsToRedeem is null or <= 0)
        {
            return 0m;
        }

        var raw = pointsToRedeem.Value * redemptionValuePerPoint;
        return Math.Round(Math.Clamp(raw, 0m, remainingAfterManualDiscount), 2);
    }
}
