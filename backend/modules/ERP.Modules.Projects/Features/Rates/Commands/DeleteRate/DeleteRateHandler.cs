using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteRateHandler(IProjectRateRepository rates) : ICommandHandler<DeleteRateCommand>
{
    public async Task<Unit> Handle(DeleteRateCommand command, CancellationToken ct)
    {
        rates.Remove(await rates.GetForUpdateAsync(command.Id, ct));
        await rates.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
