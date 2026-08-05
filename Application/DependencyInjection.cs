using Application.Common.Behaviours;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using FluentValidation;
namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // ⚠️ BẮT BUỘC: đăng ký toàn bộ AbstractValidator trong assembly này.
            //
            // Thiếu dòng này, ValidationBehaviour nhận IEnumerable<IValidator<T>> RỖNG nên
            // `validators.Any()` luôn false => TOÀN BỘ validation của hệ thống không chạy.
            // Mọi RuleFor đã viết trở thành code chết, và dữ liệu sai chỉ bị chặn ở tầng
            // dưới (Identity/SQL) với mã 409/500 thay vì 400 kèm danh sách lỗi rõ ràng.
            //
            // Rất khó phát hiện: build sạch, endpoint vẫn "chạy", chỉ mã lỗi là sai.
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

            // Mapster: nạp cấu hình ánh xạ Entity ⇄ DTO một lần lúc khởi động.
            // Compile() sinh sẵn mã ánh xạ nên lần map đầu tiên không bị chậm
            // (khác reflection, vốn trả giá ở mỗi lần gọi).
            Common.Mappings.MappingConfig.Configure().Compile();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);

                // Thứ tự khai = thứ tự chạy:
                // Logging (ngoài cùng, đo cả thời gian validate) -> Validation -> Transaction.
                // Đặt Transaction trước Validation sẽ mở transaction rồi mới phát hiện dữ
                // liệu sai — mở/đóng transaction vô ích.
                cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));
            });

            return services;
        }
    }
}
