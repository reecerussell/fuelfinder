using FuelFinder.Api.Models;

namespace FuelFinder.Api.Stores;

public sealed class FuelDataStore : IFuelDataStore
{
    private readonly TimeProvider _timeProvider;
    private volatile Snapshot _snapshot = new([], null);

    public FuelDataStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<Station> Stations => _snapshot.Stations;

    public DateTimeOffset? LastUpdated => _snapshot.LastUpdated;

    public bool IsLoaded => _snapshot.LastUpdated is not null;

    public void Replace(IReadOnlyList<Station> stations)
    {
        _snapshot = new Snapshot(stations, _timeProvider.GetUtcNow());
    }

    private sealed class Snapshot
    {
        public Snapshot(IReadOnlyList<Station> stations, DateTimeOffset? lastUpdated)
        {
            Stations = stations;
            LastUpdated = lastUpdated;
        }

        public IReadOnlyList<Station> Stations { get; }

        public DateTimeOffset? LastUpdated { get; }
    }
}
