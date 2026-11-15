using System.Text.Json.Serialization;

namespace FuelFinder.Api.Dtos;

public sealed class FuelFinderFuelPriceDto
{
    [JsonPropertyName("fuel_type")]
    public required string FuelType { get; init; }

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    [JsonPropertyName("price_last_updated")]
    public DateTimeOffset? PriceLastUpdated { get; init; }
}
