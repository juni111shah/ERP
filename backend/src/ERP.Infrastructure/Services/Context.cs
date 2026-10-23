using System.Security.Claims;
using ERP.Application.Abstractions;
using ERP.Application.Security;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

/// <summary>Caller identity from the JWT. Permissions come from "perm" claims; entities from "ents".</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    private HashSet<string>? _perms;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? OrgId => Guid.TryParse(Principal?.FindFirstValue(ErpClaims.Org), out var id) ? id : null;
    public string? Email => Principal?.FindFirstValue("email") ?? Principal?.FindFirstValue(ClaimTypes.Email);
    public Guid? LegalEntityId => accessor.HttpContext?.Items["erp.legalEntityId"] as Guid?;

    public IReadOnlyCollection<Guid>? AllowedLegalEntityIds
    {
        get
        {
            var ents = Principal?.FindAll(ErpClaims.Entities).Select(c => c.Value).ToList();
            if (ents is null || ents.Count == 0 || ents.Contains(ErpClaims.AllEntities)) return null;
            return ents.Select(e => Guid.TryParse(e, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        }
    }

    public IReadOnlySet<string> Permissions => _perms ??= Principal?.FindAll(ErpClaims.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal) ?? [];
    public Guid? PortalPartyId => Guid.TryParse(Principal?.FindFirstValue("portal_party"), out var id) ? id : null;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public bool HasPermission(string permission) => Permissions.Contains(permission) || Permissions.Contains(CorePermissions.All);
}

/// <summary>Workspace scope for EF filters: the caller's org, unless trusted code switched it with Use().</summary>
public sealed class OrgContext(ICurrentUser user) : IOrgContext
{
    private Guid? _override;
    private Guid? _legalEntity;
    private bool _legalEntitySet;

    public Guid? OrgId => _override ?? user.OrgId;
    public Guid? LegalEntityId => _legalEntitySet ? _legalEntity : (_override is null ? user.LegalEntityId : null);
    public IReadOnlyCollection<Guid>? AllowedLegalEntityIds => _override is null ? user.AllowedLegalEntityIds : null;

    public IDisposable Use(Guid orgId)
    {
        var previous = _override;
        _override = orgId;
        return new Scope(() => _override = previous);
    }

    public void SetLegalEntity(Guid? legalEntityId) { _legalEntity = legalEntityId; _legalEntitySet = true; }

    private sealed class Scope(Action onDispose) : IDisposable { public void Dispose() => onDispose(); }
}
