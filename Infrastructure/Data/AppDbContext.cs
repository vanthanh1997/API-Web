using Application.Common.Interfaces;
using Domain.Common;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace Infrastructure.Data
{
    /// <summary>
    /// DbContext duy nhất của hệ thống. Kế thừa IdentityDbContext nên đã có sẵn
    /// các bảng của ASP.NET Identity (AspNetUsers, AspNetRoles, AspNetUserRoles...).
    ///
    /// Thêm entity nghiệp vụ = khai DbSet ở đây + một class IEntityTypeConfiguration
    /// trong Data/Configurations (ApplyConfigurationsFromAssembly tự nhặt, không phải
    /// sửa OnModelCreating).
    /// </summary>
    public class AppDbContext(DbContextOptions<AppDbContext> options)
        : IdentityDbContext<ApplicationUser>(options), IAppDbContext
    {
        public DbSet<RefreshTokenEntry> RefreshTokens => Set<RefreshTokenEntry>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            ApplySoftDeleteQueryFilters(builder);
        }

        private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

                // Dựng biểu thức e => !e.IsDeleted cho đúng kiểu entity đang xét.
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                var body = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

                builder.Entity(entityType.ClrType)
                    .HasQueryFilter(Expression.Lambda(body, parameter));

                // Bản ghi đã xoá mềm gần như không bao giờ được truy vấn, nên đánh index
                // theo IsDeleted giúp SQL Server bỏ qua chúng nhanh hơn.
                builder.Entity(entityType.ClrType)
                    .HasIndex(nameof(ISoftDeletable.IsDeleted));
            }
        }
    }
}
