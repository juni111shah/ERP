using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class CreateSalesReturnValidator : AbstractValidator<CreateSalesReturnCommand>
{
    public CreateSalesReturnValidator()
    {
        RuleFor(x => x.Input).Must(i => (i.SalesOrderId is null) != (i.InvoiceId is null)).WithName("salesOrderId").WithMessage("Raise the return against either a sales order or an invoice.");
        RuleFor(x => x.Input.ReturnDate).MustBePlausibleDate();
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Input.Notes).MaximumLength(1000);
        RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("A return needs at least one line.");
        RuleFor(x => x.Input.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.InvoiceLineId)).Distinct().Count() == l.Count).WithMessage("A line can appear only once.");
        RuleForEach(x => x.Input.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}
