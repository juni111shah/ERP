namespace ERP.Modules.Projects.Features;

/// <summary>Critical-path scheduling over task durations and dependencies; no I/O. Days are calendar days.</summary>
public static class ScheduleMath
{
    public sealed record Node(Guid Id, DateOnly? Start, DateOnly? Due, decimal EstimatedHours, IReadOnlyList<Guid> DependsOn);
    public sealed record Slot(Guid Id, DateOnly Start, DateOnly End, int Duration, int Slack, bool Critical);

    public static int DurationOf(Node n)
    {
        if (n.Start is { } s && n.Due is { } d && d >= s) return d.DayNumber - s.DayNumber + 1;
        return n.EstimatedHours > 0 ? Math.Max(1, (int)Math.Ceiling(n.EstimatedHours / 8m)) : 1;
    }

    public static IReadOnlyDictionary<Guid, Slot> Compute(IReadOnlyList<Node> nodes, DateOnly projectStart)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var dur = nodes.ToDictionary(n => n.Id, DurationOf);
        var es = new Dictionary<Guid, int>(); // day number of the earliest start
        int Es(Guid id, HashSet<Guid> path)
        {
            if (es.TryGetValue(id, out var v)) return v;
            if (!path.Add(id)) return projectStart.DayNumber; // a loop cannot be scheduled; the service refuses to create one, this is only a guard
            var n = byId[id];
            // a task with only a due date ends on it; one with no dates starts with the project
            var start = n.Start?.DayNumber ?? (n.Due is { } due ? due.DayNumber - dur[id] + 1 : projectStart.DayNumber);
            foreach (var d in n.DependsOn.Where(byId.ContainsKey)) start = Math.Max(start, Es(d, path) + dur[d]);
            path.Remove(id);
            return es[id] = start;
        }
        foreach (var n in nodes) Es(n.Id, []);
        var finish = nodes.Count == 0 ? projectStart.DayNumber : nodes.Max(n => es[n.Id] + dur[n.Id] - 1);

        var successors = nodes.SelectMany(n => n.DependsOn.Where(byId.ContainsKey).Select(d => (From: d, To: n.Id))).ToLookup(e => e.From, e => e.To);
        var lf = new Dictionary<Guid, int>();
        int Lf(Guid id)
        {
            if (lf.TryGetValue(id, out var v)) return v;
            var next = successors[id].ToList();
            var late = next.Count == 0 ? finish : next.Min(s => Lf(s) - dur[s]);
            return lf[id] = late;
        }
        var result = new Dictionary<Guid, Slot>();
        foreach (var n in nodes)
        {
            var slack = Lf(n.Id) - (es[n.Id] + dur[n.Id] - 1);
            result[n.Id] = new Slot(n.Id, DateOnly.FromDayNumber(es[n.Id]), DateOnly.FromDayNumber(es[n.Id] + dur[n.Id] - 1), dur[n.Id], slack, slack == 0);
        }
        return result;
    }
}
