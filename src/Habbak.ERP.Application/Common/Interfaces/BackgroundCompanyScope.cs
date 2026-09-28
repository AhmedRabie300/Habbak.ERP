namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// The company a background job is working for. Outside a request there are no claims to read the
/// company from, so the daily jobs (depreciation, preventive maintenance) set it here around the work
/// they do for each company, and ICurrentCompanyContext reports it — with no branch limit and the
/// system (user 0) as the acting user. Async-local: it flows into everything awaited inside the
/// scope and never leaks into another request or another company's run.
/// </summary>
public static class BackgroundCompanyScope
{
    private static readonly AsyncLocal<long?> Current = new();

    public static long? CompanyId => Current.Value;

    public static IDisposable Begin(long companyId)
    {
        var previous = Current.Value;
        Current.Value = companyId;
        return new Restore(previous);
    }

    private sealed class Restore(long? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
