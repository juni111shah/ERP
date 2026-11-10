namespace ERP.Modules.Projects.Features;

/// <summary>Graph rules for tasks; no I/O.</summary>
public static class TaskGraph
{
    /// <summary>True when making <paramref name="taskId"/> depend on <paramref name="dependsOn"/> would create a loop.</summary>
    public static bool WouldLoop(Guid taskId, IEnumerable<Guid> dependsOn, IReadOnlyDictionary<Guid, List<Guid>> edges)
    {
        var seen = new HashSet<Guid>(); var stack = new Stack<Guid>(dependsOn);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n == taskId) return true;
            if (!seen.Add(n)) continue;
            if (edges.TryGetValue(n, out var next)) foreach (var m in next) stack.Push(m);
        }
        return false;
    }
}
