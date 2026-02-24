namespace MSLearnPlatformClient.Options;

public sealed class LearnCatalogOptions
{
    public Uri BaseUri { get; set; } = new("https://learn.microsoft.com/api/v1/");
    public string ApiVersion { get; set; } = "2023-11-01-preview";
    public string? DefaultLocale { get; set; }
    public int MaxPageSize { get; set; } = 100;
    public string[] Scopes { get; set; } = [];
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public int MaxConcurrency { get; set; } = 6;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public string? OutputDirectory { get; set; }
    public string UserAgent { get; set; } = "MsLearnCatalogClient/1.0";
    public int RetryCount { get; set; } = 4;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
