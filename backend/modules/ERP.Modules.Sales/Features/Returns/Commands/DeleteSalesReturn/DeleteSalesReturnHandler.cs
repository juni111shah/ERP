using ERP.Application.Common;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class DeleteSalesReturnHandler(ISalesReturnRepository returns) : ICommandHandler<DeleteSalesReturnCommand>
{
    public async Task<Unit> Handle(DeleteSalesReturnCommand cmd, CancellationToken ct)
    {
        var ret = await returns.GetForUpdateAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Draft) throw new ConflictException("Only a draft return can be deleted. Void it instead.");
        returns.Remove(ret);
        await returns.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
