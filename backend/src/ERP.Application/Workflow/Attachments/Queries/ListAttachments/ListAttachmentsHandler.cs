using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class ListAttachmentsHandler(IAttachmentRepository attachments, AttachmentOwnerAccess owners) : IQueryHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    public async Task<IReadOnlyList<AttachmentDto>> Handle(ListAttachmentsQuery query, CancellationToken ct)
    {
        await owners.ResolveAndAuthorizeAsync(query.OwnerType, query.OwnerId, isWrite: false, ct);
        return await attachments.ListForOwnerAsync(query.OwnerType, query.OwnerId, ct);
    }
}
