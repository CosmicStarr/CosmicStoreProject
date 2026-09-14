using Data.Util;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Data.Classes;

/// <summary>
/// Sends HTML mail through Microsoft Graph (confirm-email and password-reset links).
/// </summary>
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly Lazy<GraphServiceClient> _graphClient = new(() => GraphMailAuth.CreateClient(configuration, logger));

    /// <summary>
    /// Sends one HTML message. Uses app-only send when Graph client credentials are configured, otherwise delegated Me.SendMail.
    /// </summary>
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
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
                    EmailAddress = new EmailAddress { Address = email }
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
