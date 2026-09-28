using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.OpenCheckStandalone;

/// <summary>فتح شيك تيك أواي/دليفري بدون طرابيزة — DineIn له مساره الخاص عبر
/// OpenCheckForTableCommand فقط (TableId إلزامي للصالة، قاعدة 2.2).</summary>
public sealed record OpenCheckStandaloneCommand(long POSTerminalId, CheckOrderType OrderType, long? CustomerId) : IRequest<long>;

public sealed class OpenCheckStandaloneCommandValidator : AbstractValidator<OpenCheckStandaloneCommand>
{
    public OpenCheckStandaloneCommandValidator()
    {
        RuleFor(x => x.POSTerminalId).GreaterThan(0);
        RuleFor(x => x.OrderType).NotEqual(CheckOrderType.DineIn)
            .WithMessage("طلبات الصالة لازم ترتبط بطرابيزة عبر شاشة الطرابيزات.");
    }
}

public sealed class OpenCheckStandaloneCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<OpenCheckStandaloneCommand, long>
{
    public async Task<long> Handle(OpenCheckStandaloneCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == request.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);

        var openShift = await db.Shifts.FirstOrDefaultAsync(
            s => s.POSTerminalId == request.POSTerminalId && s.Status == ShiftStatus.Open, cancellationToken)
            ?? throw new BusinessRuleException("POS-NO-OPEN-SHIFT", "لا توجد وردية مفتوحة حاليًا على هذا الجهاز.");

        if (request.CustomerId is { } customerId && !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            throw new NotFoundException("Customer", customerId);
        }

        var checkCode = await codeGenerator.ResolveCodeAsync("POS_CHECKS", null, cancellationToken);

        var check = new Check
        {
            CompanyId = terminal.CompanyId,
            BranchId = terminal.BranchId,
            POSTerminalId = request.POSTerminalId,
            ShiftId = openShift.Id,
            CheckCode = checkCode,
            TableId = null,
            OrderType = request.OrderType,
            Status = CheckStatus.Open,
            CustomerId = request.CustomerId
        };

        db.Checks.Add(check);
        await db.SaveChangesAsync(cancellationToken);

        return check.Id;
    }
}
