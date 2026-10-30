using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common;

public static class GroupCounts
{
    /// <summary>Row counts per key in one grouped query (instead of one COUNT per key). Keys with no rows are absent.</summary>
    public static async Task<Dictionary<TKey, int>> CountByAsync<T, TKey>(this IQueryable<T> source, Expression<Func<T, TKey>> key, CancellationToken ct = default) where TKey : notnull
        => await source.GroupBy(key).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
}
