using Mapster;
using System.Reflection;

namespace Application.Common.Mappings
{
    public static class MappingConfig
    {
        public static TypeAdapterConfig Configure()
        {
            var config = TypeAdapterConfig.GlobalSettings;
            config.Default
                .NameMatchingStrategy(NameMatchingStrategy.Flexible)
                .PreserveReference(true);

            config.Scan(Assembly.GetExecutingAssembly());

            return config;
        }
    }
}
