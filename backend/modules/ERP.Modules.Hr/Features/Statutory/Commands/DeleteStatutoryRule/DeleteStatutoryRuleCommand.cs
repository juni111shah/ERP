using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a statutory rule never used in payroll (a used one is switched off instead).</summary>
public sealed record DeleteStatutoryRuleCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
