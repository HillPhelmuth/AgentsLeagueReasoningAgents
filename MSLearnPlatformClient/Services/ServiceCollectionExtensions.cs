using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Options;
using System.Net;

namespace MSLearnPlatformClient.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMsLearnCatalogClient(this IServiceCollection services, Action<LearnCatalogOptions>? configure = null)
    {
        services.AddOptions<LearnCatalogOptions>();
        services.TryAddSingleton<ILearnAccessTokenProvider, EntraLearnAccessTokenProvider>();

        if (configure is not null)
        {
            services.Configure(configure);
        }
        services.AddHttpClient<ILearnCatalogClient, LearnCatalogClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LearnCatalogOptions>>().Value;
            client.BaseAddress = options.BaseUri;
            client.Timeout = options.RequestTimeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            })
            .AddHttpMessageHandler(provider =>
            {
                var options = provider.GetRequiredService<IOptions<LearnCatalogOptions>>();
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("LearnCatalogClientPolicy");
                var policy = LearnHttpPolicies.CreateRetryPolicy(options, logger);
                return new RetryPolicyHandler(policy);
            });

        return services;
    }
}
