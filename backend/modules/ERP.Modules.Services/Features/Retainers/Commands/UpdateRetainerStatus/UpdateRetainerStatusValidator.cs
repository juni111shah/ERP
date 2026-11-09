using FluentValidation;

namespace ERP.Modules.Services.Features;

public class UpdateRetainerStatusValidator : AbstractValidator<UpdateRetainerStatusCommand>
{
    public UpdateRetainerStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
