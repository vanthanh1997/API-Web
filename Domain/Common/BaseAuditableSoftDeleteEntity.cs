using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public abstract class BaseAuditableSoftDeleteEntity : BaseAuditableEntity, ISoftDeletable
    {
        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public string? DeletedBy { get; set; }
    }
}
