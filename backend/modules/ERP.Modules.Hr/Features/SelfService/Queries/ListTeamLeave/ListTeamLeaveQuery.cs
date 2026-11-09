using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Leave requests of the people who report to me (so a manager can find what to decide without hr.leave.read).</summary>
public sealed record ListTeamLeaveQuery(PagedQuery Paging, string? Status) : IQuery<PagedResult<LeaveRequestRow>>;
