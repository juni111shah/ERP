using ERP.Application.Security;
using System.Text.Json;
using ERP.Application.Common;

namespace ERP.Application.Settings;

/// <summary>Sets several settings at once. Core keys need core.settings.write; a module's keys also accept &lt;module&gt;.settings.write.</summary>
public sealed record UpdateSettingsCommand(Dictionary<string, JsonElement> Values) : ICommand, IAuthorizesInHandler;
