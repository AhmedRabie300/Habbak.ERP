using Habbak.ERP.Domain.Posting;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// The fixed formula and condition sets of spec section 4. Adding one is a code change with its own
/// test, never a setting — a typo in a free-text expression would only surface when real money was
/// being posted.
/// </summary>
internal static class PostingFormulas
{
    public static decimal EvaluateAmount(PostingTemplateLine line, PostingContext context)
    {
        var raw = line.AmountFormulaType switch
        {
            AmountFormulaType.DirectField => context.GetDecimal(line.AmountFieldName!),
            AmountFormulaType.SumLineQuantityTimesUnitPrice => context.Lines.Sum(l => l.Quantity * l.UnitPrice),
            AmountFormulaType.SumLineQuantityTimesUnitCost => context.Lines.Sum(l => l.Quantity * l.UnitCost),
            AmountFormulaType.SubtotalMinusDiscount => context.GetDecimal("Subtotal") - context.GetDecimal("DiscountAmount"),

            // An explicit sum rather than the spec's rejected "BalancingAmount": a plug figure would
            // make a wrong entry balance and hide the mistake it was supposed to reveal.
            AmountFormulaType.SumShiftVarianceLiability => context.GetDecimal("MinorShortage") + context.GetDecimal("MajorShortage"),

            // Expanded item by item by the engine itself; there is no single amount to return.
            AmountFormulaType.GroupItemAmount => throw new InvalidOperationException("Group lines are expanded by the engine."),

            AmountFormulaType.Multiply => context.GetDecimal(line.AmountFieldName!) * line.AmountMultiplier!.Value,
            AmountFormulaType.PercentageOf => context.GetDecimal(line.AmountFieldName!) * line.AmountPercentage!.Value / 100m,
            AmountFormulaType.AddFields => AmountFields(line).Sum(context.GetDecimal),
            AmountFormulaType.SubtractFields => context.GetDecimal(AmountFields(line)[0]) - context.GetDecimal(AmountFields(line)[1]),
            AmountFormulaType.DivideFields => Divide(context, AmountFields(line)),

            _ => throw new NotSupportedException($"AmountFormulaType {line.AmountFormulaType}")
        };

        return Math.Round(raw, 2);
    }

    /// <summary>The fields of a multi-field formula, in the order the template lists them.</summary>
    public static string[] AmountFields(PostingTemplateLine line) =>
        (line.AmountFieldNames ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static decimal Divide(PostingContext context, string[] fields)
    {
        var divisor = context.GetDecimal(fields[1]);
        if (divisor == 0)
        {
            // Not a zero amount quietly skipped by the positive-amount check: a divisor of zero means
            // the document is missing something the template relies on, and that has to be said.
            throw new Common.Exceptions.BusinessRuleException(
                "POST-DIVIDE-BY-ZERO", $"القسمة على الحقل '{fields[1]}' وقيمته صفر.");
        }

        return context.GetDecimal(fields[0]) / divisor;
    }

    /// <summary>
    /// False means "skip this line", a deliberate outcome and not an error (spec 5.1).
    ///
    /// A condition naming a field the document never supplies is treated as an error, though: that is
    /// almost always a misspelt field name, and quietly skipping the line would drop an amount from
    /// the entry without anyone noticing. FieldNotNull is the one condition whose job is to ask
    /// whether a value is there, so for it a missing field is simply false.
    /// </summary>
    public static bool EvaluateCondition(PostingTemplateLine line, PostingContext context)
    {
        if (line.ConditionType == ConditionType.None)
        {
            return true;
        }

        var name = line.ConditionFieldName!;

        if (line.ConditionType == ConditionType.FieldNotNull)
        {
            return context.GetValue(name) is not null;
        }

        if (!context.HasField(name))
        {
            throw PostingErrors.MissingField(name);
        }

        var value = context.GetValue(name);

        return line.ConditionType switch
        {
            ConditionType.FieldGreaterThanZero => PostingContext.ToDecimal(name, value) is > 0,
            ConditionType.FieldEquals => value is not null
                && string.Equals(
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    line.ConditionFieldValue,
                    StringComparison.OrdinalIgnoreCase),
            _ => throw new NotSupportedException($"ConditionType {line.ConditionType}")
        };
    }
}
