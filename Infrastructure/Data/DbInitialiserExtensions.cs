using Domain.Constants;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data
{
    public static class DbInitialExtensions
    {
        /// <summary>
        /// Chạy migration và seed dữ liệu nền (role + tài khoản admin đầu tiên).
        /// Chỉ gọi ở Development — production nên chạy migration bằng script riêng
        /// để không có hai instance API cùng migrate một lúc.
        /// </summary>
        public static async Task InitialDatabaseAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            var logger = sp.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                var db = sp.GetRequiredService<AppDbContext>();
                var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var config = sp.GetRequiredService<IConfiguration>();

                await db.Database.MigrateAsync();

                foreach (var role in Roles.All)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                        logger.LogInformation("Đã tạo role {Role}.", role);
                    }
                }

                await SeedAdminAsync(userManager, config, logger);
            }
            catch (Exception ex)
            {
                // Không để việc seed làm chết cả API: DB chưa kịp khởi động (docker vừa
                // lên) là tình huống thường gặp lúc dev. Log rồi đi tiếp — /health/ready
                // sẽ báo chưa sẵn sàng cho tới khi DB lên.
                logger.LogError(ex, "Không khởi tạo được database. API vẫn chạy nhưng các API cần DB sẽ lỗi.");
            }
        }

        private static async Task SeedAdminAsync(
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            ILogger logger)
        {
            var adminEmail = config["Seed:AdminEmail"] ?? "admin@webshop.local";

            if (await userManager.FindByEmailAsync(adminEmail) is not null)
            {
                return;
            }

            // Đọc từ config để production đặt qua biến môi trường / user-secrets,
            // không phải sửa code. Fallback chỉ dùng cho máy dev.
            var adminPassword = config["Seed:AdminPassword"] ?? "Admin@123456";

            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Quản trị viên"
            };

            var result = await userManager.CreateAsync(admin, adminPassword);

            if (!result.Succeeded)
            {
                logger.LogError("Không tạo được admin mặc định: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));

                return;
            }

            await userManager.AddToRoleAsync(admin, Roles.Admin);

            logger.LogWarning(
                "Đã tạo admin mặc định {Email} — ĐỔI MẬT KHẨU trước khi lên production!", adminEmail);
        }
    }
}
