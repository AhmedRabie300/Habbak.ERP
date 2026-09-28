using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.CreatePurchaseRequest;

/// <summary>Creates a purchase request as Draft (screen #2). Rule-level checks (e.g. whether the
/// cycle even requires a purchase request, PurchaseCycleSettings.RequiresPurchaseRequest) belong to
/// the downstream document that would otherwise skip this step (PurchaseOrder's own create command),
/// not here — creating a request is always allowed regardless of cycle configuration.</summary>
public sealed record CreatePurchaseRequestCommand : IRequest<long>
{
    public required long BranchId { get; init; }
    public required DateOnly RequestDate { get; init; }
    public required PurchaseRequestPriority Priority { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseRequestLineInput> Lines { get; init; }
}

public sealed class CreatePurchaseRequestCommandValidator : AbstractValidator<CreatePurchaseRequestCommand>
{
    public CreatePurchaseRequestCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.RequestDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("طلب الشراء يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0).When(l => l.UnitId is not null);
        });
    }
}

public sealed class CreatePurchaseRequestCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePurchaseRequestCommand, long>
{
    public async Task<long> Handle(CreatePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var requestNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_PURCHASE_REQUEST", null, cancellationToken);

        var purchaseRequest = new PurchaseRequest
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            RequestNumber = requestNumber,
            RequestDate = request.RequestDate,
            RequestedByUserId = currentCompanyContext.UserId,
            Priority = request.Priority,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = PurchaseRequestStatus.Draft
        };

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        foreach (var lineInput in request.Lines)
        {
            var unit = units.Resolve(lineInput.ItemId, lineInput.UnitId, PurchaseUnits.NotAllowed);
            purchaseRequest.Lines.Add(new PurchaseRequestLine
            {
                ItemId = lineInput.ItemId,
                Quantity = lineInput.Quantity,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BaseQuantity = ItemUnits.ToBase(lineInput.Quantity, unit.Factor),
                Notes = lineInput.Notes
            });
        }

        db.PurchaseRequests.Add(purchaseRequest);
        await db.SaveChangesAsync(cancellationToken);

        return purchaseRequest.Id;
    }
}
