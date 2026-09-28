using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Commands.CreateBranchRequest;

/// <summary>Creates a branch supply request as Draft (00-Frontend-Specs.md, section 7 — Create).
/// Rule 4 (RequestedQuantity vs BranchItemLimit) is enforced at Submit time, not here — a Draft
/// can be saved freely while still being worked on, matching how JournalEntry defers its own
/// posting-time rules until the user actually submits/posts.</summary>
public sealed record CreateBranchRequestCommand : IRequest<long>
{
    public required long BranchId { get; init; }
    public required DateOnly RequestDate { get; init; }
    public required IReadOnlyList<BranchRequestLineInput> Lines { get; init; }
}

public sealed class CreateBranchRequestCommandValidator : AbstractValidator<CreateBranchRequestCommand>
{
    public CreateBranchRequestCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.RequestDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("الطلب يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.RequestedQuantity).GreaterThan(0);
        });
    }
}

public sealed class CreateBranchRequestCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateBranchRequestCommand, long>
{
    public async Task<long> Handle(CreateBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var requestNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_BRANCH_REQUEST", null, cancellationToken);

        var branchRequest = new BranchRequest
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            RequestNumber = requestNumber,
            RequestDate = request.RequestDate,
            RequestedByUserId = currentCompanyContext.UserId,
            Status = BranchRequestStatus.Draft
        };

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        foreach (var lineInput in request.Lines)
        {
            var unit = units.Resolve(lineInput.ItemId, lineInput.UnitId);
            branchRequest.Lines.Add(new BranchRequestLine
            {
                ItemId = lineInput.ItemId,
                RequestedQuantity = lineInput.RequestedQuantity,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor
            });
        }

        db.BranchRequests.Add(branchRequest);
        await db.SaveChangesAsync(cancellationToken);

        return branchRequest.Id;
    }
}
