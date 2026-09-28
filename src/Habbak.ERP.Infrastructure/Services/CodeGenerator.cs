using System.Text;
using Habbak.ERP.Application.Common.Coding;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// KNOWN LIMITATION: incrementing CodingRule.LastSequence has the same "max + 1" race window
/// documented on the generators this replaces — a proper fix is a dedicated concurrency-safe
/// numbering service, needed system-wide, not just here.
/// </summary>
public class CodeGenerator(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext) : ICodeGenerator
{
    public async Task<string> ResolveCodeAsync(string screenCode, string? manualCode, CancellationToken cancellationToken = default)
    {
        // The tracked copy first: a rule materialized earlier in this same unit of work is not in the
        // database yet, and a second code for the screen before the save (a shift's sales entry and
        // its variance entry, say) would otherwise add the rule twice and hand out the same number.
        var rule = db.CodingRules.Local.FirstOrDefault(
                r => r.ScreenCode == screenCode && r.CompanyId == currentCompanyContext.CompanyId)
            ?? await db.CodingRules.FirstOrDefaultAsync(
                r => r.ScreenCode == screenCode && r.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        var def = ScreenCodeCatalog.Find(screenCode);
        var isAutomatic = rule?.IsAutomatic ?? def?.DefaultIsAutomatic ?? false;

        if (!isAutomatic)
        {
            if (string.IsNullOrWhiteSpace(manualCode))
            {
                throw new BusinessRuleException("CODE-REQUIRED", "الكود إلزامي في وضع الترقيم اليدوي لهذه الشاشة.");
            }

            return manualCode;
        }

        // Materialize the rule row on first automatic use, from the screen's built-in default,
        // so its LastSequence has somewhere to live.
        rule ??= new CodingRule
        {
            CompanyId = currentCompanyContext.CompanyId,
            ScreenCode = screenCode,
            IsAutomatic = true,
            Format = def?.DefaultFormat ?? CodeFormat.LettersAndNumbers,
            Prefix = def?.DefaultPrefix,
            SequenceLength = def?.DefaultSequenceLength ?? 5,
            LastSequence = 0
        };

        if (db.Entry(rule).State == EntityState.Detached)
        {
            db.CodingRules.Add(rule);
        }

        rule.LastSequence += 1;
        return BuildCode(rule);
    }

    public async Task<bool> IsAutomaticAsync(string screenCode, CancellationToken cancellationToken = default)
    {
        var rule = await db.CodingRules.AsNoTracking().FirstOrDefaultAsync(
            r => r.ScreenCode == screenCode && r.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        return rule?.IsAutomatic ?? ScreenCodeCatalog.Find(screenCode)?.DefaultIsAutomatic ?? false;
    }

    private static string BuildCode(CodingRule rule)
    {
        var padded = rule.LastSequence.ToString().PadLeft(rule.SequenceLength, '0');

        return rule.Format switch
        {
            CodeFormat.NumbersOnly => padded,
            CodeFormat.LettersOnly => $"{rule.Prefix}{ToAlphabetic(rule.LastSequence)}",
            CodeFormat.LettersAndNumbers => string.IsNullOrEmpty(rule.Prefix) ? padded : $"{rule.Prefix}-{padded}",
            _ => padded
        };
    }

    /// <summary>1 -&gt; A, 2 -&gt; B, ..., 26 -&gt; Z, 27 -&gt; AA, ... — the auto-incrementing part when
    /// the configured format is "letters only" (the prefix stays the fixed, user-chosen start).</summary>
    private static string ToAlphabetic(long n)
    {
        var sb = new StringBuilder();
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + (n % 26)));
            n /= 26;
        }
        return sb.Length == 0 ? "A" : sb.ToString();
    }
}
