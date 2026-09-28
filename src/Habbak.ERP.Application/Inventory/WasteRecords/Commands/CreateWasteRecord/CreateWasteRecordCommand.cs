using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WasteRecords.Commands.CreateWasteRecord;

/// <summary>Screen #18's manual entry path — every record created from this screen is tagged
/// SourceDocumentType = Manual; ProductionOrder-sourced rows are created only by
/// CompleteProductionOrderCommand and never through here. Posts a TransactionType.Waste stock
/// movement immediately (rule 2.2's outbound Waste type exists exactly for this) — a waste entry
/// takes effect the moment it's recorded, there is no Draft/Posted stage for this entity. Its entry
/// (INVENTORY_WASTE template, when active) posts with it at the cost the stock actually left at.</summary>
public sealed record CreateWasteRecordCommand : IRequest<long>, IIdempotentRequest
{
    public Guid? IdempotencyKey { get; init; }

    public required long WarehouseId { get; init; }
    public required long ItemId { get; init; }
    public required decimal Quantity { get; init; }
    public required DateOnly WasteDate { get; init; }
    public required string Reason { get; init; }
}

public sealed class CreateWasteRecordCommandValidator : AbstractValidator<CreateWasteRecordCommand>
{
    public CreateWasteRecordCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.WasteDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateWasteRecordCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IStockMovementService stockMovementService,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<CreateWasteRecordCommand, long>
{
    public async Task<long> Handle(CreateWasteRecordCommand request, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), request.ItemId);

        var wasteRecord = new WasteRecord
        {
            CompanyId = currentCompanyContext.CompanyId,
            WarehouseId = request.WarehouseId,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            WasteDate = request.WasteDate,
            Reason = request.Reason,
            SourceDocumentType = WasteSourceDocumentType.Manual
        };

        // Two saves (the movement needs the record's id) in one transaction.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.WasteRecords.Add(wasteRecord);
        await db.SaveChangesAsync(cancellationToken);

        var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
        {
            CompanyId = currentCompanyContext.CompanyId,
            WarehouseId = request.WarehouseId,
            ItemId = request.ItemId,
            TransactionType = TransactionType.Waste,
            Quantity = request.Quantity,
            UnitCost = item.StandardCost ?? 0,
            TransactionDate = request.WasteDate,
            SourceDocumentType = "WasteRecord",
            SourceDocumentId = wasteRecord.Id
        }, cancellationToken);

        var cost = Math.Round(request.Quantity * applied.UnitCost, 2);
        if (cost > 0)
        {
            var companyId = currentCompanyContext.CompanyId;
            var branchId = await db.Warehouses.Where(w => w.Id == request.WarehouseId).Select(w => w.BranchId).FirstOrDefaultAsync(cancellationToken);
            wasteRecord.JournalEntry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = companyId,
                BranchId = branchId,
                ScreenCode = PostingScreenCatalog.Waste,
                SourceModule = SourceModule.Inventory,
                SourceDocumentType = SourceDocumentType.Waste,
                SourceDocumentId = wasteRecord.Id,
                EntryDate = request.WasteDate,
                Description = $"هالك: {item.NameAr} — {request.Reason}",
                IdempotencyKey = PostingKeys.For(companyId, "Waste.Create", wasteRecord.Id),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = branchId,
                    ["WarehouseId"] = request.WarehouseId,
                    [PostingScreenCatalog.HasStockMovementField] = cost > 0,
                    ["CostAmount"] = cost
                })
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return wasteRecord.Id;
    }
}
