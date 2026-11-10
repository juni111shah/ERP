using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Audit;

public sealed class GetRecordHistoryValidator : AbstractValidator<GetRecordHistoryQuery>
{
    public GetRecordHistoryValidator()
    {
        RuleFor(x => x.RecordType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RecordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
    }
}
