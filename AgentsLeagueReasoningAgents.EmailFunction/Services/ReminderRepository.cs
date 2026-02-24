using AgentsLeagueReasoningAgents.EmailFunction.Models;
using Microsoft.Azure.Cosmos;

namespace AgentsLeagueReasoningAgents.EmailFunction.Services;

public class ReminderRepository(CosmosClient cosmosClient)
{
    private readonly Container _container = cosmosClient?.GetContainer("agent-league-db", "reminders-container") ?? throw new ArgumentNullException(nameof(cosmosClient));

    public async Task<ReminderEntity?> GetReminderAsync(string userId, string reminderId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<ReminderEntity>(reminderId, new PartitionKey(userId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task MarkSentAsync(string userId, string reminderId, string providerMessageId, DateTimeOffset sentAtUtc, CancellationToken ct = default)
    {
        var reminder = await GetReminderAsync(userId, reminderId, ct)
            ?? throw new InvalidOperationException($"Reminder not found: {reminderId}");

        reminder.Status = "Sent";
        reminder.ProviderMessageId = providerMessageId;
        reminder.SentAtUtc = sentAtUtc;
        reminder.FailureReason = null;
        reminder.LastUpdatedUtc = DateTimeOffset.UtcNow;

        await _container.UpsertItemAsync(reminder, new PartitionKey(userId), cancellationToken: ct);
    }

    public async Task MarkFailedAsync(string userId, string reminderId, string failureReason, CancellationToken ct = default)
    {
        var reminder = await GetReminderAsync(userId, reminderId, ct)
            ?? throw new InvalidOperationException($"Reminder not found: {reminderId}");

        reminder.Status = "Failed";
        reminder.FailureReason = failureReason;
        reminder.LastUpdatedUtc = DateTimeOffset.UtcNow;

        await _container.UpsertItemAsync(reminder, new PartitionKey(userId), cancellationToken: ct);
    }
}
