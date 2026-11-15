using FuelFinder.Api.Models;

namespace FuelFinder.Api.Stores;

public interface IFuelDataStore
{
    IReadOnlyList<Station> Stations { get; }

    DateTimeOffset? LastUpdated { get; }

    bool IsLoaded { get; }

    void Replace(IReadOnlyList<Station> stations);
}
