using FuelFinder.Api.Dtos;
using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FuelFinder.Api.Endpoints;

public static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/status", GetStatus);

        return endpoints;
    }

    private static Ok<StatusResponse> GetStatus(IFuelDataStore store)
    {
        return TypedResults.Ok(new StatusResponse
        {
            Loaded = store.IsLoaded,
            Stations = store.Stations.Count,
            LastUpdated = store.LastUpdated,
        });
    }
}
