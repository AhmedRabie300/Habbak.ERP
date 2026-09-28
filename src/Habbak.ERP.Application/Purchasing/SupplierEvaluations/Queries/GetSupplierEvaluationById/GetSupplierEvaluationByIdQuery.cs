using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Queries.GetSupplierEvaluationById;

public sealed record GetSupplierEvaluationByIdQuery(long Id) : IRequest<SupplierEvaluationDetailDto>;

public sealed class GetSupplierEvaluationByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierEvaluationByIdQuery, SupplierEvaluationDetailDto>
{
    public async Task<SupplierEvaluationDetailDto> Handle(GetSupplierEvaluationByIdQuery request, CancellationToken cancellationToken)
    {
        var evaluation = await db.SupplierEvaluations
            .AsNoTracking()
            .Include(e => e.Supplier)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierEvaluation), request.Id);

        return new SupplierEvaluationDetailDto
        {
            Id = evaluation.Id,
            SupplierId = evaluation.SupplierId,
            SupplierCode = evaluation.Supplier!.Code,
            SupplierNameAr = evaluation.Supplier!.NameAr,
            EvaluationDate = evaluation.EvaluationDate,
            QualityScore = evaluation.QualityScore,
            DeliveryTimeScore = evaluation.DeliveryTimeScore,
            QuantityComplianceScore = evaluation.QuantityComplianceScore,
            OverallScore = evaluation.OverallScore,
            Notes = evaluation.Notes,
            RowVersion = Convert.ToBase64String(evaluation.RowVersion)
        };
    }
}
