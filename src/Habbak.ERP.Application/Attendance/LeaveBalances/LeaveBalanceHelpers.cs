using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveBalances;

/// <summary>مُستخدَمة من CreateLeaveRequestCommand وLeaveRequestApprovalOutcomeHandler وAdjustLeaveBalanceCommand —
/// نفس منطق "هات الرصيد لو موجود، أو ابنيه بصفر لو أول مرة" في مكان واحد.</summary>
public static class LeaveBalanceHelpers
{
    public static async Task<LeaveBalance> FindOrCreateAsync(
        IApplicationDbContext db, long employeeId, long leaveTypeId, int year, long? companyId, CancellationToken cancellationToken)
    {
        var balance = await db.LeaveBalances.FirstOrDefaultAsync(
            b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, cancellationToken);

        if (balance is not null)
        {
            return balance;
        }

        balance = new LeaveBalance { CompanyId = companyId, EmployeeId = employeeId, LeaveTypeId = leaveTypeId, Year = year };
        db.LeaveBalances.Add(balance);
        return balance;
    }
}
