using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using ERP.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Stamps org/audit fields, turns deletes of soft-deletable rows into updates, blocks moving rows between
/// workspaces, and writes an AuditEvent (before/after JSON) for every insert, update and delete.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser user, IOrgContext org, IClock clock) : SaveChangesInterceptor
{
    private static readonly HashSet<string> SecretProperties = new(StringComparer.OrdinalIgnoreCase)
        { "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "TokenHash", "RowVersion" };

    private static readonly HashSet<Type> NotAudited = [typeof(AuditEvent), typeof(RefreshToken), typeof(PasswordResetToken), typeof(Notification), typeof(MessageLog), typeof(PortalOtp)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Process(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null) Process(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Process(DbContext db)
    {
        var now = clock.UtcNow;
        var userId = user.UserId;
        var audits = new List<AuditEvent>();

        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            if (entry.Entity is IOrgScoped scoped)
            {
                if (entry.State == EntityState.Added && scoped.OrgId == Guid.Empty)
                    scoped.OrgId = org.OrgId ?? throw new InvalidOperationException("No workspace context: refusing to write an org-scoped row.");
                if (entry.State == EntityState.Modified && entry.Property(nameof(IOrgScoped.OrgId)).IsModified
                    && !Equals(entry.Property(nameof(IOrgScoped.OrgId)).OriginalValue, entry.Property(nameof(IOrgScoped.OrgId)).CurrentValue))
                    throw new InvalidOperationException("Rows cannot be moved between workspaces.");
                if (org.OrgId is { } current && scoped.OrgId != current)
                    throw new InvalidOperationException("Cross-workspace write blocked.");
            }

            if (entry.Entity is IAuditable a)
            {
                if (entry.State == EntityState.Added) { a.CreatedAt = now; a.CreatedBy ??= userId; }
                else if (entry.State == EntityState.Modified) { a.UpdatedAt = now; a.UpdatedBy = userId; }
            }

            var action = entry.State switch { EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated" };
            var before = entry.State == EntityState.Added ? null : Snapshot(entry, original: true);

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete sd)
            {
                entry.State = EntityState.Modified;
                sd.IsDeleted = true; sd.DeletedAt = now; sd.DeletedBy = userId;
                if (entry.Entity is IAuditable a2) { a2.UpdatedAt = now; a2.UpdatedBy = userId; }
            }

            if (NotAudited.Contains(entry.Entity.GetType())) continue;
            var orgId = entry.Entity switch
            {
                IOrgScoped s => s.OrgId,
                Organization o => o.Id,
                _ => org.OrgId ?? Guid.Empty,
            };
            if (orgId == Guid.Empty) continue; // e.g. a user created before joining a workspace

            var changed = entry.State == EntityState.Modified
                ? entry.Properties.Where(p => p.IsModified && !SecretProperties.Contains(p.Metadata.Name)).Select(p => p.Metadata.Name).ToList()
                : null;
            if (entry.State == EntityState.Modified && changed is { Count: 0 }) continue;

            audits.Add(new AuditEvent
            {
                OrgId = orgId,
                OccurredAt = now,
                UserId = userId,
                Action = action,
                RecordType = entry.Metadata.ClrType.Name,
                RecordId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "",
                ChangedFields = changed is null ? null : string.Join(",", changed),
                Before = before,
                After = entry.State == EntityState.Deleted ? null : Snapshot(entry, original: false),
                IpAddress = user.IpAddress,
            });
        }

        if (audits.Count > 0) db.Set<AuditEvent>().AddRange(audits);
    }

    private static string Snapshot(EntityEntry entry, bool original)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            if (SecretProperties.Contains(p.Metadata.Name)) continue;
            if (entry.Entity is AppUser && p.Metadata.Name is nameof(AppUser.PhoneNumber)) continue;
            dict[p.Metadata.Name] = original ? p.OriginalValue : p.CurrentValue;
        }
        return JsonSerializer.Serialize(dict);
    }
}
