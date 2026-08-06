using Application.Common.Interfaces;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;


namespace Infrastructure.Data.Interceptors
{
    public class AuditableEntityInterceptor(ICurrentUser user, TimeProvider clock)
     : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChangesAsync(eventData, result, ct);
        }

        private void UpdateEntities(DbContext? context)
        {
            if (context is null) return;

            foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

                var now = clock.GetUtcNow();

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.Created = now;
                    entry.Entity.CreatedBy = user.Id;
                }

                entry.Entity.LastModified = now;
                entry.Entity.LastModifiedBy = user.Id;
            }
        }
    }
}
