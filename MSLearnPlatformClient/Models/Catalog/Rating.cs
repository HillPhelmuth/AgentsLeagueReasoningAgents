using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class Rating
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("average")]
    public double? Average { get; set; }
}
