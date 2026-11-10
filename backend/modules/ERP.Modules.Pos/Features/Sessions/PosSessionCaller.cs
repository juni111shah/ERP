using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Who opens, closes or moves cash in a shift: always the signed-in caller, whatever the client sends. Shared by the shift commands.</summary>
public sealed class PosSessionCaller(ICurrentUser user, IUserDirectory users)
{
    /// <summary>The journal source of shift postings (cash over/short and drawer movements).</summary>
    public const string JournalSource = "pos-session";

    public Guid Id => user.UserId is { } id && id != Guid.Empty ? id : throw new ForbiddenException();

    public async Task<string> NameAsync(CancellationToken ct)
    {
        var profile = await users.GetProfileAsync(Id, ct);
        return !string.IsNullOrWhiteSpace(profile?.FullName) ? profile!.FullName
            : !string.IsNullOrWhiteSpace(user.Email) ? user.Email! : Id.ToString();
    }
}
