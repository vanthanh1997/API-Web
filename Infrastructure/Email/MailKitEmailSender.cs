using Application.Common.Interfaces;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;

namespace Infrastructure.Email
{
    public class MailKitEmailSender(
     IOptions<EmailOptions> options,
     ILogger<MailKitEmailSender> logger) : IEmailSender
    {
        private readonly EmailOptions _options = options.Value;
        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                ct);

            // SMTP dev (Mailpit) không cần đăng nhập nên UserName để trống -> bỏ qua bước này.
            // Password vẫn có thể null khi server chỉ đòi username, truyền chuỗi rỗng cho hợp lệ.
            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("Đã gửi email tới {To}: {Subject}", to, subject);
        }
    }
}
