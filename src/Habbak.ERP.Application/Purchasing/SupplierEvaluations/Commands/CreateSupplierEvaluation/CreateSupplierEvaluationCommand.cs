using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.CreateSupplierEvaluation;

public sealed record CreateSupplierEvaluationCommand : IRequest<long>
{
    public required long SupplierId { get; init; }
    public required DateOnly EvaluationDate { get; init; }
    public required decimal QualityScore { get; init; }
    public required decimal DeliveryTimeScore { get; init; }
    public required decimal QuantityComplianceScore { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateSupplierEvaluationCommandValidator : AbstractValidator<CreateSupplierEvaluationCommand>
{
    public CreateSupplierEvaluationCommandValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.QualityScore).InclusiveBetween(0, 100);
        RuleFor(x => x.DeliveryTimeScore).InclusiveBetween(0, 100);
        RuleFor(x => x.QuantityComplianceScore).InclusiveBetween(0, 100);
    }
}

public sealed class CreateSupplierEvaluationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateSupplierEvaluationCommand, long>
{
    public async Task<long> Handle(CreateSupplierEvaluationCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        var evaluation = new SupplierEvaluation
        {
            CompanyId = currentCompanyContext.CompanyId,
            SupplierId = request.SupplierId,
            EvaluationDate = request.EvaluationDate,
            QualityScore = request.QualityScore,
            DeliveryTimeScore = request.DeliveryTimeScore,
            QuantityComplianceScore = request.QuantityComplianceScore,
            OverallScore = Math.Round((request.QualityScore + request.DeliveryTimeScore + request.QuantityComplianceScore) / 3, 2),
            Notes = request.Notes
        };

        db.SupplierEvaluations.Add(evaluation);
        await db.SaveChangesAsync(cancellationToken);

        return evaluation.Id;
    }
}
