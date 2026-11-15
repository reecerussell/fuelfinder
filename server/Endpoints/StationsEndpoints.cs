using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using FuelFinder.Api.Services;
using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FuelFinder.Api.Endpoints;

public static class StationsEndpoints
{
    private const double DefaultRadiusKm = 8;
    private const double MinimumRadiusKm = 1;
    private const double MaximumRadiusKm = 50;

    public static IEndpointRouteBuilder MapStationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/stations", Search)
            .WithName("SearchStations");

        return endpoints;
    }

    private static Results<Ok<StationSearchResponse>, ValidationProblem, ProblemHttpResult> Search(
        [AsParameters] StationSearchRequest request,
        IStationSearchService searchService,
        IFuelDataStore store)
    {
        if (!TryParseSort(request.Sort, out var sort))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Sort)] = [$"Sort must be one of: {string.Join(", ", Enum.GetNames<StationSortOrder>())}."],
            });
        }

        if (!store.IsLoaded)
        {
            return TypedResults.Problem(
                detail: "Fuel data is still loading, try again in a moment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var criteria = new StationSearchCriteria
        {
            Latitude = request.Lat!.Value,
            Longitude = request.Lng!.Value,
            RadiusKm = Math.Clamp(request.RadiusKm ?? DefaultRadiusKm, MinimumRadiusKm, MaximumRadiusKm),
            FuelType = string.IsNullOrWhiteSpace(request.Fuel) ? null : request.Fuel.ToUpperInvariant(),
            Sort = sort,
        };

        var results = searchService.Search(criteria);

        return TypedResults.Ok(new StationSearchResponse
        {
            LastUpdated = store.LastUpdated,
            Total = results.Total,
            Results = results.Results.Select(ToResponse).ToList(),
        });
    }

    private static bool TryParseSort(string? value, out StationSortOrder sort)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            sort = StationSortOrder.Price;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out sort) && Enum.IsDefined(sort);
    }

    private static StationResponse ToResponse(StationSearchResult result)
    {
        var station = result.Station;

        return new StationResponse
        {
            Id = station.Id,
            Name = station.Name,
            Brand = station.Brand,
            Address = station.Address,
            Postcode = station.Postcode,
            Phone = station.Phone,
            Latitude = station.Latitude,
            Longitude = station.Longitude,
            IsMotorway = station.IsMotorway,
            IsSupermarket = station.IsSupermarket,
            Amenities = station.Amenities,
            Prices = station.Prices.Select(ToResponse).ToList(),
            DistanceKm = Math.Round(result.DistanceKm, 2),
        };
    }

    private static FuelPriceResponse ToResponse(FuelPrice price)
    {
        return new FuelPriceResponse
        {
            FuelType = price.FuelType,
            Price = price.Price,
            UpdatedAt = price.UpdatedAt,
        };
    }
}
