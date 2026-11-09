namespace ERP.Modules.Hr.Features;

/// <summary>Cycle check for parent / manager links.</summary>
public static class Hierarchy
{
    /// <summary>True when making <paramref name="newParent"/> the parent of <paramref name="id"/> would loop (it is the item itself or one of its descendants).</summary>
    public static bool WouldCycle(Guid id, Guid? newParent, Func<Guid, Guid?> parentOf)
    {
        var seen = new HashSet<Guid>();
        for (var cursor = newParent; cursor is { } c; cursor = parentOf(c))
        {
            if (c == id) return true;
            if (!seen.Add(c)) return false; // an existing loop elsewhere is not caused by this link
        }
        return false;
    }
}
