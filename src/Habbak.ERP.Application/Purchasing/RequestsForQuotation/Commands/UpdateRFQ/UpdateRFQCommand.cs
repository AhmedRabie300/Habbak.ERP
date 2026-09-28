using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.UpdateRFQ;

/// <summary>Edits a Draft RFQ — locked once Sent (suppliers may already be quoting against the
/// original lines/invite list).</summary>
public sealed record UpdateRFQCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly RFQDate { get; init; }
    public long? PurchaseRequestId { get; init; }
    public DateOnly? RequiredDate { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<RFQLineInput> Lines { get; init; }
    public required IReadOnlyList<long> SupplierIds { get; init; }
}

public sealed class UpdateRFQCommandValidator : AbstractValidator<UpdateRFQCommand>
{
    public UpdateRFQCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RFQDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("طلب عروض الأسعار يحتاج بند واحد على الأقل.");
        RuleFor(x => x.SupplierIds).NotEmpty().WithMessage("لازم تختار مورد واحد على الأقل لدعوته.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0);
        });
    }
}

public sealed class UpdateRFQCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateRFQCommand>
{
    public async Task Handle(UpdateRFQCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation
            .Include(r => r.Lines)
            .Include(r => r.Suppliers)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.Id);

        if (rfq.Status != RFQStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-EDITABLE", "لا يمكن تعديل طلب عروض الأسعار إلا وهو في حالة مسودة.");
        }

        db.Entry(rfq).Property(nameof(Domain.Purchasing.RequestForQuotation.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        rfq.BranchId = request.BranchId;
        rfq.RFQDate = request.RFQDate;
        rfq.PurchaseRequestId = request.PurchaseRequestId;
        rfq.RequiredDate = request.RequiredDate;
        rfq.Notes = request.Notes;

        // Two round trips: replacement lines/suppliers reuse ordinals/ids and the unique indexes
        // are checked per-statement.
        db.RFQLines.RemoveRange(rfq.Lines);
        rfq.Lines.Clear();
        db.RFQSuppliers.RemoveRange(rfq.Suppliers);
        rfq.Suppliers.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var lineNumber = 1;
        foreach (var line in request.Lines)
        {
            rfq.Lines.Add(new RFQLine { LineNumber = lineNumber++, ItemId = line.ItemId, Quantity = line.Quantity, UnitId = line.UnitId });
        }

        foreach (var supplierId in request.SupplierIds.Distinct())
        {
            rfq.Suppliers.Add(new RFQSupplier { SupplierId = supplierId, Status = RFQSupplierStatus.Pending });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
