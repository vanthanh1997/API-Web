namespace Application.Common.Exceptions
{
    public class NotFoundException(string message) : Exception(message)
    {
        public static void ThrowIfNull(object? entity, string name, object key)
        {
            if (entity is null)
            {
                throw new NotFoundException($"Không tìm thấy {name} với khoá '{key}'.");
            }
        }
    }
}
