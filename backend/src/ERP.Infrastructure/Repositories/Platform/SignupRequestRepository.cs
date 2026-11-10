using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class SignupRequestRepository(IRepository<SignupRequest> signups) : RepositoryBase<SignupRequest>(signups), ISignupRequestRepository
{
    public Task<List<SignupRequest>> ListPendingForEmailForUpdateAsync(string email, CancellationToken ct)
        => Tracked().Where(r => r.Email == email && (r.Status == SignupStatus.PendingEmail || r.Status == SignupStatus.PendingReview)).ToListAsync(ct);

    public Task<SignupRequest?> FindPendingEmailByTokenForUpdateAsync(string tokenHash, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(r => r.VerificationTokenHash == tokenHash && r.Status == SignupStatus.PendingEmail, ct);

    public async Task<(List<SignupRequest> Rows, int Total)> ListPageAsync(SignupStatus? status, string? search, int page, int size, CancellationToken ct)
    {
        var query = Query();
        if (status is { } parsed) query = query.Where(r => r.Status == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(r => r.BusinessName.Contains(s) || r.Email.Contains(s) || r.SignInKey.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, total);
    }

    public Task<List<string>> RegisteredEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct)
        => Query().Where(s => emails.Contains(s.Email)).Select(s => s.Email).Distinct().ToListAsync(ct);
}
