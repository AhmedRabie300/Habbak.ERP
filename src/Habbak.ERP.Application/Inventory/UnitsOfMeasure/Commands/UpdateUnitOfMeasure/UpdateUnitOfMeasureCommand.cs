using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;

namespace Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;

public sealed record UpdateUnitOfMeasureCommand(long Id, string NameAr, string NameEn, UnitCategory Category, bool IsActive) : IRequest;

public sealed class UpdateUnitOfMeasureCommandValidator : AbstractValidator<UpdateUnitOfMeasureCommand>
{
    public UpdateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateUnitOfMeasureCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateUnitOfMeasureCommand>
{
    public async Task Handle(UpdateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var unit = await db.UnitsOfMeasure.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(UnitOfMeasure), request.Id);

        unit.NameAr = request.NameAr;
        unit.NameEn = request.NameEn;
        unit.Category = request.Category;
        unit.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
