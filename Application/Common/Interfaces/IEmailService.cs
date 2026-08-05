namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        void QueueConfirmEmail(string email, string userId, string token);

        void QueueResetPassword(string email, string token);

        void QueueOrderConfirmed(string email, string orderCode, decimal total);

        void QueueExportReady(string email, string title, string downloadLink, int rows);
    }
}
