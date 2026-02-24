namespace MSLearnPlatformClient.Services;

using Polly;

internal sealed class RetryPolicyHandler(IAsyncPolicy<HttpResponseMessage> policy) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
    }
}
