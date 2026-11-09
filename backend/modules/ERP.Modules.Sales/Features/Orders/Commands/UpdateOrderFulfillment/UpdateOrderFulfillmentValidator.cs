using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class UpdateOrderFulfillmentValidator : AbstractValidator<UpdateOrderFulfillmentCommand>
{
    public UpdateOrderFulfillmentValidator()
    {
        RuleFor(x => x.DeliveryDate).MustBePlausibleDate();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.DeliveredQuantity).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4));
    }
}
