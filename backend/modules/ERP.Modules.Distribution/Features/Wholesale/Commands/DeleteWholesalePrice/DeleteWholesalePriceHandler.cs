using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;
using MediatR;

namespace ERP.Modules.Distribution.Features;

internal sealed class DeleteWholesalePriceHandler(IWholesalePriceMatrixRepository matrix) : ICommandHandler<DeleteWholesalePriceCommand>
{
    public async Task<Unit> Handle(DeleteWholesalePriceCommand request, CancellationToken cancellationToken)
    {
        var price = await matrix.GetForUpdateAsync(request.Id, cancellationToken);
        matrix.Remove(price);
        await matrix.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
