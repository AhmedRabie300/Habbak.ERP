using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Commands;

/// <summary>§3.0 — فريد على (AttendanceDeviceId, DeviceUserId). موظف واحد ممكن يكون ليه أكتر من صف.</summary>
public sealed record CreateEmployeeDeviceMappingCommand(long EmployeeId, long AttendanceDeviceId, string DeviceUserId) : IRequest<long>;

public sealed class CreateEmployeeDeviceMappingCommandValidator : AbstractValidator<CreateEmployeeDeviceMappingCommand>
{
    public CreateEmployeeDeviceMappingCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.AttendanceDeviceId).GreaterThan(0);
        RuleFor(x => x.DeviceUserId).NotEmpty().MaximumLength(50);
    }
}

public sealed class CreateEmployeeDeviceMappingCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateEmployeeDeviceMappingCommand, long>
{
    public async Task<long> Handle(CreateEmployeeDeviceMappingCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Habbak.ERP.Domain.HR.Employee), request.EmployeeId);
        }

        if (!await db.AttendanceDevices.AnyAsync(d => d.Id == request.AttendanceDeviceId, cancellationToken))
        {
            throw new NotFoundException(nameof(AttendanceDevice), request.AttendanceDeviceId);
        }

        if (await db.EmployeeDeviceMappings.AnyAsync(m => m.AttendanceDeviceId == request.AttendanceDeviceId && m.DeviceUserId == request.DeviceUserId, cancellationToken))
        {
            throw new BusinessRuleException("HR-DEVICE-MAPPING-USERID-EXISTS", "رقم المستخدم ده على نفس الجهاز مربوط بموظف آخر بالفعل.");
        }

        var entity = new EmployeeDeviceMapping
        {
            CompanyId = current.CompanyId,
            EmployeeId = request.EmployeeId,
            AttendanceDeviceId = request.AttendanceDeviceId,
            DeviceUserId = request.DeviceUserId
        };

        db.EmployeeDeviceMappings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateEmployeeDeviceMappingCommand(long Id, string DeviceUserId) : IRequest;

public sealed class UpdateEmployeeDeviceMappingCommandValidator : AbstractValidator<UpdateEmployeeDeviceMappingCommand>
{
    public UpdateEmployeeDeviceMappingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DeviceUserId).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateEmployeeDeviceMappingCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmployeeDeviceMappingCommand>
{
    public async Task Handle(UpdateEmployeeDeviceMappingCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeDeviceMappings.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDeviceMapping), request.Id);

        if (request.DeviceUserId != entity.DeviceUserId
            && await db.EmployeeDeviceMappings.AnyAsync(m => m.Id != request.Id && m.AttendanceDeviceId == entity.AttendanceDeviceId && m.DeviceUserId == request.DeviceUserId, cancellationToken))
        {
            throw new BusinessRuleException("HR-DEVICE-MAPPING-USERID-EXISTS", "رقم المستخدم ده على نفس الجهاز مربوط بموظف آخر بالفعل.");
        }

        entity.DeviceUserId = request.DeviceUserId;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteEmployeeDeviceMappingCommand(long Id) : IRequest;

public sealed class DeleteEmployeeDeviceMappingCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeDeviceMappingCommand>
{
    public async Task Handle(DeleteEmployeeDeviceMappingCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeDeviceMappings.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDeviceMapping), request.Id);

        db.EmployeeDeviceMappings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
