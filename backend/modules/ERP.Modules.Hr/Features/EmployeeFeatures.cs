using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record EmployeeInput(
    Guid LegalEntityId, string? Number, string FullName, string? Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances, decimal? RecurringDeductions, string? Iban, JsonObject? CustomFields = null, string? LabourCardNumber = null, string? BankRoutingCode = null,
    Guid? UserId = null, Guid? ManagerEmployeeId = null, Guid? DepartmentId = null, Guid? PositionId = null, DateOnly? DateOfBirth = null, string? Gender = null, string? MaritalStatus = null,
    string? EmergencyContactName = null, string? EmergencyContactPhone = null, string? Address = null, ContractType? ContractType = null, DateOnly? ProbationEndDate = null, DateOnly? ContractEndDate = null,
    int? NoticePeriodDays = null, string? BankName = null);

/// <summary>Pay fields (salary, allowances, deductions, IBAN, gross, gratuity) are null for callers without <c>hr.salary.read</c>.</summary>
public sealed record EmployeeDto(
    Guid Id, string Number, Guid LegalEntityId, string FullName, string Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances, decimal? RecurringDeductions, string? Iban, decimal? GrossMonthly,
    decimal ServiceYears, decimal? GratuityAccrued, JsonObject CustomFields, string? LabourCardNumber = null, string? BankRoutingCode = null,
    Guid? UserId = null, string? UserName = null, Guid? ManagerEmployeeId = null, string? ManagerName = null, Guid? DepartmentId = null, Guid? PositionId = null, DateOnly? DateOfBirth = null,
    string? Gender = null, string? MaritalStatus = null, string? EmergencyContactName = null, string? EmergencyContactPhone = null, string? Address = null, ContractType? ContractType = null,
    DateOnly? ProbationEndDate = null, DateOnly? ContractEndDate = null, int? NoticePeriodDays = null, string? BankName = null, bool OnLeaveToday = false);

/// <summary>List row. Gross is added up here (not in SQL) so it works on every database; pay is null without <c>hr.salary.read</c>.</summary>
public sealed record EmployeeSummaryDto(
    Guid Id, string Number, string FullName, string Designation, string? Department, Guid LegalEntityId, DateOnly JoinDate, EmployeeStatus Status,
    decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances,
    Guid? DepartmentId = null, Guid? PositionId = null, Guid? ManagerEmployeeId = null, Guid? UserId = null, bool OnLeaveToday = false)
{
    public decimal? GrossMonthly => BasicSalary is null ? null : BasicSalary + (HousingAllowance ?? 0) + (TransportAllowance ?? 0) + (OtherAllowances ?? 0);
}

public sealed record GetEmployee(Guid Id) : IQuery<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record CreateEmployee(EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdateEmployee(Guid Id, EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteEmployee(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }

public sealed partial class EmployeeInputValidator : AbstractValidator<EmployeeInput>
{
    public EmployeeInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Designation).NotEmpty().MaximumLength(150).When(x => x.PositionId is null);
        RuleFor(x => x.Number).MaximumLength(30);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.LabourCardNumber).MaximumLength(30);
        RuleFor(x => x.BankRoutingCode).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Status).IsInEnum();
        // Pay is optional on the input: callers without hr.salary.read cannot see or change it (an update then keeps the stored pay).
        RuleFor(x => x.BasicSalary!.Value).GreaterThan(0).WithMessage("The basic salary must be greater than zero.").OverridePropertyName("BasicSalary").When(x => x.BasicSalary is not null);
        RuleFor(x => x.BasicSalary!.Value).MustBeMoney().OverridePropertyName("BasicSalary").When(x => x.BasicSalary is not null);
        RuleFor(x => x.HousingAllowance!.Value).GreaterThanOrEqualTo(0).MustBeMoney().OverridePropertyName("HousingAllowance").When(x => x.HousingAllowance is not null);
        RuleFor(x => x.TransportAllowance!.Value).GreaterThanOrEqualTo(0).MustBeMoney().OverridePropertyName("TransportAllowance").When(x => x.TransportAllowance is not null);
        RuleFor(x => x.OtherAllowances!.Value).GreaterThanOrEqualTo(0).MustBeMoney().OverridePropertyName("OtherAllowances").When(x => x.OtherAllowances is not null);
        RuleFor(x => x.RecurringDeductions!.Value).GreaterThanOrEqualTo(0).MustBeMoney().OverridePropertyName("RecurringDeductions").When(x => x.RecurringDeductions is not null);
        RuleFor(x => x.Gender).MaximumLength(30);
        RuleFor(x => x.MaritalStatus).MaximumLength(30);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.ContractType!.Value).IsInEnum().OverridePropertyName("ContractType").When(x => x.ContractType is not null);
        RuleFor(x => x.NoticePeriodDays!.Value).InclusiveBetween(0, 365).OverridePropertyName("NoticePeriodDays").When(x => x.NoticePeriodDays is not null);
        RuleFor(x => x).Must(x => x.ContractEndDate is null || x.ContractEndDate > x.JoinDate).WithName("contractEndDate").WithMessage("The contract end date must be after the join date.");
        RuleFor(x => x).Must(x => x.ProbationEndDate is null || x.ProbationEndDate > x.JoinDate).WithName("probationEndDate").WithMessage("The probation end date must be after the join date.");
        RuleFor(x => x.Iban).Must(IsIbanShape).When(x => !string.IsNullOrWhiteSpace(x.Iban)).WithMessage("Enter a valid IBAN: 2 letters, 2 digits, then 11 to 30 letters or digits, without spaces.");
        RuleFor(x => x).Must(x => x.TerminationDate is null || x.TerminationDate >= x.JoinDate).WithName("terminationDate").WithMessage("The termination date cannot be before the join date.");
        RuleFor(x => x).Must(x => x.Status != EmployeeStatus.Terminated || x.TerminationDate is not null).WithName("terminationDate").WithMessage("Enter the termination date for a terminated employee.");
    }
}
public sealed partial class EmployeeInputValidator
{
    private static bool IsIbanShape(string? v) => v is not null && System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), "^[A-Za-z]{2}[0-9]{2}[A-Za-z0-9]{11,30}$");
}
public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployee>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator());
        RuleFor(x => x.Input.BasicSalary).NotNull().WithMessage("The basic salary must be greater than zero.").OverridePropertyName("BasicSalary");
    }
}
public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployee> { public UpdateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator()); }

public sealed record GratuityResult(decimal ServiceYears, bool Eligible, decimal EntitlementDays, decimal Amount, string? Rule);

/// <summary>End-of-service benefit under the employing entity's country rule (a country pack concern); zero where the country has none.</summary>
public sealed class GratuityCalculator(CountryPackRegistry packs)
{
    /// <summary>The value of an entitlement day is the basic salary over the entity's days-in-a-pay-month setting (<c>hr.daysBasis</c>).</summary>
    public GratuityResult Calculate(decimal basicSalary, DateOnly joined, DateOnly? left, DateOnly asOf, string countryCode, int daysBasis = 30)
    {
        var end = left is { } l && l < asOf ? l : asOf;
        var years = Math.Max(0m, Math.Round((end.DayNumber - joined.DayNumber) / 365.25m, 4));
        var rule = packs.Find(countryCode)?.EndOfService;
        if (rule is null) return new GratuityResult(years, false, 0, 0, null);
        if (years < rule.MinimumYears) return new GratuityResult(years, false, 0, 0, rule.Description);
        var days = Math.Round(rule.EntitlementDays(years), 2);
        return new GratuityResult(years, true, days, PayrollMath.R(basicSalary / (daysBasis > 0 ? daysBasis : 30) * days), rule.Description);
    }
}

internal sealed class EmployeeHandlers(
    IRepository<Employee> employees, ILegalEntityGuard guard, INumberingService numbers, ICustomFieldService fields, EmployeeHistoryRecorder history,
    ICurrentUser user, EmployeeDtoFactory dtos, EmployeePeople people) :
    IRequestHandler<GetEmployee, EmployeeDto>,
    IRequestHandler<CreateEmployee, EmployeeDto>,
    IRequestHandler<UpdateEmployee, EmployeeDto>,
    IRequestHandler<DeleteEmployee, Unit>
{
    public async Task<EmployeeDto> Handle(GetEmployee q, CancellationToken ct) => await ToDtoAsync(await employees.GetAsync(q.Id, ct), ct);

    public async Task<EmployeeDto> Handle(CreateEmployee cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var e = new Employee { LegalEntityId = entity.Id };
        Map(i, e, canWritePay: true);
        await people.ApplyAsync(i, e, ct);
        e.Number = string.IsNullOrWhiteSpace(i.Number) ? await numbers.NextAsync(Docs.Employee, entity.Id, null, ct) : i.Number.Trim();
        if (await employees.AnyAsync(x => x.Number == e.Number, ct)) throw new ConflictException($"Employee number '{e.Number}' is already used.");
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: true, ct);
        employees.Add(e);
        history.Joined(e);
        await employees.SaveChangesAsync(ct);
        return await ToDtoAsync(e, ct);
    }

    public async Task<EmployeeDto> Handle(UpdateEmployee cmd, CancellationToken ct)
    {
        var e = await employees.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != e.LegalEntityId) throw new FieldValidationException("legalEntityId", "An employee cannot move to another employing entity here.");
        var number = string.IsNullOrWhiteSpace(i.Number) ? e.Number : i.Number.Trim();
        if (number != e.Number && await employees.AnyAsync(x => x.Number == number && x.Id != e.Id, ct)) throw new ConflictException($"Employee number '{number}' is already used.");
        var before = EmployeeSnapshot.Of(e);
        Map(i, e, canWritePay: user.HasPermission(P.SalaryRead)); e.Number = number;
        await people.ApplyAsync(i, e, ct);
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: false, ct);
        history.Changed(e, before);
        await employees.SaveChangesAsync(ct);
        return await ToDtoAsync(e, ct);
    }

    public async Task<Unit> Handle(DeleteEmployee cmd, CancellationToken ct)
    {
        var e = await employees.GetForUpdateAsync(cmd.Id, ct);
        if (await people.HasHistoryAsync(e.Id, ct)) throw new ConflictException("This employee has payroll, leave, attendance or review history. Terminate the employee instead.");
        employees.Remove(e);
        await employees.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>Copies the input onto the employee. Pay fields are only written by callers who may see pay (hr.salary.read); on update a missing pay value keeps the stored one.</summary>
    private static void Map(EmployeeInput i, Employee e, bool canWritePay)
    {
        e.FullName = i.FullName.Trim(); e.Designation = (i.Designation ?? e.Designation ?? "").Trim(); e.Department = i.Department?.Trim(); e.JoinDate = i.JoinDate; e.TerminationDate = i.TerminationDate;
        e.Email = i.Email?.Trim(); e.Phone = i.Phone?.Trim(); e.Nationality = i.Nationality?.Trim(); e.Status = i.Status;
        if (canWritePay)
        {
            e.BasicSalary = i.BasicSalary ?? e.BasicSalary; e.HousingAllowance = i.HousingAllowance ?? e.HousingAllowance; e.TransportAllowance = i.TransportAllowance ?? e.TransportAllowance;
            e.OtherAllowances = i.OtherAllowances ?? e.OtherAllowances; e.RecurringDeductions = i.RecurringDeductions ?? e.RecurringDeductions;
            e.Iban = string.IsNullOrWhiteSpace(i.Iban) ? null : i.Iban.Trim().ToUpperInvariant();
        }
        e.LabourCardNumber = string.IsNullOrWhiteSpace(i.LabourCardNumber) ? null : i.LabourCardNumber.Trim(); e.BankRoutingCode = string.IsNullOrWhiteSpace(i.BankRoutingCode) ? null : i.BankRoutingCode.Trim();
    }

    private Task<EmployeeDto> ToDtoAsync(Employee e, CancellationToken ct) => dtos.BuildAsync(e, user.HasPermission(P.SalaryRead), ct);
}
