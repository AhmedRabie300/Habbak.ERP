namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Resolves the Code/Number for a "create new record" screen against that screen's CodingRule
/// (manual vs automatic, format, prefix, sequence length) — the single mechanism every
/// code-bearing entity in the system now goes through, replacing the previously bespoke
/// per-module generators (00-System-Wide-Corrections-01.md-adjacent request).
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Automatic mode: generates and returns the next code (ignores manualCode). Manual mode:
    /// returns manualCode as-is, throwing BusinessRuleException("CODE-REQUIRED", ...) if it's
    /// missing. Does not call SaveChangesAsync — the caller's own save persists both the new
    /// entity and this rule's incremented sequence together (same pattern as IPostingService).
    /// </summary>
    Task<string> ResolveCodeAsync(string screenCode, string? manualCode, CancellationToken cancellationToken = default);

    /// <summary>Whether the given screen is currently configured for automatic numbering — lets a
    /// create-form decide whether to show a manual Code input.</summary>
    Task<bool> IsAutomaticAsync(string screenCode, CancellationToken cancellationToken = default);
}
