using System.Text.Json.Serialization;
using FuelFinder.Api.Options;
using Microsoft.Extensions.Options;

namespace FuelFinder.Api.Services;

public sealed class FuelFinderTokenProvider : IFuelFinderTokenProvider
{
    public const string HttpClientName = "FuelFinderAuth";

    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FuelFinderOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public FuelFinderTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<FuelFinderOptions> options,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            var token = await RequestTokenAsync(cancellationToken);
            _accessToken = token.AccessToken;
            _expiresAt = _timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) - ExpiryBuffer;
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
    }

    private bool TryGetCachedToken(out string token)
    {
        token = _accessToken ?? string.Empty;
        return _accessToken is not null && _timeProvider.GetUtcNow() < _expiresAt;
    }

    private async Task<TokenData> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var request = new TokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
        };

        using var response = await client.PostAsJsonAsync("oauth/generate_access_token", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<TokenEnvelope>(cancellationToken);
        return envelope?.Data ?? throw new InvalidOperationException("Fuel Finder returned an empty token response.");
    }

    private sealed class TokenRequest
    {
        [JsonPropertyName("client_id")]
        public required string ClientId { get; init; }

        [JsonPropertyName("client_secret")]
        public required string ClientSecret { get; init; }
    }

    private sealed class TokenEnvelope
    {
        [JsonPropertyName("data")]
        public TokenData? Data { get; init; }
    }

    private sealed class TokenData
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }
    }
}
