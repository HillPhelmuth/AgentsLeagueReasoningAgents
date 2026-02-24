using AgentsLeagueReasoningAgents.EmailFunction.Models;
using AgentsLeagueReasoningAgents.EmailFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgentsLeagueReasoningAgents.EmailFunction;

public class SendEmailFunction
{
    private readonly ILogger<SendEmailFunction> _log;
    private readonly ReminderRepository _repo;
    private readonly EmailDispatchService _email;

    public SendEmailFunction(
        ILogger<SendEmailFunction> logger,
        ReminderRepository repo,
        EmailDispatchService email)
    {
        _log = logger;
        _repo = repo;
        _email = email;
    }

    //[Function(nameof(SendEmailFunction))]
    //public async Task Run(
    //    [ServiceBusTrigger("myqueue", Connection = "ConnectionStrings:ServiceBus")]
    //    ServiceBusReceivedMessage message,
    //    ServiceBusMessageActions messageActions)
    //{
    //    _logger.LogInformation("Message ID: {id}", message.MessageId);
    //    _logger.LogInformation("Message Body: {body}", message.Body);
    //    _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

    //    // Complete the message
    //    await messageActions.CompleteMessageAsync(message);
    //}
    [Function("ReminderSender")]
    public async Task RunAsync(
        [ServiceBusTrigger("reminders", Connection = "ConnectionStrings:ServiceBus")] string body,
        FunctionContext context,
        CancellationToken ct)
    {
        ReminderDispatchMessage? msg;

        try
        {
            msg = JsonSerializer.Deserialize<ReminderDispatchMessage>(body);
            if (msg is null) throw new JsonException("Message payload was null.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invalid reminder message. Body: {Body}", body);
            throw; // poison message handling via retries/DLQ
        }

        var record = await _repo.GetReminderAsync(msg.UserId, msg.ReminderId, ct);
        if (record is null)
        {
            _log.LogWarning("Reminder not found: {ReminderId}", msg.ReminderId);
            return; // no-op, avoid infinite retries for missing data
        }

        if (!string.Equals(record.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInformation("Reminder is not scheduled (Status: {Status}). Skipping. ReminderId: {ReminderId}",
                record.Status, record.Id);
            return; // idempotency + cancellation safety
        }

        try
        {
            var providerId = await _email.SendAsync(record.RecipientEmail, record.Subject, record.Body, ct);
            await _repo.MarkSentAsync(record.UserId, record.Id, providerId, DateTimeOffset.UtcNow, ct);

            _log.LogInformation("Sent reminder {ReminderId} to {EmailTo}", record.Id, record.RecipientEmail);
        }
        catch (Exception ex)
        {
            await _repo.MarkFailedAsync(record.UserId, record.Id, ex.Message, ct);
            _log.LogError(ex, "Failed sending reminder {ReminderId}", record.Id);
            throw; // let Service Bus retry, then DLQ
        }
    }
}