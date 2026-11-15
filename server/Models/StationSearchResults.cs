namespace FuelFinder.Api.Models;

public sealed class StationSearchResults
{
    public required int Total { get; init; }

    public required IReadOnlyList<StationSearchResult> Results { get; init; }
}
