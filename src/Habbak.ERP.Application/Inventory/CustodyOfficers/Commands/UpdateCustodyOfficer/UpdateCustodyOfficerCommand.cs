using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;

namespace Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.UpdateCustodyOfficer;

public sealed record UpdateCustodyOfficerCommand(long Id, string NameAr, string NameEn, long? BranchId, bool IsActive) : IRequest;

public sealed class UpdateCustodyOfficerCommandValidator : AbstractValidator<UpdateCustodyOfficerCommand>
{
    public UpdateCustodyOfficerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateCustodyOfficerCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCustodyOfficerCommand>
{
    public async Task Handle(UpdateCustodyOfficerCommand request, CancellationToken cancellationToken)
    {
        var officer = await db.CustodyOfficers.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(CustodyOfficer), request.Id);

        officer.NameAr = request.NameAr;
        officer.NameEn = request.NameEn;
        officer.BranchId = request.BranchId;
        officer.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
