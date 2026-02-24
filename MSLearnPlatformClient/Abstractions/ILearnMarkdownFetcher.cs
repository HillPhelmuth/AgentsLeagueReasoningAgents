namespace MSLearnPlatformClient.Abstractions;

public interface ILearnMarkdownFetcher
{
    Task<string> FetchMarkdownAsync(Uri uri, string? locale, CancellationToken cancellationToken);
}
