using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>Lead conversion, open pipeline and win-rate figures of one legal entity or all of them.</summary>
public record GetCrmKpisQuery(Guid? LegalEntityId = null) : IQuery<CrmKpisDto>;
