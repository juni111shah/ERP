using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Reads the numbering mask from the workspace settings and applies it: suggesting the next code and validating new or changed codes.</summary>
public sealed class AccountNumbering(ISettingsService settings, IAccountRepository accounts, IJournalEntryRepository journals)
{
    public const string SettingKey = "finance.coa.numbering";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AccountCodeMask> GetMaskAsync(CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(SettingKey, ct: ct);
        if (string.IsNullOrWhiteSpace(raw) || raw == "null") return AccountCodeMask.Default;
        try
        {
            var m = JsonSerializer.Deserialize<AccountCodeMask>(raw, Json);
            return m is not null && m.Validate() is null ? m : AccountCodeMask.Default;
        }
        catch (JsonException) { return AccountCodeMask.Default; }
    }

    public static bool IsValidJson(string raw)
    {
        try { var m = JsonSerializer.Deserialize<AccountCodeMask>(raw, Json); return m is not null && m.Validate() is null; }
        catch (JsonException) { return false; }
    }

    public static string ToJson(AccountCodeMask mask) => JsonSerializer.Serialize(mask, Json);

    public async Task<string> NextCodeAsync(Guid? parentId, AccountType? type, CancellationToken ct)
    {
        var mask = await GetMaskAsync(ct);
        if (!mask.IsHierarchical) return "";
        string? parentCode = null;
        if (parentId is { } pid)
        {
            parentCode = await accounts.GetCodeAsync(pid, ct)
                ?? throw new FieldValidationException("parentId", "Parent account not found.");
            var level = mask.LevelOf(parentCode);
            if (level == 0) throw new FieldValidationException("parentId", $"The parent code '{parentCode}' does not follow the numbering mask {mask.Pattern}, so no sub-code can be suggested.");
            if (level >= mask.Segments.Count) throw new FieldValidationException("parentId", $"'{parentCode}' is already at the deepest level of the mask ({mask.Pattern}).");
        }
        var prefix = parentCode is null ? "" : parentCode + mask.Separator;
        var depth = parentCode is null ? 1 : mask.LevelOf(parentCode) + 1;
        var existing = await accounts.ListCodesStartingWithAsync(prefix, ct);
        var taken = existing.Where(c => mask.LevelOf(c) == depth).Select(mask.LastSegment).Where(n => n >= 0).ToList();
        int next;
        if (taken.Count > 0) next = taken.Max() + 1;
        else if (parentCode is null)
        {
            // the first root starts at the thousand of its type (assets 1000, liabilities 2000, ...), scaled to the length of the first segment
            next = ((type is { } t ? (int)t : 0) + 1) * (int)Math.Pow(10, mask.Segments[0] - 1);
        }
        else next = 1;
        if (next > mask.MaxSegment(depth))
            throw new ConflictException($"No numbers are left under {parentCode ?? "the top level"} with the mask {mask.Pattern}. Widen the segment or choose another parent.");
        return mask.Format(parentCode, next);
    }

    /// <summary>Hierarchical mode only. Throws when the code, parent or type break the mask; free mode accepts anything (as before).</summary>
    public async Task ValidateAsync(string code, Guid? parentId, AccountType type, CancellationToken ct)
    {
        var mask = await GetMaskAsync(ct);
        if (!mask.IsHierarchical) return;
        var level = mask.LevelOf(code);
        if (level == 0) throw new FieldValidationException("code", $"The code must follow the numbering mask {mask.Pattern} (digits only).");
        if (parentId is not { } pid)
        {
            if (level != 1) throw new FieldValidationException("code", $"A top-level account code has {mask.Segments[0]} digits. Choose a parent for '{code}'.");
            return;
        }
        var parent = await accounts.FindCodeAndTypeAsync(pid, ct)
            ?? throw new FieldValidationException("parentId", "Parent account not found.");
        var parentLevel = mask.LevelOf(parent.Code);
        if (parentLevel == 0) throw new FieldValidationException("parentId", $"The parent code '{parent.Code}' does not follow the numbering mask {mask.Pattern}.");
        if (parentLevel >= mask.Segments.Count) throw new FieldValidationException("parentId", $"'{parent.Code}' is already at the deepest level of the mask ({mask.Pattern}).");
        if (level != parentLevel + 1 || mask.ParentCodeOf(code) != parent.Code)
            throw new FieldValidationException("code", $"A sub-account of {parent.Code} must start with '{parent.Code}{mask.Separator}' followed by {mask.Segments[parentLevel]} digits.");
        if (parent.Type != type) throw new FieldValidationException("type", "A sub-account must have the same type as its parent.");
        // A first child under an account that already has postings would leave the parent with a balance of its own.
        if (!await accounts.AnyAsync(a => a.ParentId == pid, ct) && await journals.HasPostingsAsync(pid, ct))
            throw new ConflictException($"Account {parent.Code} already has postings, so it cannot get its first sub-account. Create the sub-account under another parent or move the balance first.");
    }
}
