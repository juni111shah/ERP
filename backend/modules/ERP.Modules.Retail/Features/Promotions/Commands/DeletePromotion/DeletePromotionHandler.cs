using ERP.Application.Common;
using ERP.Modules.Retail.Repositories;
using MediatR;

namespace ERP.Modules.Retail.Features;

internal sealed class DeletePromotionHandler(IRetailPromotionRepository promotions) : ICommandHandler<DeletePromotionCommand>
{
    public async Task<Unit> Handle(DeletePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await promotions.GetForUpdateAsync(request.Id, cancellationToken);
        promotions.Remove(promo);
        await promotions.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
