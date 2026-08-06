using Application.Common.Interfaces;

namespace Worker
{
    public class SystemUser : ICurrentUser
    {
        public string? Id => "system";

        public string? Email => null;

        public List<string> Roles => [];

        public List<string> Permissions => [];
    }
}
