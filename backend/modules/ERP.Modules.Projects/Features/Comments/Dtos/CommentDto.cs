using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record CommentInput(string OwnerType, Guid OwnerId, string Text);

public sealed record CommentDto(Guid Id, string OwnerType, Guid OwnerId, Guid? UserId, string AuthorName, string Text, DateTime CreatedAt)
{
    public static CommentDto From(ProjectComment c) => new(c.Id, c.OwnerType, c.OwnerId, c.UserId, c.AuthorName, c.Text, c.CreatedAt);
}
