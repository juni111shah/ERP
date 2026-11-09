using FluentValidation;

namespace ERP.Modules.IndustryPacks.Features;

public sealed class UpdateRecordValidator : AbstractValidator<UpdateRecordCommand>
{
    public UpdateRecordValidator() => RuleFor(x => x.Input).SetValidator(new RecordInputValidator());
}
