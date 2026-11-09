using FluentValidation;

namespace ERP.Application.Customization;

public sealed class CreateCustomFieldDefinitionValidator : AbstractValidator<CreateCustomFieldDefinitionCommand>
{
    public CreateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.EntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Input.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Input.Key).NotEmpty().Matches(CustomFieldValidator.KeyFormat)
            .WithMessage("Key must be lower snake case starting with 'cf_' (max 63 characters), e.g. cf_vip_tier.");
        RuleFor(x => x.Input.DataType).IsInEnum();
    }
}
