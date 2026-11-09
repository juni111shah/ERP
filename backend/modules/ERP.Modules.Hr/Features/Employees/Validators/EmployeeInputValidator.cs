using ERP.Application.Finance;
using ERP.Modules.Hr.Domain;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

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
