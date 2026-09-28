using Habbak.ERP.Application.Common.Interfaces;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Delegates to the system-wide ICodeGenerator (screen "ACCOUNTING_JOURNAL_ENTRIES") instead of a
/// bespoke yearly-sequence query — companyId/year are unused now (the active company is resolved
/// from ICurrentCompanyContext inside ICodeGenerator) but kept in the interface to avoid touching
/// every caller. NOTE: this drops the old per-year reset (JV-2026-00001 restarting at 2027) in
/// favor of one ever-incrementing sequence per company — flagged as a deliberate trade-off.
/// </summary>
public class JournalEntryNumberGenerator(ICodeGenerator codeGenerator) : IJournalEntryNumberGenerator
{
    public Task<string> GenerateAsync(long companyId, int year, CancellationToken cancellationToken = default) =>
        codeGenerator.ResolveCodeAsync("ACCOUNTING_JOURNAL_ENTRIES", manualCode: null, cancellationToken);
}
