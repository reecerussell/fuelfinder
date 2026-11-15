using FuelFinder.Api;

var builder = WebApplication.CreateBuilder(args);

var fuelOptions = builder.Configuration.GetSection("FuelFinder").Get<FuelFinderOptions>() ?? new FuelFinderOptions();
builder.Services.AddSingleton(fuelOptions);
// Singleton so the access token and the one-request-at-a-time gate are shared.
// The API's firewall returns 403 for requests without a User-Agent.
var fuelHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
fuelHttp.DefaultRequestHeaders.UserAgent.ParseAdd("FuelFinderWeb/1.0");
builder.Services.AddSingleton(sp => new FuelFinderClient(
    fuelHttp,
    fuelOptions,
    sp.GetRequiredService<ILogger<FuelFinderClient>>()));
builder.Services.AddSingleton<FuelDataStore>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FuelDataStore>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/status", (FuelDataStore store) => new
{
    loaded = store.IsLoaded,
    stations = store.Count,
    lastUpdated = store.LastUpdated,
});

api.MapGet("/stations", (FuelDataStore store, double lat, double lng, double? radiusKm, string? fuel, string? sort) =>
{
    if (!store.IsLoaded)
        return Results.Problem("Fuel data is still loading, try again in a moment.", statusCode: 503);

    var radius = Math.Clamp(radiusKm ?? 8, 1, 50);
    var fuelType = string.IsNullOrWhiteSpace(fuel) ? null : fuel.ToUpperInvariant();

    var results = store.Search(lat, lng, radius, fuelType);
    results = sort == "distance" || fuelType is null
        ? results.OrderBy(r => r.DistanceKm)
        : results.OrderBy(r => r.Station.Prices.First(p => p.FuelType == fuelType).Price).ThenBy(r => r.DistanceKm);

    var list = results.ToList();
    return Results.Ok(new
    {
        lastUpdated = store.LastUpdated,
        total = list.Count,
        results = list.Take(50).Select(r => new
        {
            r.Station.Id,
            r.Station.Name,
            r.Station.Brand,
            r.Station.Address,
            r.Station.Postcode,
            r.Station.Phone,
            r.Station.Latitude,
            r.Station.Longitude,
            r.Station.IsMotorway,
            r.Station.IsSupermarket,
            r.Station.Amenities,
            r.Station.Prices,
            distanceKm = Math.Round(r.DistanceKm, 2),
        }),
    });
});

// Let the React router handle anything else.
app.MapFallbackToFile("index.html");

app.Run();
