using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>Switches core areas (budgets, cheques...) off or back on for the workspace. Returns the updated profile.</summary>
public sealed record SetCapabilitiesCommand(CapabilitiesInput Input) : ICommand<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
