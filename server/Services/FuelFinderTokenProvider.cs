using FuelFinder.Api.Dtos;
using FuelFinder.Api.Options;
using FuelFinder.Api.Serialization;
using Microsoft.Extensions.Options;

namespace FuelFinder.Api.Services;

public sealed class FuelFinderTokenProvider : IFuelFinderTokenProvider
{
    public const string HttpClientName = "FuelFinderAuth";

    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FuelFinderOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile CachedToken? _token;

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
        var token = _token;
        if (token is not null && !token.HasExpired(_timeProvider))
        {
            return token.Value;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            token = _token;
            if (token is null || token.HasExpired(_timeProvider))
            {
                token = await RequestTokenAsync(cancellationToken);
                _token = token;
            }

            return token.Value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        _token = null;
    }

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var request = new FuelFinderTokenRequestDto
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
        };

        using var response = await client.PostAsJsonAsync(
            "oauth/generate_access_token",
            request,
            AppJsonSerializerContext.Default.FuelFinderTokenRequestDto,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.FuelFinderTokenResponseDto,
            cancellationToken);
        var data = body?.Data ?? throw new InvalidOperationException("Fuel Finder returned an empty token response.");

        var expiresAt = _timeProvider.GetUtcNow().AddSeconds(data.ExpiresIn) - ExpiryBuffer;
        return new CachedToken(data.AccessToken, expiresAt);
    }

    private sealed class CachedToken
    {
        public CachedToken(string value, DateTimeOffset expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }

        public string Value { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool HasExpired(TimeProvider timeProvider)
        {
            return timeProvider.GetUtcNow() >= ExpiresAt;
        }
    }
}
