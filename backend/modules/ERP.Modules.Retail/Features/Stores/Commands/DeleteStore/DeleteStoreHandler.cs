using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using MediatR;

namespace ERP.Modules.Retail.Features;

internal sealed class DeleteStoreHandler(IRepository<RetailStore> stores) : ICommandHandler<DeleteStoreCommand>
{
    public async Task<Unit> Handle(DeleteStoreCommand request, CancellationToken cancellationToken)
    {
        var store = await stores.GetForUpdateAsync(request.Id, cancellationToken);
        stores.Remove(store);
        await stores.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
