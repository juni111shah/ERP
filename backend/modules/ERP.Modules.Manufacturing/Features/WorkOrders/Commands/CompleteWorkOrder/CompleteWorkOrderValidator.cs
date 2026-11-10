using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class CompleteWorkOrderValidator : AbstractValidator<CompleteWorkOrderCommand>
{
    public CompleteWorkOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CompletedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ScrappedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualTotalCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.CompletedQuantity + x.ScrappedQuantity > 0).WithName("CompletedQuantity").WithMessage("Enter what was made or scrapped. To stop an order that made nothing, cancel it.");
        RuleForEach(x => x.OperationHours).ChildRules(o =>
        {
            o.RuleFor(x => x.OperationId).NotEmpty();
            o.RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        });
    }
}
