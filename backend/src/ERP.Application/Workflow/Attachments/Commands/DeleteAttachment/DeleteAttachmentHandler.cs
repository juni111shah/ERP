using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Workflow;

internal sealed class DeleteAttachmentHandler(IAttachmentRepository attachments, AttachmentOwnerAccess owners) : ICommandHandler<DeleteAttachmentCommand>
{
    public async Task<Unit> Handle(DeleteAttachmentCommand command, CancellationToken ct)
    {
        var a = await attachments.GetForUpdateAsync(command.Id, ct);
        await owners.ResolveAndAuthorizeAsync(a.OwnerType, a.OwnerId, isWrite: true, ct);
        attachments.Remove(a);
        await attachments.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
