using System.Text.Json.Serialization;

namespace AgentsLeagueReasoningAgents.Models;

public sealed class ReminderEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTime ScheduleUtc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public long? ScheduledSequenceNumber { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}