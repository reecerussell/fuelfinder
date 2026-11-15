using FuelFinder.Api.Endpoints;
using FuelFinder.Api.Middleware;
using FuelFinder.Api.Options;
using FuelFinder.Api.Serialization;
using FuelFinder.Api.Services;
using FuelFinder.Api.Stores;
using Microsoft.Extensions.Options;

namespace FuelFinder.Api;

public static class Program
{
    private const string UserAgent = "FuelFinderWeb/1.0";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();

        ConfigurePipeline(app);

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<FuelFinderOptions>()
            .Bind(configuration.GetSection(FuelFinderOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret),
                "FuelFinder:ClientId and FuelFinder:ClientSecret must be configured.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFuelDataStore, FuelDataStore>();
        services.AddSingleton<IFuelFinderTokenProvider, FuelFinderTokenProvider>();
        services.AddSingleton<IFuelFinderApiClient, FuelFinderApiClient>();
        services.AddSingleton<IStationSearchService, StationSearchService>();
        services.AddTransient<FuelFinderAuthenticationHandler>();
        services.AddHostedService<FuelDataRefreshService>();

        services
            .AddHttpClient(FuelFinderTokenProvider.HttpClientName, ConfigureFuelFinderClient)
            .AddStandardResilienceHandler();

        services
            .AddHttpClient(FuelFinderApiClient.HttpClientName, ConfigureFuelFinderClient)
            .AddHttpMessageHandler<FuelFinderAuthenticationHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        services.AddValidation();
        services.AddFindFuelRateLimiting();
        services.AddProblemDetails();
    }

    private static void ConfigureFuelFinderClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<FuelFinderOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;

        // The API's firewall rejects requests without a User-Agent with a 403.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseMiddleware<ResponseHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseRateLimiter();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapStationsEndpoints();
        app.MapStatusEndpoints();
        app.MapHealthEndpoints();
        app.MapFallbackToFile("index.html");
    }
}
