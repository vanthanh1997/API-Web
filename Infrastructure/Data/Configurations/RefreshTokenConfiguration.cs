using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenEntry>
    {
        public void Configure(EntityTypeBuilder<RefreshTokenEntry> builder)
        {
            // 200 ký tự đủ chứa token 64 byte encode base64 (88 ký tự) và còn dư.
            builder.Property(t => t.Token).HasMaxLength(200).IsRequired();

            // Refresh/Logout tra cứu bằng đúng chuỗi token -> phải có index.
            // Unique để hai user không thể vô tình giữ cùng một token.
            builder.HasIndex(t => t.Token).IsUnique();

            // IssueTokensAsync dọn token chết theo (UserId + ExpiresAt/RevokedAt) mỗi lần
            // login/refresh. Chỉ có index trên UserId (do FK sinh ra) thì SQL Server vẫn
            // phải đọc từng dòng của user để so ngày; thêm ExpiresAt vào index cho phép
            // lọc luôn trên index. Bảng này ghi rất nhiều nên giữ index gọn, không INCLUDE thêm.
            builder.HasIndex(t => new { t.UserId, t.ExpiresAt });

            builder.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                // Xoá user thì token của user đó phải đi theo, không để lại token mồ côi
                // vẫn đổi được access token.
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
