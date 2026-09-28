using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateAccountDimensionLink;

/// <summary>Links an account to a dimension (01-Module-Accounting.md, rule 24: max 5 links per account).</summary>
public sealed record CreateAccountDimensionLinkCommand(long AccountId, long DimensionId, int DisplayOrder, bool IsMandatory)
    : IRequest<long>;

public sealed class CreateAccountDimensionLinkCommandValidator : AbstractValidator<CreateAccountDimensionLinkCommand>
{
    public CreateAccountDimensionLinkCommandValidator()
    {
        RuleFor(x => x.AccountId).GreaterThan(0);
        RuleFor(x => x.DimensionId).GreaterThan(0);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(1, 5);
    }
}

public sealed class CreateAccountDimensionLinkCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateAccountDimensionLinkCommand, long>
{
    public async Task<long> Handle(CreateAccountDimensionLinkCommand request, CancellationToken cancellationToken)
    {
        var existingCount = await db.AccountDimensionLinks
            .CountAsync(l => l.AccountId == request.AccountId, cancellationToken);

        // Rule 24: hard cap of 5 dimensions per account.
        if (existingCount >= 5)
        {
            throw new BusinessRuleException(
                "ACC-R24-MAX-DIMENSIONS", "لا يمكن ربط أكتر من 5 أبعاد بنفس الحساب.");
        }

        var alreadyLinked = await db.AccountDimensionLinks
            .AnyAsync(l => l.AccountId == request.AccountId && l.CostCenterDimensionId == request.DimensionId, cancellationToken);

        if (alreadyLinked)
        {
            throw new BusinessRuleException("ACC-DIMENSION-ALREADY-LINKED", "هذا البُعد مرتبط بالحساب بالفعل.");
        }

        var link = new AccountDimensionLink
        {
            AccountId = request.AccountId,
            CostCenterDimensionId = request.DimensionId,
            DisplayOrder = request.DisplayOrder,
            IsMandatory = request.IsMandatory
        };

        db.AccountDimensionLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return link.Id;
    }
}
