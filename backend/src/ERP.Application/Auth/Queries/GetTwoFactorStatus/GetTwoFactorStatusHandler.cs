using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class GetTwoFactorStatusHandler(IAuthService auth, ICurrentUser user) : IQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatus>
{
    public Task<TwoFactorStatus> Handle(GetTwoFactorStatusQuery query, CancellationToken ct) => auth.TwoFactorStatusAsync(user.UserId ?? throw new ForbiddenException(), ct);
}
