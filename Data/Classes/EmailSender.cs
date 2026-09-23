using Azure;
using Azure.Communication.Email;
using Data.Util;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Data.Classes;

/// <summary>
/// Sends HTML mail through Azure Communication Services Email when configured,
/// otherwise Microsoft Graph (confirm-email and password-reset links).
/// </summary>
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly Lazy<GraphServiceClient> _graphClient = new(() => GraphMailAuth.CreateClient(configuration, logger));
    private readonly Lazy<EmailClient?> _acsClient = new(() =>
    {
        var connectionString = configuration.GetConnectionString("AzureCommunicationEmail")
            ?? configuration["Email:ConnectionString"];
        return string.IsNullOrWhiteSpace(connectionString) ? null : new EmailClient(connectionString);
    });

    /// <summary>
    /// Sends one HTML message. Prefers ACS Email when a connection string is set;
    /// otherwise uses Graph app-only or delegated Me.SendMail.
    /// </summary>
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var acs = _acsClient.Value;
        if (acs is not null)
        {
            await SendViaAcsAsync(acs, email, subject, htmlMessage);
            return;
        }

        await SendViaGraphAsync(email, subject, htmlMessage);
    }

    private async Task SendViaAcsAsync(EmailClient client, string email, string subject, string htmlMessage)
    {
        var sender = configuration["ReturnPath:SenderEmail"]
            ?? configuration["Email:SenderAddress"]
            ?? throw new InvalidOperationException("ReturnPath:SenderEmail is not configured for ACS Email.");

        var message = new EmailMessage(
            senderAddress: sender,
            content: new EmailContent(subject)
            {
                Html = htmlMessage
            },
            recipients: new EmailRecipients([new Azure.Communication.Email.EmailAddress(email)]));

        try
        {
            var operation = await client.SendAsync(WaitUntil.Started, message);
            logger.LogInformation(
                "Queued ACS email to {Email} (operation {OperationId}).",
                email,
                operation.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email} via Azure Communication Services.", email);
            throw;
        }
    }

    private async Task SendViaGraphAsync(string email, string subject, string htmlMessage)
    {
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = htmlMessage
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new Microsoft.Graph.Models.EmailAddress { Address = email }
                }
            ]
        };

        try
        {
            if (GraphMailAuth.IsClientCredentials(configuration))
            {
                var sender = configuration["ReturnPath:SenderEmail"]
                    ?? configuration["Graph:SenderEmail"]
                    ?? throw new InvalidOperationException("ReturnPath:SenderEmail is not configured.");

                await _graphClient.Value.Users[sender].SendMail.PostAsync(
                    new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                    {
                        Message = message,
                        SaveToSentItems = true
                    });
            }
            else
            {
                await _graphClient.Value.Me.SendMail.PostAsync(
                    new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
                    {
                        Message = message,
                        SaveToSentItems = true
                    });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email} via Microsoft Graph.", email);
            throw;
        }
    }
}
