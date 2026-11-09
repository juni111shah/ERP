using System.Text.Json.Nodes;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record AccountInput(
    string Code, string Name, AccountType Type, string? ReportingGroup = null, Guid? ParentId = null, bool IsPostable = true, bool IsBank = false,
    bool IsActive = true, string? CurrencyCode = null, JsonObject? CustomFields = null);
