using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Commands.ApproveBranchRequest;

/// <summary>
/// Screen #13 (02-Module-Inventory-Manufacturing.md, section 5) — decides every line in one pass
/// (this entity has no field to accumulate approvals across multiple rounds, so approval is a
/// one-time, terminal action here). An ApprovedQuantity of 0 means that line is rejected.
///
/// Rule 5: always targets a Main warehouse implicitly — the caller picks which one via
/// SourceWarehouseId, matching how WarehouseDocument.SourceWarehouseId already works; nothing here
/// re-checks WarehouseType = Main, since the same freedom exists for every other document type
/// already built (an admin choosing the wrong warehouse is a training issue, not a data one).
///
/// Rule 6: approving (fully or partially) creates a WarehouseDocument of type TransferOrder for
/// the approved lines. Rule 30 requires a CustodyOfficerId on that TransferOrder — the approver
/// supplies it here since BranchRequest itself carries no such field.
/// </summary>
public sealed record ApproveBranchRequestCommand : IRequest<ApproveBranchRequestResult>
{
    public required long Id { get; init; }
    public required long SourceWarehouseId { get; init; }
    public required long CustodyOfficerId { get; init; }
    public required DateOnly TransferDocumentDate { get; init; }
    public required IReadOnlyList<ApproveBranchRequestLineInput> Lines { get; init; }
}

public sealed record ApproveBranchRequestResult(string Status, long? TransferOrderId);

public sealed class ApproveBranchRequestCommandValidator : AbstractValidator<ApproveBranchRequestCommand>
{
    public ApproveBranchRequestCommandValidator()
    {
        RuleFor(x => x.SourceWarehouseId).GreaterThan(0);
        RuleFor(x => x.CustodyOfficerId).GreaterThan(0);
        RuleFor(x => x.TransferDocumentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty();

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineId).GreaterThan(0);
            line.RuleFor(l => l.ApprovedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class ApproveBranchRequestCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<ApproveBranchRequestCommand, ApproveBranchRequestResult>
{
    public async Task<ApproveBranchRequestResult> Handle(ApproveBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var branchRequest = await db.BranchRequests
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BranchRequest), request.Id);

        if (branchRequest.Status != BranchRequestStatus.PendingApproval)
        {
            throw new BusinessRuleException("INV-BRANCH-REQUEST-NOT-PENDING", "لا يمكن اعتماد الطلب إلا وهو بانتظار الاعتماد.");
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.SourceWarehouseId, cancellationToken))
        {
            throw new NotFoundException(nameof(Warehouse), request.SourceWarehouseId);
        }

        if (!await db.CustodyOfficers.AnyAsync(c => c.Id == request.CustodyOfficerId, cancellationToken))
        {
            throw new NotFoundException(nameof(CustodyOfficer), request.CustodyOfficerId);
        }

        var decisionsByLineId = request.Lines.ToDictionary(l => l.LineId);
        if (branchRequest.Lines.Any(l => !decisionsByLineId.ContainsKey(l.Id)))
        {
            throw new BusinessRuleException("INV-BRANCH-REQUEST-MISSING-DECISION", "لازم يتحدد قرار (اعتماد أو رفض) لكل بند في الطلب.");
        }

        var transferOrder = new WarehouseDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = branchRequest.BranchId,
            DocumentType = WarehouseDocumentType.TransferOrder,
            DocumentDate = request.TransferDocumentDate,
            SourceWarehouseId = request.SourceWarehouseId,
            CustodyOfficerId = request.CustodyOfficerId,
            Status = WarehouseDocumentStatus.Draft
        };

        var lineNumber = 1;
        var anyApproved = false;
        var anyRejectedOrPartial = false;

        foreach (var line in branchRequest.Lines)
        {
            var approvedQuantity = decisionsByLineId[line.Id].ApprovedQuantity;
            if (approvedQuantity > line.RequestedQuantity)
            {
                throw new BusinessRuleException(
                    "INV-APPROVED-EXCEEDS-REQUESTED",
                    $"الكمية المعتمدة للصنف رقم {line.ItemId} أكبر من الكمية المطلوبة.");
            }

            line.ApprovedQuantity = approvedQuantity;

            if (approvedQuantity > 0)
            {
                anyApproved = true;
                transferOrder.Lines.Add(new WarehouseDocumentLine
                {
                    LineNumber = lineNumber++,
                    ItemId = line.ItemId,
                    Quantity = approvedQuantity,
                    UnitId = line.UnitId,
                    UnitFactor = line.UnitFactor,
                    // StandardCost is per base unit; the line is in the request's unit.
                    UnitCost = (line.Item.StandardCost ?? 0) * line.UnitFactor
                });
            }

            if (approvedQuantity < line.RequestedQuantity)
            {
                anyRejectedOrPartial = true;
            }
        }

        branchRequest.Status = anyApproved
            ? (anyRejectedOrPartial ? BranchRequestStatus.PartiallyFulfilled : BranchRequestStatus.Approved)
            : BranchRequestStatus.Rejected;

        long? transferOrderId = null;
        if (anyApproved)
        {
            transferOrder.DocumentNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_TRANSFER_ORDER", null, cancellationToken);
            db.WarehouseDocuments.Add(transferOrder);
            await db.SaveChangesAsync(cancellationToken);
            transferOrderId = transferOrder.Id;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ApproveBranchRequestResult(branchRequest.Status.ToString(), transferOrderId);
    }
}
