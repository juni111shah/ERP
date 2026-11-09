using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

public sealed class AllocatePaymentValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentValidator()
    {
        RuleFor(x => x.Allocations).NotNull().Must(a => a is { Count: > 0 }).WithMessage("Choose at least one bill to allocate to.");
        RuleFor(x => x.Allocations).Must(a => a is null || a.Select(x => x.BillId).Distinct().Count() == a.Count).WithMessage("A bill can appear only once.");
        RuleForEach(x => x.Allocations).ChildRules(l => l.RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney());
    }
}
