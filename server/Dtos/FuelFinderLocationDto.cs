using System.Text.Json.Serialization;

namespace FuelFinder.Api.Dtos;

public sealed class FuelFinderLocationDto
{
    [JsonPropertyName("address_line_1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("address_line_2")]
    public string? AddressLine2 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }
}
