using Application.Common.Interfaces;
using Hangfire;

namespace Infrastructure.Jobs
{
    /// <summary>
    /// Gửi email qua Hangfire để không block request. Hangfire tự retry khi SMTP lỗi tạm thời.
    /// </summary>
    public class SendEmailJob(IEmailSender sender)
    {
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 600])]
        public Task RunAsync(string to, string subject, string htmlBody)
            => sender.SendAsync(to, subject, htmlBody, CancellationToken.None);
    }
}
