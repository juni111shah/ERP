using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.IndustryPacks.Domain;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Checks a record's input against its definition and copies it onto the record (create and update).</summary>
internal sealed class RecordInputApplier(IIndustryRecordRepository records, IReadRepository<Party> parties, IReadRepository<Item> items, IReadRepository<BusinessUnit> units, ICurrentUser user)
{
    public async Task ApplyAsync(IndustryRecord record, RecordTypeDef def, string module, RecordInput i, CancellationToken ct)
    {
        record.Title = i.Title.Trim();
        record.RecordDate = i.RecordDate;
        record.DueDate = def.HasDueDate ? i.DueDate : null;
        record.Notes = i.Notes?.Trim();
        record.Data = ValidateData(def, module, i.Data, record.Data);

        if (i.BusinessUnitId is { } unitId)
        {
            if (!await units.AnyAsync(u => u.Id == unitId && u.LegalEntityId == i.LegalEntityId, ct)) throw new FieldValidationException("businessUnitId", "Choose a business unit of this legal entity.");
        }
        record.BusinessUnitId = i.BusinessUnitId;

        if (i.PartyId is { } partyId)
        {
            if (def.PartyRole is null) throw new FieldValidationException("partyId", $"{def.Label} do not have a party.");
            var party = await parties.FindAsync(partyId, ct) ?? throw new FieldValidationException("partyId", "That party was not found.");
            if (def.PartyRole == "Customer") ERP.Application.Parties.CustomerRules.RequireCustomer(party);
            if (def.PartyRole == "Vendor") ERP.Application.Parties.VendorRules.RequireVendor(party);
            record.PartyId = party.Id;
            record.PartyName = party.Name;
        }
        else { record.PartyId = null; record.PartyName = null; }

        if (i.ParentId is { } parentId)
        {
            if (def.ParentType is null) throw new FieldValidationException("parentId", $"{def.Label} do not belong to another record.");
            var parent = await records.FindParentAsync(parentId, module, def.ParentType, ct)
                ?? throw new FieldValidationException("parentId", $"Choose a valid {(def.ParentLabel ?? "record").ToLowerInvariant()}.");
            if (parent.LegalEntityId != i.LegalEntityId) throw new FieldValidationException("parentId", "It belongs to another legal entity.");
            record.ParentId = parent.Id;
            record.ParentTitle = parent.Title;
        }
        else { record.ParentId = null; record.ParentTitle = null; }

        var itemIds = (i.Lines ?? []).Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0 && await items.CountAsync(x => itemIds.Contains(x.Id), ct) != itemIds.Count) throw new FieldValidationException("lines", "One of the items was not found.");
    }

    /// <summary>Keeps only declared fields, checks each value against its type, and keeps restricted values the caller was not shown.</summary>
    private string ValidateData(RecordTypeDef def, string module, JsonObject? input, string existingJson)
    {
        var existing = JsonNode.Parse(existingJson)?.AsObject() ?? [];
        var canSeeSensitive = user.HasPermission(PackModule.SensitivePermission(module, def.Key));
        var errors = new Dictionary<string, string[]>();
        var result = new JsonObject();
        foreach (var field in def.Fields)
        {
            if (field.Sensitive && !canSeeSensitive)
            {
                if (existing[field.Key] is { } kept) result[field.Key] = kept.DeepClone();
                continue;
            }
            var value = input?[field.Key];
            var isEmpty = value is null || (value is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s));
            if (isEmpty)
            {
                if (field.Required) errors[field.Key] = [$"{field.Label} is required."];
                continue;
            }
            var error = CheckField(field, value!);
            if (error is not null) errors[field.Key] = [error];
            else result[field.Key] = value!.DeepClone();
        }
        if (errors.Count > 0) throw new FieldValidationException(errors);
        return result.ToJsonString();
    }

    private static string? CheckField(RecordFieldDef field, JsonNode value)
    {
        if (value is not JsonValue v) return $"{field.Label} has an invalid value.";
        switch (field.Type)
        {
            case "number":
            case "money":
                return v.TryGetValue<decimal>(out var n) && (field.Type != "money" || n >= 0) ? null : $"{field.Label} must be a valid number.";
            case "date":
                return v.TryGetValue<string>(out var d) && DateOnly.TryParse(d, out _) ? null : $"{field.Label} must be a date.";
            case "bool":
                return v.TryGetValue<bool>(out _) ? null : $"{field.Label} must be yes or no.";
            case "select":
                return v.TryGetValue<string>(out var o) && field.Options!.Contains(o) ? null : $"{field.Label} must be one of: {string.Join(", ", field.Options!)}.";
            default:
                return v.TryGetValue<string>(out var t) && t.Length <= 2000 ? null : $"{field.Label} is too long.";
        }
    }
}
