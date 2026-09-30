using FluentValidation;
using Microsoft.AspNetCore.Http;
using SwiftBets.Contracts.Errors;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>Validates the first argument of type <typeparamref name="TRequest"/> before the endpoint runs.</summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return ErrorEnvelopes.ToResult(ErrorEnvelopes.Create(context.HttpContext, StatusCodes.Status400BadRequest, "request_missing"));
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return await next(context).ConfigureAwait(false);
        }

        var errors = validation.Errors
            .Select(e => new FieldError(ToCamelCase(e.PropertyName), e.ErrorCode, e.ErrorMessage))
            .ToList();
        return ErrorEnvelopes.ToResult(ErrorEnvelopes.Create(context.HttpContext, StatusCodes.Status400BadRequest, "validation_failed", errors: errors));
    }

    private static string ToCamelCase(string name) =>
        string.Join('.', name.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
