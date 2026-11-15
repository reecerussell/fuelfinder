using FuelFinder.Api.Stores;
using Microsoft.AspNetCore.Mvc;

namespace FuelFinder.Api.Controllers;

[ApiController]
[Route("healthz")]
public sealed class HealthController : ControllerBase
{
    private readonly IFuelDataStore _store;

    public HealthController(IFuelDataStore store)
    {
        _store = store;
    }

    // Used as the Kubernetes readiness probe, so the pod only takes traffic once prices have loaded.
    [HttpGet]
    public IActionResult Get()
    {
        return _store.IsLoaded ? Ok("ok") : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
