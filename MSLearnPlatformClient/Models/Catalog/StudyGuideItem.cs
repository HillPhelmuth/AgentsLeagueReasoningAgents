namespace MSLearnPlatformClient.Models.Catalog;

public sealed record StudyGuideItem
{
    public string? Uid { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public string? Type { get; init; }
}