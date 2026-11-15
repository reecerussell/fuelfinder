namespace FuelFinder.Api.Models;

public sealed class StationSearchCriteria
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public required double RadiusKm { get; init; }

    public string? FuelType { get; init; }

    public StationSortOrder Sort { get; init; } = StationSortOrder.Price;
}
