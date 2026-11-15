using System.Globalization;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;

namespace FuelFinder.Api.Services;

public static class StationFactory
{
    private const decimal MinimumPlausiblePrice = 80m;
    private const decimal MaximumPlausiblePrice = 400m;

    private static readonly TextInfo TextInfo = CultureInfo.GetCultureInfo("en-GB").TextInfo;

    public static Station? Create(FuelFinderStationDto station, IReadOnlyList<FuelFinderFuelPriceDto> prices)
    {
        if (station.PermanentClosure == true || station.TemporaryClosure == true)
        {
            return null;
        }

        var location = station.Location;
        if (location?.Latitude is not { } latitude || location.Longitude is not { } longitude)
        {
            return null;
        }

        var fuelPrices = prices
            .Select(CreatePrice)
            .OfType<FuelPrice>()
            .ToList();

        if (fuelPrices.Count == 0)
        {
            return null;
        }

        return new Station
        {
            Id = station.NodeId,
            Name = ToTitleCase(station.TradingName ?? station.BrandName ?? "Unknown"),
            Brand = station.BrandName is null ? null : ToTitleCase(station.BrandName),
            Address = FormatAddress(location),
            Postcode = location.Postcode,
            Phone = station.PhoneNumber,
            Latitude = latitude,
            Longitude = longitude,
            IsMotorway = station.IsMotorway == true,
            IsSupermarket = station.IsSupermarket == true,
            Amenities = station.Amenities ?? [],
            Prices = fuelPrices,
        };
    }

    // Prices are pence per litre, but some retailers submit pounds (e.g. 1.459), and a few submit nonsense.
    private static FuelPrice? CreatePrice(FuelFinderFuelPriceDto dto)
    {
        if (dto.Price is not { } price || price <= 0)
        {
            return null;
        }

        if (price < 10)
        {
            price *= 100;
        }

        if (price is < MinimumPlausiblePrice or > MaximumPlausiblePrice)
        {
            return null;
        }

        return new FuelPrice
        {
            FuelType = dto.FuelType.ToUpperInvariant(),
            Price = Math.Round(price, 1),
            UpdatedAt = dto.PriceLastUpdated,
        };
    }

    private static string FormatAddress(FuelFinderLocationDto location)
    {
        var parts = new[] { location.AddressLine1, location.AddressLine2, location.City }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => ToTitleCase(part!));

        return string.Join(", ", parts);
    }

    private static string ToTitleCase(string value)
    {
        return TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
    }
}
