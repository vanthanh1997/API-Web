using Application.Common.Exporting;
using Application.Common.Interfaces;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.LicenseManagement;
using Hangfire;
using Infrastructure.Caching;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Email;
using Infrastructure.Exporting;
using Infrastructure.Identity;
using Infrastructure.Jobs;
using Infrastructure.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        /// <param name="runJobs">
        /// true  = tiến trình này CHẠY job (Shop.Worker).
        /// false = chỉ đẩy job vào hàng đợi (Shop.Presentation/API).
        /// Tách như vậy để job nặng không ăn RAM/CPU của API.
        /// </param>
        public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, bool runJobs = false)
        {
            services.AddSingleton(TimeProvider.System);
            services.Configure<AppUrlOptions>(config.GetSection(AppUrlOptions.SectionName));
            services.AddDatabase(config);
            services.AddIdentityAndJwt(config);
            services.AddRedisCache(config);
            services.AddMessaging(config);
            services.AddElasticsearch(config);
            services.AddBackgroundJobs(config, runJobs);
            services.AddEmail(config);
            services.AddExporting(config);
            services.AddDataProtectionLayer(config);
            return services;
        }

        /// <summary>
        /// Data Protection API — dùng để mã hoá dữ liệu nhạy cảm (token trong link email,
        /// giá trị cần bảo vệ trong cookie...).
        ///
        /// ⚠️ Vấn đề then chốt là NƠI LƯU KHOÁ. Mặc định .NET lưu khoá trong thư mục tạm
        /// của tiến trình, nên:
        ///   • Restart container -> khoá mới -> mọi dữ liệu đã mã hoá trước đó KHÔNG giải
        ///     mã được nữa (link reset password đang gửi đi trở thành vô hiệu).
        ///   • Chạy nhiều instance -> mỗi instance một khoá -> instance A mã hoá, B không
        ///     đọc được.
        ///
        /// Vì vậy production PHẢI trỏ <c>DataProtection:KeyPath</c> vào ổ đĩa dùng chung
        /// (volume, file share). Không khai thì vẫn chạy được nhưng chỉ đúng cho máy dev
        /// một instance — và log cảnh báo để không ai vô tình đem cấu hình đó lên production.
        /// </summary>
        private static void AddDataProtectionLayer(
            this IServiceCollection services, IConfiguration config)
        {
            var builder = services.AddDataProtection()
                .SetApplicationName("WebShop");

            var keyPath = config["DataProtection:KeyPath"];

            if (!string.IsNullOrWhiteSpace(keyPath))
            {
                builder.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            }
        }
        private static void AddDatabase(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");
            services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
            // Đổi Remove() thành cập nhật cờ IsDeleted cho entity ISoftDeletable.
            services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
            services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString);
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            });

            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<ITransaction, EfTransaction>();
            // Đường ĐỌC nặng (Dapper/stored procedure) và GHI hàng loạt (SqlBulkCopy).
            // Mặc định nghiệp vụ vẫn dùng IAppDbContext (EF) cho dễ đọc/dễ test;
            // chỉ chuyển sang hai cái này khi ĐÃ ĐO thấy chậm thật.
            // Vẫn đăng ký sẵn cả hai: để trống thì lúc inject sẽ nổ
            // InvalidOperationException giữa production, mà cả hai class đều đã viết xong.
            services.AddScoped<ISqlQuery, SqlQuery>();
            services.AddScoped<IBulkWriter, SqlBulkWriter>();
        }
        private static void AddIdentityAndJwt(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
            services.Configure<GoogleAuthOptions>(config.GetSection(GoogleAuthOptions.SectionName));

            var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException("Thiếu section Jwt trong appsettings.");

            // Kiểm tra NGAY lúc khởi động thay vì để nổ giữa lúc phục vụ request.
            // Secret rỗng khiến SymmetricSecurityKey ném
            // "IDX10703: ... key length is zero" ở request ĐẦU TIÊN đi qua middleware
            // authentication — thông báo đó không hề chỉ ra rằng chỉ thiếu config.
            // HmacSha256 cần khoá >= 256 bit = 32 byte, nên chặn luôn secret quá ngắn.
            if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Secret phải có ít nhất 32 ký tự (256 bit) cho HmacSha256. " +
                    "Đặt qua appsettings.Development.json khi dev, hoặc biến môi trường / " +
                    "user-secrets ở production — KHÔNG commit secret thật.");
            }

            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;

                // Xác nhận email là bắt buộc về mặt token: GenerateEmailConfirmationTokenAsync
                // cần provider bên dưới. Không bật SignIn.RequireConfirmedEmail vì luồng
                // hiện tại cho phép đăng nhập trước rồi xác nhận sau.
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                // BẮT BUỘC: thiếu dòng này thì mọi hàm sinh token của Identity đều nổ
                // NotSupportedException("No IUserTwoFactorTokenProvider<TUser> named 'Default'
                // is registered") — kéo theo 4 luồng chết hẳn: đăng ký (gửi mail xác nhận),
                // confirm-email, forgot-password và reset-password.
                // Lỗi chỉ xuất hiện lúc CHẠY nên build sạch vẫn không phát hiện được.
                .AddDefaultTokenProviders();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });

            services.AddAuthorization();

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IUserAdminService, UserAdminService>();
            services.AddSingleton<IJwtTokenService, JwtTokenService>();
        }
        /// <summary>
        /// Cache. Có ConnectionStrings:Redis thì dùng Redis (chia sẻ được giữa nhiều
        /// instance API); không có thì lùi về cache trong RAM của tiến trình.
        ///
        /// Vì sao cần fallback: AddStackExchangeRedisCache với Configuration rỗng vẫn
        /// đăng ký thành công nhưng nổ ở LẦN GỌI ĐẦU TIÊN, tức lỗi hiện ra giữa lúc
        /// đang phục vụ request chứ không phải lúc khởi động.
        ///
        /// ⚠️ Cache RAM chỉ đúng cho máy dev / 1 instance: chạy nhiều instance mà không
        /// có Redis thì mỗi instance giữ một bản cache riêng, dữ liệu sẽ lệch nhau.
        /// Production BẮT BUỘC khai Redis.
        /// </summary>
        private static void AddRedisCache(this IServiceCollection services, IConfiguration config)
        {
            var redis = config.GetConnectionString("Redis");

            if (string.IsNullOrWhiteSpace(redis))
            {
                services.AddDistributedMemoryCache();
            }
            else
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redis;
                    options.InstanceName = "webshop:";
                });
            }

            // RedisCacheService chỉ phụ thuộc IDistributedCache nên chạy đúng với cả
            // hai lựa chọn ở trên, không cần class riêng cho bản RAM.
            services.AddScoped<ICacheService, RedisCacheService>();

            // Danh sách đen access token — cũng chỉ cần IDistributedCache nên hoạt động
            // với cả Redis và cache RAM (bản RAM chỉ đúng cho 1 instance).
            services.AddScoped<ITokenBlacklist, RedisTokenBlacklist>();

            // Distributed lock BẮT BUỘC có Redis thật: nó cần lệnh nguyên tử SET NX mà
            // IDistributedCache không có. Không có Redis thì đăng ký bản no-op để code
            // nghiệp vụ vẫn chạy (chấp nhận không có khoá) thay vì nổ khi inject.
            if (string.IsNullOrWhiteSpace(redis))
            {
                services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
            }
            else
            {
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                    _ => StackExchange.Redis.ConnectionMultiplexer.Connect(redis));

                services.AddSingleton<IDistributedLock, RedisDistributedLock>();
            }
        }
        /// <summary>
        /// Message bus (MassTransit + RabbitMQ). CHỈ bật khi có ConnectionStrings:RabbitMq.
        ///
        /// Vì sao phải kiểm tra: MassTransit đăng ký một IHostedService, khi start nó cố
        /// kết nối broker và RETRY liên tục. Broker chưa lên (chưa chạy docker compose)
        /// thì cả API treo ở bước khởi động — KHÔNG mở port, /health/live cũng không
        /// trả lời, nhìn như API chết hẳn dù lỗi chỉ nằm ở một hạ tầng phụ.
        ///
        /// Không khai RabbitMq thì IEventBus vẫn inject được nhưng là bản no-op, nhờ vậy
        /// code nghiệp vụ gọi PublishAsync không phải bọc if null ở mọi chỗ.
        /// </summary>
        private static void AddMessaging(this IServiceCollection services, IConfiguration config)
        {
            var rabbitMq = config.GetConnectionString("RabbitMq");

            if (string.IsNullOrWhiteSpace(rabbitMq))
            {
                services.AddSingleton<IEventBus, NoOpEventBus>();
                return;
            }

            services.AddMassTransit(x =>
            {
                x.AddConsumers(typeof(DependencyInjection).Assembly);

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMq);

                    // ---- Retry: lỗi TẠM THỜI thì thử lại ----
                    // Mạng chớp tắt, DB timeout, service phụ thuộc restart... là chuyện
                    // bình thường. Không có retry thì message rơi vào lỗi ngay lần đầu.
                    // Khoảng cách tăng dần (2s, 5s, 10s) để không dồn tải khi hệ thống
                    // đang hồi phục.
                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10)));

                    // ---- Redelivery: lỗi KÉO DÀI thì hẹn lại sau ----
                    // Khác retry ở chỗ: retry giữ message trong bộ nhớ và thử lại ngay,
                    // còn redelivery trả message về broker rồi hẹn giờ giao lại — phù hợp
                    // khi service phụ thuộc chết vài phút.
                    cfg.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(15)));

                    // ---- Dead-Letter Queue ----
                    // Hết mọi lần thử mà vẫn lỗi, MassTransit tự đưa message sang queue
                    // "<tên-queue>_error" thay vì XOÁ. Nhờ vậy không mất dữ liệu và có
                    // thể xem/replay trong RabbitMQ UI (http://localhost:15672).
                    // ConfigureEndpoints bật hành vi này mặc định — khai ở đây để rõ ý
                    // và để chỗ điều chỉnh khi cần.
                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddScoped<IEventBus, MassTransitEventBus>();
        }
        /// <summary>
        /// Elasticsearch (dùng cho search sản phẩm sau này; Serilog đẩy log vào ES qua
        /// cấu hình riêng ở SerilogSetup, KHÔNG dùng client này).
        ///
        /// ⚠️ Phải dùng IsNullOrWhiteSpace, KHÔNG dùng `?? "..."`:
        /// appsettings.json ship "Elasticsearch": "" (chuỗi RỖNG, không phải null) nên
        /// toán tử ?? không đỡ được, và new Uri("") ném UriFormatException làm
        /// CHẾT NGAY lúc khởi động — đúng cấu hình production mặc định.
        /// </summary>
        private static void AddElasticsearch(this IServiceCollection services, IConfiguration config)
        {
            var uri = config.GetConnectionString("Elasticsearch");

            if (string.IsNullOrWhiteSpace(uri))
            {
                // Chưa khai thì không đăng ký client. Chỗ nào cần search sẽ tự báo thiếu
                // dependency, tốt hơn là để cả tiến trình không start được.
                return;
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:Elasticsearch không phải URL hợp lệ: '{uri}'. " +
                    "Ví dụ đúng: http://localhost:9200");
            }

            services.AddSingleton(new ElasticsearchClient(parsed));
        }
        private static void AddBackgroundJobs(this IServiceCollection services, IConfiguration config, bool runJobs)
        {
            services.AddHangfire(cfg => cfg
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"), new Hangfire.SqlServer.SqlServerStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    UseRecommendedIsolationLevel = true
                }));

            // CHỈ Shop.Worker mới chạy job. API để runJobs=false nên chỉ enqueue —
            // nhờ vậy job xuất 765 MB không bao giờ ăn RAM của tiến trình phục vụ request.
            // (AddHangfireServer của worker khai riêng trong Worker/Program.cs để đặt WorkerCount/Queues.)
            if (runJobs)
            {
                services.AddHangfireServer();
            }

            //services.AddScoped<ReindexProductsJob>();
            services.AddScoped<SendEmailJob>();

            // BackgroundService dọn refresh token đã chết. Dùng IDistributedLock nên chạy
            // trên nhiều instance vẫn chỉ một instance thực sự dọn.
            services.AddHostedService<ExpiredTokenCleanupService>();
        }
        private static void AddEmail(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<EmailOptions>(config.GetSection(EmailOptions.SectionName));

            services.AddScoped<IEmailSender, MailKitEmailSender>();   // hạ tầng SMTP
            services.AddScoped<IEmailService, EmailService>();        // nghiệp vụ email
        }
        private static void AddExporting(this IServiceCollection services, IConfiguration config)
        {
            // QuestPDF bắt buộc khai license trước lần render đầu tiên,
            // không khai thì xuất PDF sẽ throw ngay. Community = miễn phí.
            global::QuestPDF.Settings.License = global::QuestPDF.Infrastructure.LicenseType.Community;

            // ExportService gom mọi IExportWriter vào dictionary theo Format,
            // nên writer mới chỉ cần AddScoped thêm 1 dòng ở đây.
            services.AddScoped<IExportWriter, CsvExportWriter>();
            services.AddScoped<IExportWriter, ExcelExportWriter>();
            services.AddScoped<IExportWriter, ExcelStreamExportWriter>();
            services.AddScoped<IExportWriter, HtmlExportWriter>();
            services.AddScoped<IExportWriter, PdfExportWriter>();
            services.AddScoped<IExportWriter, WordExportWriter>();

            services.AddScoped<IExportService, ExportService>();
        }
    }
}
