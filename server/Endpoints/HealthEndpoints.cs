using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FuelFinder.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/healthz", GetReadiness)
            .WithName("GetReadiness")
            .ExcludeFromDescription();

        return endpoints;
    }

    // Used as the Kubernetes readiness probe, so the pod only takes traffic once prices have loaded.
    private static Results<Ok<string>, StatusCodeHttpResult> GetReadiness(IFuelDataStore store)
    {
        return store.IsLoaded
            ? TypedResults.Ok("ok")
            : TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
