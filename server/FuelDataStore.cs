namespace FuelFinder.Api;

public record FuelPrice(string FuelType, decimal Price, DateTimeOffset? UpdatedAt);

public record Station(
    string Id,
    string Name,
    string? Brand,
    string Address,
    string? Postcode,
    string? Phone,
    double Latitude,
    double Longitude,
    bool IsMotorway,
    bool IsSupermarket,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<FuelPrice> Prices);

public record StationResult(Station Station, double DistanceKm);

/// <summary>
/// The Fuel Finder API is bulk-only (no search by location), so we keep the whole
/// dataset in memory and query it locally. Prices refresh every 15 minutes and
/// station details every hour, in line with the API's caching guidance.
/// </summary>
public class FuelDataStore(FuelFinderClient client, ILogger<FuelDataStore> logger) : BackgroundService
{
    private static readonly TimeSpan PriceRefresh = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StationRefresh = TimeSpan.FromHours(1);

    private List<ApiStation> _rawStations = [];
    private DateTimeOffset _stationsLoadedAt = DateTimeOffset.MinValue;
    private volatile IReadOnlyList<Station> _stations = [];

    public DateTimeOffset? LastUpdated { get; private set; }
    public bool IsLoaded => LastUpdated is not null;
    public int Count => _stations.Count;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to refresh fuel data");
            }

            // Retry sooner if we've never loaded successfully.
            await Task.Delay(IsLoaded ? PriceRefresh : TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _stationsLoadedAt > StationRefresh)
        {
            logger.LogInformation("Loading station details…");
            _rawStations = await client.GetAllStationsAsync(ct);
            _stationsLoadedAt = DateTimeOffset.UtcNow;
        }

        logger.LogInformation("Loading fuel prices…");
        var prices = (await client.GetAllPricesAsync(ct))
            .GroupBy(p => p.NodeId)
            .ToDictionary(g => g.Key, g => g.First().FuelPrices ?? []);

        var stations = new List<Station>(_rawStations.Count);
        foreach (var s in _rawStations)
        {
            if (s.PermanentClosure == true || s.TemporaryClosure == true) continue;
            if (s.Location?.Latitude is not { } lat || s.Location.Longitude is not { } lng) continue;

            var stationPrices = prices.GetValueOrDefault(s.NodeId, [])
                .Select(p => NormalisePrice(p))
                .OfType<FuelPrice>()
                .ToList();
            if (stationPrices.Count == 0) continue;

            var loc = s.Location;
            var address = string.Join(", ", new[] { loc.AddressLine1, loc.AddressLine2, loc.City }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => TitleCase(x!)));

            stations.Add(new Station(
                s.NodeId,
                TitleCase(s.TradingName ?? s.BrandName ?? "Unknown"),
                s.BrandName is null ? null : TitleCase(s.BrandName),
                address,
                loc.Postcode,
                s.PhoneNumber,
                lat, lng,
                s.IsMotorway == true, s.IsSupermarket == true,
                s.Amenities ?? [],
                stationPrices));
        }

        _stations = stations;
        LastUpdated = DateTimeOffset.UtcNow;
        logger.LogInformation("Loaded {Count} stations with prices", stations.Count);
    }

    /// <summary>Prices are pence per litre. Some retailers submit pounds (e.g. 1.459), so fix those up and drop nonsense values.</summary>
    private static FuelPrice? NormalisePrice(ApiFuelPrice p)
    {
        if (p.Price is not { } price || price <= 0) return null;
        if (price < 10) price *= 100;
        if (price is < 80 or > 400) return null;
        return new FuelPrice(p.FuelType.ToUpperInvariant(), Math.Round(price, 1), p.PriceLastUpdated);
    }

    private static string TitleCase(string s) =>
        System.Globalization.CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());

    public IEnumerable<StationResult> Search(double lat, double lng, double radiusKm, string? fuelType)
    {
        foreach (var s in _stations)
        {
            if (fuelType is not null && !s.Prices.Any(p => p.FuelType == fuelType)) continue;
            var d = HaversineKm(lat, lng, s.Latitude, s.Longitude);
            if (d <= radiusKm) yield return new StationResult(s, d);
        }
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double ToRad(double x) => x * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
