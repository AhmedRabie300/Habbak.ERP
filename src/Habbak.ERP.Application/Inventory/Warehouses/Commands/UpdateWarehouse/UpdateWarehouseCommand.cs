using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;

namespace Habbak.ERP.Application.Inventory.Warehouses.Commands.UpdateWarehouse;

public sealed record UpdateWarehouseCommand(
    long Id, string NameAr, string NameEn, WarehouseType WarehouseType, long? BranchId, bool AllowNegativeBalance, bool IsActive) : IRequest;

public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);

        RuleFor(x => x.BranchId)
            .Null().When(x => x.WarehouseType == WarehouseType.Main)
            .WithMessage("المخزن الرئيسي لا يُربط بفرع.");
        RuleFor(x => x.BranchId)
            .NotNull().When(x => x.WarehouseType is WarehouseType.BranchMaterials or WarehouseType.Production or WarehouseType.FinishedGoods)
            .WithMessage("لازم تحديد الفرع لهذا النوع من المخازن.");
    }
}

public sealed class UpdateWarehouseCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWarehouseCommand>
{
    public async Task Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.Id);

        warehouse.NameAr = request.NameAr;
        warehouse.NameEn = request.NameEn;
        warehouse.WarehouseType = request.WarehouseType;
        warehouse.BranchId = request.BranchId;
        warehouse.AllowNegativeBalance = request.AllowNegativeBalance;
        warehouse.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
