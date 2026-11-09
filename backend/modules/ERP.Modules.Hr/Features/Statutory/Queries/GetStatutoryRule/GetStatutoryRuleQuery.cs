using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One statutory rule with its slabs.</summary>
public sealed record GetStatutoryRuleQuery(Guid Id) : IQuery<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
