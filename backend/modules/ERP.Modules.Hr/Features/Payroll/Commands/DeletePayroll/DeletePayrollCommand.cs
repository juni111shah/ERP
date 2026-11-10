using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a draft run with its loan recoveries and statutory rows.</summary>
public sealed record DeletePayrollCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
