using System.Security.Cryptography;
using FluentValidation;
using Habbak.ERP.Application.Attendance.AttendanceDevices.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §6/§7 قرار 4 — نفس أسلوب SessionIssuer.rawRefresh
/// (32 بايت عشوائي، Base64Url) للسر الخام، وIPasswordHasher (نفس الاستخدام لكلمات مرور المستخدمين)
/// للـHash المخزَّن. السر الخام بيتعرض مرة واحدة بس وقت التسجيل/إعادة التوليد.
/// </summary>
internal static class DeviceSecretGenerator
{
    public static string GenerateRaw() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record CreateAttendanceDeviceCommand(string? Code, string NameAr, string NameEn, string? Model, string? SerialNumber, long? BranchId)
    : IRequest<AttendanceDeviceSecretDto>;

public sealed class CreateAttendanceDeviceCommandValidator : AbstractValidator<CreateAttendanceDeviceCommand>
{
    public CreateAttendanceDeviceCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
    }
}

public sealed class CreateAttendanceDeviceCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateAttendanceDeviceCommand, AttendanceDeviceSecretDto>
{
    public async Task<AttendanceDeviceSecretDto> Handle(CreateAttendanceDeviceCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_ATTENDANCE_DEVICES", request.Code, cancellationToken);

        if (await db.AttendanceDevices.AnyAsync(d => d.CompanyId == current.CompanyId && d.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("HR-DEVICE-CODE-EXISTS", "يوجد جهاز آخر بنفس الكود بالفعل.");
        }

        if (request.SerialNumber is { Length: > 0 } sn && await db.AttendanceDevices.IgnoreQueryFilters().AnyAsync(d => !d.IsDeleted && d.SerialNumber == sn, cancellationToken))
        {
            // §3.0 — فريد على مستوى الـDB كله (مفتاح مطابقة الـPush)، مش لكل شركة.
            throw new BusinessRuleException("HR-DEVICE-SERIAL-EXISTS", "يوجد جهاز آخر بنفس الرقم التسلسلي بالفعل.");
        }

        var rawSecret = DeviceSecretGenerator.GenerateRaw();
        var entity = new AttendanceDevice
        {
            CompanyId = current.CompanyId,
            BranchId = request.BranchId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            IsActive = true,
            DeviceSecretHash = passwordHasher.Hash(rawSecret)
        };

        db.AttendanceDevices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new AttendanceDeviceSecretDto(entity.Id, rawSecret);
    }
}

public sealed record UpdateAttendanceDeviceCommand(long Id, string NameAr, string NameEn, string? Model, string? SerialNumber, long? BranchId, bool IsActive) : IRequest;

public sealed class UpdateAttendanceDeviceCommandValidator : AbstractValidator<UpdateAttendanceDeviceCommand>
{
    public UpdateAttendanceDeviceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
    }
}

public sealed class UpdateAttendanceDeviceCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAttendanceDeviceCommand>
{
    public async Task Handle(UpdateAttendanceDeviceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.AttendanceDevices.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceDevice), request.Id);

        if (request.SerialNumber is { Length: > 0 } sn && sn != entity.SerialNumber
            && await db.AttendanceDevices.IgnoreQueryFilters().AnyAsync(d => !d.IsDeleted && d.Id != request.Id && d.SerialNumber == sn, cancellationToken))
        {
            throw new BusinessRuleException("HR-DEVICE-SERIAL-EXISTS", "يوجد جهاز آخر بنفس الرقم التسلسلي بالفعل.");
        }

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.Model = request.Model;
        entity.SerialNumber = request.SerialNumber;
        entity.BranchId = request.BranchId;
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteAttendanceDeviceCommand(long Id) : IRequest;

public sealed class DeleteAttendanceDeviceCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteAttendanceDeviceCommand>
{
    public async Task Handle(DeleteAttendanceDeviceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.AttendanceDevices.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceDevice), request.Id);

        if (await db.EmployeeDeviceMappings.AnyAsync(m => m.AttendanceDeviceId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException("HR-DEVICE-HAS-MAPPINGS", "لا يمكن حذف جهاز مربوط بموظفين. احذف الربط أولًا.");
        }

        db.AttendanceDevices.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>سر جديد بيلغي القديم فورًا (Hash واحد بس مخزَّن) — نفس فكرة "إعادة تعيين كلمة المرور".</summary>
public sealed record RegenerateAttendanceDeviceSecretCommand(long Id) : IRequest<AttendanceDeviceSecretDto>;

public sealed class RegenerateAttendanceDeviceSecretCommandHandler(IApplicationDbContext db, IPasswordHasher passwordHasher)
    : IRequestHandler<RegenerateAttendanceDeviceSecretCommand, AttendanceDeviceSecretDto>
{
    public async Task<AttendanceDeviceSecretDto> Handle(RegenerateAttendanceDeviceSecretCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.AttendanceDevices.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceDevice), request.Id);

        var rawSecret = DeviceSecretGenerator.GenerateRaw();
        entity.DeviceSecretHash = passwordHasher.Hash(rawSecret);
        await db.SaveChangesAsync(cancellationToken);

        return new AttendanceDeviceSecretDto(entity.Id, rawSecret);
    }
}
