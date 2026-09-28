using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

[Screen("ACCOUNTING_PAYMENT_VOUCHERS")]
[Route("api/v1/accounting/payment-vouchers")]
public class PaymentVouchersController(ISender mediator) : VouchersControllerBase(mediator)
{
    protected override VoucherType FixedVoucherType => VoucherType.Payment;
}
