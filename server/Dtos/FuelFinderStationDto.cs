using System.Text.Json.Serialization;

namespace FuelFinder.Api.Dtos;

public sealed class FuelFinderStationDto
{
    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }

    [JsonPropertyName("trading_name")]
    public string? TradingName { get; init; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; init; }

    [JsonPropertyName("public_phone_number")]
    public string? PhoneNumber { get; init; }

    [JsonPropertyName("temporary_closure")]
    public bool? TemporaryClosure { get; init; }

    [JsonPropertyName("permanent_closure")]
    public bool? PermanentClosure { get; init; }

    [JsonPropertyName("is_motorway_service_station")]
    public bool? IsMotorway { get; init; }

    [JsonPropertyName("is_supermarket_service_station")]
    public bool? IsSupermarket { get; init; }

    [JsonPropertyName("location")]
    public FuelFinderLocationDto? Location { get; init; }

    [JsonPropertyName("amenities")]
    public List<string>? Amenities { get; init; }
}
