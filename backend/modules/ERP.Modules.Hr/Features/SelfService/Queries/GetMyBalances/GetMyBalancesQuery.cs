using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>My own leave balances of a year (this year when none is given).</summary>
public sealed record GetMyBalancesQuery(int? Year) : IQuery<IReadOnlyList<LeaveBalanceRow>>;
