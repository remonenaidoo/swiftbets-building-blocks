using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Errors;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>
/// Arms and disarms this service's named fault points. Mapped only when fault injection is enabled, which the host
/// refuses in Production, and restricted to operators and the Steward service identity.
/// </summary>
public static class FaultEndpoints
{
    public static IEndpointRouteBuilder MapSwiftBetsFaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var faults = endpoints.ServiceProvider.GetRequiredService<ConfigurableFaultPoint>();
        if (!faults.IsEnabled)
        {
            return endpoints;
        }

        var group = endpoints.MapGroup("/faults").RequireAuthorization(Roles.OperatorOrService);
        group.MapGet("/", () => Results.Ok(faults.Armed));
        group.MapPost("/{name}", (string name, int? times, HttpContext context) =>
        {
            if (times is < 1 or > 100_000)
            {
                return Error.Validation("invalid_times", "times must be between 1 and 100000.").ToHttpResult(context);
            }

            faults.Arm(name, times ?? 1);
            return Results.Ok(new { name, armed = faults.Armed.GetValueOrDefault(name) });
        });
        group.MapDelete("/{name}", (string name) =>
        {
            faults.Disarm(name);
            return Results.NoContent();
        });
        return endpoints;
    }
}
