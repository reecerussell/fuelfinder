namespace FuelFinder.Api.Models;

public sealed class StationSearchResult
{
    public required Station Station { get; init; }

    public required double DistanceKm { get; init; }
}
