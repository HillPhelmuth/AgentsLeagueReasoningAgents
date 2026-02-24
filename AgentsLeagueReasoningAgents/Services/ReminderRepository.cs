using System;
using System.Collections.Generic;
using System.Text;
using AgentsLeagueReasoningAgents.Models;
using Microsoft.Azure.Cosmos;

namespace AgentsLeagueReasoningAgents.Services;

public class ReminderRepository(CosmosClient cosmosClient)
{
    private readonly Container _container = cosmosClient?.GetContainer("agent-league-db", "reminders-container") ?? throw new ArgumentNullException(nameof(cosmosClient));
    
    public async Task AddReminderAsync(ReminderEntity reminder, CancellationToken ct = default)
    {
        await _container.CreateItemAsync(reminder, new PartitionKey(reminder.UserId), cancellationToken: ct);
    }

    public async Task UpsertReminderAsync(ReminderEntity reminder, CancellationToken ct = default)
    {
        reminder.LastUpdatedUtc = DateTimeOffset.UtcNow;
        await _container.UpsertItemAsync(reminder, new PartitionKey(reminder.UserId), cancellationToken: ct);
    }

    public async Task DeleteReminderAsync(string userId, string reminderId, CancellationToken ct = default)
    {
        await _container.DeleteItemAsync<ReminderEntity>(reminderId, new PartitionKey(userId), cancellationToken: ct);
    }

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

    public async Task MarkScheduledAsync(string userId, string reminderId, long sequenceNumber, CancellationToken ct = default)
    {
        var reminder = await GetReminderAsync(userId, reminderId, ct)
            ?? throw new InvalidOperationException($"Reminder not found: {reminderId}");

        reminder.Status = "Scheduled";
        reminder.ScheduledSequenceNumber = sequenceNumber;
        reminder.FailureReason = null;

        await UpsertReminderAsync(reminder, ct);
    }

    public async Task MarkSentAsync(string userId, string reminderId, string providerMessageId, DateTimeOffset sentAtUtc, CancellationToken ct = default)
    {
        var reminder = await GetReminderAsync(userId, reminderId, ct)
            ?? throw new InvalidOperationException($"Reminder not found: {reminderId}");

        reminder.Status = "Sent";
        reminder.ProviderMessageId = providerMessageId;
        reminder.SentAtUtc = sentAtUtc;
        reminder.FailureReason = null;

        await UpsertReminderAsync(reminder, ct);
    }

    public async Task MarkFailedAsync(string userId, string reminderId, string failureReason, CancellationToken ct = default)
    {
        var reminder = await GetReminderAsync(userId, reminderId, ct)
            ?? throw new InvalidOperationException($"Reminder not found: {reminderId}");

        reminder.Status = "Failed";
        reminder.FailureReason = failureReason;

        await UpsertReminderAsync(reminder, ct);
    }
}