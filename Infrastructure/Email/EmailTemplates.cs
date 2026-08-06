namespace Infrastructure.Email
{
    public static class EmailTemplates
    {
        public static string ConfirmEmail(string link) => Wrap(
            "Xác nhận địa chỉ email",
            $"""
         <p>Cảm ơn bạn đã đăng ký tài khoản WebShop.</p>
         <p>Bấm vào nút bên dưới để xác nhận địa chỉ email:</p>
         <p><a href="{link}" style="background:#2563eb;color:#fff;padding:10px 18px;
            border-radius:6px;text-decoration:none;display:inline-block">Xác nhận email</a></p>
         <p style="color:#666;font-size:13px">Nếu bạn không đăng ký tài khoản, hãy bỏ qua email này.</p>
         """);

        public static string ResetPassword(string link) => Wrap(
            "Đặt lại mật khẩu",
            $"""
         <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
         <p><a href="{link}" style="background:#2563eb;color:#fff;padding:10px 18px;
            border-radius:6px;text-decoration:none;display:inline-block">Đặt lại mật khẩu</a></p>
         <p style="color:#666;font-size:13px">Liên kết có hiệu lực trong 1 giờ.
            Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu hiện tại vẫn an toàn.</p>
         """);

        public static string OrderConfirmed(string orderCode, decimal total) => Wrap(
            "Xác nhận đơn hàng",
            $"""
         <p>Đơn hàng <strong>{orderCode}</strong> của bạn đã được tiếp nhận.</p>
         <p>Tổng tiền: <strong>{total:N0} đ</strong></p>
         <p style="color:#666;font-size:13px">Chúng tôi sẽ báo bạn khi đơn hàng được giao.</p>
         """);

        public static string ExportReady(string title, string link, int rows) => Wrap(
            "File của bạn đã sẵn sàng",
            $"""
         <p>Báo cáo <strong>{title}</strong> ({rows:N0} dòng) đã xuất xong.</p>
         <p><a href="{link}" style="background:#2563eb;color:#fff;padding:10px 18px;
            border-radius:6px;text-decoration:none;display:inline-block">Tải file về</a></p>
         <p style="color:#666;font-size:13px">Liên kết có hiệu lực trong 7 ngày.</p>
         """);

        private static string Wrap(string title, string body) =>
            $"""
         <div style="font-family:system-ui,-apple-system,'Segoe UI',sans-serif;max-width:560px;
              margin:0 auto;padding:24px;color:#111">
           <h2 style="margin:0 0 16px">{title}</h2>
           {body}
           <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0">
           <p style="color:#888;font-size:12px">WebShop — email tự động, vui lòng không trả lời.</p>
         </div>
         """;
    }
}
