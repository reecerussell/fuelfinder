namespace FuelFinder.Api.Dtos;

public sealed class FuelPriceResponse
{
    public required string FuelType { get; init; }

    public required decimal Price { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
