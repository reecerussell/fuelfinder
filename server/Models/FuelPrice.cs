namespace FuelFinder.Api.Models;

public sealed class FuelPrice
{
    public required string FuelType { get; init; }

    public required decimal Price { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
