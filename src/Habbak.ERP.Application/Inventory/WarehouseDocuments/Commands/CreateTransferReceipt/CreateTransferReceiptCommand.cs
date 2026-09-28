using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.CreateTransferReceipt;

/// <summary>
/// Creates a TransferReceipt as Draft against an existing Posted TransferOrder (rule 37).
/// CustodyOfficerId is copied automatically from the order (rule 34) — not a caller input. Each
/// line's UnitCost and ExpectedQuantity are copied from the matching order line (matched by
/// ItemId); the caller only supplies the quantity actually received, and VarianceQuantity is
/// computed live (rule 7) — this is the "شاشة تفاعلية" screen #11.
/// </summary>
public sealed record CreateTransferReceiptCommand : IRequest<long>
{
    public required long RelatedWarehouseDocumentId { get; init; }
    public long? BranchId { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required long DestinationWarehouseId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<TransferReceiptLineInput> Lines { get; init; }
}

public sealed class CreateTransferReceiptCommandValidator : AbstractValidator<CreateTransferReceiptCommand>
{
    public CreateTransferReceiptCommandValidator()
    {
        RuleFor(x => x.RelatedWarehouseDocumentId).GreaterThan(0);
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

public sealed class CreateTransferReceiptCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateTransferReceiptCommand, long>
{
    public async Task<long> Handle(CreateTransferReceiptCommand request, CancellationToken cancellationToken)
    {
        var order = await db.WarehouseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.RelatedWarehouseDocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseDocument), request.RelatedWarehouseDocumentId);

        // Rule 37: the related document must actually be a Posted TransferOrder.
        if (order.DocumentType != WarehouseDocumentType.TransferOrder || order.Status != WarehouseDocumentStatus.Posted)
        {
            throw new BusinessRuleException("INV-R37-TRANSFER-ORDER-NOT-POSTED", "لا يمكن إنشاء استلام تحويل بدون أمر تحويل مُرحَّل صحيح.");
        }

        var orderLinesByItem = order.Lines.ToDictionary(l => l.ItemId);
        foreach (var lineInput in request.Lines)
        {
            if (!orderLinesByItem.ContainsKey(lineInput.ItemId))
            {
                throw new BusinessRuleException("INV-TRANSFER-RECEIPT-ITEM-NOT-IN-ORDER", "أحد الأصناف غير موجود في أمر التحويل الأصلي.");
            }
        }

        var documentNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_TRANSFER_RECEIPT", null, cancellationToken);

        var receipt = new WarehouseDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = await WarehouseDocumentBranch.ResolveAsync(db, WarehouseDocumentType.TransferReceipt, null, request.DestinationWarehouseId, request.BranchId ?? order.BranchId, cancellationToken),
            DocumentType = WarehouseDocumentType.TransferReceipt,
            DocumentNumber = documentNumber,
            DocumentDate = request.DocumentDate,
            DestinationWarehouseId = request.DestinationWarehouseId,
            RelatedWarehouseDocumentId = order.Id,
            // Rule 34: the officer stays accountable for the goods until receipt is confirmed.
            CustodyOfficerId = order.CustodyOfficerId,
            Status = WarehouseDocumentStatus.Draft,
            Notes = request.Notes
        };

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
                ExpiryDate = ExpiryDateResolver.Resolve(request.DocumentDate, lineInput.ExpiryDate, itemsById[lineInput.ItemId]),
                ExpectedQuantity = orderLine.Quantity,
                VarianceQuantity = lineInput.Quantity - orderLine.Quantity
            });
        }

        db.WarehouseDocuments.Add(receipt);
        await db.SaveChangesAsync(cancellationToken);

        return receipt.Id;
    }
}
