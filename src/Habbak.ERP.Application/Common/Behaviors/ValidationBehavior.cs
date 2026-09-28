using FluentValidation;
using MediatR;

namespace Habbak.ERP.Application.Common.Behaviors;

/// <summary>
/// Runs every registered FluentValidation validator for a request before its handler executes,
/// throwing Exceptions.ValidationException on failure. Registered once in DependencyInjection
/// so individual command/query handlers never call validators by hand.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new Exceptions.ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
