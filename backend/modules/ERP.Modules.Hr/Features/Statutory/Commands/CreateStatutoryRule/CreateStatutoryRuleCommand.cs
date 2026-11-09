using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a statutory rule (social security, tax ...) of a legal entity; the code is unique per entity.</summary>
public sealed record CreateStatutoryRuleCommand(StatutoryRuleInput Input) : ICommand<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
