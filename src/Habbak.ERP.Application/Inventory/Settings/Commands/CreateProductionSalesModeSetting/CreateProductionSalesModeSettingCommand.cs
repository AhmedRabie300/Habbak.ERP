using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductionSalesModeSettingEntity = Habbak.ERP.Domain.Inventory.ProductionSalesModeSetting;

namespace Habbak.ERP.Application.Inventory.Settings.Commands.CreateProductionSalesModeSetting;

/// <summary>Screen #21 — adds one row to the priority matrix. ScopeId must be null exactly when
/// ScopeType = Company (module doc's own field-table note); POS scope existence isn't validated
/// (05-Module-POS-Shifts.md isn't built yet), Branch/Item scopes are.</summary>
public sealed record CreateProductionSalesModeSettingCommand : IRequest<long>
{
    public required SettingScopeType ScopeType { get; init; }
    public long? ScopeId { get; init; }
    public required ProductionSalesMode Mode { get; init; }
}

public sealed class CreateProductionSalesModeSettingCommandValidator : AbstractValidator<CreateProductionSalesModeSettingCommand>
{
    public CreateProductionSalesModeSettingCommandValidator()
    {
        RuleFor(x => x.ScopeId).Null().When(x => x.ScopeType == SettingScopeType.Company)
            .WithMessage("نطاق الشركة لا يحمل معرّف نطاق.");
        RuleFor(x => x.ScopeId).NotNull().When(x => x.ScopeType != SettingScopeType.Company)
            .WithMessage("هذا النطاق يتطلب تحديد المعرّف.");
    }
}

public sealed class CreateProductionSalesModeSettingCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateProductionSalesModeSettingCommand, long>
{
    public async Task<long> Handle(CreateProductionSalesModeSettingCommand request, CancellationToken cancellationToken)
    {
        var duplicate = await db.ProductionSalesModeSettings.AnyAsync(
            s => s.CompanyId == currentCompanyContext.CompanyId && s.ScopeType == request.ScopeType && s.ScopeId == request.ScopeId,
            cancellationToken);
        if (duplicate)
        {
            throw new BusinessRuleException("INV-SALES-MODE-DUPLICATE-SCOPE", "يوجد إعداد بالفعل لهذا النطاق.");
        }

        if (request.ScopeType == SettingScopeType.Branch
            && !await db.Branches.AnyAsync(b => b.Id == request.ScopeId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.ScopeId!.Value);
        }

        if (request.ScopeType == SettingScopeType.Item
            && !await db.Items.AnyAsync(i => i.Id == request.ScopeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Item), request.ScopeId!.Value);
        }

        var setting = new ProductionSalesModeSettingEntity
        {
            CompanyId = currentCompanyContext.CompanyId,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            Mode = request.Mode
        };

        db.ProductionSalesModeSettings.Add(setting);
        await db.SaveChangesAsync(cancellationToken);

        return setting.Id;
    }
}
