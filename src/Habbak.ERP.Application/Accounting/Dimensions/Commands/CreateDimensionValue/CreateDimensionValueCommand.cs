using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateDimensionValue;

/// <summary>
/// Adds a value under a dimension. ParentId is used only for naturally-hierarchical dimensions
/// (Cost Center); Level is computed from it (01-Module-Accounting.md, section 2.1).
/// </summary>
public sealed record CreateDimensionValueCommand(long DimensionId, string? Code, string NameAr, string NameEn, long? ParentId)
    : IRequest<long>;

public sealed class CreateDimensionValueCommandValidator : AbstractValidator<CreateDimensionValueCommand>
{
    public CreateDimensionValueCommandValidator()
    {
        RuleFor(x => x.DimensionId).GreaterThan(0);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateDimensionValueCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateDimensionValueCommand, long>
{
    public async Task<long> Handle(CreateDimensionValueCommand request, CancellationToken cancellationToken)
    {
        var dimension = await db.CostCenterDimensions.FindAsync([request.DimensionId], cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenterDimension), request.DimensionId);

        // Cashiers are the one linked kind with no master screen to mirror from, so their values are
        // entered here — coded with the user number, which is what the posting engine matches on.
        var isCashier = dimension.LinkedEntityType == CostCenterLinkedEntityType.Cashier;
        if (dimension.LinkedEntityType != CostCenterLinkedEntityType.None && !isCashier)
        {
            throw new BusinessRuleException(
                "ACC-DIMENSION-VALUES-LINKED", "قيم هذا المركز تُدار تلقائيًا من شاشتها المرتبطة ولا تُضاف هنا يدويًا.");
        }

        if (isCashier && (request.Code is null || !long.TryParse(request.Code, out _)))
        {
            throw new BusinessRuleException(
                "ACC-DIMENSION-CASHIER-CODE", "قيمة الكاشير كودها لازم يكون رقم المستخدم بتاعه — ده اللي القيد بيطابق عليه.");
        }

        var level = 0;
        if (request.ParentId is not null)
        {
            var parent = await db.CostCenterDimensionValues.FindAsync([request.ParentId.Value], cancellationToken)
                ?? throw new NotFoundException(nameof(CostCenterDimensionValue), request.ParentId.Value);

            if (parent.CostCenterDimensionId != request.DimensionId)
            {
                throw new BusinessRuleException("ACC-DIMENSION-VALUE-PARENT-MISMATCH", "القيمة الأصل لازم تنتمي لنفس البُعد.");
            }

            level = parent.Level + 1;
        }

        var code = isCashier ? request.Code! : await codeGenerator.ResolveCodeAsync("ACCOUNTING_DIMENSION_VALUES", request.Code, cancellationToken);

        var value = new CostCenterDimensionValue
        {
            CostCenterDimensionId = request.DimensionId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ParentId = request.ParentId,
            Level = level
        };

        db.CostCenterDimensionValues.Add(value);
        await db.SaveChangesAsync(cancellationToken);

        return value.Id;
    }
}
