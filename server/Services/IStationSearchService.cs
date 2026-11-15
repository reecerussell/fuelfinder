using FuelFinder.Api.Models;

namespace FuelFinder.Api.Services;

public interface IStationSearchService
{
    StationSearchPage Search(StationSearchCriteria criteria);
}
