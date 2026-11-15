using System.Text.Json.Serialization;

namespace FuelFinder.Api.Dtos;

public sealed class FuelFinderStationPricesDto
{
    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }

    [JsonPropertyName("fuel_prices")]
    public List<FuelFinderFuelPriceDto>? FuelPrices { get; init; }
}
