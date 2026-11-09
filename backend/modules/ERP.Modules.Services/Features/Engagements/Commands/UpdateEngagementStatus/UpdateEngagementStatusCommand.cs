using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Moves an engagement to another status; completing it stamps the actual end date.</summary>
public record UpdateEngagementStatusCommand(
    Guid Id,
    EngagementStatus Status) : ICommand<EngagementDto>;
