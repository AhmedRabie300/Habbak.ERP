using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ItemGroups.Commands.CreateItemGroup;

public sealed record CreateItemGroupCommand(string? Code, string NameAr, string NameEn, long? ParentId) : IRequest<long>;

public sealed class CreateItemGroupCommandValidator : AbstractValidator<CreateItemGroupCommand>
{
    public CreateItemGroupCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateItemGroupCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateItemGroupCommand, long>
{
    public async Task<long> Handle(CreateItemGroupCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_ITEM_GROUPS", request.Code, cancellationToken);

        var codeExists = await db.ItemGroups
            .AnyAsync(g => g.CompanyId == currentCompanyContext.CompanyId && g.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-ITEM-GROUP-CODE-EXISTS", "توجد مجموعة أصناف أخرى بنفس الكود بالفعل.");
        }

        if (request.ParentId is not null)
        {
            var parentExists = await db.ItemGroups.AnyAsync(g => g.Id == request.ParentId, cancellationToken);
            if (!parentExists)
            {
                throw new NotFoundException(nameof(ItemGroup), request.ParentId.Value);
            }
        }

        var group = new ItemGroup
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ParentId = request.ParentId,
            IsActive = true
        };

        db.ItemGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);

        return group.Id;
    }
}
