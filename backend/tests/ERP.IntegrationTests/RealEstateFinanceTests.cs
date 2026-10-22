using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Real estate money flows: commission invoicing and agent payout, security deposits, termination credits, auto-allocation and the nightly expiry job.</summary>
public class RealEstateFinanceTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Bank, JsonArray Accounts);

    private async Task<Ctx> OwnerAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        return new Ctx(c, (string)me["legalEntities"]![0]!["id"]!, (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!, accounts);
    }

    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<(string LeaseId, string TenantId, string UnitId)> LeaseAsync(Ctx x, string start, string end, decimal rent = 120_000m, decimal deposit = 10_000m, bool activate = true)
    {
        var type = await Ok(await x.C.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await x.C.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = x.Entity, name = U("Tower "), propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1_000_000m }), HttpStatusCode.Created);
        var unit = await Ok(await x.C.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "1", unitTypeId = (string)type["id"]!, areaSqft = 900, askingRent = rent }), HttpStatusCode.Created);
        var tenant = await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Tenant "), kind = "Individual", roles = "Customer" }), HttpStatusCode.Created);
        var lease = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases", new { unitId = (string)unit["id"]!, lesseePartyId = (string)tenant["id"]!, startDate = start, endDate = end, annualRent = rent, paymentFrequency = 4, securityDepositAmount = deposit, activateNow = activate }), HttpStatusCode.Created);
        return ((string)lease["id"]!, (string)tenant["id"]!, (string)unit["id"]!);
    }

    [Fact]
    public async Task A_deal_is_invoiced_collected_and_the_agent_paid_in_order()
    {
        var x = await OwnerAsync();
        var client = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Buyer "), kind = "Individual", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var agent = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Agent "), kind = "Individual", roles = "Employee" }), HttpStatusCode.Created))["id"]!;
        var deal = await Ok(await x.C.PostAsJsonAsync($"{Re}/deals", new
        {
            legalEntityId = x.Entity, dealType = "sale", clientPartyId = client, clientName = "Buyer", agentPartyId = agent, dealDate = "2026-05-01", dealValue = 4_800_000m,
            commissionRatePercent = 2m, agentSharePercent = 35m, teamLeadSharePercent = 10m,
        }), HttpStatusCode.Created);
        var id = (string)deal["id"]!;
        Assert.Equal(96_000m, (decimal)deal["grossCommission"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Re}/deals/{id}/invoice-commission", null)).StatusCode); // still a lead
        foreach (var stage in new[] { "viewing", "negotiation", "agreed" }) await Ok(await x.C.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage }));

        var invoiced = await Ok(await x.C.PostAsync($"{Re}/deals/{id}/invoice-commission", null));
        Assert.Equal("invoiced", (string)invoiced["stage"]!);
        var invoiceId = (string)invoiced["commissionInvoiceId"]!;
        var invoice = await Ok(await x.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal(96_000m, (decimal)invoice["total"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/deals/{id}/pay-agent", new { bankAccountId = x.Bank, date = "2026-06-01" })).StatusCode); // client has not paid

        // the client pays: stage follows the invoice
        await Ok(await x.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = x.Entity, partyId = client, date = "2026-05-20", method = "bankTransfer", bankAccountId = x.Bank, amount = 96_000m, autoAllocate = true }), HttpStatusCode.Created);
        Assert.Equal("collected", (string)(await Ok(await x.C.GetAsync($"{Re}/deals/{id}")))["stage"]!);

        var paid = await Ok(await x.C.PostAsJsonAsync($"{Re}/deals/{id}/pay-agent", new { bankAccountId = x.Bank, date = "2026-06-01" }));
        Assert.Equal("agentPaid", (string)paid["stage"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/deals/{id}/pay-agent", new { bankAccountId = x.Bank, date = "2026-06-02" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage = "lost", lostReason = "x" })).StatusCode);
    }

    [Fact]
    public async Task Voiding_a_commission_invoice_returns_the_deal_to_agreed()
    {
        var x = await OwnerAsync();
        var client = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Buyer "), kind = "Individual", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var deal = await Ok(await x.C.PostAsJsonAsync($"{Re}/deals", new { legalEntityId = x.Entity, dealType = "lease", clientPartyId = client, clientName = "B", dealDate = "2026-05-01", dealValue = 100_000m, commissionRatePercent = 5m }), HttpStatusCode.Created);
        var id = (string)deal["id"]!;
        foreach (var stage in new[] { "viewing", "negotiation", "agreed" }) await Ok(await x.C.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage }));
        var invoiced = await Ok(await x.C.PostAsync($"{Re}/deals/{id}/invoice-commission", null));
        await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)invoiced["commissionInvoiceId"]!}/void", null));
        var after = await Ok(await x.C.GetAsync($"{Re}/deals/{id}"));
        Assert.Equal("agreed", (string)after["stage"]!);
        Assert.Null(after["commissionInvoiceId"]);
        Assert.Equal("invoiced", (string)(await Ok(await x.C.PostAsync($"{Re}/deals/{id}/invoice-commission", null)))["stage"]!);
    }

    [Fact]
    public async Task A_security_deposit_is_received_then_settled_against_damages_arrears_and_a_refund()
    {
        var x = await OwnerAsync();
        var (leaseId, tenantId, _) = await LeaseAsync(x, "2026-01-01", "2026-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit", new { bankAccountId = x.Bank, date = "2026-01-02", amount = 20_000m })).StatusCode); // above the agreed 10,000
        var received = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit", new { bankAccountId = x.Bank, date = "2026-01-02", amount = 10_000m }));
        Assert.Equal((10_000m, 10_000m), ((decimal)received["received"]!, (decimal)received["held"]!));

        // one invoiced, unpaid instalment becomes the arrears
        await Ok(await x.C.PostAsJsonAsync($"{Re}/billing/run", new { asOf = "2026-01-01", legalEntityId = x.Entity }));
        var invoiceId = (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{leaseId}/schedule"))).AsArray()[0]!["invoiceId"]!;

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit/settle", new { date = "2026-12-31", damages = 100m, refund = 0m })).StatusCode); // lease still active
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/terminate", new { date = "2026-03-31", reason = "Tenant left" }));

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit/settle", new { date = "2026-04-05", damages = 6_000m, refund = 6_000m, refundBankAccountId = x.Bank })).StatusCode); // 12,000 > 10,000 held
        var settled = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit/settle", new
        {
            date = "2026-04-05", damages = 1_500m, damagesDescription = "Wall repairs", arrears = new[] { new { invoiceId, amount = 4_000m } }, refund = 4_500m, refundBankAccountId = x.Bank,
        }));
        Assert.Equal(0m, (decimal)settled["held"]!);
        Assert.Equal(10_000m, (decimal)settled["applied"]!);
        var invoice = await Ok(await x.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal(4_000m, (decimal)invoice["paid"]!);
        Assert.Equal("partlyPaid", (string)invoice["status"]!);
    }

    [Fact]
    public async Task A_deposit_rolls_over_to_the_renewal_without_cash_moving()
    {
        var x = await OwnerAsync();
        var (leaseId, tenantId, unitId) = await LeaseAsync(x, "2026-01-01", "2026-12-31");
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit", new { bankAccountId = x.Bank, date = "2026-01-02", amount = 10_000m }));
        var renewal = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/renew", new { annualRent = 130_000m }), HttpStatusCode.Created);
        var renewalId = (string)renewal["id"]!;

        var moved = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit/transfer", new { toLeaseId = renewalId }));
        Assert.Equal(10_000m, (decimal)moved["held"]!);
        Assert.Equal(0m, (decimal)(await Ok(await x.C.GetAsync($"{Re}/leases/{leaseId}/deposit")))["held"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/deposit/transfer", new { toLeaseId = renewalId })).StatusCode); // nothing left
    }

    [Fact]
    public async Task Terminating_early_credits_rent_invoiced_beyond_the_termination_date()
    {
        var x = await OwnerAsync();
        var (leaseId, _, _) = await LeaseAsync(x, "2026-01-01", "2026-12-31");
        await Ok(await x.C.PostAsJsonAsync($"{Re}/billing/run", new { asOf = "2026-07-01", legalEntityId = x.Entity })); // instalments of Jan, Apr and Jul
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/terminate", new { date = "2026-05-15", reason = "Moved out" }));

        var credits = (await Ok(await x.C.PostAsync($"{Re}/leases/{leaseId}/termination-credits", null))).AsArray();
        Assert.Equal(2, credits.Count); // April (partly used) and July (all after the date)
        Assert.Contains(credits, c => (decimal)c!["amount"]! == 30_000m); // July in full
        var april = credits.Single(c => (decimal)c!["amount"]! != 30_000m)!;
        Assert.Equal(15_164.84m, (decimal)april["amount"]!); // 30,000 x 46 unused days of 91 (15 May to 30 Jun)
        Assert.True((bool)april["appliedToInvoice"]!);
        Assert.Empty((await Ok(await x.C.PostAsync($"{Re}/leases/{leaseId}/termination-credits", null))).AsArray()); // idempotent
    }

    [Fact]
    public async Task The_nightly_job_ends_expired_leases_and_frees_their_units()
    {
        var x = await OwnerAsync();
        var (leaseId, _, unitId) = await LeaseAsync(x, "2024-01-01", "2024-12-31");
        Assert.Equal("occupied", (string)(await Ok(await x.C.GetAsync($"{Re}/units/{unitId}")))["status"]!);

        var runner = f.Services.GetRequiredService<ERP.Infrastructure.Services.ScheduledJobRunner>();
        await runner.RunOnceAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        Assert.Equal("ended", (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{leaseId}")))["status"]!);
        Assert.Equal("vacant", (string)(await Ok(await x.C.GetAsync($"{Re}/units/{unitId}")))["status"]!);
    }

    [Fact]
    public async Task Auto_allocation_settles_the_oldest_invoices_first_and_keeps_the_rest_on_account()
    {
        var x = await OwnerAsync();
        var customer = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Auto "), kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var ids = new List<string>();
        foreach (var (date, price) in new[] { ("2026-03-01", 100m), ("2026-02-01", 200m), ("2026-04-01", 300m) })
        {
            var d = await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = customer, invoiceDate = date, dueDate = date, lines = new[] { new { description = "x", quantity = 1m, price } } }), HttpStatusCode.Created);
            ids.Add((string)(await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)d["id"]!}/issue", null)))["id"]!);
        }
        var receipt = await Ok(await x.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = x.Entity, partyId = customer, date = "2026-05-01", method = "cash", bankAccountId = x.Bank, amount = 350m, autoAllocate = true }), HttpStatusCode.Created);
        Assert.Equal(0m, (decimal)receipt["unallocated"]!);
        Assert.Equal("paid", (string)(await Ok(await x.C.GetAsync($"{Fin}/invoices/{ids[1]}")))["status"]!);       // 200, oldest
        Assert.Equal("paid", (string)(await Ok(await x.C.GetAsync($"{Fin}/invoices/{ids[0]}")))["status"]!);       // 100
        var third = await Ok(await x.C.GetAsync($"{Fin}/invoices/{ids[2]}"));
        Assert.Equal(("partlyPaid", 50m), ((string)third["status"]!, (decimal)third["paid"]!));
        var more = await Ok(await x.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = x.Entity, partyId = customer, date = "2026-05-02", method = "cash", bankAccountId = x.Bank, amount = 400m, autoAllocate = true }), HttpStatusCode.Created);
        Assert.Equal(150m, (decimal)more["unallocated"]!); // 250 settled the rest, 150 stays on account
    }
}
