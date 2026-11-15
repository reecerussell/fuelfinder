using FuelFinder.Api.Models;
using FuelFinder.Api.Stores;

namespace FuelFinder.Api.Services;

public sealed class StationSearchService : IStationSearchService
{
    private const int MaxResults = 50;

    private readonly IFuelDataStore _store;

    public StationSearchService(IFuelDataStore store)
    {
        _store = store;
    }

    public StationSearchResults Search(StationSearchCriteria criteria)
    {
        var matches = _store.Stations
            .Where(station => criteria.FuelType is null || station.GetPrice(criteria.FuelType) is not null)
            .Select(station => new StationSearchResult
            {
                Station = station,
                DistanceKm = GeoDistance.HaversineKm(criteria.Latitude, criteria.Longitude, station.Latitude, station.Longitude),
            })
            .Where(result => result.DistanceKm <= criteria.RadiusKm);

        var ordered = Sort(matches, criteria).ToList();

        return new StationSearchResults
        {
            Total = ordered.Count,
            Results = ordered.Take(MaxResults).ToList(),
        };
    }

    private static IEnumerable<StationSearchResult> Sort(
        IEnumerable<StationSearchResult> results,
        StationSearchCriteria criteria)
    {
        if (criteria.Sort == StationSortOrder.Distance || criteria.FuelType is null)
        {
            return results.OrderBy(result => result.DistanceKm);
        }

        var fuelType = criteria.FuelType;
        return results
            .OrderBy(result => result.Station.GetPrice(fuelType)!.Price)
            .ThenBy(result => result.DistanceKm);
    }
}
