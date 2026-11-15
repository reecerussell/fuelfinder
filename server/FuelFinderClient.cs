using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuelFinder.Api;

public class FuelFinderOptions
{
    public string BaseUrl { get; set; } = "https://www.fuel-finder.service.gov.uk/api/v1/";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

/// <summary>
/// Thin client for the GOV.UK Fuel Finder API. The API only allows one concurrent
/// request per client, so every call is serialised through a semaphore.
/// </summary>
public class FuelFinderClient(HttpClient http, FuelFinderOptions options, ILogger<FuelFinderClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpires;

    public Task<List<ApiStation>> GetAllStationsAsync(CancellationToken ct) =>
        GetAllBatchesAsync<ApiStation>("pfs", ct);

    public Task<List<ApiStationPrices>> GetAllPricesAsync(CancellationToken ct) =>
        GetAllBatchesAsync<ApiStationPrices>("pfs/fuel-prices", ct);

    /// <summary>Data is paged in batches of 500; a 404 means we've gone past the last batch.</summary>
    private async Task<List<T>> GetAllBatchesAsync<T>(string path, CancellationToken ct)
    {
        var all = new List<T>();
        for (var batch = 1; batch < 200; batch++)
        {
            var items = await GetAsync<List<T>>($"{path}?batch-number={batch}", ct);
            if (items is null || items.Count == 0) break;
            all.AddRange(items);
            if (items.Count < 500) break;
        }
        return all;
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var token = await GetTokenAsync(ct);
                using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.BaseUrl), relativeUrl));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var res = await http.SendAsync(req, ct);

                if (res.StatusCode == HttpStatusCode.NotFound) return default;
                if (res.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { _token = null; continue; }
                if (res.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
                {
                    logger.LogWarning("Rate limited by Fuel Finder API, backing off");
                    await Task.Delay(TimeSpan.FromSeconds(15 * (attempt + 1)), ct);
                    continue;
                }
                res.EnsureSuccessStatusCode();
                return await res.Content.ReadFromJsonAsync<T>(Json, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpires) return _token;

        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            throw new InvalidOperationException("FuelFinder:ClientId and FuelFinder:ClientSecret must be configured.");

        using var res = await http.PostAsJsonAsync(
            new Uri(new Uri(options.BaseUrl), "oauth/generate_access_token"),
            new { client_id = options.ClientId, client_secret = options.ClientSecret }, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<TokenEnvelope>(Json, ct)
                   ?? throw new InvalidOperationException("Empty token response");

        _token = body.Data.AccessToken;
        _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(body.Data.ExpiresIn - 60);
        return _token;
    }

    private record TokenEnvelope(TokenData Data);
    private record TokenData(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

// ---- API response shapes (only the fields we use) ----

public record ApiStation(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("trading_name")] string? TradingName,
    [property: JsonPropertyName("brand_name")] string? BrandName,
    [property: JsonPropertyName("public_phone_number")] string? PhoneNumber,
    [property: JsonPropertyName("temporary_closure")] bool? TemporaryClosure,
    [property: JsonPropertyName("permanent_closure")] bool? PermanentClosure,
    [property: JsonPropertyName("is_motorway_service_station")] bool? IsMotorway,
    [property: JsonPropertyName("is_supermarket_service_station")] bool? IsSupermarket,
    [property: JsonPropertyName("location")] ApiLocation? Location,
    [property: JsonPropertyName("amenities")] List<string>? Amenities);

public record ApiLocation(
    [property: JsonPropertyName("address_line_1")] string? AddressLine1,
    [property: JsonPropertyName("address_line_2")] string? AddressLine2,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("postcode")] string? Postcode,
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude);

public record ApiStationPrices(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("fuel_prices")] List<ApiFuelPrice>? FuelPrices);

public record ApiFuelPrice(
    [property: JsonPropertyName("fuel_type")] string FuelType,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("price_last_updated")] DateTimeOffset? PriceLastUpdated);
