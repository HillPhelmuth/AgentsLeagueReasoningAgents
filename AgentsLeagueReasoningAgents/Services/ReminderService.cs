using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace AgentsLeagueReasoningAgents.Services;

public class ReminderService(ServiceBusClient client, string queueName) 
{
    private readonly string _queueName = string.IsNullOrWhiteSpace(queueName) ? throw new ArgumentException("Queue name is required.", nameof(queueName)) : queueName;

    public async Task<long> ScheduleReminderAsync(string reminderId,
        string userId,
        DateTimeOffset sendAtUtc,
        CancellationToken ct = default)
    {
        var sender = client.CreateSender(_queueName);

        var payload = new ReminderDispatchMessage(reminderId, userId);
        var json = JsonSerializer.Serialize(payload);

        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = "reminder.dispatch",
            MessageId = reminderId.ToString()
        };

        // Schedule for exact delivery time (UTC)
        return await sender.ScheduleMessageAsync(message, sendAtUtc.UtcDateTime, ct);
    }

    public async Task CancelScheduledReminderAsync(long sequenceNumber, CancellationToken ct = default)
    {
        var sender = client.CreateSender(_queueName);
        await sender.CancelScheduledMessageAsync(sequenceNumber, ct);
    }
}
public sealed record ReminderDispatchMessage(
    string ReminderId,
    string UserId
);