using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.UpdateEmployeeDocumentType;

public sealed record UpdateEmployeeDocumentTypeCommand(long Id, string NameAr, string NameEn, bool RequiresExpiry, bool IsMandatory, int? ExpiryAlertDays, bool IsActive) : IRequest;

public sealed class UpdateEmployeeDocumentTypeCommandValidator : AbstractValidator<UpdateEmployeeDocumentTypeCommand>
{
    public UpdateEmployeeDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExpiryAlertDays).GreaterThanOrEqualTo(0).When(x => x.ExpiryAlertDays.HasValue);
    }
}

public sealed class UpdateEmployeeDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmployeeDocumentTypeCommand>
{
    public async Task Handle(UpdateEmployeeDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var documentType = await db.EmployeeDocumentTypes.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocumentType), request.Id);

        documentType.NameAr = request.NameAr;
        documentType.NameEn = request.NameEn;
        documentType.RequiresExpiry = request.RequiresExpiry;
        documentType.IsMandatory = request.IsMandatory;
        documentType.ExpiryAlertDays = request.ExpiryAlertDays;
        documentType.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
