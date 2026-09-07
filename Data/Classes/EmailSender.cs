using Microsoft.AspNetCore.Identity.UI.Services;

namespace Data.Classes // Adjust namespace to match your project
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // TODO: Implement your actual email sending logic here later
            // For now, this just bypasses the error by returning a completed task
            return Task.CompletedTask;
        }
    }
}