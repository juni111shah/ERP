using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Submits one of my own draft leave requests.</summary>
public sealed record SubmitMyLeaveCommand(Guid Id) : ICommand<LeaveRequestDto>;
