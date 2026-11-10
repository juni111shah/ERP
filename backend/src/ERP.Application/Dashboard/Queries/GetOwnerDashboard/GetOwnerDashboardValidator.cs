using FluentValidation;

namespace ERP.Application.Dashboard;

public sealed class GetOwnerDashboardValidator : AbstractValidator<GetOwnerDashboardQuery>
{
    public GetOwnerDashboardValidator()
        => RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || f <= t).WithName("To").WithMessage("The end date must be on or after the start date.");
}
