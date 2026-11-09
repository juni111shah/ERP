using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

internal sealed class GetStoreByIdHandler(IReadRepository<RetailStore> stores) : IQueryHandler<GetStoreByIdQuery, RetailStoreDto?>
{
    public async Task<RetailStoreDto?> Handle(GetStoreByIdQuery request, CancellationToken cancellationToken)
    {
        var store = await stores.FindAsync(request.Id, cancellationToken);
        return store is null ? null : RetailStoreDto.From(store);
    }
}
