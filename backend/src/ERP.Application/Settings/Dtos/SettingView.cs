using System.Text.Json;

namespace ERP.Application.Settings;

public sealed record SettingView(string Key, string Label, string Module, string Type, string? Description, JsonElement Value);
