using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

internal sealed class UploadAttachmentHandler(IAttachmentRepository attachments, AttachmentOwnerAccess owners, IFileStore store, ISettingsService settings)
    : ICommandHandler<UploadAttachmentCommand, AttachmentDto>
{
    public async Task<AttachmentDto> Handle(UploadAttachmentCommand command, CancellationToken ct)
    {
        // 1. The caller may write the owner record.
        await owners.ResolveAndAuthorizeAsync(command.OwnerType, command.OwnerId, isWrite: true, ct);

        // 2. Size and type limits.
        var maxMb = await settings.GetAsync<int?>("core.maxAttachmentMb", ct: ct) ?? 10;
        if (command.Length > maxMb * 1024L * 1024L) throw new FieldValidationException("file", $"Files can be at most {maxMb} MB.");
        var ext = Path.GetExtension(command.FileName).ToLowerInvariant();
        if (!AttachmentFileTypes.AllowedExtensions.Contains(ext)) throw new FieldValidationException("file", "This file type is not allowed.");

        // 3. Store the file (the content type comes from the extension, not the client) and record it.
        var derivedContentType = AttachmentFileTypes.GetContentType(command.FileName);
        var key = await store.SaveAsync(command.Content, command.FileName, ct);
        var attachment = new Attachment
        {
            OwnerType = command.OwnerType, OwnerId = command.OwnerId, FileName = Path.GetFileName(command.FileName), ContentType = derivedContentType,
            SizeBytes = command.Length, StorageKey = key, Category = command.Category?.Trim(), Description = command.Description?.Trim(), ExpiresOn = command.ExpiresOn,
        };
        attachments.Add(attachment);
        await attachments.SaveChangesAsync(ct);
        return new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt, attachment.Category, attachment.Description, attachment.ExpiresOn);
    }
}
