using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.API.Middleware;

/// <summary>
/// The single place that turns an exception into the standard error contract
/// (00-Frontend-Specs.md, section 12.1). No controller ever builds an ErrorResponse by hand.
/// </summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        var (statusCode, response) = exception switch
        {
            ValidationException ex => (StatusCodes.Status400BadRequest, new ErrorResponse
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "توجد أخطاء في البيانات المُدخلة.",
                Details = ex.Errors
                    .SelectMany(kv => kv.Value.Select(message => new ErrorDetail(kv.Key, message)))
                    .ToList(),
                CorrelationId = correlationId
            }),

            PostingValidationException ex => (StatusCodes.Status400BadRequest, new ErrorResponse
            {
                ErrorCode = "POSTING_VALIDATION_ERROR",
                Message = "تعذّر ترحيل القيد لعدم استيفاء قواعد الترحيل.",
                Details = ex.Errors.Select(e => new ErrorDetail(e.Code, e.Message)).ToList(),
                CorrelationId = correlationId
            }),

            AuthenticationFailedException ex => (StatusCodes.Status401Unauthorized, new ErrorResponse
            {
                ErrorCode = ex.Code,
                Message = ex.Message,
                CorrelationId = correlationId
            }),

            ForbiddenException ex => (StatusCodes.Status403Forbidden, new ErrorResponse
            {
                ErrorCode = ex.Code,
                Message = ex.Message,
                CorrelationId = correlationId
            }),

            NotFoundException ex => (StatusCodes.Status404NotFound, new ErrorResponse
            {
                ErrorCode = "NOT_FOUND",
                Message = ex.Message,
                CorrelationId = correlationId
            }),

            BusinessRuleException ex => (StatusCodes.Status409Conflict, new ErrorResponse
            {
                ErrorCode = ex.Code,
                Message = ex.Message,
                CorrelationId = correlationId
            }),

            // Optimistic concurrency (00-Project-Overview.md, section 13; 00-Frontend-Specs.md, section 7).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, new ErrorResponse
            {
                ErrorCode = "CONCURRENCY_CONFLICT",
                Message = "تم تعديل هذا السجل من مستخدم آخر — يُرجى إعادة تحميل الصفحة والمحاولة مجددًا.",
                CorrelationId = correlationId
            }),

            _ => (StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "حدث خطأ غير متوقع.",
                CorrelationId = correlationId
            })
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
