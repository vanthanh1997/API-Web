// ============================================================================
// FILE: Program.cs                      |  TẦNG: Presentation
// LÀ GÌ: Điểm khởi động API. Đăng ký DI, dựng pipeline middleware, map controller
//        và dashboard Hangfire.
// AI GỌI: .NET runtime chạy file này đầu tiên khi start API.
// NÓ GỌI: AddApplication() (Application), AddInfrastructure() (Infrastructure),
//         các *Setup trong Presentation/Infrastructure.
// ĐỌC TRƯỚC: (không có — đây là nơi bắt đầu đọc project)
// ĐỌC SAU  : Infrastructure/SecuritySetup.cs, Controllers/ProductsController.cs
// ⚠️ LƯU Ý: Thứ tự middleware BẮT BUỘC là UseCors -> UseAuthentication ->
//    UseRateLimiter -> UseAuthorization. CORS trước để request bị chặn vẫn có
//    header CORS (không thì browser báo lỗi CORS thay vì lỗi thật); RateLimiter
//    sau Authentication mới đếm được theo user.
// ⚠️ LƯU Ý: AddInfrastructure(config) KHÔNG truyền runJobs => mặc định false:
//    API CHỈ ENQUEUE job, KHÔNG chạy job. Job do Shop.Worker (project riêng) chạy.
//    API vẫn map /hangfire để xem dashboard, nhưng khoá chỉ Admin.
// ⚠️ LƯU Ý: InitialiseDatabaseAsync chỉ chạy ở Development.
// ⚠️ LƯU Ý: Configure<ApiBehaviorOptions> đặt InvalidModelStateResponseFactory để
//    bọc lỗi model binding vào ApiResponse — đường này KHÔNG đi qua exception
//    handler nên phải xử riêng.
// ============================================================================

using Application;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;
using Hangfire;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Infrastructure;
using Presentation.Infrastructure.Authorization;
using Presentation.Services;
using Serilog;

using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.UseSerilogLogging();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Sinh policy "perm:xxx" lúc chạy cho [HasPermission], khỏi đăng ký tay từng permission.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

builder.Services.AddAppLocalization();
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddAppHealthChecks(builder.Configuration);

builder.Services.AddExceptionHandler<ApiResponseExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiResponseWrapperFilter>();
});

// [ApiController] mặc định tự trả 400 ProblemDetails khi model binding lỗi
// (VD: gửi "abc" vào field decimal). Đường này KHÔNG đi qua exception handler,
// nên phải tự bọc để client luôn nhận đúng một khuôn ApiResponse.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", traceId, errors));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.InitialDatabaseAsync();   // migrate + seed role + admin
}

// CorrelationId đặt ĐẦU TIÊN: mọi dòng log sau đây (kể cả log lỗi) đều mang được id này.
app.UseCorrelationId();

// Security header ngay sau đó, để cả response lỗi do exception handler tạo ra cũng có.
app.UseSecurityHeaders();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HSTS chỉ bật ở production: nó bắt browser GHI NHỚ "chỉ dùng HTTPS" cho domain này.
    // Bật ở localhost sẽ khiến browser từ chối http://localhost của MỌI project khác
    // trên cùng máy, rất khó gỡ (phải xoá thủ công trong cấu hình browser).
    app.UseHsts();
}

app.UseHttpsRedirection();

// Đa ngôn ngữ: đặt trước controller để CultureInfo đã đúng khi sinh thông báo lỗi.
app.UseAppLocalization();

// Thứ tự BẮT BUỘC: CORS -> Authentication -> TokenBlacklist -> RateLimiter -> Authorization.
// CORS phải trước để request bị chặn vẫn có header CORS (không thì browser báo lỗi CORS
// thay vì lỗi thật). RateLimiter sau Authentication thì mới đếm được theo user.
app.UseCors(SecuritySetup.CorsPolicy);
app.UseAuthentication();

// Chặn token đã bị thu hồi — phải SAU Authentication (cần claims) và TRƯỚC Authorization
// (chặn trước khi vào endpoint). Bật/tắt bằng Security:EnableTokenBlacklist.
if (builder.Configuration.GetValue("Security:EnableTokenBlacklist", true))
{
    app.UseTokenBlacklist();
}

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Probe của load balancer gọi liên tục — cho ra ngoài rate limit, nếu không
// chính probe sẽ ăn hết hạn mức rồi bị 429 và bị coi là service chết.
app.MapAppHealthChecks();

// ⚠️ Dashboard Hangfire mặc định KHÔNG có auth. Chỉ Admin mới vào được.
// API vẫn xem được dashboard dù KHÔNG chạy job — job do Shop.Worker chạy.
app.MapHangfireDashboard("/hangfire")
    .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Admin });

app.Run();

/// <summary>
/// Với top-level statements, class Program do compiler sinh ra là `internal`, nên
/// WebApplicationFactory&lt;Program&gt; trong project test KHÔNG tham chiếu được.
/// Khai partial public ở đây để integration test dựng được API thật.
/// Không ảnh hưởng gì tới lúc chạy bình thường.
/// </summary>
public partial class Program;
