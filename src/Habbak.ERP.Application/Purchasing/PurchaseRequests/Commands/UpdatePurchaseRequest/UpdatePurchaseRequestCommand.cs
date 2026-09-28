using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.UpdatePurchaseRequest;

/// <summary>Edits a Draft purchase request — only a Draft can change (section 6.1).</summary>
public sealed record UpdatePurchaseRequestCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required DateOnly RequestDate { get; init; }
    public required PurchaseRequestPriority Priority { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseRequestLineInput> Lines { get; init; }
}

public sealed class UpdatePurchaseRequestCommandValidator : AbstractValidator<UpdatePurchaseRequestCommand>
{
    public UpdatePurchaseRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdatePurchaseRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePurchaseRequestCommand>
{
    public async Task Handle(UpdatePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        if (purchaseRequest.Status != PurchaseRequestStatus.Draft)
        {
            throw new BusinessRuleException("PUR-REQUEST-NOT-DRAFT", "لا يمكن تعديل طلب الشراء إلا وهو في حالة مسودة.");
        }

        db.Entry(purchaseRequest).Property(nameof(PurchaseRequest.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        purchaseRequest.RequestDate = request.RequestDate;
        purchaseRequest.Priority = request.Priority;
        purchaseRequest.Reason = request.Reason;
        purchaseRequest.Notes = request.Notes;

        // Units are checked before the old lines go, so a refused unit leaves the request as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        foreach (var lineInput in request.Lines)
        {
            units.Resolve(lineInput.ItemId, lineInput.UnitId, PurchaseUnits.NotAllowed);
        }

        db.PurchaseRequestLines.RemoveRange(purchaseRequest.Lines);
        purchaseRequest.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

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

        await db.SaveChangesAsync(cancellationToken);
    }
}
