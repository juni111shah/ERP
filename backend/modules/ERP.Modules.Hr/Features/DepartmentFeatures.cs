using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record DepartmentInput(string Code, string Name, Guid? ParentDepartmentId = null, Guid? HeadEmployeeId = null, Guid? BusinessUnitId = null, bool IsActive = true);
public sealed record DepartmentDto(Guid Id, string Code, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId, Guid? BusinessUnitId, bool IsActive);
public sealed record PositionInput(string Code, string Title, string? Grade = null, bool IsActive = true);
public sealed record PositionDto(Guid Id, string Code, string Title, string? Grade, bool IsActive);

public sealed record CreateDepartment(DepartmentInput Input) : ICommand<DepartmentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdateDepartment(Guid Id, DepartmentInput Input) : ICommand<DepartmentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteDepartment(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record CreatePosition(PositionInput Input) : ICommand<PositionDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdatePosition(Guid Id, PositionInput Input) : ICommand<PositionDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeletePosition(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record GetOrgChart() : IQuery<OrgChartDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }

public sealed class DepartmentInputValidator : AbstractValidator<DepartmentInput>
{
    public DepartmentInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartment> { public CreateDepartmentValidator() => RuleFor(x => x.Input).SetValidator(new DepartmentInputValidator()); }
public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartment> { public UpdateDepartmentValidator() => RuleFor(x => x.Input).SetValidator(new DepartmentInputValidator()); }
public sealed class PositionInputValidator : AbstractValidator<PositionInput>
{
    public PositionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Grade).MaximumLength(50);
    }
}
public sealed class CreatePositionValidator : AbstractValidator<CreatePosition> { public CreatePositionValidator() => RuleFor(x => x.Input).SetValidator(new PositionInputValidator()); }
public sealed class UpdatePositionValidator : AbstractValidator<UpdatePosition> { public UpdatePositionValidator() => RuleFor(x => x.Input).SetValidator(new PositionInputValidator()); }

internal sealed class StructureHandlers(
    IRepository<Department> departments, IRepository<Position> positions, IReadRepository<Employee> employees, IReadRepository<BusinessUnit> units) :
    IRequestHandler<CreateDepartment, DepartmentDto>,
    IRequestHandler<UpdateDepartment, DepartmentDto>,
    IRequestHandler<DeleteDepartment, Unit>,
    IRequestHandler<CreatePosition, PositionDto>,
    IRequestHandler<UpdatePosition, PositionDto>,
    IRequestHandler<DeletePosition, Unit>,
    IRequestHandler<GetOrgChart, OrgChartDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartment cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await departments.AnyAsync(d => d.Code == code, ct)) throw new ConflictException($"A department with code '{code}' already exists.");
        var d = new Department();
        await FillAsync(d, cmd.Input, code, ct);
        departments.Add(d);
        await departments.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<DepartmentDto> Handle(UpdateDepartment cmd, CancellationToken ct)
    {
        var d = await departments.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != d.Code && await departments.AnyAsync(x => x.Code == code && x.Id != d.Id, ct)) throw new ConflictException($"A department with code '{code}' already exists.");
        await FillAsync(d, cmd.Input, code, ct);
        await departments.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<Unit> Handle(DeleteDepartment cmd, CancellationToken ct)
    {
        var d = await departments.GetForUpdateAsync(cmd.Id, ct);
        var id = d.Id;
        if (await employees.AnyAsync(e => e.DepartmentId == id, ct)) throw new ConflictException("Employees belong to this department. Move them first.");
        if (await departments.AnyAsync(x => x.ParentDepartmentId == id, ct)) throw new ConflictException("This department has sub-departments. Move or delete them first.");
        departments.Remove(d);
        await departments.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<PositionDto> Handle(CreatePosition cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await positions.AnyAsync(p => p.Code == code, ct)) throw new ConflictException($"A position with code '{code}' already exists.");
        var p = new Position();
        Fill(p, cmd.Input, code);
        positions.Add(p);
        await positions.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PositionDto> Handle(UpdatePosition cmd, CancellationToken ct)
    {
        var p = await positions.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != p.Code && await positions.AnyAsync(x => x.Code == code && x.Id != p.Id, ct)) throw new ConflictException($"A position with code '{code}' already exists.");
        Fill(p, cmd.Input, code);
        await positions.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<Unit> Handle(DeletePosition cmd, CancellationToken ct)
    {
        var p = await positions.GetForUpdateAsync(cmd.Id, ct);
        var id = p.Id;
        if (await employees.AnyAsync(e => e.PositionId == id, ct)) throw new ConflictException("Employees hold this position. Move them first.");
        positions.Remove(p);
        await positions.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<OrgChartDto> Handle(GetOrgChart q, CancellationToken ct)
    {
        var depts = await departments.Query().Select(d => new OrgDepartmentSource(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId)).ToListAsync(ct);
        var people = await employees.Query(new Specification<Employee>(e => e.Status != EmployeeStatus.Terminated))
            .Select(e => new OrgPersonSource(e.Id, e.Number, e.FullName, e.Designation, e.DepartmentId, e.ManagerEmployeeId)).ToListAsync(ct);
        return OrgChartBuilder.Build(depts, people);
    }

    private async Task FillAsync(Department d, DepartmentInput i, string code, CancellationToken ct)
    {
        if (i.ParentDepartmentId is { } parent)
        {
            if (parent == d.Id) throw new FieldValidationException("parentDepartmentId", "A department cannot be its own parent.");
            if (!await departments.AnyAsync(x => x.Id == parent, ct)) throw new FieldValidationException("parentDepartmentId", "Parent department not found.");
            var map = await departments.Query().Select(x => new { x.Id, x.ParentDepartmentId }).ToDictionaryAsync(x => x.Id, x => x.ParentDepartmentId, ct);
            if (Hierarchy.WouldCycle(d.Id, parent, id => map.GetValueOrDefault(id))) throw new ConflictException("That parent is below this department; the hierarchy would loop.");
        }
        if (i.HeadEmployeeId is { } head && !await employees.AnyAsync(e => e.Id == head, ct)) throw new FieldValidationException("headEmployeeId", "Head employee not found.");
        if (i.BusinessUnitId is { } unit && !await units.AnyAsync(u => u.Id == unit, ct)) throw new FieldValidationException("businessUnitId", "Business unit not found.");
        d.Code = code; d.Name = i.Name.Trim(); d.ParentDepartmentId = i.ParentDepartmentId; d.HeadEmployeeId = i.HeadEmployeeId; d.BusinessUnitId = i.BusinessUnitId; d.IsActive = i.IsActive;
    }

    private static void Fill(Position p, PositionInput i, string code)
    {
        p.Code = code; p.Title = i.Title.Trim(); p.Grade = string.IsNullOrWhiteSpace(i.Grade) ? null : i.Grade.Trim(); p.IsActive = i.IsActive;
    }

    private static DepartmentDto ToDto(Department d) => new(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId, d.BusinessUnitId, d.IsActive);
    private static PositionDto ToDto(Position p) => new(p.Id, p.Code, p.Title, p.Grade, p.IsActive);
}

public sealed record OrgDepartmentSource(Guid Id, string Code, string Name, Guid? ParentId, Guid? HeadEmployeeId);
public sealed record OrgPersonSource(Guid Id, string Number, string Name, string Designation, Guid? DepartmentId, Guid? ManagerId);
public sealed record OrgMember(Guid Id, string Number, string Name, string Designation);
public sealed record OrgDepartmentNode(Guid Id, string Code, string Name, Guid? HeadEmployeeId, IReadOnlyList<OrgMember> Members, IReadOnlyList<OrgDepartmentNode> Children);
public sealed record OrgPersonNode(Guid Id, string Number, string Name, string Designation, Guid? DepartmentId, IReadOnlyList<OrgPersonNode> Reports);
/// <summary>Departments as a tree with their members, and people as a reporting tree (manager relation).</summary>
public sealed record OrgChartDto(IReadOnlyList<OrgDepartmentNode> Departments, IReadOnlyList<OrgMember> Unassigned, IReadOnlyList<OrgPersonNode> ReportingLines);

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
