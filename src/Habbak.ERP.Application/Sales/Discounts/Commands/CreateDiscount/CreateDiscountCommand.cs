using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Discounts.Commands.CreateDiscount;

public sealed record CreateDiscountCommand : IRequest<long>
{
    public string? Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required DiscountType DiscountType { get; init; }
    public required decimal Value { get; init; }
    public int ApplicationPriority { get; init; }
    public bool IsStackable { get; init; }
    public decimal? MinInvoiceAmount { get; init; }
    public decimal? MinQuantity { get; init; }
    public bool IsHappyHour { get; init; }
    public TimeOnly? HappyHourFromTime { get; init; }
    public TimeOnly? HappyHourToTime { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
}

public sealed class CreateDiscountCommandValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("قيمة الخصم كنسبة مئوية لا يمكن أن تتجاوز 100.");
        RuleFor(x => x.MinInvoiceAmount).GreaterThanOrEqualTo(0).When(x => x.MinInvoiceAmount.HasValue);
        RuleFor(x => x.MinQuantity).GreaterThanOrEqualTo(0).When(x => x.MinQuantity.HasValue);
        RuleFor(x => x.HappyHourFromTime).NotNull().When(x => x.IsHappyHour)
            .WithMessage("وقت بداية الـ Happy Hour إلزامي.");
        RuleFor(x => x.HappyHourToTime).NotNull().When(x => x.IsHappyHour)
            .WithMessage("وقت نهاية الـ Happy Hour إلزامي.");
    }
}

public sealed class CreateDiscountCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateDiscountCommand, long>
{
    public async Task<long> Handle(CreateDiscountCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("SALES_DISCOUNTS", request.Code, cancellationToken);

        var codeExists = await db.Discounts
            .AnyAsync(d => d.CompanyId == currentCompanyContext.CompanyId && d.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("SALES-DISCOUNT-CODE-EXISTS", "يوجد خصم آخر بنفس الكود بالفعل.");
        }

        var discount = new Discount
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DiscountType = request.DiscountType,
            Value = request.Value,
            ApplicationPriority = request.ApplicationPriority,
            IsStackable = request.IsStackable,
            MinInvoiceAmount = request.MinInvoiceAmount,
            MinQuantity = request.MinQuantity,
            IsHappyHour = request.IsHappyHour,
            HappyHourFromTime = request.IsHappyHour ? request.HappyHourFromTime : null,
            HappyHourToTime = request.IsHappyHour ? request.HappyHourToTime : null,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            IsActive = true
        };

        db.Discounts.Add(discount);
        await db.SaveChangesAsync(cancellationToken);

        return discount.Id;
    }
}
