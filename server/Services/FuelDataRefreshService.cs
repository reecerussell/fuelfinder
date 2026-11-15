using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using FuelFinder.Api.Stores;

namespace FuelFinder.Api.Services;

public sealed class FuelDataRefreshService : BackgroundService
{
    private static readonly TimeSpan PriceRefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StationRefreshInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

    private readonly IFuelFinderApiClient _apiClient;
    private readonly IFuelDataStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FuelDataRefreshService> _logger;
    private IReadOnlyList<FuelFinderStationDto> _stations = [];
    private DateTimeOffset _stationsLoadedAt = DateTimeOffset.MinValue;

    public FuelDataRefreshService(
        IFuelFinderApiClient apiClient,
        IFuelDataStore store,
        TimeProvider timeProvider,
        ILogger<FuelDataRefreshService> logger)
    {
        _apiClient = apiClient;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to refresh fuel data");
            }

            var delay = _store.IsLoaded ? PriceRefreshInterval : RetryInterval;
            await Task.Delay(delay, _timeProvider, stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_timeProvider.GetUtcNow() - _stationsLoadedAt > StationRefreshInterval)
        {
            _logger.LogInformation("Loading station details");
            _stations = await _apiClient.GetStationsAsync(cancellationToken);
            _stationsLoadedAt = _timeProvider.GetUtcNow();
        }

        _logger.LogInformation("Loading fuel prices");
        var prices = await _apiClient.GetPricesAsync(cancellationToken);
        var pricesByStation = prices
            .GroupBy(price => price.NodeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<FuelFinderFuelPriceDto>)(group.First().FuelPrices ?? []));

        var stations = new List<Station>(_stations.Count);
        foreach (var dto in _stations)
        {
            var stationPrices = pricesByStation.GetValueOrDefault(dto.NodeId, []);
            var station = StationFactory.Create(dto, stationPrices);
            if (station is not null)
            {
                stations.Add(station);
            }
        }

        _store.Replace(stations);
        _logger.LogInformation("Loaded {Count} stations with prices", stations.Count);
    }
}
