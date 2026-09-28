using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Users;

/// <summary>
/// Checks for a user id that arrives in a request (a cashier picked for a shift, a user assigned to a
/// terminal) rather than from the signed-in session: the foreign key would refuse an unknown id
/// anyway, but with a database error instead of a message.
/// </summary>
public static class CompanyUsers
{
    public static async Task EnsureWorksHereAsync(IApplicationDbContext db, long userId, long companyId, CancellationToken cancellationToken)
    {
        var ok = userId != User.SystemUserId
                 && await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, cancellationToken)
                 && await db.UserScopes.IgnoreQueryFilters().AnyAsync(
                     s => s.UserId == userId && s.CompanyId == companyId && s.IsActive && !s.IsDeleted, cancellationToken);

        if (!ok)
        {
            throw new BusinessRuleException("SET-USER-NOT-IN-COMPANY", "المستخدم ده مش موجود أو مش شغّال أو مالوش صلاحية دخول على الشركة دي.");
        }
    }
}

public sealed record UserLookupDto(long Id, string Username, string FullName, bool IsActive);

/// <summary>
/// Everyone with access to the current company — for showing names instead of ids ("closed by",
/// "cashier") and for picking a cashier. Readable by any signed-in user; carries nothing beyond names.
/// </summary>
public sealed record GetUsersLookupQuery : MediatR.IRequest<IReadOnlyList<UserLookupDto>>;

public sealed class GetUsersLookupQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : MediatR.IRequestHandler<GetUsersLookupQuery, IReadOnlyList<UserLookupDto>>
{
    public async Task<IReadOnlyList<UserLookupDto>> Handle(GetUsersLookupQuery request, CancellationToken cancellationToken)
    {
        var companyId = current.CompanyId;
        return await db.Users.AsNoTracking()
            .Where(u => u.Id != User.SystemUserId
                        && db.UserScopes.IgnoreQueryFilters().Any(s => s.UserId == u.Id && s.CompanyId == companyId && !s.IsDeleted))
            .OrderBy(u => u.FullName)
            .Select(u => new UserLookupDto(u.Id, u.Username, u.FullName, u.Status == UserStatus.Active))
            .ToListAsync(cancellationToken);
    }
}
