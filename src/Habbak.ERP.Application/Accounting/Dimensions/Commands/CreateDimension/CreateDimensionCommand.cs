using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateDimension;

/// <summary>
/// Creates a new cost center type (01-Module-Accounting.md, section 2.1 — branch, sales channel,
/// cashier... all one mechanism, max 5 linked per account per rule 24). When LinkedEntityType is
/// not None, its values are not entered by hand (cashiers aside) — they mirror that other screen's
/// records, backfilled below for any that already existed before this cost center type was created.
/// </summary>
public sealed record CreateDimensionCommand(string? Code, string NameAr, string NameEn, CostCenterLinkedEntityType LinkedEntityType) : IRequest<long>;

public sealed class CreateDimensionCommandValidator : AbstractValidator<CreateDimensionCommand>
{
    public CreateDimensionCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateDimensionCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateDimensionCommand, long>
{
    public async Task<long> Handle(CreateDimensionCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("ACCOUNTING_DIMENSIONS", request.Code, cancellationToken);

        var codeExists = await db.CostCenterDimensions
            .AnyAsync(d => d.CompanyId == currentCompanyContext.CompanyId && d.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new Common.Exceptions.BusinessRuleException("ACC-DIMENSION-CODE-EXISTS", "يوجد بُعد آخر بنفس الكود بالفعل.");
        }

        var dimension = new CostCenterDimension
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true,
            LinkedEntityType = request.LinkedEntityType
        };

        db.CostCenterDimensions.Add(dimension);
        await db.SaveChangesAsync(cancellationToken);

        // Mirror the linked screen's existing records (branches, terminals, warehouses, customers,
        // suppliers) as this dimension's values, matched by code. Records added later get theirs from
        // BranchDimensionSync (branches) or, for the rest, on first use by the posting engine
        // (PostingEntityValueMapper). Cashiers have no master records and are entered by hand.
        var records = request.LinkedEntityType switch
        {
            CostCenterLinkedEntityType.Branch => await db.Branches.AsNoTracking()
                .Select(e => new { e.Code, e.NameAr, e.NameEn, e.IsActive }).ToListAsync(cancellationToken),
            CostCenterLinkedEntityType.POSTerminal => await db.POSTerminals.AsNoTracking()
                .Select(e => new { e.Code, e.NameAr, e.NameEn, e.IsActive }).ToListAsync(cancellationToken),
            CostCenterLinkedEntityType.Warehouse => await db.Warehouses.AsNoTracking()
                .Select(e => new { e.Code, e.NameAr, e.NameEn, e.IsActive }).ToListAsync(cancellationToken),
            CostCenterLinkedEntityType.Customer => await db.Customers.AsNoTracking()
                .Select(e => new { e.Code, e.NameAr, e.NameEn, e.IsActive }).ToListAsync(cancellationToken),
            CostCenterLinkedEntityType.Supplier => await db.Suppliers.AsNoTracking()
                .Select(e => new { e.Code, e.NameAr, e.NameEn, e.IsActive }).ToListAsync(cancellationToken),
            _ => []
        };

        if (records.Count > 0)
        {
            foreach (var record in records)
            {
                db.CostCenterDimensionValues.Add(new CostCenterDimensionValue
                {
                    CostCenterDimensionId = dimension.Id,
                    Code = record.Code,
                    NameAr = record.NameAr,
                    NameEn = record.NameEn,
                    ParentId = null,
                    Level = 0,
                    IsActive = record.IsActive
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return dimension.Id;
    }
}
