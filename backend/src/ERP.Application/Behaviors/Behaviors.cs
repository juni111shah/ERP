using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using FluentValidation;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>Runs FluentValidation validators and throws a field-keyed validation error.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count > 0)
            throw new FieldValidationException(failures
                .GroupBy(f => ToCamel(f.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()));
        return await next();
    }

    private static string ToCamel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>Defence in depth: requests implementing IRequirePermission are checked even if an endpoint forgot its policy.</summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser user)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IRequirePermission rp && !user.HasPermission(rp.RequiredPermission))
            throw new ForbiddenException();
        return next();
    }
}
