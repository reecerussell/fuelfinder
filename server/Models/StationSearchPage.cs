namespace FuelFinder.Api.Models;

public sealed class StationSearchPage
{
    public required int Total { get; init; }

    public required IReadOnlyList<StationMatch> Matches { get; init; }
}
