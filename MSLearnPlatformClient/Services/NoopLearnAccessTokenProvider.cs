using MSLearnPlatformClient.Abstractions;

namespace MSLearnPlatformClient.Services;

public sealed class NoopLearnAccessTokenProvider : ILearnAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(string[] scopes, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}
