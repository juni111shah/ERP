using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>The sign-offs asked of the portal customer, waiting ones first.</summary>
public sealed record ListPortalAcceptancesQuery : IQuery<IReadOnlyList<PortalAcceptanceDto>>;
