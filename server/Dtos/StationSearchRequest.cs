using System.ComponentModel.DataAnnotations;
using FuelFinder.Api.Models;

namespace FuelFinder.Api.Dtos;

public sealed class StationSearchRequest
{
    [Required]
    [Range(-90, 90)]
    public double? Lat { get; init; }

    [Required]
    [Range(-180, 180)]
    public double? Lng { get; init; }

    public double? RadiusKm { get; init; }

    public string? Fuel { get; init; }

    public StationSortOrder Sort { get; init; } = StationSortOrder.Price;
}
