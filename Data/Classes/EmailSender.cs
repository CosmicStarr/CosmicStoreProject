using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;

namespace Data.Classes;

public class EmailSender(IConfiguration configuration) : IEmailSender
{
    private readonly IConfiguration _configuration = configuration;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        return Task.CompletedTask;
        // var emailFromMySite = _configuration["ReturnPath:SenderEmail"]
        //     ?? throw new InvalidOperationException("ReturnPath:SenderEmail is not configured.");
        // var newMessage = new MailMessage(emailFromMySite, email, subject, htmlMessage)
        // {
        //     IsBodyHtml = true,
        //     Body = htmlMessage
        // };

        // using var client = new SmtpClient(
        //     _configuration["STMP:Host"],
        //     int.Parse(_configuration["STMP:Port"] ?? "587"))
        // {
        //     EnableSsl = true,
        //     Credentials = new NetworkCredential(
        //         _configuration["STMP:Username"],
        //         _configuration["STMP:Password"])
        // };

        // await client.SendMailAsync(newMessage);
    }
}
