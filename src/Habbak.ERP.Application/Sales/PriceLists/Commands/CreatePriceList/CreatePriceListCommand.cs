using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.PriceLists.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.PriceLists.Commands.CreatePriceList;

public sealed record CreatePriceListCommand : IRequest<long>
{
    public string? Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public required IReadOnlyList<long> BranchIds { get; init; }
    public required IReadOnlyList<PriceListLineInput> Lines { get; init; }
}

public sealed class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
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

public sealed class CreatePriceListCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePriceListCommand, long>
{
    public async Task<long> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("SALES_PRICE_LISTS", request.Code, cancellationToken);

        var codeExists = await db.PriceLists
            .AnyAsync(p => p.CompanyId == currentCompanyContext.CompanyId && p.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("SALES-PRICE-LIST-CODE-EXISTS", "توجد قائمة أسعار أخرى بنفس الكود بالفعل.");
        }

        var priceList = new PriceList
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            IsActive = true
        };

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

        db.PriceLists.Add(priceList);
        await db.SaveChangesAsync(cancellationToken);

        return priceList.Id;
    }
}
