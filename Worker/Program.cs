// ============================================================================
// FILE: Program.cs                      |  TẦNG: Worker
// LÀ GÌ: Tiến trình CHẠY job nền Hangfire (gửi email, export nặng...).
//        API chỉ enqueue job; Worker mới là nơi thực thi — nhờ vậy job nặng
//        không ăn RAM/CPU của tiến trình phục vụ request.
// AI GỌI: .NET runtime khi start Worker.
// NÓ GỌI: AddApplication() (Application), AddInfrastructure(runJobs: true).
// ⚠️ LƯU Ý: runJobs: true => AddInfrastructure sẽ gọi AddHangfireServer()
//    cho tiến trình này. KHÔNG gọi thêm AddHangfireServer ở đây kẻo chạy 2 server.
// ⚠️ LƯU Ý: Job nền không có HttpContext nên ICurrentUser phải là SystemUser
//    cố định — nếu quên, AuditableEntityInterceptor sẽ không có CreatedBy.
// ============================================================================

using Application;
using Application.Common.Interfaces;
using Infrastructure;
using Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();

// runJobs: true — tiến trình này CHẠY job (API để mặc định false, chỉ enqueue).
builder.Services.AddInfrastructure(builder.Configuration, runJobs: true);

// Job nền không có HttpContext — audit ghi nhận user "system".
builder.Services.AddScoped<ICurrentUser, SystemUser>();

var app = builder.Build();

// Endpoint duy nhất: healthcheck cho docker/k8s probe.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "worker" }));

app.Run();
