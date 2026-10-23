using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
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
    Guid LegalEntityId, string? Number, string FullName, string Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal BasicSalary, decimal HousingAllowance, decimal TransportAllowance, decimal OtherAllowances, decimal RecurringDeductions, string? Iban, JsonObject? CustomFields = null, string? LabourCardNumber = null, string? BankRoutingCode = null);

public sealed record EmployeeDto(
    Guid Id, string Number, Guid LegalEntityId, string FullName, string Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal BasicSalary, decimal HousingAllowance, decimal TransportAllowance, decimal OtherAllowances, decimal RecurringDeductions, string? Iban, decimal GrossMonthly,
    decimal ServiceYears, decimal GratuityAccrued, JsonObject CustomFields, string? LabourCardNumber = null, string? BankRoutingCode = null);

/// <summary>List row. Gross is added up here (not in SQL) so it works on every database.</summary>
public sealed record EmployeeSummaryDto(
    Guid Id, string Number, string FullName, string Designation, string? Department, Guid LegalEntityId, DateOnly JoinDate, EmployeeStatus Status,
    decimal BasicSalary, decimal HousingAllowance, decimal TransportAllowance, decimal OtherAllowances)
{
    public decimal GrossMonthly => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowances;
}

public sealed record GetEmployee(Guid Id) : IQuery<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record CreateEmployee(EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdateEmployee(Guid Id, EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteEmployee(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }

public sealed class EmployeeInputValidator : AbstractValidator<EmployeeInput>
{
    public EmployeeInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Designation).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Number).MaximumLength(30);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.LabourCardNumber).MaximumLength(30);
        RuleFor(x => x.BankRoutingCode).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.BasicSalary).GreaterThan(0).WithMessage("The basic salary must be greater than zero.");
        RuleFor(x => x.HousingAllowance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TransportAllowance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherAllowances).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RecurringDeductions).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.TerminationDate is null || x.TerminationDate >= x.JoinDate).WithName("terminationDate").WithMessage("The termination date cannot be before the join date.");
        RuleFor(x => x).Must(x => x.Status != EmployeeStatus.Terminated || x.TerminationDate is not null).WithName("terminationDate").WithMessage("Enter the termination date for a terminated employee.");
    }
}
public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployee> { public CreateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator()); }
public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployee> { public UpdateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator()); }

public sealed record GratuityResult(decimal ServiceYears, bool Eligible, decimal EntitlementDays, decimal Amount, string? Rule);

/// <summary>End-of-service benefit under the employing entity's country rule (a country pack concern); zero where the country has none.</summary>
public sealed class GratuityCalculator(CountryPackRegistry packs)
{
    public GratuityResult Calculate(decimal basicSalary, DateOnly joined, DateOnly? left, DateOnly asOf, string countryCode)
    {
        var end = left is { } l && l < asOf ? l : asOf;
        var years = Math.Max(0m, Math.Round((end.DayNumber - joined.DayNumber) / 365.25m, 4));
        var rule = packs.Find(countryCode)?.EndOfService;
        if (rule is null) return new GratuityResult(years, false, 0, 0, null);
        if (years < rule.MinimumYears) return new GratuityResult(years, false, 0, 0, rule.Description);
        var days = Math.Round(rule.EntitlementDays(years), 2);
        return new GratuityResult(years, true, days, PayrollMath.R(basicSalary / 30m * days), rule.Description);
    }
}

internal sealed class EmployeeHandlers(
    IRepository<Employee> employees, IReadRepository<LegalEntity> legalEntities, ILegalEntityGuard guard, INumberingService numbers, ICustomFieldService fields, GratuityCalculator gratuity,
    ICurrentUser user, IClock clock) :
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
        Map(i, e);
        e.Number = string.IsNullOrWhiteSpace(i.Number) ? await numbers.NextAsync(Docs.Employee, entity.Id, null, ct) : i.Number.Trim();
        if (await employees.AnyAsync(x => x.Number == e.Number, ct)) throw new ConflictException($"Employee number '{e.Number}' is already used.");
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: true, ct);
        employees.Add(e);
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
        Map(i, e); e.Number = number;
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: false, ct);
        await employees.SaveChangesAsync(ct);
        return await ToDtoAsync(e, ct);
    }

    public async Task<Unit> Handle(DeleteEmployee cmd, CancellationToken ct)
    {
        employees.Remove(await employees.GetForUpdateAsync(cmd.Id, ct));
        await employees.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static void Map(EmployeeInput i, Employee e)
    {
        e.FullName = i.FullName.Trim(); e.Designation = i.Designation.Trim(); e.Department = i.Department?.Trim(); e.JoinDate = i.JoinDate; e.TerminationDate = i.TerminationDate;
        e.Email = i.Email?.Trim(); e.Phone = i.Phone?.Trim(); e.Nationality = i.Nationality?.Trim(); e.Status = i.Status; e.BasicSalary = i.BasicSalary;
        e.HousingAllowance = i.HousingAllowance; e.TransportAllowance = i.TransportAllowance; e.OtherAllowances = i.OtherAllowances; e.RecurringDeductions = i.RecurringDeductions; e.Iban = i.Iban?.Trim();
        e.LabourCardNumber = string.IsNullOrWhiteSpace(i.LabourCardNumber) ? null : i.LabourCardNumber.Trim(); e.BankRoutingCode = string.IsNullOrWhiteSpace(i.BankRoutingCode) ? null : i.BankRoutingCode.Trim();
    }

    private async Task<EmployeeDto> ToDtoAsync(Employee e, CancellationToken ct)
    {
        var country = await legalEntities.Query(new Specification<LegalEntity>(l => l.Id == e.LegalEntityId)).Select(l => l.CountryCode).FirstOrDefaultAsync(ct) ?? "";
        var g = gratuity.Calculate(e.BasicSalary, e.JoinDate, e.TerminationDate, DateOnly.FromDateTime(clock.UtcNow), country);
        var iban = e.Iban is { Length: > 4 } v && !user.HasPermission(CorePermissions.SensitiveRead) ? new string('*', v.Length - 4) + v[^4..] : e.Iban;
        return new EmployeeDto(e.Id, e.Number, e.LegalEntityId, e.FullName, e.Designation, e.Department, e.JoinDate, e.TerminationDate, e.Email, e.Phone, e.Nationality, e.Status, e.BasicSalary, e.HousingAllowance,
            e.TransportAllowance, e.OtherAllowances, e.RecurringDeductions, iban, e.GrossMonthly, g.ServiceYears, g.Amount, await fields.ReadAsync(Docs.Employee, e, ct), e.LabourCardNumber, e.BankRoutingCode);
    }
}
