using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSLearnPlatformClient.Options;
using Polly;
using Polly.Extensions.Http;

namespace MSLearnPlatformClient.Services;

internal static class LearnHttpPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(IOptions<LearnCatalogOptions> options, ILogger logger)
    {
        var retryCount = Math.Max(0, options.Value.RetryCount);
        var baseDelay = options.Value.RetryBaseDelay;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(retryCount, attempt => GetDelay(attempt, baseDelay),
                onRetryAsync: async (outcome, timespan, attempt, _) =>
                {
                    if (outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response && response.Headers.RetryAfter is not null)
                    {
                        var retryAfter = response.Headers.RetryAfter.Delta ?? response.Headers.RetryAfter.Date - DateTimeOffset.UtcNow;
                        if (retryAfter is { TotalSeconds: > 0 } && retryAfter > timespan)
                        {
                            logger.LogWarning("Received 429. Respecting Retry-After of {Delay}.", retryAfter);
                            await Task.Delay((TimeSpan)(retryAfter - timespan)).ConfigureAwait(false);
                        }
                    }

                    logger.LogWarning("Retry {Attempt} after {Delay} for {Reason}.", attempt, timespan, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    private static TimeSpan GetDelay(int attempt, TimeSpan baseDelay)
    {
        var multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier);
    }
}
