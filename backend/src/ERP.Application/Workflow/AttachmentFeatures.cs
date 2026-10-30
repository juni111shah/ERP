using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, string? Category = null, string? Description = null, DateOnly? ExpiresOn = null);
/// <summary>A row of the document vault: any tracked document with what it belongs to and how soon it expires.</summary>
public sealed record VaultRow(Guid Id, string OwnerType, Guid OwnerId, string FileName, string? Category, string? Description, DateOnly? ExpiresOn, DateTime CreatedAt);
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);

public sealed record ListAttachments(string OwnerType, Guid OwnerId) : IQuery<IReadOnlyList<AttachmentDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}

/// <summary>The HTTP layer hands over the file as a plain stream; size and type rules are enforced here.</summary>
public sealed record UploadAttachment(string OwnerType, Guid OwnerId, string FileName, string ContentType, long Length, Stream Content, string? Category = null, string? Description = null, DateOnly? ExpiresOn = null)
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

public sealed class ListAttachmentsValidator : AbstractValidator<ListAttachments>
{
    public ListAttachmentsValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner type and id are required.");
    }
}

public sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachment>
{
    public UploadAttachmentValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

internal sealed class AttachmentHandlers(
    IRepository<Attachment> attachments,
    IFileStore store,
    ISettingsService settings,
    ICurrentUser user,
    IEnumerable<IAttachmentOwnerResolver> resolvers) :
    IRequestHandler<ListAttachments, IReadOnlyList<AttachmentDto>>,
    IRequestHandler<UploadAttachment, AttachmentDto>,
    IRequestHandler<DownloadAttachment, AttachmentDownload>,
    IRequestHandler<DeleteAttachment, Unit>
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv", ".xlsx", ".xls", ".docx", ".doc", ".zip"];

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword",
        [".zip"] = "application/zip",
    };

    public static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext) && ContentTypeByExtension.TryGetValue(ext, out var mime))
            return mime;
        return "application/octet-stream";
    }

    private async Task<AttachmentOwnerDescriptor> ResolveAndAuthorizeAsync(string ownerType, Guid ownerId, bool isWrite, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            throw new FieldValidationException("ownerType", "Owner type and id are required.");
        if (ownerId == Guid.Empty)
            throw new FieldValidationException("ownerId", "Owner type and id are required.");

        var resolver = resolvers.FirstOrDefault(r => string.Equals(r.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        if (resolver is null)
            throw new FieldValidationException("ownerType", $"Unknown owner type '{ownerType}'.");

        var desc = await resolver.ResolveAsync(ownerId, ct);
        if (desc is null)
            throw new NotFoundException($"Owner record of type '{ownerType}' with ID '{ownerId}' was not found.");

        var requiredPermission = isWrite ? desc.WritePermission : desc.ReadPermission;
        if (!string.IsNullOrEmpty(requiredPermission) && !user.HasPermission(requiredPermission))
            throw new ForbiddenException();

        return desc;
    }

    public async Task<IReadOnlyList<AttachmentDto>> Handle(ListAttachments q, CancellationToken ct)
    {
        await ResolveAndAuthorizeAsync(q.OwnerType, q.OwnerId, isWrite: false, ct);
        return await attachments.Query(new Specification<Attachment>(x => x.OwnerType.ToLower() == q.OwnerType.ToLower() && x.OwnerId == q.OwnerId))
            .Select(x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt, x.Category, x.Description, x.ExpiresOn)).ToListAsync(ct);
    }

    public async Task<AttachmentDto> Handle(UploadAttachment cmd, CancellationToken ct)
    {
        await ResolveAndAuthorizeAsync(cmd.OwnerType, cmd.OwnerId, isWrite: true, ct);

        var maxMb = await settings.GetAsync<int?>("core.maxAttachmentMb", ct: ct) ?? 10;
        if (cmd.Length > maxMb * 1024L * 1024L) throw new FieldValidationException("file", $"Files can be at most {maxMb} MB.");
        var ext = Path.GetExtension(cmd.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new FieldValidationException("file", "This file type is not allowed.");

        var derivedContentType = GetContentType(cmd.FileName);
        var key = await store.SaveAsync(cmd.Content, cmd.FileName, ct);
        var attachment = new Attachment
        {
            OwnerType = cmd.OwnerType, OwnerId = cmd.OwnerId, FileName = Path.GetFileName(cmd.FileName), ContentType = derivedContentType,
            SizeBytes = cmd.Length, StorageKey = key, Category = cmd.Category?.Trim(), Description = cmd.Description?.Trim(), ExpiresOn = cmd.ExpiresOn,
        };
        attachments.Add(attachment);
        await attachments.SaveChangesAsync(ct);
        return new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt, attachment.Category, attachment.Description, attachment.ExpiresOn);
    }

    public async Task<AttachmentDownload> Handle(DownloadAttachment q, CancellationToken ct)
    {
        var a = await attachments.GetAsync(q.Id, ct);
        await ResolveAndAuthorizeAsync(a.OwnerType, a.OwnerId, isWrite: false, ct);
        var contentType = GetContentType(a.FileName);
        return new(await store.OpenAsync(a.StorageKey, ct), contentType, a.FileName);
    }

    public async Task<Unit> Handle(DeleteAttachment cmd, CancellationToken ct)
    {
        var a = await attachments.GetForUpdateAsync(cmd.Id, ct);
        await ResolveAndAuthorizeAsync(a.OwnerType, a.OwnerId, isWrite: true, ct);
        attachments.Remove(a);
        await attachments.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
