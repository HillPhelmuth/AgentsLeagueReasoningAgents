namespace MSLearnPlatformClient.Abstractions;

public interface ILearnAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(string[] scopes, CancellationToken cancellationToken);
}
