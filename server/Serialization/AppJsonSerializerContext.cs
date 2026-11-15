using System.Text.Json;
using System.Text.Json.Serialization;
using FuelFinder.Api.Dtos;

namespace FuelFinder.Api.Serialization;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StationSearchResponse))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(List<FuelFinderStationDto>))]
[JsonSerializable(typeof(List<FuelFinderStationPricesDto>))]
[JsonSerializable(typeof(FuelFinderTokenRequestDto))]
[JsonSerializable(typeof(FuelFinderTokenResponseDto))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
