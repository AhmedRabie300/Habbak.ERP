namespace Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Dtos;

public sealed class EmployeeDeviceMappingDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required string EmployeeCode { get; init; }
    public required string EmployeeNameAr { get; init; }
    public required long AttendanceDeviceId { get; init; }
    public required string AttendanceDeviceCode { get; init; }
    public required string DeviceUserId { get; init; }
}
