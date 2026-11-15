namespace FuelFinder.Api.Dtos;

public sealed class StationSearchResponse
{
    public DateTimeOffset? LastUpdated { get; init; }

    public required int Total { get; init; }

    public required IReadOnlyList<StationResponse> Results { get; init; }
}
