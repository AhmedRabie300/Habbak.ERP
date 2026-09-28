using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Branches;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Branches.Commands.CreateBranch;

public sealed record CreateBranchCommand(string? Code, string NameAr, string NameEn) : IRequest<long>;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateBranchCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateBranchCommand, long>
{
    public async Task<long> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("ORG_BRANCHES", request.Code, cancellationToken);

        var codeExists = await db.Branches
            .AnyAsync(b => b.CompanyId == currentCompanyContext.CompanyId && b.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("ORG-BRANCH-CODE-EXISTS", "يوجد فرع آخر بنفس الكود بالفعل.");
        }

        var branch = new Branch
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        await BranchDimensionSync.UpsertAsync(db, branch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}
