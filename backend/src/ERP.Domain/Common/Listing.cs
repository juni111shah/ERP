namespace ERP.Domain.Common;

// Per-property whitelists for the generic list pipeline (ApplySearch / ApplyFilters / ApplySort).
// Only annotated properties can be searched, filtered or sorted from the API, so a new table gets
// list + search + filter + sort by annotating its properties and mapping one endpoint.

/// <summary>Included in the free-text <c>search</c> (string properties, OR-ed, "contains").</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SearchableAttribute : Attribute;

/// <summary>Allowed in <c>sort=name,-createdAt</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SortableAttribute : Attribute;

/// <summary>Allowed in <c>filter=field:op:value</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterableAttribute : Attribute;

/// <summary>Searchable + sortable + filterable (typical for code / name columns).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ListableAttribute : Attribute;

/// <summary>Default sort when the caller sends none, e.g. <c>[DefaultSort("code")]</c> or <c>"-occurredAt"</c>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DefaultSortAttribute(string sort) : Attribute
{
    public string Sort { get; } = sort;
}
