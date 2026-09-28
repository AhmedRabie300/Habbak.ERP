using Habbak.ERP.Shared.Enums;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// Records a posting that failed. Implementations must write on their own connection: the failure
/// rolls back the caller's unit of work, and the record has to survive that.
/// </summary>
public interface IPostingFailureRecorder
{
    Task RecordAsync(PostingFailureRecord record, CancellationToken cancellationToken = default);
}

public sealed record PostingFailureRecord(
    long CompanyId,
    long? BranchId,
    string ScreenCode,
    SourceModule SourceModule,
    long SourceDocumentId,
    string Description,
    string ErrorCode,
    string ErrorMessage);
