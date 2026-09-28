using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Commands.UpdateTable;

public sealed record UpdateTableCommand(long Id, string NameAr, string NameEn, long BranchId, bool IsActive) : IRequest;

public sealed class UpdateTableCommandValidator : AbstractValidator<UpdateTableCommand>
{
    public UpdateTableCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public sealed class UpdateTableCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateTableCommand>
{
    public async Task Handle(UpdateTableCommand request, CancellationToken cancellationToken)
    {
        var table = await db.Tables.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.Id);

        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        table.NameAr = request.NameAr;
        table.NameEn = request.NameEn;
        table.BranchId = request.BranchId;
        table.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
