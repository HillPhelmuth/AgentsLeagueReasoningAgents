using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Options;

namespace MSLearnPlatformClient.Services;

public sealed class EntraLearnAccessTokenProvider : ILearnAccessTokenProvider
{
    private readonly LearnCatalogOptions _options;
    private readonly ILogger<EntraLearnAccessTokenProvider> _logger;
    private TokenCredential? _credential;

    public EntraLearnAccessTokenProvider(
        IOptions<LearnCatalogOptions> options,
        ILogger<EntraLearnAccessTokenProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(string[] scopes, CancellationToken cancellationToken)
    {
        if (scopes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var credential = _credential ??= CreateCredential();
            var requestContext = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(token.Token) ? null : token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Learn access token for scopes: {Scopes}", string.Join(",", scopes));
            throw;
        }
    }

    private TokenCredential CreateCredential()
    {
        if (!string.IsNullOrWhiteSpace(_options.TenantId)
            && !string.IsNullOrWhiteSpace(_options.ClientId)
            && !string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        }

        var credentialOptions = new DefaultAzureCredentialOptions();

        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            credentialOptions.TenantId = _options.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(_options.ClientId))
        {
            credentialOptions.ManagedIdentityClientId = _options.ClientId;
        }

        return new DefaultAzureCredential(credentialOptions);
    }
}
