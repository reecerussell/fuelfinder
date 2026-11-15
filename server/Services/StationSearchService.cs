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

    public StationSearchPage Search(StationSearchCriteria criteria)
    {
        var matches = _store.Stations
            .Where(station => criteria.FuelType is null || station.GetPrice(criteria.FuelType) is not null)
            .Select(station => new StationMatch
            {
                Station = station,
                DistanceKm = GeoDistance.HaversineKm(criteria.Latitude, criteria.Longitude, station.Latitude, station.Longitude),
            })
            .Where(match => match.DistanceKm <= criteria.RadiusKm);

        var ordered = Sort(matches, criteria).ToList();

        return new StationSearchPage
        {
            Total = ordered.Count,
            Matches = ordered.Take(MaxResults).ToList(),
        };
    }

    private static IEnumerable<StationMatch> Sort(
        IEnumerable<StationMatch> matches,
        StationSearchCriteria criteria)
    {
        if (criteria.Sort == StationSortOrder.Distance || criteria.FuelType is null)
        {
            return matches.OrderBy(match => match.DistanceKm);
        }

        var fuelType = criteria.FuelType;
        return matches
            .OrderBy(match => match.Station.GetPrice(fuelType)!.Price)
            .ThenBy(match => match.DistanceKm);
    }
}
