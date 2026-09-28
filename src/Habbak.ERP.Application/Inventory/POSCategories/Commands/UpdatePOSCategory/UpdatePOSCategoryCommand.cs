using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;

namespace Habbak.ERP.Application.Inventory.POSCategories.Commands.UpdatePOSCategory;

public sealed record UpdatePOSCategoryCommand(long Id, string NameAr, string NameEn, int DisplayOrder, bool IsActive) : IRequest;

public sealed class UpdatePOSCategoryCommandValidator : AbstractValidator<UpdatePOSCategoryCommand>
{
    public UpdatePOSCategoryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdatePOSCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePOSCategoryCommand>
{
    public async Task Handle(UpdatePOSCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.POSCategories.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(POSCategory), request.Id);

        category.NameAr = request.NameAr;
        category.NameEn = request.NameEn;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
