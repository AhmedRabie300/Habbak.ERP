using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.UpdateTransferReceipt;

/// <summary>Edits a Draft TransferReceipt — RelatedWarehouseDocumentId and CustodyOfficerId are
/// locked at creation (rule 34) and never change here; only the received quantities/batch/expiry
/// per line can change, with ExpectedQuantity/UnitCost re-copied from the same order line and
/// VarianceQuantity recomputed live (rule 7).</summary>
public sealed record UpdateTransferReceiptCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required long DestinationWarehouseId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<TransferReceiptLineInput> Lines { get; init; }
}

public sealed class UpdateTransferReceiptCommandValidator : AbstractValidator<UpdateTransferReceiptCommand>
{
    public UpdateTransferReceiptCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DestinationWarehouseId).GreaterThan(0);
        RuleFor(x => x.DocumentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("المستند يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class UpdateTransferReceiptCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateTransferReceiptCommand>
{
    public async Task Handle(UpdateTransferReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await db.WarehouseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseDocument), request.Id);

        if (receipt.DocumentType != WarehouseDocumentType.TransferReceipt)
        {
            throw new NotFoundException(nameof(WarehouseDocument), request.Id);
        }

        if (receipt.Status != WarehouseDocumentStatus.Draft)
        {
            throw new BusinessRuleException("INV-WHDOC-NOT-DRAFT", "لا يمكن تعديل مستند إلا وهو في حالة مسودة.");
        }

        var order = await db.WarehouseDocuments
            .Include(d => d.Lines)
            .FirstAsync(d => d.Id == receipt.RelatedWarehouseDocumentId, cancellationToken);

        var orderLinesByItem = order.Lines.ToDictionary(l => l.ItemId);
        foreach (var lineInput in request.Lines)
        {
            if (!orderLinesByItem.ContainsKey(lineInput.ItemId))
            {
                throw new BusinessRuleException("INV-TRANSFER-RECEIPT-ITEM-NOT-IN-ORDER", "أحد الأصناف غير موجود في أمر التحويل الأصلي.");
            }
        }

        db.Entry(receipt).Property(nameof(WarehouseDocument.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        receipt.BranchId = await WarehouseDocumentBranch.ResolveAsync(db, WarehouseDocumentType.TransferReceipt, null, request.DestinationWarehouseId, request.BranchId ?? receipt.BranchId, cancellationToken);
        receipt.DocumentDate = request.DocumentDate;
        receipt.DestinationWarehouseId = request.DestinationWarehouseId;
        receipt.Notes = request.Notes;

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (WarehouseDocumentId, LineNumber) index is checked per-statement.
        db.WarehouseDocumentLines.RemoveRange(receipt.Lines);
        receipt.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var itemsById = await db.Items.Where(i => request.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);

        var lineNumber = 1;
        foreach (var lineInput in request.Lines)
        {
            var orderLine = orderLinesByItem[lineInput.ItemId];

            receipt.Lines.Add(new WarehouseDocumentLine
            {
                LineNumber = lineNumber++,
                ItemId = lineInput.ItemId,
                Quantity = lineInput.Quantity,
                UnitCost = orderLine.UnitCost,
                UnitId = orderLine.UnitId,
                UnitFactor = orderLine.UnitFactor,
                BatchNumber = lineInput.BatchNumber,
                // TransferReceipt is always inbound (rule 32 / remark 3.5).
                ExpiryDate = ExpiryDateResolver.Resolve(receipt.DocumentDate, lineInput.ExpiryDate, itemsById[lineInput.ItemId]),
                ExpectedQuantity = orderLine.Quantity,
                VarianceQuantity = lineInput.Quantity - orderLine.Quantity
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
