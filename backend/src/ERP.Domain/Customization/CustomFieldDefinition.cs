using ERP.Domain.Common;

namespace ERP.Domain.Customization;

public enum CustomFieldDataType { Text, Number, Decimal, Date, DateTime, Bool, Select, MultiSelect, Lookup }

/// <summary>
/// Field-metadata registry (the equivalent of Odoo's ir.model.fields). One row per user-defined field
/// on an extensible entity type. The UI renders forms and list columns from these rows.
/// </summary>
public class CustomFieldDefinition : OrgEntity
{
    /// <summary>Entity type key from the customizable-entity registry, e.g. "party", "item", "realestate.property".</summary>
    public string EntityType { get; set; } = default!;
    /// <summary>JSON key, lower snake case with a "cf_" prefix. Immutable after creation.</summary>
    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public CustomFieldDataType DataType { get; set; }
    public bool Required { get; set; }
    /// <summary>
    /// JSON with type options: select/multiselect {"choices":[{"value":"gold","label":"Gold"}]},
    /// text {"maxLength":200,"pattern":"^..$"}, number/decimal {"min":0,"max":100,"scale":2},
    /// lookup {"target":"party"}, multiselect {"maxSelections":3}.
    /// </summary>
    public string? Options { get; set; }
    /// <summary>Default value (JSON) applied on create when the caller sends nothing.</summary>
    public string? DefaultValue { get; set; }
    /// <summary>Restricts the field to one legal entity (null = all entities of the workspace).</summary>
    public Guid? LegalEntityId { get; set; }
    public int Order { get; set; }
    public string? Section { get; set; }
    public string? HelpText { get; set; }
    public bool IsSearchable { get; set; }
    public bool ShowInList { get; set; }
    public bool Sortable { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Module that ships this field as part of a pack (null = user-defined).</summary>
    public string? ModuleKey { get; set; }
}
