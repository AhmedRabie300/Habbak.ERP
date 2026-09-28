using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.POS.Posting;
using Habbak.ERP.Application.POS.Returns.Dtos;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.POS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Returns.Commands.CreatePOSReturn;

/// <summary>قاعدة 16: SourceInvoiceId إلزامي دايمًا، ويُرجِع كمية المخزون فورًا — لا يوجد Draft
/// (جدول الحقول بالمواصفة يعرّف 3 حالات بس، نفس مبدأ POSInvoice قاعدة 15)، فالإنشاء والترحيل فعل
/// واحد ذرّي هنا (جوه Transaction صريحة، عشان الحفظ على مرحلتين). لا يغيّر حالة POSInvoice المصدر
/// (نفس قرار SalesReturn الفعلي). القيد بيترحّل مع المرتجع في كل أوضاع الترحيل (قالب POS_RETURN):
/// المبلغ المردود بيطلع من خزينة كاش الجهاز، والأصناف المخزنية بترجع المخزن بتكلفتها. المشروب
/// اللحظي مابيرجعش مخزن — مكوّناته اتستهلكت وقت البيع — فمالوش تكلفة ترجع.</summary>
public sealed record CreatePOSReturnCommand(
    long ShiftId, long SourceInvoiceId, DateOnly ReturnDate, string Reason,
    IReadOnlyList<POSReturnLineInput> Lines, Guid? IdempotencyKey = null) : IRequest<long>, IIdempotentRequest;

public sealed class CreatePOSReturnCommandValidator : AbstractValidator<CreatePOSReturnCommand>
{
    public CreatePOSReturnCommandValidator()
    {
        RuleFor(x => x.ShiftId).GreaterThan(0);
        RuleFor(x => x.SourceInvoiceId).GreaterThan(0);
        RuleFor(x => x.ReturnDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("مرتجع نقطة البيع يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreatePOSReturnCommandHandler(
    IApplicationDbContext db,
    ICodeGenerator codeGenerator,
    IStockMovementService stockMovementService,
    IPOSPostingService posPosting)
    : IRequestHandler<CreatePOSReturnCommand, long>
{
    public async Task<long> Handle(CreatePOSReturnCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("POS-SHIFT-NOT-OPEN", "لا يمكن تسجيل مرتجع إلا على وردية مفتوحة حاليًا.");
        }

        var sourceInvoice = await db.POSInvoices.FirstOrDefaultAsync(i => i.Id == request.SourceInvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSInvoice), request.SourceInvoiceId);

        if (sourceInvoice.Status != POSInvoiceStatus.Posted)
        {
            throw new BusinessRuleException("POS-RETURN-INVOICE-NOT-POSTED", "لا يمكن إنشاء مرتجع مرتبط بفاتورة غير مرحَّلة.");
        }

        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == shift.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), shift.POSTerminalId);

        if (terminal.DefaultWarehouseId is null)
        {
            throw new BusinessRuleException("POS-TERMINAL-NO-WAREHOUSE", "هذا الجهاز مش محدَّد له مخزن افتراضي — لا يمكن تسجيل المرتجع.");
        }

        var returnNumber = await codeGenerator.ResolveCodeAsync("POS_RETURNS", null, cancellationToken);

        var posReturn = new POSReturn
        {
            CompanyId = shift.CompanyId,
            BranchId = shift.BranchId,
            POSTerminalId = shift.POSTerminalId,
            ShiftId = shift.Id,
            SourceInvoiceId = request.SourceInvoiceId,
            ReturnNumber = returnNumber,
            ReturnDate = request.ReturnDate,
            Reason = request.Reason,
            Status = POSReturnStatus.Posted
        };

        var lineNumber = 1;
        foreach (var line in request.Lines)
        {
            posReturn.Lines.Add(new POSReturnLine
            {
                LineNumber = lineNumber++,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.POSReturns.Add(posReturn);
        await db.SaveChangesAsync(cancellationToken);

        var costAmount = 0m;
        foreach (var line in request.Lines)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == line.ItemId, cancellationToken)
                ?? throw new NotFoundException(nameof(Item), line.ItemId);

            if (!item.IsStocked)
            {
                continue;
            }

            var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = shift.CompanyId!.Value,
                WarehouseId = terminal.DefaultWarehouseId.Value,
                ItemId = item.Id,
                TransactionType = TransactionType.POSReturn,
                Quantity = line.Quantity,
                UnitCost = await stockMovementService.ResolveInboundCostAsync(item.Id, terminal.DefaultWarehouseId.Value, cancellationToken),
                TransactionDate = request.ReturnDate,
                SourceDocumentType = "POSReturn",
                SourceDocumentId = posReturn.Id
            }, cancellationToken);
            costAmount += line.Quantity * applied.UnitCost;
        }

        await posPosting.PostReturnAsync(posReturn, costAmount, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return posReturn.Id;
    }
}
