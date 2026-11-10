using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Reminders;

/// <summary>Runs the reminder pass now (admins and tests); returns how many messages were sent.</summary>
public sealed record SendRemindersNowCommand : ICommand<int>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
