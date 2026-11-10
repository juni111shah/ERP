using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class DownloadAttachmentHandler(IAttachmentRepository attachments, AttachmentOwnerAccess owners, IFileStore store) : IQueryHandler<DownloadAttachmentQuery, AttachmentDownload>
{
    public async Task<AttachmentDownload> Handle(DownloadAttachmentQuery query, CancellationToken ct)
    {
        var a = await attachments.GetAsync(query.Id, ct);
        await owners.ResolveAndAuthorizeAsync(a.OwnerType, a.OwnerId, isWrite: false, ct);
        var contentType = AttachmentFileTypes.GetContentType(a.FileName);
        return new(await store.OpenAsync(a.StorageKey, ct), contentType, a.FileName);
    }
}
