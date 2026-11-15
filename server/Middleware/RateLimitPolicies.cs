using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FuelFinder.Api.Middleware;

public static class RateLimitPolicies
{
    public const string Search = "search";

    private const int SearchPermitsPerMinute = 60;

    public static IServiceCollection AddFindFuelRateLimiting(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejected;
            options.AddPolicy(Search, CreateSearchPartition);
        });
    }

    private static RateLimitPartition<string> CreateSearchPartition(HttpContext context)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            ClientIpAddress.Resolve(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = SearchPermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }

    private static ValueTask OnRejected(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        return ValueTask.CompletedTask;
    }
}
