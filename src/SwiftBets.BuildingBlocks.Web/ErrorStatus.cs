using Microsoft.AspNetCore.Http;
using SwiftBets.Contracts.Errors;

namespace SwiftBets.BuildingBlocks.Web;

public static class ErrorStatus
{
    public static int For(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };
}
