using Mapster;

namespace Application.Common.Mappings
{
    public class UserMappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // Khi thêm entity nghiệp vụ, khai theo mẫu:
            //
            // config.NewConfig<Product, ProductDto>()
            //       .Map(dest => dest.CategoryName, src => src.Category.Name)
            //       .Ignore(dest => dest.SomeInternalField);            //
            // Hiện chưa có entity nghiệp vụ nào nên chưa khai gì — giữ file để chỗ khai
            // ánh xạ là một nơi cố định, không rải rác trong handler.
            config.Default.IgnoreNullValues(false);
        }
    }
}
