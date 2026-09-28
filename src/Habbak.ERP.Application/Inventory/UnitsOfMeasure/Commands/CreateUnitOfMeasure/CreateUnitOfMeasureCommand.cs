using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.CreateUnitOfMeasure;

public sealed record CreateUnitOfMeasureCommand(string? Code, string NameAr, string NameEn, UnitCategory Category) : IRequest<long>;

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(20);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateUnitOfMeasureCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateUnitOfMeasureCommand, long>
{
    public async Task<long> Handle(CreateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_UNITS_OF_MEASURE", request.Code, cancellationToken);

        var codeExists = await db.UnitsOfMeasure
            .AnyAsync(u => u.CompanyId == currentCompanyContext.CompanyId && u.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-UOM-CODE-EXISTS", "توجد وحدة قياس أخرى بنفس الكود بالفعل.");
        }

        var unit = new UnitOfMeasure
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Category = request.Category,
            IsActive = true
        };

        db.UnitsOfMeasure.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}
