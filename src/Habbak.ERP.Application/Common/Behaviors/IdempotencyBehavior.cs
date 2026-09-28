using System.Text.Json;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Behaviors;

/// <summary>
/// 00-Project-Overview.md, section 14.1 — a request implementing IIdempotentRequest with a
/// non-null IdempotencyKey is checked against ProcessedIdempotencyKeys before its handler runs.
/// A key seen before short-circuits straight to the stored result (no re-execution, no duplicate
/// side effects); a new key lets the handler run once, then stores its result under that key.
/// Requests that don't implement IIdempotentRequest, or that leave the key null (every normal
/// online call — a client only sends one when it's replaying a Retry/Offline-sync attempt),
/// pass straight through untouched.
/// </summary>
public class IdempotencyBehavior<TRequest, TResponse>(IApplicationDbContext db) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest { IdempotencyKey: { } key })
        {
            return await next(cancellationToken);
        }

        var existing = await db.ProcessedIdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.IdempotencyKey == key, cancellationToken);

        if (existing is not null)
        {
            return JsonSerializer.Deserialize<TResponse>(existing.ResultJson)!;
        }

        var response = await next(cancellationToken);

        db.ProcessedIdempotencyKeys.Add(new ProcessedIdempotencyKey
        {
            IdempotencyKey = key,
            OperationType = typeof(TRequest).Name,
            ResultJson = JsonSerializer.Serialize(response),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync(cancellationToken);

        return response;
    }
}
