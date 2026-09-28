using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.UpdateWarehouseDocument;

/// <summary>Edits a Draft warehouse document — only a Draft can change (section 4.1); DocumentType
/// never changes after creation, matching how VoucherType is immutable on Update. TransferReceipt
/// is edited through its own dedicated UpdateTransferReceiptCommand instead.</summary>
public sealed record UpdateWarehouseDocumentCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public long? SourceWarehouseId { get; init; }
    public long? DestinationWarehouseId { get; init; }
    public long? CustodyOfficerId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<WarehouseDocumentLineInput> Lines { get; init; }
}

public sealed class UpdateWarehouseDocumentCommandValidator : AbstractValidator<UpdateWarehouseDocumentCommand>
{
    public UpdateWarehouseDocumentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DocumentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("المستند يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateWarehouseDocumentCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWarehouseDocumentCommand>
{
    public async Task Handle(UpdateWarehouseDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.WarehouseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseDocument), request.Id);

        if (document.Status != WarehouseDocumentStatus.Draft)
        {
            throw new BusinessRuleException("INV-WHDOC-NOT-DRAFT", "لا يمكن تعديل مستند إلا وهو في حالة مسودة.");
        }

        if (document.DocumentType is WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance)
        {
            if (request.DestinationWarehouseId is null)
            {
                throw new BusinessRuleException("INV-WHDOC-DESTINATION-REQUIRED", "المستند يتطلب تحديد المخزن المستقبل.");
            }
        }
        else if (document.DocumentType is WarehouseDocumentType.StockOut or WarehouseDocumentType.TransferOrder)
        {
            if (request.SourceWarehouseId is null)
            {
                throw new BusinessRuleException("INV-WHDOC-SOURCE-REQUIRED", "هذا المستند يتطلب تحديد المخزن المصدر.");
            }
        }

        if (document.DocumentType == WarehouseDocumentType.TransferOrder)
        {
            if (request.CustodyOfficerId is null)
            {
                throw new BusinessRuleException("INV-WHDOC-CUSTODY-OFFICER-REQUIRED", "أمر التحويل يتطلب تحديد مسؤول العهدة (قاعدة 30).");
            }

            if (!await db.CustodyOfficers.AnyAsync(c => c.Id == request.CustodyOfficerId, cancellationToken))
            {
                throw new NotFoundException(nameof(CustodyOfficer), request.CustodyOfficerId!.Value);
            }
        }

        // Screen #15 (standalone manual InventoryAdjustment): exactly one of source/destination,
        // resolved per-document rather than fixed by DocumentType like StockIn/StockOut are.
        if (document.DocumentType == WarehouseDocumentType.InventoryAdjustment
            && (request.SourceWarehouseId is null) == (request.DestinationWarehouseId is null))
        {
            throw new BusinessRuleException("INV-WHDOC-ADJUSTMENT-DIRECTION-REQUIRED", "تسوية الجرد تتطلب تحديد مخزن واحد فقط — مصدر (للنقص) أو مستقبل (للزيادة).");
        }

        db.Entry(document).Property(nameof(WarehouseDocument.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        document.BranchId = await WarehouseDocumentBranch.ResolveAsync(db, document.DocumentType, request.SourceWarehouseId, request.DestinationWarehouseId, request.BranchId, cancellationToken);
        document.DocumentDate = request.DocumentDate;
        document.SourceWarehouseId = document.DocumentType switch
        {
            WarehouseDocumentType.StockOut or WarehouseDocumentType.TransferOrder => request.SourceWarehouseId,
            WarehouseDocumentType.InventoryAdjustment => request.SourceWarehouseId,
            _ => null
        };
        document.DestinationWarehouseId = document.DocumentType switch
        {
            WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance => request.DestinationWarehouseId,
            WarehouseDocumentType.InventoryAdjustment => request.DestinationWarehouseId,
            _ => null
        };
        document.CustodyOfficerId = document.DocumentType == WarehouseDocumentType.TransferOrder ? request.CustodyOfficerId : null;
        document.Notes = request.Notes;

        // Units are checked before the old lines go, so a refused unit leaves the document as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineUnits = request.Lines.Select(l => units.Resolve(l.ItemId, l.UnitId)).ToList();

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (WarehouseDocumentId, LineNumber) index is checked per-statement.
        db.WarehouseDocumentLines.RemoveRange(document.Lines);
        document.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        // Rule 32 / remark 3.5: only an inbound line ever needs an expiry date at all.
        var isInbound = document.DocumentType is WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance
            || (document.DocumentType == WarehouseDocumentType.InventoryAdjustment && document.DestinationWarehouseId is not null);

        var itemsById = isInbound
            ? await db.Items.Where(i => request.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken)
            : [];

        var lineNumber = 1;
        foreach (var (lineInput, unit) in request.Lines.Zip(lineUnits))
        {
            document.Lines.Add(new WarehouseDocumentLine
            {
                LineNumber = lineNumber++,
                ItemId = lineInput.ItemId,
                Quantity = lineInput.Quantity,
                UnitCost = lineInput.UnitCost,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BatchNumber = lineInput.BatchNumber,
                ExpiryDate = isInbound ? ExpiryDateResolver.Resolve(document.DocumentDate, lineInput.ExpiryDate, itemsById[lineInput.ItemId]) : lineInput.ExpiryDate
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
