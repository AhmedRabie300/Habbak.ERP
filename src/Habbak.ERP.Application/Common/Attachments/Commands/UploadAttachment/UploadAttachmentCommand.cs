using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using MediatR;

namespace Habbak.ERP.Application.Common.Attachments.Commands.UploadAttachment;

/// <summary>My Remarks/Remarks2.md, remark 3.3 — a single generic upload endpoint any
/// Edit/New screen can call once its own record has an Id (EntityType is the screen's own fixed
/// tag, e.g. "PurchaseInvoice", never free client input beyond that fixed set — see the
/// controller's own allow-list).</summary>
public sealed record UploadAttachmentCommand(string EntityType, long EntityId, string FileName, string ContentType, byte[] Content) : IRequest<long>;

public sealed class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    /// <summary>10 MB per file — generous for a scanned invoice/receipt, small enough that
    /// storing content directly in SQL Server (varbinary(max)) stays reasonable.</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public UploadAttachmentCommandValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100)
            .Must(AttachmentEntityTypes.All.Contains)
            .WithMessage("نوع المستند غير مدعوم للمرفقات.");
        RuleFor(x => x.EntityId).GreaterThan(0);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Content).NotEmpty()
            .Must(c => c.Length <= MaxFileSizeBytes)
            .WithMessage("حجم الملف يتجاوز الحد الأقصى المسموح به (10 ميجابايت).");
    }
}

public sealed class UploadAttachmentCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UploadAttachmentCommand, long>
{
    public async Task<long> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = new Attachment
        {
            CompanyId = currentCompanyContext.CompanyId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.Content.Length,
            Content = request.Content
        };

        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return attachment.Id;
    }
}
