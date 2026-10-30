using ERP.Domain.Common;

namespace ERP.Domain.Parties;

/// <summary>A tenant-defined grouping of parties (for example "Key accounts", "Government", "Wholesale"). Used to filter and group reports such as receivables ageing.</summary>
[DefaultSort("code")]
public class PartyGroup : OrgEntity
{
    [Listable]
    public string Code { get; set; } = default!;
    [Listable]
    public string Name { get; set; } = default!;
    [Filterable, Sortable]
    public bool IsActive { get; set; } = true;
}
