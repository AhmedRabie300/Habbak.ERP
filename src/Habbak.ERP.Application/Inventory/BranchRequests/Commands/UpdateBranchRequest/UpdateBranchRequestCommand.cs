using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Commands.UpdateBranchRequest;

/// <summary>Edits a Draft branch request — only a Draft can change (section 4.2).</summary>
public sealed record UpdateBranchRequestCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required DateOnly RequestDate { get; init; }
    public required IReadOnlyList<BranchRequestLineInput> Lines { get; init; }
}

public sealed class UpdateBranchRequestCommandValidator : AbstractValidator<UpdateBranchRequestCommand>
{
    public UpdateBranchRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RequestDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("الطلب يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.RequestedQuantity).GreaterThan(0);
        });
    }
}

public sealed class UpdateBranchRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBranchRequestCommand>
{
    public async Task Handle(UpdateBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var branchRequest = await db.BranchRequests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BranchRequest), request.Id);

        if (branchRequest.Status != BranchRequestStatus.Draft)
        {
            throw new BusinessRuleException("INV-BRANCH-REQUEST-NOT-DRAFT", "لا يمكن تعديل الطلب إلا وهو في حالة مسودة.");
        }

        db.Entry(branchRequest).Property(nameof(BranchRequest.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        branchRequest.RequestDate = request.RequestDate;

        // Units are checked before the old lines go, so a refused unit leaves the request as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineUnits = request.Lines.Select(l => units.Resolve(l.ItemId, l.UnitId)).ToList();

        db.BranchRequestLines.RemoveRange(branchRequest.Lines);
        branchRequest.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (lineInput, unit) in request.Lines.Zip(lineUnits))
        {
            branchRequest.Lines.Add(new BranchRequestLine
            {
                ItemId = lineInput.ItemId,
                RequestedQuantity = lineInput.RequestedQuantity,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
