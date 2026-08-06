using Application.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Infrastructure.Identity
{
    public static class IdentityTokenEncoder
    {
        public static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        public static string Decode(string token)
        {
            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (FormatException)
            {
                throw new ConflictException("Liên kết không hợp lệ hoặc đã hết hạn.");
            }
        }
    }
}
