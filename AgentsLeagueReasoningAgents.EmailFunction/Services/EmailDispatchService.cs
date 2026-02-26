using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;

namespace AgentsLeagueReasoningAgents.EmailFunction.Services;

public class EmailDispatchService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;

    public EmailDispatchService(IConfiguration configuration)
    {
        var connectionString = configuration["CommunicationServices:ConnectionString"]
            ?? throw new InvalidOperationException("Missing CommunicationServices:ConnectionString setting.");

        _senderAddress = configuration["EMAIL_SENDER"]
            ?? throw new InvalidOperationException("Missing EMAIL_SENDER setting.");

        _emailClient = new EmailClient(connectionString);
    }

    public async Task<string> SendAsync(string to, string subject, string textContent, CancellationToken cancellationToken = default)
    {
        var emailMessage = new EmailMessage(
            senderAddress: _senderAddress,
            content: new EmailContent(subject)
            {
                PlainText = textContent
            },
            recipients: new EmailRecipients([new EmailAddress(to)]));

        var sendOperation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);
        return sendOperation.Id;
    }
}
