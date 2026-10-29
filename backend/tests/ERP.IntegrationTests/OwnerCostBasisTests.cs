using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Owner statement cost basis: who bears each maintenance job and which vendor bill paid for it.</summary>
public class OwnerCostBasisTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Owner, string Property, string Vendor, string Expense);

    private async Task<Ctx> SetupAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var owner = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Owner " + tag, kind = "Individual", roles = "Vendor" }), HttpStatusCode.Created);
        var vendor = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "Managed " + tag, propertyType = "residential", ownershipType = "managedForOwner", ownerPartyId = (string)owner["id"]!, managementFeePercent = 0, totalUnits = 0, indicativeValue = 1m }), HttpStatusCode.Created);
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        return new Ctx(c, entity, (string)owner["id"]!, (string)prop["id"]!, (string)vendor["id"]!, (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!);
    }

    private static async Task<string> CompletedOrderAsync(Ctx x, string issue, decimal actual, decimal estimate = 0)
    {
        var wo = await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders", new { propertyId = x.Property, issue, priority = "normal", technician = "Crew", estimatedCost = estimate }), HttpStatusCode.Created);
        var id = (string)wo["id"]!;
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "assigned" }));
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "inProgress" }));
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "completed", actualCost = actual }));
        return id;
    }

    private static async Task<string> BillAsync(Ctx x, decimal net, bool post = true)
    {
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Fin}/bills", new
        {
            legalEntityId = x.Entity, partyId = x.Vendor, vendorReference = "CB-" + Guid.NewGuid().ToString("N")[..6], billDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            lines = new[] { new { description = "Repairs", quantity = 1m, price = net, accountId = x.Expense } },
        }), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        if (post) await Ok(await x.C.PostAsync($"{Fin}/bills/{id}/post", null));
        return id;
    }

    private static async Task<JsonNode> StatementAsync(Ctx x)
    {
        var from = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
        return await Ok(await x.C.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={x.Owner}&from={from}&to={to}"));
    }

    private static async Task SetBasisAsync(Ctx x, string orderId, object body, HttpStatusCode expected = HttpStatusCode.OK)
        => await Ok(await x.C.PutAsJsonAsync($"{Re}/work-orders/{orderId}/cost-basis", body), expected);

    [Fact]
    public async Task Without_a_cost_basis_the_owner_bears_the_actual_cost_as_before()
    {
        var x = await SetupAsync();
        await CompletedOrderAsync(x, "Repaint", 400m);
        var st = await StatementAsync(x);
        Assert.Equal(400m, (decimal)st["maintenanceCosts"]!);
        var line = Assert.Single(st["lines"]!.AsArray())!;
        Assert.Equal("actual", (string)line["costSource"]!);
        Assert.Equal("Owner", (string)line["borneBy"]!);
    }

    [Fact]
    public async Task A_job_charged_to_the_tenant_or_company_is_not_deducted_from_the_owner()
    {
        var x = await SetupAsync();
        var tenantJob = await CompletedOrderAsync(x, "Broken window (tenant)", 300m);
        var companyJob = await CompletedOrderAsync(x, "Lobby paint (company)", 200m);
        var ownerJob = await CompletedOrderAsync(x, "Roof", 100m);
        await SetBasisAsync(x, tenantJob, new { chargeableTo = "tenant" });
        await SetBasisAsync(x, companyJob, new { chargeableTo = "company" });
        var st = await StatementAsync(x);
        Assert.Equal(100m, (decimal)st["maintenanceCosts"]!);
        Assert.Equal(300m, (decimal)st["chargedToTenant"]!);
        var lines = st["lines"]!.AsArray();
        Assert.Equal(3, lines.Count);
        Assert.Equal("Tenant", (string)lines.Single(l => (string)l!["workOrderId"]! == tenantJob)!["borneBy"]!);
        Assert.Equal(0m, (decimal)lines.Single(l => (string)l!["workOrderId"]! == companyJob)!["ownerAmount"]!);
        Assert.Equal(100m, (decimal)lines.Single(l => (string)l!["workOrderId"]! == ownerJob)!["ownerAmount"]!);
    }

    [Fact]
    public async Task A_shared_job_deducts_only_the_owners_percentage()
    {
        var x = await SetupAsync();
        var job = await CompletedOrderAsync(x, "Pump", 1000m);
        await SetBasisAsync(x, job, new { chargeableTo = "shared", ownerSharePercent = 60 });
        var st = await StatementAsync(x);
        Assert.Equal(600m, (decimal)st["maintenanceCosts"]!);
        Assert.StartsWith("Shared", (string)st["lines"]![0]!["borneBy"]!);
        await SetBasisAsync(x, job, new { chargeableTo = "shared" }, HttpStatusCode.BadRequest);
        await SetBasisAsync(x, job, new { chargeableTo = "shared", ownerSharePercent = 150 }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_linked_bill_replaces_the_actual_cost_and_is_split_across_its_work_orders()
    {
        var x = await SetupAsync();
        var single = await CompletedOrderAsync(x, "Plumbing", 100m);
        var bill = await BillAsync(x, 750m);
        await SetBasisAsync(x, single, new { billId = bill });
        var st = await StatementAsync(x);
        Assert.Equal(750m, (decimal)st["maintenanceCosts"]!);
        Assert.Equal("bill", (string)st["lines"]![0]!["costSource"]!);

        var x2 = await SetupAsync();
        var a = await CompletedOrderAsync(x2, "A", 100m);
        var b = await CompletedOrderAsync(x2, "B", 300m);
        var shared = await BillAsync(x2, 800m);
        await SetBasisAsync(x2, a, new { billId = shared });
        await SetBasisAsync(x2, b, new { billId = shared });
        var st2 = await StatementAsync(x2);
        var lines = st2["lines"]!.AsArray();
        Assert.Equal(200m, (decimal)lines.Single(l => (string)l!["workOrderId"]! == a)!["cost"]!);
        Assert.Equal(600m, (decimal)lines.Single(l => (string)l!["workOrderId"]! == b)!["cost"]!);
        Assert.Equal(800m, (decimal)st2["maintenanceCosts"]!);

        await SetBasisAsync(x2, a, new { billId = (string?)null });
        var wo = await Ok(await x2.C.GetAsync($"{Re}/work-orders/{a}"));
        Assert.Null((string?)wo["billId"]);
    }

    [Fact]
    public async Task Only_posted_bills_can_be_linked_and_defaults_are_unchanged()
    {
        var x = await SetupAsync();
        var job = await CompletedOrderAsync(x, "Estimate only", 0m, estimate: 250m);
        await SetBasisAsync(x, job, new { billId = await BillAsync(x, 10m, post: false) }, HttpStatusCode.BadRequest);
        await SetBasisAsync(x, job, new { billId = Guid.NewGuid() }, HttpStatusCode.BadRequest);
        var wo = await Ok(await x.C.GetAsync($"{Re}/work-orders/{job}"));
        Assert.Null((string?)wo["chargeableTo"]);
        Assert.Equal(100m, (decimal)wo["ownerSharePercent"]!);
    }
}
