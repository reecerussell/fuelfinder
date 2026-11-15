using System.Net;
using System.Text.Json.Serialization.Metadata;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Serialization;

namespace FuelFinder.Api.Services;

public sealed class FuelFinderApiClient : IFuelFinderApiClient
{
    public const string HttpClientName = "FuelFinder";

    private const int BatchSize = 500;
    private const int MaxBatches = 200;

    private readonly IHttpClientFactory _httpClientFactory;

    // The Fuel Finder API allows only one concurrent request per client.
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    public FuelFinderApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<IReadOnlyList<FuelFinderStationDto>> GetStationsAsync(CancellationToken cancellationToken)
    {
        return GetAllBatchesAsync("pfs", AppJsonSerializerContext.Default.ListFuelFinderStationDto, cancellationToken);
    }

    public Task<IReadOnlyList<FuelFinderStationPricesDto>> GetPricesAsync(CancellationToken cancellationToken)
    {
        return GetAllBatchesAsync("pfs/fuel-prices", AppJsonSerializerContext.Default.ListFuelFinderStationPricesDto, cancellationToken);
    }

    // Data is paged in batches of 500; a 404 means we've gone past the last batch.
    private async Task<IReadOnlyList<T>> GetAllBatchesAsync<T>(
        string path,
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        var all = new List<T>();

        for (var batchNumber = 1; batchNumber <= MaxBatches; batchNumber++)
        {
            var batch = await GetBatchAsync($"{path}?batch-number={batchNumber}", typeInfo, cancellationToken);
            all.AddRange(batch);

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return all;
    }

    private async Task<List<T>> GetBatchAsync<T>(
        string relativeUrl,
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(relativeUrl, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken) ?? [];
        }
        finally
        {
            _requestGate.Release();
        }
    }
}
