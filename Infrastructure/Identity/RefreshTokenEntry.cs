using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Identity
{
    public class RefreshTokenEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Token { get; set; } = null!;

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
    }
}
