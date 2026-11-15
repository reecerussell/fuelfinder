using System.Text.Json.Serialization;

namespace FuelFinder.Api.Dtos;

public sealed class FuelFinderTokenResponseDto
{
    [JsonPropertyName("data")]
    public FuelFinderTokenDataDto? Data { get; init; }
}
