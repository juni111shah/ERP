using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using MediatR;

namespace ERP.Modules.Pos.Features;

internal sealed class DeleteRegisterHandler(IRepository<PosRegister> registers, PosRegisterSetup setup) : ICommandHandler<DeleteRegisterCommand>
{
    public async Task<Unit> Handle(DeleteRegisterCommand request, CancellationToken cancellationToken)
    {
        var register = await registers.GetForUpdateAsync(request.Id, cancellationToken);
        await setup.EnsureNoOpenSessionAsync(register.Id, "delete", cancellationToken);
        registers.Remove(register);
        await registers.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
