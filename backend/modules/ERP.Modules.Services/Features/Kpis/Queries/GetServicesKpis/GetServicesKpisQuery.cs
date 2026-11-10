using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>A legal entity's engagements, recurring retainer revenue, milestone pipeline and utilisation.</summary>
public record GetServicesKpisQuery(Guid LegalEntityId) : IQuery<ServicesKpisDto>;
