namespace MSLearnPlatformClient.Models;

public sealed class LearnMarkdownDocument
{
    public string? Uid { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public Uri? SourceUri { get; init; }
    public string? SourceType { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public string[]? ParentUids { get; init; }
    public string Markdown { get; init; } = string.Empty;
}
