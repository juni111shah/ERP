using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Pauses, resumes or terminates a retainer.</summary>
public record UpdateRetainerStatusCommand(
    Guid Id,
    RetainerStatus Status) : ICommand<RetainerDto>;
