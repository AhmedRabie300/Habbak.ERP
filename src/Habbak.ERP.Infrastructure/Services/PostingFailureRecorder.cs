using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Posting;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Writes on a context of its own, so the row outlives the rollback of the document that failed.
/// A failure to record is logged and swallowed: losing the log entry must never replace the real
/// posting error the user needs to see.
/// </summary>
public sealed class PostingFailureRecorder(
    DbContextOptions<AppDbContext> options,
    ICurrentCompanyContext currentCompanyContext,
    ILogger<PostingFailureRecorder> logger) : IPostingFailureRecorder
{
    public async Task RecordAsync(PostingFailureRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new AppDbContext(options, currentCompanyContext);
            db.PostingFailures.Add(new PostingFailure
            {
                CompanyId = record.CompanyId,
                BranchId = record.BranchId,
                ScreenCode = record.ScreenCode,
                SourceModule = record.SourceModule,
                SourceDocumentId = record.SourceDocumentId,
                Description = Truncate(record.Description, 500),
                ErrorCode = Truncate(record.ErrorCode, 100),
                ErrorMessage = Truncate(record.ErrorMessage, 2000),
                OccurredAtUtc = DateTime.UtcNow,
                UserId = currentCompanyContext.UserId
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not record posting failure {ErrorCode} for {ScreenCode} #{DocumentId}",
                record.ErrorCode, record.ScreenCode, record.SourceDocumentId);
        }
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
