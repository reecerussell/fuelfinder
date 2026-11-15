namespace FuelFinder.Api.Dtos;

public sealed class StatusResponse
{
    public required bool Loaded { get; init; }

    public required int Stations { get; init; }

    public DateTimeOffset? LastUpdated { get; init; }
}
