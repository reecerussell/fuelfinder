using FuelFinder.Api.Dtos;

namespace FuelFinder.Api.Services;

public interface IFuelFinderApiClient
{
    Task<IReadOnlyList<FuelFinderStationDto>> GetStationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FuelFinderStationPricesDto>> GetPricesAsync(CancellationToken cancellationToken);
}
