using Application.Common.Interfaces;
using Hangfire;
using Infrastructure.Identity;
using Infrastructure.Jobs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Infrastructure.Email
{
    public class EmailService(
     IBackgroundJobClient jobs,
     IOptions<AppUrlOptions> urlOptions) : IEmailService
    {
        private readonly AppUrlOptions _urls = urlOptions.Value;
        public void QueueConfirmEmail(string email, string userId, string token)
        {
            var link = Link("confirm-email",
      ("userId", userId),
      ("token", Encode(token)));

            Queue(email, "Xác nhận địa chỉ email", EmailTemplates.ConfirmEmail(link));
        }

        public void QueueExportReady(string email, string title, string downloadLink, int rows)
          => Queue(email, $"{title} — file đã sẵn sàng",
              EmailTemplates.ExportReady(title, downloadLink, rows));

        public void QueueOrderConfirmed(string email, string orderCode, decimal total)
         => Queue(email, $"Xác nhận đơn hàng {orderCode}",
             EmailTemplates.OrderConfirmed(orderCode, total));

        public void QueueResetPassword(string email, string token)
        {
            var link = Link("reset-password",
                ("email", email),
                ("token", Encode(token)));

            Queue(email, "Đặt lại mật khẩu", EmailTemplates.ResetPassword(link));
        }


        private void Queue(string to, string subject, string html)
            => jobs.Enqueue<SendEmailJob>(j => j.RunAsync(to, subject, html));

        private string Link(string path, params (string Key, string Value)[] query)
       => QueryHelpers.AddQueryString(
           $"{_urls.Frontend.TrimEnd('/')}/{path}",
           query.ToDictionary(q => q.Key, q => q.Value));

        private static string Encode(string token) => IdentityTokenEncoder.Encode(token);
    }
}
