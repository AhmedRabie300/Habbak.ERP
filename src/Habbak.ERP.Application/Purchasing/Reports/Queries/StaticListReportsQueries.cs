using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Reports.Queries;

// ---- Report #3: أوامر شراء مفتوحة (Open Purchase Orders) — ثابت ----

public sealed class OpenPurchaseOrderRowDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal RemainingQuantity { get; init; }
}

/// <summary>My Remarks/Remarks2.md follow-up — From/To filters OrderDate (when each order was
/// placed), not whether it's "currently" open: the Status filter below already scopes to open
/// orders regardless of period.</summary>
public sealed record GetOpenPurchaseOrdersReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<OpenPurchaseOrderRowDto>>;

public sealed class GetOpenPurchaseOrdersReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOpenPurchaseOrdersReportQuery, IReadOnlyList<OpenPurchaseOrderRowDto>>
{
    private static readonly PurchaseOrderStatus[] OpenStatuses =
    [
        PurchaseOrderStatus.Draft, PurchaseOrderStatus.Sent, PurchaseOrderStatus.Confirmed, PurchaseOrderStatus.PartiallyReceived
    ];

    public async Task<IReadOnlyList<OpenPurchaseOrderRowDto>> Handle(GetOpenPurchaseOrdersReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Where(o => OpenStatuses.Contains(o.Status) && o.OrderDate >= request.From && o.OrderDate <= request.To)
            .Select(o => new OpenPurchaseOrderRowDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                SupplierCode = o.Supplier!.Code,
                SupplierNameAr = o.Supplier!.NameAr,
                Status = o.Status.ToString(),
                TotalAmount = o.TotalAmount,
                RemainingQuantity = o.Lines.Sum(l => l.Quantity - l.ReceivedQuantity)
            })
            .OrderBy(r => r.OrderDate)
            .ToListAsync(cancellationToken);
}

// ---- Report #4: فواتير شراء غير مدفوعة (Unpaid Purchase Invoices) — ثابت ----

public sealed class UnpaidPurchaseInvoiceRowDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly DueDate { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal RemainingAmount { get; init; }
    public required int DaysOverdue { get; init; }
}

/// <summary>From/To filters InvoiceDate (when the invoice was issued) — the Status filter below
/// already scopes to invoices still unpaid regardless of period.</summary>
public sealed record GetUnpaidPurchaseInvoicesReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<UnpaidPurchaseInvoiceRowDto>>;

public sealed class GetUnpaidPurchaseInvoicesReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetUnpaidPurchaseInvoicesReportQuery, IReadOnlyList<UnpaidPurchaseInvoiceRowDto>>
{
    private static readonly PurchaseInvoiceStatus[] UnpaidStatuses =
    [
        PurchaseInvoiceStatus.Posted, PurchaseInvoiceStatus.PartiallyPaid, PurchaseInvoiceStatus.Overdue
    ];

    public async Task<IReadOnlyList<UnpaidPurchaseInvoiceRowDto>> Handle(GetUnpaidPurchaseInvoicesReportQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => UnpaidStatuses.Contains(i.Status) && i.InvoiceDate >= request.From && i.InvoiceDate <= request.To)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.DueDate,
                SupplierCode = i.Supplier!.Code,
                SupplierNameAr = i.Supplier!.NameAr,
                Status = i.Status.ToString(),
                i.TotalAmount,
                i.AmountPaid
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new UnpaidPurchaseInvoiceRowDto
            {
                Id = r.Id,
                InvoiceNumber = r.InvoiceNumber,
                DueDate = r.DueDate,
                SupplierCode = r.SupplierCode,
                SupplierNameAr = r.SupplierNameAr,
                Status = r.Status,
                TotalAmount = r.TotalAmount,
                AmountPaid = r.AmountPaid,
                RemainingAmount = r.TotalAmount - r.AmountPaid,
                DaysOverdue = Math.Max(0, today.DayNumber - r.DueDate.DayNumber)
            })
            .OrderByDescending(r => r.DaysOverdue)
            .ToList();
    }
}

// ---- Report #5: مردودات المشتريات (Purchase Returns) — ثابت ----

public sealed class PurchaseReturnReportRowDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string Reason { get; init; }
    public required string Status { get; init; }
    public required decimal TotalValue { get; init; }
}

public sealed record GetPurchaseReturnsReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<PurchaseReturnReportRowDto>>;

public sealed class GetPurchaseReturnsReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseReturnsReportQuery, IReadOnlyList<PurchaseReturnReportRowDto>>
{
    public async Task<IReadOnlyList<PurchaseReturnReportRowDto>> Handle(GetPurchaseReturnsReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseReturns
            .AsNoTracking()
            .Include(r => r.Supplier)
            .Where(r => r.ReturnDate >= request.From && r.ReturnDate <= request.To)
            .Select(r => new PurchaseReturnReportRowDto
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate = r.ReturnDate,
                SupplierCode = r.Supplier!.Code,
                SupplierNameAr = r.Supplier!.NameAr,
                Reason = r.Reason.ToString(),
                Status = r.Status.ToString(),
                TotalValue = r.Lines.Sum(l => l.Quantity * l.UnitCost)
            })
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync(cancellationToken);
}

// ---- Report #8: مصروفات الشراء (نقل، شحن، جمارك) (Purchase Expenses) — ثابت ----

public sealed class PurchaseExpenseReportRowDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required decimal AdditionalCosts { get; init; }
    public string? AllocationMethod { get; init; }
    public required decimal TotalAmount { get; init; }
}

/// <summary>No dedicated PurchaseExpense entity exists (section 4.8's "توزيع على بنود الفاتورة"
/// is folded into PurchaseInvoice.AdditionalCosts, same scope reduction documented on
/// PurchaseInvoice itself) — this reports on that field directly.</summary>
public sealed record GetPurchaseExpensesReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<PurchaseExpenseReportRowDto>>;

public sealed class GetPurchaseExpensesReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseExpensesReportQuery, IReadOnlyList<PurchaseExpenseReportRowDto>>
{
    public async Task<IReadOnlyList<PurchaseExpenseReportRowDto>> Handle(GetPurchaseExpensesReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.AdditionalCosts > 0 && i.InvoiceDate >= request.From && i.InvoiceDate <= request.To)
            .Select(i => new PurchaseExpenseReportRowDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                SupplierCode = i.Supplier!.Code,
                SupplierNameAr = i.Supplier!.NameAr,
                AdditionalCosts = i.AdditionalCosts,
                AllocationMethod = i.AdditionalCostAllocationMethod == null ? null : i.AdditionalCostAllocationMethod.ToString(),
                TotalAmount = i.TotalAmount
            })
            .OrderByDescending(r => r.InvoiceDate)
            .ToListAsync(cancellationToken);
}

// ---- Report #9: عقود الموردين المنتهية (Expiring/Expired Supplier Contracts) — ثابت ----

public sealed class ExpiringSupplierContractRowDto
{
    public required long Id { get; init; }
    public required string ContractNumber { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int DaysRemaining { get; init; }
    public required bool AutoRenew { get; init; }
}

/// <summary>DaysRemaining is computed here (negative = already past EndDate) rather than relying
/// on SupplierContractStatus.Expired, which stays unreachable without a scheduled job — same
/// pattern as RFQSupplierQuote.IsExpired.</summary>
public sealed record GetExpiringSupplierContractsReportQuery(int WithinDays) : IRequest<IReadOnlyList<ExpiringSupplierContractRowDto>>;

public sealed class GetExpiringSupplierContractsReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetExpiringSupplierContractsReportQuery, IReadOnlyList<ExpiringSupplierContractRowDto>>
{
    public async Task<IReadOnlyList<ExpiringSupplierContractRowDto>> Handle(GetExpiringSupplierContractsReportQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(request.WithinDays);

        var rows = await db.SupplierContracts
            .AsNoTracking()
            .Include(c => c.Supplier)
            .Where(c => c.Status == SupplierContractStatus.Active && c.EndDate <= cutoff)
            .Select(c => new { c.Id, c.ContractNumber, SupplierCode = c.Supplier!.Code, SupplierNameAr = c.Supplier!.NameAr, c.StartDate, c.EndDate, c.AutoRenew })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ExpiringSupplierContractRowDto
            {
                Id = r.Id,
                ContractNumber = r.ContractNumber,
                SupplierCode = r.SupplierCode,
                SupplierNameAr = r.SupplierNameAr,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                DaysRemaining = r.EndDate.DayNumber - today.DayNumber,
                AutoRenew = r.AutoRenew
            })
            .OrderBy(r => r.DaysRemaining)
            .ToList();
    }
}
