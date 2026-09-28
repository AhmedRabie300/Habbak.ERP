using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.CreateSupplierContract;

/// <summary>Creates a supplier contract as Active — screen #10 has no draft/approval step
/// (section 6 defines no state machine for it).</summary>
public sealed record CreateSupplierContractCommand : IRequest<long>
{
    public required long SupplierId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public bool AutoRenew { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<ContractItemInput> Items { get; init; }
}

public sealed class CreateSupplierContractCommandValidator : AbstractValidator<CreateSupplierContractCommand>
{
    public CreateSupplierContractCommandValidator()
    {
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

public sealed class CreateSupplierContractCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSupplierContractCommand, long>
{
    public async Task<long> Handle(CreateSupplierContractCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        var contractNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_SUPPLIER_CONTRACT", null, cancellationToken);

        var contract = new SupplierContract
        {
            CompanyId = currentCompanyContext.CompanyId,
            SupplierId = request.SupplierId,
            ContractNumber = contractNumber,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AutoRenew = request.AutoRenew,
            Status = SupplierContractStatus.Active,
            Notes = request.Notes
        };

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

        db.SupplierContracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return contract.Id;
    }
}
