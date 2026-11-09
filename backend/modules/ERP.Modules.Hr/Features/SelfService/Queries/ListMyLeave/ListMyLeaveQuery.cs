using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>My own leave requests.</summary>
public sealed record ListMyLeaveQuery(PagedQuery Paging) : IQuery<PagedResult<LeaveRequestRow>>;
