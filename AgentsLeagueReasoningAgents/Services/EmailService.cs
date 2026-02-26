using System;
using System.Collections.Generic;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentsLeagueReasoningAgents.Services;

public class EmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _logger = logger;
        var connectionString = configuration["CommunicationServices:ConnectionString"];
        _senderAddress = configuration["EMAIL_SENDER"] ?? string.Empty;

        _emailClient = new EmailClient(connectionString);
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string textContent, CancellationToken cancellationToken = default)
    {
        var emailMessage = new EmailMessage(
            senderAddress: _senderAddress,
            content: new EmailContent(subject)
            {
                PlainText = textContent
            },
            recipients: new EmailRecipients(new List<EmailAddress> { new(to) })
        );

        try
        {
            await _emailClient.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to send email to recipient {Recipient}", to);
            return false;
        }
    }

}