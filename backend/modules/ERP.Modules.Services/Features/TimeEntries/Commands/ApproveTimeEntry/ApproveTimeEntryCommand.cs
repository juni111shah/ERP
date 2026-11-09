using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>Approves someone else's time. The approver is always the signed-in user; ApprovedByUserId is accepted for older clients and ignored.</summary>
public record ApproveTimeEntryCommand(
    Guid Id,
    Guid? ApprovedByUserId = null) : ICommand<TimeEntryDto>;
