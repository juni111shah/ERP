using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record ShiftInput(string Code, string Name, TimeOnly StartTime, int GraceMinutes = 10, int HoursPerDay = 8, bool IsActive = true);
public sealed record ShiftDto(Guid Id, string Code, string Name, TimeOnly StartTime, int GraceMinutes, int HoursPerDay, bool IsActive, int Employees);
public sealed record AssignShiftInput(IReadOnlyList<Guid> EmployeeIds);

public sealed record CreateShift(ShiftInput Input) : ICommand<ShiftDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record UpdateShift(Guid Id, ShiftInput Input) : ICommand<ShiftDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record DeleteShift(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record ListShifts() : IQuery<IReadOnlyList<ShiftDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
/// <summary>Puts the employees on the shift (they leave any other one). An empty list clears nobody; use <see cref="UnassignShift"/>.</summary>
public sealed record AssignShift(Guid Id, IReadOnlyList<Guid> EmployeeIds) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record UnassignShift(IReadOnlyList<Guid> EmployeeIds) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }

public sealed class ShiftInputValidator : AbstractValidator<ShiftInput>
{
    public ShiftInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GraceMinutes).InclusiveBetween(0, 240);
        RuleFor(x => x.HoursPerDay).InclusiveBetween(1, 24);
    }
}
public sealed class CreateShiftValidator : AbstractValidator<CreateShift> { public CreateShiftValidator() => RuleFor(x => x.Input).SetValidator(new ShiftInputValidator()); }
public sealed class UpdateShiftValidator : AbstractValidator<UpdateShift> { public UpdateShiftValidator() => RuleFor(x => x.Input).SetValidator(new ShiftInputValidator()); }
public sealed class AssignShiftValidator : AbstractValidator<AssignShift> { public AssignShiftValidator() => RuleFor(x => x.EmployeeIds).NotEmpty().Must(i => i.Count <= 500); }
public sealed class UnassignShiftValidator : AbstractValidator<UnassignShift> { public UnassignShiftValidator() => RuleFor(x => x.EmployeeIds).NotEmpty().Must(i => i.Count <= 500); }

internal sealed class ShiftHandlers(IRepository<Shift> shifts, IRepository<Employee> employees, ILegalEntityGuard guard) :
    IRequestHandler<CreateShift, ShiftDto>,
    IRequestHandler<UpdateShift, ShiftDto>,
    IRequestHandler<DeleteShift, Unit>,
    IRequestHandler<ListShifts, IReadOnlyList<ShiftDto>>,
    IRequestHandler<AssignShift, int>,
    IRequestHandler<UnassignShift, int>
{
    public async Task<ShiftDto> Handle(CreateShift cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await shifts.AnyAsync(s => s.Code == code, ct)) throw new ConflictException($"A shift with code '{code}' already exists.");
        var s = new Shift();
        Fill(s, cmd.Input, code);
        shifts.Add(s);
        await shifts.SaveChangesAsync(ct);
        return ToDto(s, 0);
    }

    public async Task<ShiftDto> Handle(UpdateShift cmd, CancellationToken ct)
    {
        var s = await shifts.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != s.Code && await shifts.AnyAsync(x => x.Code == code && x.Id != s.Id, ct)) throw new ConflictException($"A shift with code '{code}' already exists.");
        Fill(s, cmd.Input, code);
        await shifts.SaveChangesAsync(ct);
        var id = s.Id;
        return ToDto(s, await employees.Query(new Specification<Employee>(e => e.ShiftId == id)).CountAsync(ct));
    }

    public async Task<Unit> Handle(DeleteShift cmd, CancellationToken ct)
    {
        var s = await shifts.GetForUpdateAsync(cmd.Id, ct);
        var id = s.Id;
        if (await employees.AnyAsync(e => e.ShiftId == id, ct)) throw new ConflictException("Employees are on this shift. Move them first.");
        shifts.Remove(s);
        await shifts.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<IReadOnlyList<ShiftDto>> Handle(ListShifts q, CancellationToken ct)
    {
        var all = await shifts.Query().ToListAsync(ct);
        var counts = (await employees.Query(new Specification<Employee>(e => e.ShiftId != null)).GroupBy(e => e.ShiftId!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Id, x => x.N);
        return all.OrderBy(s => s.Code).Select(s => ToDto(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<int> Handle(AssignShift cmd, CancellationToken ct)
    {
        var s = await shifts.GetAsync(cmd.Id, ct);
        if (!s.IsActive) throw new ConflictException("This shift is not active.");
        return await SetAsync(cmd.EmployeeIds, s.Id, ct);
    }

    public Task<int> Handle(UnassignShift cmd, CancellationToken ct) => SetAsync(cmd.EmployeeIds, null, ct);

    private async Task<int> SetAsync(IReadOnlyList<Guid> ids, Guid? shiftId, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var staff = await employees.Tracked(new Specification<Employee>(e => list.Contains(e.Id))).ToListAsync(ct);
        if (staff.Count != list.Count) throw new NotFoundException("One of the employees was not found.");
        foreach (var e in staff) { await guard.RequireAsync(e.LegalEntityId, ct); e.ShiftId = shiftId; }
        await employees.SaveChangesAsync(ct);
        return staff.Count;
    }

    private static void Fill(Shift s, ShiftInput i, string code)
    {
        s.Code = code; s.Name = i.Name.Trim(); s.StartTime = i.StartTime; s.GraceMinutes = i.GraceMinutes; s.HoursPerDay = i.HoursPerDay; s.IsActive = i.IsActive;
    }

    private static ShiftDto ToDto(Shift s, int employees) => new(s.Id, s.Code, s.Name, s.StartTime, s.GraceMinutes, s.HoursPerDay, s.IsActive, employees);
}
