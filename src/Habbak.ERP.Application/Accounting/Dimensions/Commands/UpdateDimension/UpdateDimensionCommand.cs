using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.UpdateDimension;

/// <summary>My Remarks/Remarks2.md, remark 2.1 — the Dimensions screen becomes a standard
/// List/Edit pair; Code and LinkedEntityType stay immutable after creation (same as every other
/// master-data Update in this codebase, and changing LinkedEntityType post-creation would orphan
/// or duplicate its Values).</summary>
public sealed record UpdateDimensionCommand(long Id, string NameAr, string NameEn, bool IsActive) : IRequest;

public sealed class UpdateDimensionCommandValidator : AbstractValidator<UpdateDimensionCommand>
{
    public UpdateDimensionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateDimensionCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateDimensionCommand>
{
    public async Task Handle(UpdateDimensionCommand request, CancellationToken cancellationToken)
    {
        var dimension = await db.CostCenterDimensions.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenterDimension), request.Id);

        dimension.NameAr = request.NameAr;
        dimension.NameEn = request.NameEn;
        dimension.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
