using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace Presentation.Infrastructure
{
    /// <summary>
    /// Đa ngôn ngữ (I18n) cho thông báo trả về client.
    /// </summary>
    public static class LocalizationSetup
    {
        /// <summary>Ngôn ngữ mặc định khi client không chỉ định.</summary>
        public const string DefaultCulture = "vi";

        /// <summary>Danh sách ngôn ngữ hệ thống hỗ trợ.</summary>
        private static readonly string[] SupportedCultures = [DefaultCulture, "en"];

        public static IServiceCollection AddAppLocalization(this IServiceCollection services)
        {
            // ResourcesPath: nơi đặt file .resx (VD Resources/Messages.en.resx).
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var cultures = SupportedCultures.Select(c => new CultureInfo(c)).ToList();

                options.DefaultRequestCulture = new RequestCulture(DefaultCulture);
                options.SupportedCultures = cultures;          // ảnh hưởng format số/ngày
                options.SupportedUICultures = cultures;         // ảnh hưởng chuỗi dịch

                // Khai tường minh thứ tự nhà cung cấp để hành vi rõ ràng, không phụ thuộc
                // mặc định của framework (có thể đổi giữa các phiên bản).
                options.RequestCultureProviders =
                [
                    new QueryStringRequestCultureProvider(),
                    new CookieRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                ];
            });

            return services;
        }

        /// <summary>
        /// Đặt SỚM trong pipeline — trước UseRouting/MapControllers — để CultureInfo của
        /// luồng đã đúng trước khi controller và validator sinh thông báo.
        /// </summary>
        public static IApplicationBuilder UseAppLocalization(this IApplicationBuilder app)
            => app.UseRequestLocalization();
    }
}
