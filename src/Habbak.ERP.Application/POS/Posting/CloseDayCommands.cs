using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Posting;

public sealed record POSDayStatusDto(
    long BranchId,
    DateOnly Date,
    string PostingMode,
    int UnpostedInvoiceCount,
    decimal UnpostedTotal,
    int PostedInvoiceCount,
    int OpenShiftCount,
    IReadOnlyList<string> PostedEntryNumbers);

/// <summary>Where a branch's POS day stands before pressing "إقفال اليوم".</summary>
public sealed record GetPOSDayStatusQuery(long BranchId, DateOnly Date) : IRequest<POSDayStatusDto>;

public sealed class GetPOSDayStatusQueryHandler(IApplicationDbContext db, IPOSPostingService posPosting)
    : IRequestHandler<GetPOSDayStatusQuery, POSDayStatusDto>
{
    public async Task<POSDayStatusDto> Handle(GetPOSDayStatusQuery request, CancellationToken cancellationToken)
    {
        var invoices = await db.POSInvoices.AsNoTracking()
            .Where(i => i.BranchId == request.BranchId && i.InvoiceDate == request.Date && i.Status == POSInvoiceStatus.Posted)
            .Select(i => new { i.Total, i.JournalEntryId, EntryNumber = i.JournalEntry != null ? i.JournalEntry.EntryNumber : null })
            .ToListAsync(cancellationToken);

        var openShifts = await db.Shifts.CountAsync(
            s => s.BranchId == request.BranchId && s.Status == ShiftStatus.Open, cancellationToken);

        return new POSDayStatusDto(
            request.BranchId,
            request.Date,
            (await posPosting.GetModeAsync(request.BranchId, cancellationToken)).ToString(),
            invoices.Count(i => i.JournalEntryId is null),
            invoices.Where(i => i.JournalEntryId is null).Sum(i => i.Total),
            invoices.Count(i => i.JournalEntryId is not null),
            openShifts,
            invoices.Where(i => i.EntryNumber is not null).Select(i => i.EntryNumber!).Distinct().Order().ToList());
    }
}

public sealed record CloseDayResult(int PostedInvoiceCount, decimal PostedTotal, string? EntryNumber, bool PostingConfigured);

/// <summary>
/// "إقفال اليوم" (decided 2026-09-18): posts one branch's still-unposted POS invoices for a date as a
/// single entry. Pressed by hand — there is no background job deciding when a day is over.
///
/// Allowed in every posting mode, not only PerDay: it only ever takes invoices with no entry yet, so
/// it is also how the stragglers left behind by a mode switch get posted. Invoices from a shift that
/// is still open are included; if more are sold later that day, pressing it again posts those as a
/// second entry.
/// </summary>
public sealed record ClosePOSDayCommand(long BranchId, DateOnly Date, Guid? IdempotencyKey = null)
    : IRequest<CloseDayResult>, IIdempotentRequest;

public sealed class ClosePOSDayCommandValidator : AbstractValidator<ClosePOSDayCommand>
{
    public ClosePOSDayCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.Date).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("مينفعش تقفل يوم لسه مجاش.");
    }
}

public sealed class ClosePOSDayCommandHandler(IApplicationDbContext db, IPOSPostingService posPosting)
    : IRequestHandler<ClosePOSDayCommand, CloseDayResult>
{
    public async Task<CloseDayResult> Handle(ClosePOSDayCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        var invoices = await db.POSInvoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Where(i => i.BranchId == request.BranchId
                && i.InvoiceDate == request.Date
                && i.JournalEntryId == null
                && i.Status == POSInvoiceStatus.Posted)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            throw new BusinessRuleException("POS-DAY-NOTHING-TO-POST", "مفيش فواتير لسه ماترحّلتش في اليوم ده على الفرع ده.");
        }

        var entry = await posPosting.PostInvoicesAsync(invoices, POSPostingScope.Day, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new CloseDayResult(
            entry is null ? 0 : invoices.Count,
            entry is null ? 0m : invoices.Sum(i => i.Total),
            entry?.EntryNumber,
            entry is not null);
    }
}
