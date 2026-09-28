using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Invoices.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Invoices.Queries.GetPOSInvoiceById;

public sealed record GetPOSInvoiceByIdQuery(long Id) : IRequest<POSInvoiceDetailDto>;

public sealed class GetPOSInvoiceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPOSInvoiceByIdQuery, POSInvoiceDetailDto>
{
    public async Task<POSInvoiceDetailDto> Handle(GetPOSInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.POSInvoices
            .AsNoTracking()
            .Include(i => i.Check)
            .Include(i => i.Lines)
            .Include(i => i.Payments).ThenInclude(p => p.PaymentMethod)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSInvoice), request.Id);

        var itemIds = invoice.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.NameAr })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        return new POSInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
            CheckId = invoice.CheckId,
            CheckCode = invoice.Check!.CheckCode,
            CustomerId = invoice.CustomerId,
            OrderType = invoice.OrderType.ToString(),
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            ManualDiscountAmount = invoice.ManualDiscountAmount,
            LoyaltyPointsRedeemed = invoice.LoyaltyPointsRedeemed,
            LoyaltyDiscountAmount = invoice.LoyaltyDiscountAmount,
            ServiceChargeAmount = invoice.ServiceChargeAmount,
            TipAmount = invoice.TipAmount,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            Status = invoice.Status.ToString(),
            ETAReceiptStatus = invoice.ETAReceiptStatus.ToString(),
            Lines = invoice.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new POSInvoiceLineDto
                {
                    ItemId = l.ItemId,
                    ItemCode = items.TryGetValue(l.ItemId, out var item) ? item.Code : "",
                    ItemNameAr = items.TryGetValue(l.ItemId, out var item2) ? item2.NameAr : "",
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount,
                    LineTotal = l.Quantity * l.UnitPrice - l.DiscountAmount
                })
                .ToList(),
            Payments = invoice.Payments
                .Select(p => new POSPaymentDto
                {
                    Id = p.Id,
                    PaymentMethodId = p.PaymentMethodId,
                    PaymentMethodNameAr = p.PaymentMethod!.NameAr,
                    Amount = p.Amount,
                    CardTransactionReference = p.CardTransactionReference,
                    AmountTendered = p.AmountTendered,
                    ChangeGiven = p.ChangeGiven,
                    CashRoundingAdjustment = p.CashRoundingAdjustment,
                    Status = p.Status.ToString()
                })
                .ToList()
        };
    }
}
