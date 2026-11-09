using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;
using MediatR;

namespace ERP.Modules.Purchasing.Features;

internal sealed class DeletePurchaseReturnHandler(IPurchaseReturnRepository returns) : ICommandHandler<DeletePurchaseReturnCommand>
{
    public async Task<Unit> Handle(DeletePurchaseReturnCommand cmd, CancellationToken ct)
    {
        var ret = await returns.GetForUpdateAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Draft) throw new ConflictException("Only a draft return can be deleted. Void it instead.");
        returns.Remove(ret);
        await returns.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
