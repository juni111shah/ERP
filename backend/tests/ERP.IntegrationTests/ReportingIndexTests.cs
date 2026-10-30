using ERP.Domain.Finance;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Composite indexes the reports and ageing queries rely on exist in the model (TASK-123).</summary>
public class ReportingIndexTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly (Type Entity, string[] Columns)[] Expected =
    [
        (typeof(JournalEntry), ["OrgId", "LegalEntityId", "Status", "Date"]),
        (typeof(Invoice), ["OrgId", "LegalEntityId", "Status", "InvoiceDate"]),
        (typeof(Bill), ["OrgId", "LegalEntityId", "Status", "BillDate"]),
        (typeof(Settlement), ["OrgId", "LegalEntityId", "Direction", "Date"]),
    ];

    [Fact]
    public void Document_tables_have_entity_status_date_indexes()
    {
        using var scope = f.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;
        foreach (var (entity, columns) in Expected)
            Assert.Contains(model.FindEntityType(entity)!.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(columns));
    }
}
