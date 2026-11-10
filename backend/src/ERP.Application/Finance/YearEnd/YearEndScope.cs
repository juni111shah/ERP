using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;

namespace ERP.Application.Finance;

/// <summary>Which legal entity a year-end close or reopen may act on.</summary>
public sealed class YearEndScope(ILegalEntityGuard guard, ICurrentUser user)
{
    /// <summary>A selected working entity narrows every journal query, so closing another one would read no balances and post nothing.</summary>
    public async Task RequireEntityAsync(Guid legalEntityId, CancellationToken ct)
    {
        await guard.RequireAsync(legalEntityId, ct);
        if (user.LegalEntityId is { } selected && selected != legalEntityId)
            throw new FieldValidationException("legalEntityId", "Switch to this legal entity, or to all entities, to close its year.");
    }
}
