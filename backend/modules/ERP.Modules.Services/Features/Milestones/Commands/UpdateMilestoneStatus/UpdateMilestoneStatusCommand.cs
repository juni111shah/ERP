using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Moves a milestone to another status; ready to invoice stamps the completion date.</summary>
public record UpdateMilestoneStatusCommand(
    Guid Id,
    MilestoneStatus Status,
    Guid? InvoiceId) : ICommand<MilestoneDto>;
