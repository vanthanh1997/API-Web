using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface ICurrentUser
    {
        string? Id { get; }

        string? Email { get; }

        List<string> Roles { get; }

        /// <summary>Permission lấy từ claim "permission" trong token.</summary>
        List<string> Permissions { get; }
    }
}
