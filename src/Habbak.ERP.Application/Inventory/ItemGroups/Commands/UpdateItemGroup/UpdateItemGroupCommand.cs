using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ItemGroups.Commands.UpdateItemGroup;

public sealed record UpdateItemGroupCommand(long Id, string NameAr, string NameEn, long? ParentId, bool IsActive) : IRequest;

public sealed class UpdateItemGroupCommandValidator : AbstractValidator<UpdateItemGroupCommand>
{
    public UpdateItemGroupCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateItemGroupCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateItemGroupCommand>
{
    public async Task Handle(UpdateItemGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.ItemGroups.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(ItemGroup), request.Id);

        if (request.ParentId == request.Id)
        {
            throw new BusinessRuleException("INV-ITEM-GROUP-SELF-PARENT", "لا يمكن أن تكون المجموعة أصلًا لنفسها.");
        }

        if (request.ParentId is not null)
        {
            var parentExists = await db.ItemGroups.AnyAsync(g => g.Id == request.ParentId, cancellationToken);
            if (!parentExists)
            {
                throw new NotFoundException(nameof(ItemGroup), request.ParentId.Value);
            }
        }

        group.NameAr = request.NameAr;
        group.NameEn = request.NameEn;
        group.ParentId = request.ParentId;
        group.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
