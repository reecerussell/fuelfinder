using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using FuelFinder.Api.Services;
using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Mvc;

namespace FuelFinder.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController : ControllerBase
{
    private const double DefaultRadiusKm = 8;
    private const double MinimumRadiusKm = 1;
    private const double MaximumRadiusKm = 50;

    private readonly IStationSearchService _searchService;
    private readonly IFuelDataStore _store;

    public StationsController(IStationSearchService searchService, IFuelDataStore store)
    {
        _searchService = searchService;
        _store = store;
    }

    [HttpGet]
    [ProducesResponseType<StationSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<StationSearchResponse> Search([FromQuery] StationSearchRequest request)
    {
        if (!_store.IsLoaded)
        {
            return Problem(
                detail: "Fuel data is still loading, try again in a moment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var criteria = new StationSearchCriteria
        {
            Latitude = request.Lat!.Value,
            Longitude = request.Lng!.Value,
            RadiusKm = Math.Clamp(request.RadiusKm ?? DefaultRadiusKm, MinimumRadiusKm, MaximumRadiusKm),
            FuelType = string.IsNullOrWhiteSpace(request.Fuel) ? null : request.Fuel.ToUpperInvariant(),
            Sort = request.Sort,
        };

        var results = _searchService.Search(criteria);

        return new StationSearchResponse
        {
            LastUpdated = _store.LastUpdated,
            Total = results.Total,
            Results = results.Results.Select(ToResponse).ToList(),
        };
    }

    private static StationResponse ToResponse(StationSearchResult result)
    {
        var station = result.Station;

        return new StationResponse
        {
            Id = station.Id,
            Name = station.Name,
            Brand = station.Brand,
            Address = station.Address,
            Postcode = station.Postcode,
            Phone = station.Phone,
            Latitude = station.Latitude,
            Longitude = station.Longitude,
            IsMotorway = station.IsMotorway,
            IsSupermarket = station.IsSupermarket,
            Amenities = station.Amenities,
            Prices = station.Prices.Select(ToResponse).ToList(),
            DistanceKm = Math.Round(result.DistanceKm, 2),
        };
    }

    private static FuelPriceResponse ToResponse(FuelPrice price)
    {
        return new FuelPriceResponse
        {
            FuelType = price.FuelType,
            Price = price.Price,
            UpdatedAt = price.UpdatedAt,
        };
    }
}
