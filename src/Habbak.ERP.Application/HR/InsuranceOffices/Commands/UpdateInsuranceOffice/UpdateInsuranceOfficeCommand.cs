using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.InsuranceOffices.Commands.UpdateInsuranceOffice;

public sealed record UpdateInsuranceOfficeCommand(long Id, string NameAr, string NameEn, string? OfficialCode, string? Address, bool IsActive) : IRequest;

public sealed class UpdateInsuranceOfficeCommandValidator : AbstractValidator<UpdateInsuranceOfficeCommand>
{
    public UpdateInsuranceOfficeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OfficialCode).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpdateInsuranceOfficeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateInsuranceOfficeCommand>
{
    public async Task Handle(UpdateInsuranceOfficeCommand request, CancellationToken cancellationToken)
    {
        var office = await db.InsuranceOffices.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(InsuranceOffice), request.Id);

        office.NameAr = request.NameAr;
        office.NameEn = request.NameEn;
        office.OfficialCode = request.OfficialCode;
        office.Address = request.Address;
        office.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
