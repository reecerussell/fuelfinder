namespace FuelFinder.Api.Models;

public sealed class Station
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Brand { get; init; }

    public required string Address { get; init; }

    public string? Postcode { get; init; }

    public string? Phone { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public bool IsMotorway { get; init; }

    public bool IsSupermarket { get; init; }

    public required IReadOnlyList<string> Amenities { get; init; }

    public required IReadOnlyList<FuelPrice> Prices { get; init; }

    public FuelPrice? GetPrice(string fuelType)
    {
        return Prices.FirstOrDefault(price => price.FuelType == fuelType);
    }
}
