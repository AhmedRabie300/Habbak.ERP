using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.CreateRFQ;

/// <summary>Creates an RFQ as Draft (screen #3) — one RFQSupplier row (Pending) per invited
/// SupplierId, feeding the quote-comparison grid once responses start coming in.</summary>
public sealed record CreateRFQCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required DateOnly RFQDate { get; init; }
    public long? PurchaseRequestId { get; init; }
    public DateOnly? RequiredDate { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<RFQLineInput> Lines { get; init; }
    public required IReadOnlyList<long> SupplierIds { get; init; }
}

public sealed class CreateRFQCommandValidator : AbstractValidator<CreateRFQCommand>
{
    public CreateRFQCommandValidator()
    {
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

public sealed class CreateRFQCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateRFQCommand, long>
{
    public async Task<long> Handle(CreateRFQCommand request, CancellationToken cancellationToken)
    {
        if (request.PurchaseRequestId is { } purchaseRequestId
            && !await db.PurchaseRequests.AnyAsync(r => r.Id == purchaseRequestId, cancellationToken))
        {
            throw new NotFoundException(nameof(PurchaseRequest), purchaseRequestId);
        }

        var supplierCount = await db.Suppliers.CountAsync(s => request.SupplierIds.Contains(s.Id), cancellationToken);
        if (supplierCount != request.SupplierIds.Distinct().Count())
        {
            throw new NotFoundException("Supplier", 0);
        }

        var rfqNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_RFQ", null, cancellationToken);

        var rfq = new Domain.Purchasing.RequestForQuotation
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            RFQNumber = rfqNumber,
            RFQDate = request.RFQDate,
            PurchaseRequestId = request.PurchaseRequestId,
            Status = RFQStatus.Draft,
            RequiredDate = request.RequiredDate,
            Notes = request.Notes
        };

        var lineNumber = 1;
        foreach (var line in request.Lines)
        {
            rfq.Lines.Add(new RFQLine { LineNumber = lineNumber++, ItemId = line.ItemId, Quantity = line.Quantity, UnitId = line.UnitId });
        }

        foreach (var supplierId in request.SupplierIds.Distinct())
        {
            rfq.Suppliers.Add(new RFQSupplier { SupplierId = supplierId, Status = RFQSupplierStatus.Pending });
        }

        db.RequestsForQuotation.Add(rfq);
        await db.SaveChangesAsync(cancellationToken);

        return rfq.Id;
    }
}
