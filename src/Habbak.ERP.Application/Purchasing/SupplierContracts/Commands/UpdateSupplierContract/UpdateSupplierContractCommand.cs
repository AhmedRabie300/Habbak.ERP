using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.UpdateSupplierContract;

/// <summary>Edits an Active contract — locked once Cancelled/Archived.</summary>
public sealed record UpdateSupplierContractCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required long SupplierId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public bool AutoRenew { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<ContractItemInput> Items { get; init; }
}

public sealed class UpdateSupplierContractCommandValidator : AbstractValidator<UpdateSupplierContractCommand>
{
    public UpdateSupplierContractCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("تاريخ الانتهاء يجب أن يكون بعد تاريخ البداية.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("عقد المورد يحتاج صنف واحد على الأقل.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateSupplierContractCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSupplierContractCommand>
{
    public async Task Handle(UpdateSupplierContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.SupplierContracts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierContract), request.Id);

        if (contract.Status != SupplierContractStatus.Active)
        {
            throw new BusinessRuleException("PUR-CONTRACT-NOT-EDITABLE", "لا يمكن تعديل عقد المورد إلا وهو نشط.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        db.Entry(contract).Property(nameof(SupplierContract.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        contract.SupplierId = request.SupplierId;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.AutoRenew = request.AutoRenew;
        contract.Notes = request.Notes;

        // Two round trips: the unique (SupplierContractId, ItemId) index is checked per-statement.
        db.ContractItems.RemoveRange(contract.Items);
        contract.Items.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            contract.Items.Add(new ContractItem
            {
                ItemId = item.ItemId,
                UnitPrice = item.UnitPrice,
                MinQuantity = item.MinQuantity,
                MaxQuantity = item.MaxQuantity,
                DiscountPercentage = item.DiscountPercentage
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
