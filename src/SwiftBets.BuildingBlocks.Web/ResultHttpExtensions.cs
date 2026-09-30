using Microsoft.AspNetCore.Http;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.BuildingBlocks.Web;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? Results.Json(result.Value, ContractJson.Options, statusCode: successStatus)
            : result.Error!.ToHttpResult(context);

    public static IResult ToHttpResult(this Result result, HttpContext context) =>
        result.IsSuccess ? Results.NoContent() : result.Error!.ToHttpResult(context);

    public static IResult ToHttpResult(this Error error, HttpContext context) =>
        ErrorEnvelopes.ToResult(ErrorEnvelopes.Create(context, ErrorStatus.For(error.Kind), error.Code, error.Message));
}
