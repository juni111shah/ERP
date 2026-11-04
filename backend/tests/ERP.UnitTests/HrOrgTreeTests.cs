using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

/// <summary>Cycle check for manager and parent links, and the org-chart tree builder.</summary>
public class HrOrgTreeTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    [Fact]
    public void A_link_to_itself_or_to_a_descendant_is_a_cycle()
    {
        var parents = new Dictionary<Guid, Guid?> { [B] = A, [C] = B, [A] = null, [D] = null };
        Guid? ParentOf(Guid id) => parents.GetValueOrDefault(id);

        Assert.True(Hierarchy.WouldCycle(A, A, ParentOf));      // own parent
        Assert.True(Hierarchy.WouldCycle(A, C, ParentOf));      // C reports (through B) to A
        Assert.False(Hierarchy.WouldCycle(C, A, ParentOf));     // moving C up is fine
        Assert.False(Hierarchy.WouldCycle(D, A, ParentOf));     // unrelated branch
        Assert.False(Hierarchy.WouldCycle(A, null, ParentOf));  // clearing the parent
    }

    [Fact]
    public void An_existing_loop_elsewhere_does_not_hang_the_check()
    {
        var parents = new Dictionary<Guid, Guid?> { [B] = C, [C] = B };
        Assert.False(Hierarchy.WouldCycle(A, B, id => parents.GetValueOrDefault(id)));
    }

    [Fact]
    public void The_org_chart_nests_departments_and_reporting_lines()
    {
        var ops = new OrgDepartmentSource(A, "OPS", "Operations", null, null);
        var field = new OrgDepartmentSource(B, "FLD", "Field", A, null);
        var boss = Guid.NewGuid(); var worker = Guid.NewGuid(); var loner = Guid.NewGuid();
        var people = new[]
        {
            new OrgPersonSource(boss, "E1", "Boss", "Head", A, null),
            new OrgPersonSource(worker, "E2", "Worker", "Agent", B, boss),
            new OrgPersonSource(loner, "E3", "Loner", "Agent", null, null),
        };

        var chart = OrgChartBuilder.Build([field, ops], people);

        var root = Assert.Single(chart.Departments);
        Assert.Equal("OPS", root.Code);
        Assert.Equal("Boss", Assert.Single(root.Members).Name);
        var child = Assert.Single(root.Children);
        Assert.Equal("Worker", Assert.Single(child.Members).Name);
        Assert.Equal("Loner", Assert.Single(chart.Unassigned).Name);

        Assert.Equal(2, chart.ReportingLines.Count);                           // Boss and Loner have no manager
        var top = chart.ReportingLines.Single(n => n.Id == boss);
        Assert.Equal(worker, Assert.Single(top.Reports).Id);
    }

    [Fact]
    public void A_manager_loop_in_the_data_still_shows_everyone_once()
    {
        var x = Guid.NewGuid(); var y = Guid.NewGuid();
        var chart = OrgChartBuilder.Build([], [new OrgPersonSource(x, "E1", "X", "T", null, y), new OrgPersonSource(y, "E2", "Y", "T", null, x)]);

        static int Count(IEnumerable<OrgPersonNode> nodes) => nodes.Sum(n => 1 + Count(n.Reports));
        Assert.Equal(2, Count(chart.ReportingLines));
    }
}
