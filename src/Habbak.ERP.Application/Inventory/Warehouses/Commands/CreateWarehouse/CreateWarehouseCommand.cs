using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Warehouses.Commands.CreateWarehouse;

public sealed record CreateWarehouseCommand(
    string? Code, string NameAr, string NameEn, WarehouseType WarehouseType, long? BranchId, bool AllowNegativeBalance) : IRequest<long>;

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);

        // Rule 24 (02-Module-Inventory-Manufacturing.md): Main warehouses have no branch; branch
        // warehouses (BranchMaterials/Production/FinishedGoods) must have one.
        RuleFor(x => x.BranchId)
            .Null().When(x => x.WarehouseType == WarehouseType.Main)
            .WithMessage("المخزن الرئيسي لا يُربط بفرع.");
        RuleFor(x => x.BranchId)
            .NotNull().When(x => x.WarehouseType is WarehouseType.BranchMaterials or WarehouseType.Production or WarehouseType.FinishedGoods)
            .WithMessage("لازم تحديد الفرع لهذا النوع من المخازن.");
    }
}

public sealed class CreateWarehouseCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateWarehouseCommand, long>
{
    public async Task<long> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_WAREHOUSES", request.Code, cancellationToken);

        var codeExists = await db.Warehouses
            .AnyAsync(w => w.CompanyId == currentCompanyContext.CompanyId && w.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-WAREHOUSE-CODE-EXISTS", "يوجد مخزن آخر بنفس الكود بالفعل.");
        }

        var warehouse = new Warehouse
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            WarehouseType = request.WarehouseType,
            BranchId = request.BranchId,
            AllowNegativeBalance = request.AllowNegativeBalance,
            IsActive = true
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}
