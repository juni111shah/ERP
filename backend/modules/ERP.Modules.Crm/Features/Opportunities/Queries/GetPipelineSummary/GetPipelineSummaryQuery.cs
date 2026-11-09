using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>The count and expected revenue of the opportunities in each pipeline stage.</summary>
public record GetPipelineSummaryQuery(Guid? LegalEntityId = null) : IQuery<List<PipelineStageSummaryDto>>;
