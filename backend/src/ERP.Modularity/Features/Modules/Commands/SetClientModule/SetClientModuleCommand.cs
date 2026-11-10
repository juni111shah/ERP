using ERP.Application.Common;

namespace ERP.Modularity.Features;

/// <summary>A platform admin switching a module for a client. The caller is checked by the platform-admin policy on the endpoint.</summary>
public sealed record SetClientModuleCommand(Guid OrgId, string Key, bool Enable) : ICommand<ModuleState>;
