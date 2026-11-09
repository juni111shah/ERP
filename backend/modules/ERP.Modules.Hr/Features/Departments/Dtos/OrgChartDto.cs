namespace ERP.Modules.Hr.Features;

public sealed record OrgDepartmentSource(Guid Id, string Code, string Name, Guid? ParentId, Guid? HeadEmployeeId);
public sealed record OrgPersonSource(Guid Id, string Number, string Name, string Designation, Guid? DepartmentId, Guid? ManagerId);
public sealed record OrgMember(Guid Id, string Number, string Name, string Designation);
public sealed record OrgDepartmentNode(Guid Id, string Code, string Name, Guid? HeadEmployeeId, IReadOnlyList<OrgMember> Members, IReadOnlyList<OrgDepartmentNode> Children);
public sealed record OrgPersonNode(Guid Id, string Number, string Name, string Designation, Guid? DepartmentId, IReadOnlyList<OrgPersonNode> Reports);
/// <summary>Departments as a tree with their members, and people as a reporting tree (manager relation).</summary>
public sealed record OrgChartDto(IReadOnlyList<OrgDepartmentNode> Departments, IReadOnlyList<OrgMember> Unassigned, IReadOnlyList<OrgPersonNode> ReportingLines);
