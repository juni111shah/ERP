using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);

public sealed record ListAttachments(string OwnerType, Guid OwnerId) : IQuery<IReadOnlyList<AttachmentDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}

/// <summary>The HTTP layer hands over the file as a plain stream; size and type rules are enforced here.</summary>
public sealed record UploadAttachment(string OwnerType, Guid OwnerId, string FileName, string ContentType, long Length, Stream Content)
    : ICommand<AttachmentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsWrite;
}

public sealed record DownloadAttachment(Guid Id) : IQuery<AttachmentDownload>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}

/// <summary>Soft delete: the stored file is kept for audit.</summary>
public sealed record DeleteAttachment(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsWrite;
}

public sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachment>
{
    public UploadAttachmentValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.FileName).NotEmpty();
    }
}

internal sealed class AttachmentHandlers(IRepository<Attachment> attachments, IFileStore store, ISettingsService settings) :
    IRequestHandler<ListAttachments, IReadOnlyList<AttachmentDto>>,
    IRequestHandler<UploadAttachment, AttachmentDto>,
    IRequestHandler<DownloadAttachment, AttachmentDownload>,
    IRequestHandler<DeleteAttachment, Unit>
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv", ".xlsx", ".xls", ".docx", ".doc", ".zip"];

    public async Task<IReadOnlyList<AttachmentDto>> Handle(ListAttachments q, CancellationToken ct)
        => await attachments.Query(new Specification<Attachment>(x => x.OwnerType == q.OwnerType && x.OwnerId == q.OwnerId))
            .Select(x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt)).ToListAsync(ct);

    public async Task<AttachmentDto> Handle(UploadAttachment cmd, CancellationToken ct)
    {
        var maxMb = await settings.GetAsync<int?>("core.maxAttachmentMb", ct: ct) ?? 10;
        if (cmd.Length > maxMb * 1024L * 1024L) throw new FieldValidationException("file", $"Files can be at most {maxMb} MB.");
        if (!AllowedExtensions.Contains(Path.GetExtension(cmd.FileName).ToLowerInvariant())) throw new FieldValidationException("file", "This file type is not allowed.");
        var key = await store.SaveAsync(cmd.Content, cmd.FileName, ct);
        var attachment = new Attachment
        {
            OwnerType = cmd.OwnerType, OwnerId = cmd.OwnerId, FileName = Path.GetFileName(cmd.FileName), ContentType = cmd.ContentType,
            SizeBytes = cmd.Length, StorageKey = key,
        };
        attachments.Add(attachment);
        await attachments.SaveChangesAsync(ct);
        return new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt);
    }

    public async Task<AttachmentDownload> Handle(DownloadAttachment q, CancellationToken ct)
    {
        var a = await attachments.GetAsync(q.Id, ct);
        return new(await store.OpenAsync(a.StorageKey, ct), a.ContentType, a.FileName);
    }

    public async Task<Unit> Handle(DeleteAttachment cmd, CancellationToken ct)
    {
        attachments.Remove(await attachments.GetForUpdateAsync(cmd.Id, ct));
        await attachments.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
