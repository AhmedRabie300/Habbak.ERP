using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WasteRecords.Commands.UpdateWasteRecord;

/// <summary>Only a Manual-sourced record can be edited — one generated automatically from a
/// completed ProductionOrder is a historical trace of an already-posted stock movement and would
/// desync from it if changed here. Even a Manual record only lets WasteDate/Reason change:
/// Quantity/ItemId/WarehouseId already posted a TransactionType.Waste stock movement the moment
/// the record was created (CreateWasteRecordCommand) — editing those here would desync the
/// WasteRecord row from the movement it caused, with no reversal-and-reapply logic to keep them in
/// sync. A correction to the substance of an already-effective waste entry needs a new record
/// (and, if the original was wrong, a separate manual stock adjustment), the same principle that
/// keeps a Posted WarehouseDocument's lines immutable.</summary>
public sealed record UpdateWasteRecordCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required DateOnly WasteDate { get; init; }
    public required string Reason { get; init; }
}

public sealed class UpdateWasteRecordCommandValidator : AbstractValidator<UpdateWasteRecordCommand>
{
    public UpdateWasteRecordCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.WasteDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class UpdateWasteRecordCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWasteRecordCommand>
{
    public async Task Handle(UpdateWasteRecordCommand request, CancellationToken cancellationToken)
    {
        var wasteRecord = await db.WasteRecords.FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WasteRecord), request.Id);

        if (wasteRecord.SourceDocumentType != WasteSourceDocumentType.Manual)
        {
            throw new BusinessRuleException("INV-WASTE-NOT-MANUAL", "لا يمكن تعديل سجل هالك ناتج آليًا عن مستند آخر.");
        }

        db.Entry(wasteRecord).Property(nameof(WasteRecord.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        wasteRecord.WasteDate = request.WasteDate;
        wasteRecord.Reason = request.Reason;

        await db.SaveChangesAsync(cancellationToken);
    }
}
