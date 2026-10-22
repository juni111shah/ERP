using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Checks that a legal entity exists in the workspace and that the caller may work in it.</summary>
public interface ILegalEntityGuard
{
    /// <summary>Returns the entity or throws a field error on <c>legalEntityId</c>.</summary>
    Task<LegalEntity> RequireAsync(Guid legalEntityId, CancellationToken ct = default);
}

public sealed class LegalEntityGuard(IReadRepository<LegalEntity> legalEntities, ICurrentUser user) : ILegalEntityGuard
{
    public async Task<LegalEntity> RequireAsync(Guid legalEntityId, CancellationToken ct = default)
    {
        var entity = await legalEntities.FindAsync(legalEntityId, ct);
        if (entity is null || (user.AllowedLegalEntityIds is { } allowed && !allowed.Contains(legalEntityId)))
            throw new FieldValidationException("legalEntityId", "Legal entity not found.");
        return entity;
    }
}
