namespace FuelFinder.Api.Models;

public sealed class StationMatch
{
    public required Station Station { get; init; }

    public required double DistanceKm { get; init; }
}
