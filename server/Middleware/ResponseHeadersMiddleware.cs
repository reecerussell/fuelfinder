namespace FuelFinder.Api.Middleware;

public sealed class ResponseHeadersMiddleware
{
    private const string ServerName = "FindFuel";

    private readonly RequestDelegate _next;

    public ResponseHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(ApplyHeaders, context);
        return _next(context);
    }

    private static Task ApplyHeaders(object state)
    {
        var headers = ((HttpContext)state).Response.Headers;

        headers.Server = ServerName;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";

        return Task.CompletedTask;
    }
}
