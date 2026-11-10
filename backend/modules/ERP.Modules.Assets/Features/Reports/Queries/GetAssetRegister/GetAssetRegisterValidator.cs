using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class GetAssetRegisterValidator : AbstractValidator<GetAssetRegisterQuery>
{
    public GetAssetRegisterValidator() => RuleFor(x => x.AsOf).MustBePlausibleDate();
}
