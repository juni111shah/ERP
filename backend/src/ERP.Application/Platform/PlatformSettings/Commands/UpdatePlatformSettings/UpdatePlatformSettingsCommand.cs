using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Saves the platform's settings. Legal texts sent back unchanged stay the starter wording.</summary>
public sealed record UpdatePlatformSettingsCommand(PlatformSettingsInput Input) : ICommand<PlatformSettingsView>;
