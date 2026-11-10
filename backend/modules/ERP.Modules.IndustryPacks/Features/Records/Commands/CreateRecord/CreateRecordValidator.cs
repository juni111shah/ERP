using FluentValidation;

namespace ERP.Modules.IndustryPacks.Features;

public sealed class CreateRecordValidator : AbstractValidator<CreateRecordCommand>
{
    public CreateRecordValidator() => RuleFor(x => x.Input).SetValidator(new RecordInputValidator());
}
