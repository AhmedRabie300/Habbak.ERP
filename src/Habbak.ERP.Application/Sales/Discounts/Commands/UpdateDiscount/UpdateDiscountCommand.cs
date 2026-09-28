using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Discounts.Commands.UpdateDiscount;

public sealed record UpdateDiscountCommand : IRequest
{
    public required long Id { get; init; }
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
    public required bool IsActive { get; init; }
}

public sealed class UpdateDiscountCommandValidator : AbstractValidator<UpdateDiscountCommand>
{
    public UpdateDiscountCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdateDiscountCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateDiscountCommand>
{
    public async Task Handle(UpdateDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = await db.Discounts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.Id);

        discount.NameAr = request.NameAr;
        discount.NameEn = request.NameEn;
        discount.DiscountType = request.DiscountType;
        discount.Value = request.Value;
        discount.ApplicationPriority = request.ApplicationPriority;
        discount.IsStackable = request.IsStackable;
        discount.MinInvoiceAmount = request.MinInvoiceAmount;
        discount.MinQuantity = request.MinQuantity;
        discount.IsHappyHour = request.IsHappyHour;
        discount.HappyHourFromTime = request.IsHappyHour ? request.HappyHourFromTime : null;
        discount.HappyHourToTime = request.IsHappyHour ? request.HappyHourToTime : null;
        discount.EffectiveFromDate = request.EffectiveFromDate;
        discount.EffectiveToDate = request.EffectiveToDate;
        discount.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
