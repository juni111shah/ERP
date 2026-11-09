using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Sets the manual adjustment (and its note) of one line of a draft run.</summary>
public sealed record AdjustPayrollLineCommand(Guid RunId, Guid LineId, decimal Adjustment, string? Note) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
