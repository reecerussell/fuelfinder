using System.Net;
using System.Net.Http.Headers;

namespace FuelFinder.Api.Services;

public sealed class FuelFinderAuthenticationHandler : DelegatingHandler
{
    private readonly IFuelFinderTokenProvider _tokenProvider;

    public FuelFinderAuthenticationHandler(IFuelFinderTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _tokenProvider.Invalidate();
        await AttachTokenAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
