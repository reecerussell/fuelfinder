using FuelFinder.Api.Dtos;
using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Mvc;

namespace FuelFinder.Api.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    private readonly IFuelDataStore _store;

    public StatusController(IFuelDataStore store)
    {
        _store = store;
    }

    [HttpGet]
    public StatusResponse Get()
    {
        return new StatusResponse
        {
            Loaded = _store.IsLoaded,
            Stations = _store.Stations.Count,
            LastUpdated = _store.LastUpdated,
        };
    }
}
