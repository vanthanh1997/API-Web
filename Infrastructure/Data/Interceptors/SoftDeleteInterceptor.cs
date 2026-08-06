using Application.Common.Interfaces;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace Infrastructure.Data.Interceptors
{
    /// <summary>
    /// Biến thao tác XOÁ THẬT thành XOÁ MỀM cho mọi entity implement <see cref="ISoftDeletable"/>.
    ///
    /// Nhờ interceptor này, code nghiệp vụ vẫn viết <c>db.Products.Remove(product)</c> như
    /// bình thường — không phải nhớ đổi thành gán cờ thủ công. Quên gán cờ ở dù chỉ một
    /// handler là dữ liệu bị xoá vĩnh viễn, không lấy lại được.
    ///
    /// Chạy CÙNG với <see cref="AuditableEntityInterceptor"/>: thứ tự không quan trọng vì
    /// hai interceptor ghi vào các trường khác nhau.
    /// </summary>
    public class SoftDeleteInterceptor(ICurrentUser user, TimeProvider clock) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            ConvertDeletesToSoftDeletes(eventData.Context);

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            ConvertDeletesToSoftDeletes(eventData.Context);

            return base.SavingChangesAsync(eventData, result, ct);
        }

        private void ConvertDeletesToSoftDeletes(DbContext? context)
        {
            if (context is null) return;

            var now = clock.GetUtcNow();

            foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State != EntityState.Deleted) continue;

                // Điểm cốt lõi: đổi Deleted -> Modified để EF sinh câu UPDATE thay vì DELETE.
                entry.State = EntityState.Modified;

                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedBy = user.Id;
            }
        }
    }
}
