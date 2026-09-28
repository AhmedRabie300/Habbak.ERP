using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.PriceLists.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.PriceLists.Commands.UpdatePriceList;

public sealed record UpdatePriceListCommand : IRequest
{
    public required long Id { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public required IReadOnlyList<long> BranchIds { get; init; }
    public required IReadOnlyList<PriceListLineInput> Lines { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class UpdatePriceListCommandValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchIds).NotEmpty().WithMessage("قائمة الأسعار تحتاج فرعًا واحدًا على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.DineInPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.TakeawayPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.DeliveryPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdatePriceListCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePriceListCommand>
{
    public async Task Handle(UpdatePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await db.PriceLists
            .Include(p => p.Branches)
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceList), request.Id);

        priceList.NameAr = request.NameAr;
        priceList.NameEn = request.NameEn;
        priceList.EffectiveFromDate = request.EffectiveFromDate;
        priceList.EffectiveToDate = request.EffectiveToDate;
        priceList.IsActive = request.IsActive;

        // Two round trips: replacement branches/lines could otherwise collide with the still-present
        // old rows on the (PriceListId, BranchId)/(PriceListId, ItemId) unique indexes within the
        // same statement batch — same reasoning as UpdatePurchaseOrderCommand's line replacement.
        db.PriceListBranches.RemoveRange(priceList.Branches);
        db.PriceListLines.RemoveRange(priceList.Lines);
        priceList.Branches.Clear();
        priceList.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var branchId in request.BranchIds.Distinct())
        {
            priceList.Branches.Add(new PriceListBranch { BranchId = branchId });
        }

        foreach (var line in request.Lines)
        {
            priceList.Lines.Add(new PriceListLine
            {
                ItemId = line.ItemId,
                DineInPrice = line.DineInPrice,
                TakeawayPrice = line.TakeawayPrice,
                DeliveryPrice = line.DeliveryPrice
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
