using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

[Screen("ACCOUNTING_RECEIPT_VOUCHERS")]
[Route("api/v1/accounting/receipt-vouchers")]
public class ReceiptVouchersController(ISender mediator) : VouchersControllerBase(mediator)
{
    protected override VoucherType FixedVoucherType => VoucherType.Receipt;
}
