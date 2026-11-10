using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>One engagement; null when it does not exist.</summary>
public record GetEngagementByIdQuery(Guid Id) : IQuery<EngagementDto?>;
