using Habbak.ERP.Application.Common.Interfaces;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// A non-HTTP-bound ICurrentCompanyContext used only when writing to the database outside a
/// request (startup seeding of system-wide reference data). The real CurrentCompanyContext reads
/// JWT claims off HttpContext by design and throws when there is no active request — this
/// substitute mirrors the equivalent test-only stand-in already used by the API test factory.
/// </summary>
internal sealed class SystemCurrentCompanyContext : ICurrentCompanyContext
{
    public long CompanyId => 0;
    public long? BranchId => null;
    public long UserId => 0;
    public long? EmployeeId => null;
}
