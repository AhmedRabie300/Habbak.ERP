using Habbak.ERP.Domain.POS;

namespace Habbak.ERP.Application.POS.Checks;

/// <summary>مراجعة 2026-09-13 — حساب واحد مشترك بين معاينة السلة (GetCheckByIdQuery) وإتمام الدفع
/// (CompleteCheckPaymentCommand) عشان القيمتين ميتفرقوش أبدًا. Percentage تُحسب على صافي البنود بعد
/// خصم البنود (netAfterLineDiscount)؛ Fixed تُقصّ عند نفس القيمة لو تجاوزتها (مينفعش خصم يدوي يخلي
/// الفاتورة بالسالب).</summary>
internal static class ManualDiscountCalculator
{
    public static decimal Calculate(POSManualDiscountType? type, decimal? value, decimal netAfterLineDiscount)
    {
        if (type is null || value is null)
        {
            return 0m;
        }

        var raw = type == POSManualDiscountType.Percentage
            ? netAfterLineDiscount * value.Value / 100m
            : value.Value;

        return Math.Round(Math.Clamp(raw, 0m, netAfterLineDiscount), 2);
    }
}
