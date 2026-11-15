namespace FuelFinder.Api.Middleware;

public static class ClientIpAddress
{
    // Traffic arrives through the Cloudflare tunnel, so RemoteIpAddress is always the cloudflared pod.
    // Cloudflare overwrites CF-Connecting-IP on every request, so clients can't spoof it.
    private const string CloudflareClientIpHeader = "CF-Connecting-IP";

    public static string Resolve(HttpContext context)
    {
        var forwarded = context.Request.Headers[CloudflareClientIpHeader].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
