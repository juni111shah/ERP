using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Reminders;

/// <summary>Sends a short test message so an administrator can check the email settings. Recorded in the message log.</summary>
public sealed record SendTestEmailCommand(string To) : ICommand<MessageLogDto>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
