using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Cancels one of my own leave requests.</summary>
public sealed record CancelMyLeaveCommand(Guid Id) : ICommand<LeaveRequestDto>;
