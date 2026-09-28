using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §6 (3B.5) — رفع يدوي (تصدير USB) بنفس مسار المعالجة
/// اللي بيمشي عليه Push (SourceType = FileImport، نفس RawPunch/AttendanceDeviceLog). طلب مُصادَق
/// عليه عاديًا (JWT + صلاحية HR_ATTENDANCE_DEVICES)، عكس الـPush الأوتوماتيكي — الجهاز هنا لازم
/// يكون في نفس شركة المستخدم الحالي أصلًا (فلتر الاستعلام العادي).
/// </summary>
public sealed record ImportRawPunchesFromFileCommand(long AttendanceDeviceId, string FileName, byte[] Content) : IRequest<IngestResultDto>;

public sealed class ImportRawPunchesFromFileCommandValidator : AbstractValidator<ImportRawPunchesFromFileCommand>
{
    public ImportRawPunchesFromFileCommandValidator()
    {
        RuleFor(x => x.AttendanceDeviceId).GreaterThan(0);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.Content).NotEmpty();
    }
}

public sealed class ImportRawPunchesFromFileCommandHandler(ISender mediator, IApplicationDbContext db) : IRequestHandler<ImportRawPunchesFromFileCommand, IngestResultDto>
{
    public async Task<IngestResultDto> Handle(ImportRawPunchesFromFileCommand request, CancellationToken cancellationToken)
    {
        if (!await db.AttendanceDevices.AnyAsync(d => d.Id == request.AttendanceDeviceId, cancellationToken))
        {
            throw new NotFoundException(nameof(AttendanceDevice), request.AttendanceDeviceId);
        }

        var lines = RawPunchFileParser.Parse(request.FileName, request.Content);
        return await mediator.Send(new IngestDevicePunchesCommand(request.AttendanceDeviceId, lines, RawPunchSourceType.FileImport), cancellationToken);
    }
}
