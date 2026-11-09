namespace ERP.Application.Workflow;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, string? Category = null, string? Description = null, DateOnly? ExpiresOn = null);
/// <summary>A row of the document vault: any tracked document with what it belongs to and how soon it expires.</summary>
public sealed record VaultRow(Guid Id, string OwnerType, Guid OwnerId, string FileName, string? Category, string? Description, DateOnly? ExpiresOn, DateTime CreatedAt);
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);
