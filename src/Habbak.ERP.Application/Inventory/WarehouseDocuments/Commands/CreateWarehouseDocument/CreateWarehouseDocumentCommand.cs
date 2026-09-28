using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.CreateWarehouseDocument;

/// <summary>Creates a warehouse document as Draft. DocumentType is fixed by which concrete
/// controller route was hit (StockIn/StockOut/TransferOrder), never by client input — same
/// pattern as VouchersControllerBase/VoucherType. TransferReceipt is created through its own
/// dedicated CreateTransferReceiptCommand instead (its shape differs too much: it copies fields
/// from the related TransferOrder rather than taking them from the caller).</summary>
public sealed record CreateWarehouseDocumentCommand : IRequest<long>
{
    public required WarehouseDocumentType DocumentType { get; init; }
    public long? BranchId { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public long? SourceWarehouseId { get; init; }
    public long? DestinationWarehouseId { get; init; }
    public long? CustodyOfficerId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<WarehouseDocumentLineInput> Lines { get; init; }
}

public sealed class CreateWarehouseDocumentCommandValidator : AbstractValidator<CreateWarehouseDocumentCommand>
{
    public CreateWarehouseDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("المستند يحتاج بند واحد على الأقل.");

        // StockIn/OpeningBalance add to a destination only; StockOut removes from a source only;
        // TransferOrder removes from a source and hands custody to CustodyOfficerId — the
        // destination is only confirmed later at TransferReceipt (section 2.3, rule 30).
        RuleFor(x => x.DestinationWarehouseId)
            .NotNull().When(x => x.DocumentType is WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance)
            .WithMessage("المستند يتطلب تحديد المخزن المستقبل.");
        RuleFor(x => x.SourceWarehouseId)
            .Null().When(x => x.DocumentType is WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance)
            .WithMessage("هذا المستند لا يحمل مخزن مصدر.");
        RuleFor(x => x.SourceWarehouseId)
            .NotNull().When(x => x.DocumentType is WarehouseDocumentType.StockOut or WarehouseDocumentType.TransferOrder)
            .WithMessage("هذا المستند يتطلب تحديد المخزن المصدر.");
        RuleFor(x => x.DestinationWarehouseId)
            .Null().When(x => x.DocumentType is WarehouseDocumentType.StockOut or WarehouseDocumentType.TransferOrder)
            .WithMessage("هذا المستند لا يحمل مخزن مستقبل.");
        RuleFor(x => x.CustodyOfficerId)
            .NotNull().When(x => x.DocumentType == WarehouseDocumentType.TransferOrder)
            .WithMessage("أمر التحويل يتطلب تحديد مسؤول العهدة (قاعدة 30).");
        RuleFor(x => x.CustodyOfficerId)
            .Null().When(x => x.DocumentType != WarehouseDocumentType.TransferOrder)
            .WithMessage("مسؤول العهدة مطلوب لأمر التحويل فقط.");

        // Screen #15 (standalone manual InventoryAdjustment): exactly one of source/destination
        // determines direction — the same source-only/destination-only convention StockOut/StockIn
        // already use, just resolved per-document instead of fixed by DocumentType.
        RuleFor(x => x)
            .Must(x => (x.SourceWarehouseId is null) != (x.DestinationWarehouseId is null))
            .When(x => x.DocumentType == WarehouseDocumentType.InventoryAdjustment)
            .WithMessage("تسوية الجرد تتطلب تحديد مخزن واحد فقط — مصدر (للنقص) أو مستقبل (للزيادة).")
            .OverridePropertyName("SourceWarehouseId");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateWarehouseDocumentCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateWarehouseDocumentCommand, long>
{
    public async Task<long> Handle(CreateWarehouseDocumentCommand request, CancellationToken cancellationToken)
    {
        var screenCode = request.DocumentType switch
        {
            WarehouseDocumentType.StockIn => "INVENTORY_STOCK_IN",
            WarehouseDocumentType.StockOut => "INVENTORY_STOCK_OUT",
            WarehouseDocumentType.TransferOrder => "INVENTORY_TRANSFER_ORDER",
            WarehouseDocumentType.InventoryAdjustment => "INVENTORY_ADJUSTMENT",
            WarehouseDocumentType.OpeningBalance => "INVENTORY_OPENING_BALANCES",
            _ => throw new NotSupportedException($"{request.DocumentType} not yet supported.")
        };

        if (request.CustodyOfficerId is { } custodyOfficerId
            && !await db.CustodyOfficers.AnyAsync(c => c.Id == custodyOfficerId, cancellationToken))
        {
            throw new NotFoundException(nameof(CustodyOfficer), custodyOfficerId);
        }

        var documentNumber = await codeGenerator.ResolveCodeAsync(screenCode, null, cancellationToken);

        var document = new WarehouseDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = await WarehouseDocumentBranch.ResolveAsync(db, request.DocumentType, request.SourceWarehouseId, request.DestinationWarehouseId, request.BranchId, cancellationToken),
            DocumentType = request.DocumentType,
            DocumentNumber = documentNumber,
            DocumentDate = request.DocumentDate,
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            CustodyOfficerId = request.CustodyOfficerId,
            Status = WarehouseDocumentStatus.Draft,
            Notes = request.Notes
        };

        // Rule 32 / remark 3.5: only an inbound line ever needs an expiry date at all.
        var isInbound = request.DocumentType is WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance
            || (request.DocumentType == WarehouseDocumentType.InventoryAdjustment && request.DestinationWarehouseId is not null);

        var itemsById = isInbound
            ? await db.Items.Where(i => request.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken)
            : [];

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineNumber = 1;
        foreach (var lineInput in request.Lines)
        {
            var unit = units.Resolve(lineInput.ItemId, lineInput.UnitId);
            document.Lines.Add(new WarehouseDocumentLine
            {
                LineNumber = lineNumber++,
                ItemId = lineInput.ItemId,
                Quantity = lineInput.Quantity,
                UnitCost = lineInput.UnitCost,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BatchNumber = lineInput.BatchNumber,
                ExpiryDate = isInbound ? ExpiryDateResolver.Resolve(request.DocumentDate, lineInput.ExpiryDate, itemsById[lineInput.ItemId]) : lineInput.ExpiryDate
            });
        }

        db.WarehouseDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
