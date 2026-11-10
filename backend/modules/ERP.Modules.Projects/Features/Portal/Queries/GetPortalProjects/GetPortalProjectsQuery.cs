using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>The portal customer's running projects with their progress and milestones.</summary>
public sealed record GetPortalProjectsQuery : IQuery<IReadOnlyList<PortalProjectDto>>;
