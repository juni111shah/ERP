using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>
/// Moves a workspace to another business type. Only ever adds: modules, roles and accounts it does not have yet. Nothing is switched off,
/// removed or renumbered, because the workspace may already hold data in them.
/// </summary>
public sealed record ChangeIndustryCommand(ChangeIndustryInput Input) : ICommand<ChangeIndustryResult>, IRequirePermission { public string RequiredPermission => CorePermissions.ModulesManage; }
