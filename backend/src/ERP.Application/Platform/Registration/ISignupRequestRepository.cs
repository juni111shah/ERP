using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for registrations. Implemented in ERP.Infrastructure/Repositories/Platform/SignupRequestRepository.cs.</summary>
public interface ISignupRequestRepository : IRepository<SignupRequest>
{
    /// <summary>The registrations of <paramref name="email"/> still waiting for email confirmation or review, tracked for changes.</summary>
    Task<List<SignupRequest>> ListPendingForEmailForUpdateAsync(string email, CancellationToken ct);

    /// <summary>The registration waiting for email confirmation whose link token hashes to <paramref name="tokenHash"/>, tracked, or null.</summary>
    Task<SignupRequest?> FindPendingEmailByTokenForUpdateAsync(string tokenHash, CancellationToken ct);

    /// <summary>One page of registrations, newest first, optionally in one state and matching a search, with the total count.</summary>
    Task<(List<SignupRequest> Rows, int Total)> ListPageAsync(SignupStatus? status, string? search, int page, int size, CancellationToken ct);

    /// <summary>Which of <paramref name="emails"/> have registered.</summary>
    Task<List<string>> RegisteredEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct);
}
