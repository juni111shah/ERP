using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record CoaNumberingDto(string Mode, IReadOnlyList<int> Segments, string Separator, string Pattern, int MaxLength);
public sealed record NextAccountCodeDto(string Code);

public sealed record GetCoaNumbering : IQuery<CoaNumberingDto>, IRequirePermission { public string RequiredPermission => CorePermissions.AccountsRead; }
public sealed record SetCoaNumbering(string Mode, IReadOnlyList<int> Segments, string Separator) : ICommand<CoaNumberingDto>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
/// <summary>The next free code under a parent (or the next root of a type). In free mode it returns an empty code: nothing is suggested.</summary>
public sealed record GetNextAccountCode(Guid? ParentId, string? Type) : IQuery<NextAccountCodeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.AccountsWrite; }

public sealed class SetCoaNumberingValidator : AbstractValidator<SetCoaNumbering>
{
    public SetCoaNumberingValidator() => RuleFor(x => x).Custom((x, ctx) =>
    {
        var error = new AccountCodeMask(x.Mode?.ToLowerInvariant() ?? "", x.Segments ?? [], x.Separator ?? "").Validate();
        if (error is not null) ctx.AddFailure("mask", error);
    });
}

/// <summary>Reads the numbering mask from the workspace settings and applies it: suggesting the next code and validating new or changed codes.</summary>
public sealed class AccountNumbering(ISettingsService settings, IReadRepository<Account> accounts, IReadRepository<JournalEntry> journals)
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
            parentCode = await accounts.Query(new Specification<Account>(a => a.Id == pid)).Select(a => a.Code).FirstOrDefaultAsync(ct)
                ?? throw new FieldValidationException("parentId", "Parent account not found.");
            var level = mask.LevelOf(parentCode);
            if (level == 0) throw new FieldValidationException("parentId", $"The parent code '{parentCode}' does not follow the numbering mask {mask.Pattern}, so no sub-code can be suggested.");
            if (level >= mask.Segments.Count) throw new FieldValidationException("parentId", $"'{parentCode}' is already at the deepest level of the mask ({mask.Pattern}).");
        }
        var prefix = parentCode is null ? "" : parentCode + mask.Separator;
        var depth = parentCode is null ? 1 : mask.LevelOf(parentCode) + 1;
        var existing = await accounts.Query(new Specification<Account>(a => a.Code.StartsWith(prefix))).Select(a => a.Code).ToListAsync(ct);
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
        var parent = await accounts.Query(new Specification<Account>(a => a.Id == pid)).Select(a => new { a.Code, a.Type }).FirstOrDefaultAsync(ct)
            ?? throw new FieldValidationException("parentId", "Parent account not found.");
        var parentLevel = mask.LevelOf(parent.Code);
        if (parentLevel == 0) throw new FieldValidationException("parentId", $"The parent code '{parent.Code}' does not follow the numbering mask {mask.Pattern}.");
        if (parentLevel >= mask.Segments.Count) throw new FieldValidationException("parentId", $"'{parent.Code}' is already at the deepest level of the mask ({mask.Pattern}).");
        if (level != parentLevel + 1 || mask.ParentCodeOf(code) != parent.Code)
            throw new FieldValidationException("code", $"A sub-account of {parent.Code} must start with '{parent.Code}{mask.Separator}' followed by {mask.Segments[parentLevel]} digits.");
        if (parent.Type != type) throw new FieldValidationException("type", "A sub-account must have the same type as its parent.");
        // A first child under an account that already has postings would leave the parent with a balance of its own.
        if (!await accounts.AnyAsync(a => a.ParentId == pid, ct) && await HasPostingsAsync(pid, ct))
            throw new ConflictException($"Account {parent.Code} already has postings, so it cannot get its first sub-account. Create the sub-account under another parent or move the balance first.");
    }

    private Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct)
        => journals.Query().SelectMany(j => j.Lines).AnyAsync(l => l.AccountId == accountId, ct);
}

internal sealed class AccountNumberingHandlers(AccountNumbering numbering, ISettingsService settings, IUnitOfWork unitOfWork) :
    IRequestHandler<GetCoaNumbering, CoaNumberingDto>,
    IRequestHandler<SetCoaNumbering, CoaNumberingDto>,
    IRequestHandler<GetNextAccountCode, NextAccountCodeDto>
{
    public async Task<CoaNumberingDto> Handle(GetCoaNumbering q, CancellationToken ct) => ToDto(await numbering.GetMaskAsync(ct));

    public async Task<CoaNumberingDto> Handle(SetCoaNumbering cmd, CancellationToken ct)
    {
        var mask = new AccountCodeMask(cmd.Mode.ToLowerInvariant(), cmd.Segments.ToList(), cmd.Separator ?? "");
        await settings.SetRawAsync(AccountNumbering.SettingKey, AccountNumbering.ToJson(mask), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(mask);
    }

    public async Task<NextAccountCodeDto> Handle(GetNextAccountCode q, CancellationToken ct)
    {
        AccountType? type = null;
        if (!string.IsNullOrWhiteSpace(q.Type))
            type = Enum.TryParse<AccountType>(q.Type, ignoreCase: true, out var t) && Enum.IsDefined(t) && !int.TryParse(q.Type, out _) ? t
                : throw new FieldValidationException("type", "Unknown account type. Use asset, liability, equity, income or expense.");
        return new(await numbering.NextCodeAsync(q.ParentId, type, ct));
    }

    private static CoaNumberingDto ToDto(AccountCodeMask m) => new(m.Mode, m.Segments, m.Separator, m.Pattern, m.MaxLength);
}
