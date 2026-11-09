using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Rules shared by creating and changing an account: where it may sit in the tree and how its input is applied.</summary>
public sealed class AccountEditing(IAccountRepository accounts)
{
    /// <summary>The custom-field entity type of accounts.</summary>
    public const string CustomFieldType = "account";

    public async Task EnsureParentAsync(Guid? parentId, Guid? selfId, CancellationToken ct)
    {
        if (parentId is not { } parent) return;
        if (parent == selfId) throw new FieldValidationException("parentId", "An account cannot be its own parent.");
        if (!await accounts.AnyAsync(a => a.Id == parent, ct)) throw new FieldValidationException("parentId", "Parent account not found.");
        // Walk up the tree so a parent can never be one of the account's own descendants.
        for (Guid? current = parent; current is not null && selfId is not null;)
        {
            var next = await accounts.GetParentIdAsync(current.Value, ct);
            if (next == selfId) throw new FieldValidationException("parentId", "That parent would create a loop in the chart of accounts.");
            current = next;
        }
    }

    public static void Apply(AccountInput i, Account a)
    {
        a.Name = i.Name.Trim(); a.ReportingGroup = i.ReportingGroup; a.ParentId = i.ParentId; a.IsPostable = i.IsPostable; a.IsBank = i.IsBank;
        a.IsActive = i.IsActive; a.CurrencyCode = i.CurrencyCode?.ToUpperInvariant();
    }
}
