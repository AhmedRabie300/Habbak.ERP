using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Commands.CreateTable;

public sealed record CreateTableCommand(string? Code, string NameAr, string NameEn, long BranchId) : IRequest<long>;

public sealed class CreateTableCommandValidator : AbstractValidator<CreateTableCommand>
{
    public CreateTableCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public sealed class CreateTableCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateTableCommand, long>
{
    public async Task<long> Handle(CreateTableCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        var code = await codeGenerator.ResolveCodeAsync("POS_TABLES", request.Code, cancellationToken);

        var codeExists = await db.Tables
            .AnyAsync(t => t.CompanyId == currentCompanyContext.CompanyId && t.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("POS-TABLE-CODE-EXISTS", "يوجد طرابيزة أخرى بنفس الكود بالفعل.");
        }

        var table = new Table
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            BranchId = request.BranchId,
            IsActive = true,
            Status = TableStatus.Free
        };

        db.Tables.Add(table);
        await db.SaveChangesAsync(cancellationToken);

        return table.Id;
    }
}
