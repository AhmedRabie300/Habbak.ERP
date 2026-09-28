using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.UpdateSupplierEvaluation;

/// <summary>No Status/workflow to lock against — always editable, same as Supplier itself.</summary>
public sealed record UpdateSupplierEvaluationCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required long SupplierId { get; init; }
    public required DateOnly EvaluationDate { get; init; }
    public required decimal QualityScore { get; init; }
    public required decimal DeliveryTimeScore { get; init; }
    public required decimal QuantityComplianceScore { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSupplierEvaluationCommandValidator : AbstractValidator<UpdateSupplierEvaluationCommand>
{
    public UpdateSupplierEvaluationCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.QualityScore).InclusiveBetween(0, 100);
        RuleFor(x => x.DeliveryTimeScore).InclusiveBetween(0, 100);
        RuleFor(x => x.QuantityComplianceScore).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateSupplierEvaluationCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSupplierEvaluationCommand>
{
    public async Task Handle(UpdateSupplierEvaluationCommand request, CancellationToken cancellationToken)
    {
        var evaluation = await db.SupplierEvaluations.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierEvaluation), request.Id);

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        db.Entry(evaluation).Property(nameof(SupplierEvaluation.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        evaluation.SupplierId = request.SupplierId;
        evaluation.EvaluationDate = request.EvaluationDate;
        evaluation.QualityScore = request.QualityScore;
        evaluation.DeliveryTimeScore = request.DeliveryTimeScore;
        evaluation.QuantityComplianceScore = request.QuantityComplianceScore;
        evaluation.OverallScore = Math.Round((request.QualityScore + request.DeliveryTimeScore + request.QuantityComplianceScore) / 3, 2);
        evaluation.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
    }
}
