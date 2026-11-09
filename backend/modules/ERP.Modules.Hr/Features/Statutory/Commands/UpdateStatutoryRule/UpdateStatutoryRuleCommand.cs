using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a statutory rule and replaces its slabs; it stays with its entity.</summary>
public sealed record UpdateStatutoryRuleCommand(Guid Id, StatutoryRuleInput Input) : ICommand<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
