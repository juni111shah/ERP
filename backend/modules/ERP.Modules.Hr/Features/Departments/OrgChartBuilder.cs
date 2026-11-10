namespace ERP.Modules.Hr.Features;

public static class OrgChartBuilder
{
    public static OrgChartDto Build(IReadOnlyCollection<OrgDepartmentSource> departments, IReadOnlyCollection<OrgPersonSource> people)
    {
        var deptIds = departments.ToDictionary(d => d.Id);
        var byDept = people.Where(p => p.DepartmentId is { } d && deptIds.ContainsKey(d)).ToLookup(p => p.DepartmentId!.Value);
        var children = departments.Where(d => d.ParentId is { } p && deptIds.ContainsKey(p)).ToLookup(d => d.ParentId!.Value);

        OrgDepartmentNode DeptNode(OrgDepartmentSource d, HashSet<Guid> path)
        {
            path.Add(d.Id);
            var kids = children[d.Id].Where(c => !path.Contains(c.Id)).OrderBy(c => c.Code).Select(c => DeptNode(c, path)).ToList();
            path.Remove(d.Id);
            return new OrgDepartmentNode(d.Id, d.Code, d.Name, d.HeadEmployeeId, byDept[d.Id].OrderBy(p => p.Number).Select(Member).ToList(), kids);
        }

        var roots = departments.Where(d => d.ParentId is not { } p || !deptIds.ContainsKey(p)).OrderBy(d => d.Code).ToList();
        // a department loop has no root; show its members' departments as roots so nothing disappears
        var seen = new HashSet<Guid>();
        var deptNodes = roots.Select(d => DeptNode(d, new HashSet<Guid>())).ToList();
        void Mark(OrgDepartmentNode n) { seen.Add(n.Id); foreach (var c in n.Children) Mark(c); }
        foreach (var n in deptNodes) Mark(n);
        foreach (var d in departments.Where(d => !seen.Contains(d.Id)).OrderBy(d => d.Code))
            if (!seen.Contains(d.Id)) { var n = DeptNode(d, new HashSet<Guid>()); deptNodes.Add(n); Mark(n); }

        var unassigned = people.Where(p => p.DepartmentId is not { } d || !deptIds.ContainsKey(d)).OrderBy(p => p.Number).Select(Member).ToList();

        var ids = people.Select(p => p.Id).ToHashSet();
        var reports = people.Where(p => p.ManagerId is { } m && ids.Contains(m)).ToLookup(p => p.ManagerId!.Value);
        var placed = new HashSet<Guid>();
        OrgPersonNode PersonNode(OrgPersonSource p)
        {
            placed.Add(p.Id);
            var kids = reports[p.Id].Where(c => !placed.Contains(c.Id)).OrderBy(c => c.Number).Select(PersonNode).ToList();
            return new OrgPersonNode(p.Id, p.Number, p.Name, p.Designation, p.DepartmentId, kids);
        }
        var lines = people.Where(p => p.ManagerId is not { } m || !ids.Contains(m)).OrderBy(p => p.Number).Select(PersonNode).ToList();
        foreach (var p in people.Where(p => !placed.Contains(p.Id)).OrderBy(p => p.Number)) // a manager loop has no root
            if (!placed.Contains(p.Id)) lines.Add(PersonNode(p));
        return new OrgChartDto(deptNodes, unassigned, lines);
    }

    private static OrgMember Member(OrgPersonSource p) => new(p.Id, p.Number, p.Name, p.Designation);
}
