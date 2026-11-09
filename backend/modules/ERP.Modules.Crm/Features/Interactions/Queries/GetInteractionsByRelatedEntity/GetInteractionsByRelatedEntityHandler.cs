using ERP.Application.Common;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class GetInteractionsByRelatedEntityHandler(ICrmInteractionRepository interactions)
    : IQueryHandler<GetInteractionsByRelatedEntityQuery, List<CustomerInteractionDto>>
{
    public async Task<List<CustomerInteractionDto>> Handle(GetInteractionsByRelatedEntityQuery request, CancellationToken cancellationToken)
    {
        var list = await interactions.ListForRelatedEntityAsync(request.RelatedEntityKind, request.RelatedEntityId, cancellationToken);
        return list.Select(CustomerInteractionDto.From).ToList();
    }
}
