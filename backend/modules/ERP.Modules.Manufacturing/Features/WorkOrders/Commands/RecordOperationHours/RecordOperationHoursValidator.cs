using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class RecordOperationHoursValidator : AbstractValidator<RecordOperationHoursCommand>
{
    public RecordOperationHoursValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}
