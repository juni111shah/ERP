using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.UnitTests;

public class PagingTests
{
    private static IQueryable<Party> Data() => new[]
    {
        new Party { Code = "P3", Name = "Gamma", Roles = PartyRoles.Customer | PartyRoles.Vendor, Email = "g@x.io" },
        new Party { Code = "P1", Name = "Alpha", Roles = PartyRoles.Customer },
        new Party { Code = "P2", Name = "Beta", Roles = PartyRoles.Vendor, IsActive = false },
    }.AsQueryable();

    [Fact]
    public void Default_sort_comes_from_the_entity_attribute() =>
        Assert.Equal(["P1", "P2", "P3"], Data().ApplySort(null).Select(p => p.Code));

    [Fact]
    public void Sort_search_filter_and_page_compose()
    {
        Assert.Equal(["Gamma", "Beta", "Alpha"], Data().ApplySort("-name").Select(p => p.Name));
        Assert.Single(Data().ApplySearch("g@x"));
        Assert.Equal(2, Data().ApplyFilters(["roles:has:vendor"]).Count());
        Assert.Single(Data().ApplyFilters(["isActive:eq:false"]));
        Assert.Equal(2, Data().ApplyFilters(["code:in:P1|P3"]).Count());
        Assert.Equal(["P2"], Data().ApplySort("code").ApplyPaging(2, 1).Select(p => p.Code));
    }

    [Fact]
    public void Non_whitelisted_fields_are_rejected()
    {
        Assert.Throws<FieldValidationException>(() => Data().ApplySort("iban").ToList());
        Assert.Throws<FieldValidationException>(() => Data().ApplyFilters(["idNumber:eq:1"]).ToList());
        Assert.Throws<FieldValidationException>(() => Data().ApplyFilters(["name:gt:a"]).ToList());
    }

    [Fact]
    public void Paged_query_clamps_page_and_size()
    {
        var q = new PagedQuery { Page = -3, PageSize = 10_000 };
        Assert.Equal(1, q.PageNumber);
        Assert.Equal(PagedQuery.MaxPageSize, q.Size);
    }
}
