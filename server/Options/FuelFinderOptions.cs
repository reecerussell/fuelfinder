namespace FuelFinder.Api.Options;

public sealed class FuelFinderOptions
{
    public const string SectionName = "FuelFinder";

    public string BaseUrl { get; set; } = "https://www.fuel-finder.service.gov.uk/api/v1/";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
