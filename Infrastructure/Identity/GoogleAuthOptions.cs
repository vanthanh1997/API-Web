namespace Infrastructure.Identity
{
    public class GoogleAuthOptions
    {
        public const string SectionName = "GoogleAuth";

        /// <summary>
        /// Client ID lấy từ Google Cloud Console (OAuth 2.0 Client IDs).
        /// Server dùng để kiểm tra idToken có đúng được cấp cho ứng dụng này không —
        /// thiếu bước này thì token của app khác cũng login được vào hệ thống.
        /// </summary>
        public string ClientId { get; set; } = null!;
    }
}
