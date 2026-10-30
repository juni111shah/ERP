namespace ERP.Application.Common;

/// <summary>
/// Enforces valid document status transitions and guards system-only statuses.
/// </summary>
/// <typeparam name="TStatus">Enum type representing document status.</typeparam>
public class StatusTransitions<TStatus> where TStatus : struct, Enum
{
    private readonly IReadOnlyDictionary<TStatus, IReadOnlySet<TStatus>> _allowed;
    private readonly IReadOnlySet<TStatus> _systemOnly;

    public StatusTransitions(
        IDictionary<TStatus, IEnumerable<TStatus>> allowed,
        IEnumerable<TStatus>? systemOnly = null)
    {
        _allowed = allowed.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<TStatus>)kvp.Value.ToHashSet());
        _systemOnly = systemOnly != null ? systemOnly.ToHashSet() : new HashSet<TStatus>();
    }

    public bool CanTransition(TStatus from, TStatus to, bool isSystem = false)
    {
        if (!isSystem && _systemOnly.Contains(to))
            return false;

        return _allowed.TryGetValue(from, out var next) && next.Contains(to);
    }

    public IReadOnlyCollection<TStatus> AllowedTransitions(TStatus from, bool isSystem = false)
    {
        if (!_allowed.TryGetValue(from, out var next))
            return Array.Empty<TStatus>();

        return isSystem ? next.ToList() : next.Where(s => !_systemOnly.Contains(s)).ToList();
    }

    public void Ensure(TStatus from, TStatus to, bool isSystem = false)
    {
        if (!isSystem && _systemOnly.Contains(to))
        {
            throw new ConflictException(
                $"Status '{to}' can only be set by the system conversion process, not through the status endpoint.");
        }

        if (!_allowed.TryGetValue(from, out var allowed) || !allowed.Contains(to))
        {
            var userAllowed = allowed != null
                ? (isSystem ? allowed : allowed.Where(s => !_systemOnly.Contains(s)))
                : Enumerable.Empty<TStatus>();

            var list = string.Join(", ", userAllowed.Select(s => s.ToString()));
            var allowedMsg = string.IsNullOrEmpty(list) ? "none" : list;

            throw new ConflictException(
                $"Cannot transition from '{from}' to '{to}'. Allowed transitions from '{from}': {allowedMsg}.");
        }
    }
}
