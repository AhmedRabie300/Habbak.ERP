using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Branches;
using Habbak.ERP.Domain.Organization;
using MediatR;

namespace Habbak.ERP.Application.Organization.Branches.Commands.UpdateBranch;

public sealed record UpdateBranchCommand(long Id, string NameAr, string NameEn, bool IsActive) : IRequest;

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateBranchCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBranchCommand>
{
    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        branch.NameAr = request.NameAr;
        branch.NameEn = request.NameEn;
        branch.IsActive = request.IsActive;

        await BranchDimensionSync.UpsertAsync(db, branch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
