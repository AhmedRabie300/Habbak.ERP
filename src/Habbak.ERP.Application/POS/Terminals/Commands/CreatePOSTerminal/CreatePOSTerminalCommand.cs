using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Terminals.Commands.CreatePOSTerminal;

public sealed record CreatePOSTerminalCommand(string? Code, string NameAr, string NameEn, long BranchId, long? DefaultWarehouseId) : IRequest<long>;

public sealed class CreatePOSTerminalCommandValidator : AbstractValidator<CreatePOSTerminalCommand>
{
    public CreatePOSTerminalCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public sealed class CreatePOSTerminalCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePOSTerminalCommand, long>
{
    public async Task<long> Handle(CreatePOSTerminalCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        if (request.DefaultWarehouseId is { } warehouseId && !await db.Warehouses.AnyAsync(w => w.Id == warehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", warehouseId);
        }

        var code = await codeGenerator.ResolveCodeAsync("POS_TERMINALS", request.Code, cancellationToken);

        var codeExists = await db.POSTerminals
            .AnyAsync(t => t.CompanyId == currentCompanyContext.CompanyId && t.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("POS-TERMINAL-CODE-EXISTS", "يوجد جهاز نقطة بيع آخر بنفس الكود بالفعل.");
        }

        var terminal = new POSTerminal
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            BranchId = request.BranchId,
            DefaultWarehouseId = request.DefaultWarehouseId,
            IsActive = true
        };

        db.POSTerminals.Add(terminal);
        await db.SaveChangesAsync(cancellationToken);

        return terminal.Id;
    }
}
