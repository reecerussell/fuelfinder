using FuelFinder.Api.Models;

namespace FuelFinder.Api.Services;

public interface IStationSearchService
{
    StationSearchResults Search(StationSearchCriteria criteria);
}
