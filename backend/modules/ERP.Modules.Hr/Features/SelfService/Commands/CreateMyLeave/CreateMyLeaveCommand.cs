using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Files a leave request for myself.</summary>
public sealed record CreateMyLeaveCommand(MyLeaveInput Input) : ICommand<LeaveRequestDto>;
