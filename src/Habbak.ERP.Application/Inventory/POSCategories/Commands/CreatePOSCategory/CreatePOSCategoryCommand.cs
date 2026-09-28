using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.POSCategories.Commands.CreatePOSCategory;

public sealed record CreatePOSCategoryCommand(string? Code, string NameAr, string NameEn, int DisplayOrder) : IRequest<long>;

public sealed class CreatePOSCategoryCommandValidator : AbstractValidator<CreatePOSCategoryCommand>
{
    public CreatePOSCategoryCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreatePOSCategoryCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePOSCategoryCommand, long>
{
    public async Task<long> Handle(CreatePOSCategoryCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_POS_CATEGORIES", request.Code, cancellationToken);

        var codeExists = await db.POSCategories
            .AnyAsync(c => c.CompanyId == currentCompanyContext.CompanyId && c.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-POS-CATEGORY-CODE-EXISTS", "يوجد تصنيف نقطة بيع آخر بنفس الكود بالفعل.");
        }

        var category = new POSCategory
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DisplayOrder = request.DisplayOrder,
            IsActive = true
        };

        db.POSCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
