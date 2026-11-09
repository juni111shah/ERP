using ERP.Application.Common;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

/// <summary>Rules shared by the create and update definition handlers.</summary>
internal static class CustomFieldDefinitionRules
{
    /// <summary>Throws a field validation error for every problem of the definition (options, default value...).</summary>
    public static void ThrowIfInvalid(CustomFieldDefinition def)
    {
        var errors = CustomFieldValidator.ValidateDefinition(def);
        if (errors.Count > 0) throw new FieldValidationException(errors.ToDictionary(k => k.Key, v => v.Value));
    }
}
