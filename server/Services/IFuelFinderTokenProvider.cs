namespace FuelFinder.Api.Services;

public interface IFuelFinderTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    void Invalidate();
}
