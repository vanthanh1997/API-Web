using Serilog;
using Serilog.Sinks.Elasticsearch;
namespace Presentation.Infrastructure
{
    public static class SerilogSetup
    {
        public static void UseSerilogLogging(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, config) =>
            {
                config
                    .ReadFrom.Configuration(context.Configuration)
                    // FromLogContext: bắt buộc để CorrelationId do CorrelationIdMiddleware
                    // đẩy vào LogContext xuất hiện trong mọi dòng log.
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "WebShop")
                    .WriteTo.Console();

                // ---- Seq ----
                // Seq là công cụ xem log có giao diện web (http://localhost:5341), nhẹ hơn
                // và dễ dùng hơn Elasticsearch+Kibana cho việc truy vết hằng ngày:
                // tìm theo CorrelationId, lọc theo mức log, xem structured property.
                // Không khai thì bỏ qua — Console vẫn có log.
                var seqUrl = context.Configuration.GetConnectionString("Seq");

                if (!string.IsNullOrWhiteSpace(seqUrl)
                    && Uri.TryCreate(seqUrl, UriKind.Absolute, out _))
                {
                    config.WriteTo.Seq(seqUrl);
                }

                var elasticUri = context.Configuration.GetConnectionString("Elasticsearch");

                // ⚠️ Phải dùng IsNullOrWhiteSpace, KHÔNG dùng `?? "http://..."`:
                // appsettings.json ship "Elasticsearch": "" (chuỗi RỖNG, không phải null)
                // nên toán tử ?? không đỡ được, và new Uri("") ném UriFormatException
                // làm CHẾT NGAY lúc khởi động — đúng cấu hình production mặc định.
                // (Cùng loại lỗi với AddElasticsearch trong Infrastructure/DependencyInjection.cs.)
                if (string.IsNullOrWhiteSpace(elasticUri))
                {
                    // Không khai thì chỉ log ra Console. Log vẫn đọc được, chỉ là
                    // không tập trung vào Kibana.
                    return;
                }

                if (!Uri.TryCreate(elasticUri, UriKind.Absolute, out var parsed))
                {
                    throw new InvalidOperationException(
                        $"ConnectionStrings:Elasticsearch không phải URL hợp lệ: '{elasticUri}'. " +
                        "Ví dụ đúng: http://localhost:9200");
                }

                config.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(parsed)
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = "logs-webshop-{0:yyyy.MM}"
                });
            });
        }
    }
}
