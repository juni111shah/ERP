using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Rent held for owners: the owner payable, payouts through a payment or a bill, reports net of tax, a tenant with two leases and a renewal activated after the old lease ended.</summary>
public class RealEstateOwnerFundsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";
    private const string Payable = "realestate.owner.payable", FeeIncome = "realestate.income.management", Recharge = "realestate.income.recharges", Leasing = "realestate.income.leasing";

    private static Task<JsonNode> Ok(HttpResponseMessage r, HttpStatusCode e = HttpStatusCode.OK) => ProjectsTestKit.Ok(r, e);
    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private sealed record Ctx(HttpClient C, string Entity, string Bank, string Tax, Dictionary<string, string> Roles);

    private async Task<Ctx> CtxAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        var tax = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = U("TX"), name = "VAT 5", kind = "vat", rate = 5 }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity, (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!, tax, accounts.ToDictionary(a => (string)a!["id"]!, a => (string?)a!["systemRole"] ?? (string)a!["code"]!));
    }

    private static async Task<string> PropertyAsync(Ctx x, string ownership, string? owner = null, decimal fee = 0m, bool? creditToOwner = null)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = x.Entity, name = U("Tower "), propertyType = "residential", ownershipType = ownership, ownerPartyId = owner, managementFeePercent = fee, creditRentToOwner = creditToOwner, totalUnits = 0, indicativeValue = 1_000_000m }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> PartyAsync(Ctx x, string role) => (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U(role + " "), kind = "Individual", roles = role }), HttpStatusCode.Created))["id"]!;

    /// <summary>A unit let from the start of 2026 at 10,000 a month plus 5% tax.</summary>
    private static async Task<(string Lease, string Unit)> LeaseAsync(Ctx x, string property, string tenant, bool taxed = true)
    {
        var type = await Ok(await x.C.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var unit = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/units", new { propertyId = property, unitNumber = U("U"), unitTypeId = (string)type["id"]!, areaSqft = 900, askingRent = 120_000m }), HttpStatusCode.Created))["id"]!;
        var lease = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/leases", new { unitId = unit, lesseePartyId = tenant, startDate = "2026-01-01", endDate = "2026-12-31", annualRent = 120_000m, paymentFrequency = 12, taxCodeId = taxed ? x.Tax : null, activateNow = true }), HttpStatusCode.Created))["id"]!;
        return (lease, unit);
    }

    /// <summary>Invoices the first instalment of a lease and returns the invoice with its journal effect, by account role.</summary>
    private static async Task<(string InvoiceId, decimal Total, Dictionary<string, decimal> Ledger)> InvoiceFirstAsync(Ctx x, string lease)
    {
        var instalment = (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{lease}/schedule"))).AsArray()[0]!["id"]!;
        var made = await Ok(await x.C.PostAsync($"{Re}/billing/instalments/{instalment}/invoice", null));
        var invoice = await Ok(await x.C.GetAsync($"{Fin}/invoices/{(string)made["invoiceId"]!}"));
        return ((string)made["invoiceId"]!, (decimal)invoice["total"]!, await JournalAsync(x, (string)invoice["journalEntryId"]!));
    }

    private static async Task<Dictionary<string, decimal>> JournalAsync(Ctx x, string id)
    {
        var journal = await Ok(await x.C.GetAsync($"{Fin}/journals/{id}"));
        return journal["lines"]!.AsArray().GroupBy(l => x.Roles[(string)l!["accountId"]!]).ToDictionary(g => g.Key, g => g.Sum(l => (decimal)l!["debit"]! - (decimal)l!["credit"]!));
    }

    private static async Task ReceiptAsync(Ctx x, string tenant, string invoice, decimal amount, string date = "2026-01-10")
        => await Ok(await x.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = x.Entity, partyId = tenant, date, method = "bankTransfer", bankAccountId = x.Bank, amount, allocations = new[] { new { invoiceId = invoice, amount } } }), HttpStatusCode.Created);

    [Fact]
    public async Task Rent_of_a_managed_property_credits_the_owner_payable_and_other_rent_stays_company_income()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");

        var managed = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant)).Lease);
        Assert.Equal(10_500m, managed.Total);
        Assert.Equal(10_500m, managed.Ledger["ar.control"]);
        Assert.Equal(-10_000m, managed.Ledger[Payable]);
        Assert.Equal(-500m, managed.Ledger["tax.output"]);
        Assert.False(managed.Ledger.ContainsKey(Leasing));

        var owned = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "owned"), await PartyAsync(x, "Customer"))).Lease);
        Assert.Equal(-10_000m, owned.Ledger[Leasing]);
        Assert.False(owned.Ledger.ContainsKey(Payable));

        // the choice per property: a managed property can stay on the old treatment, and that survives a save that does not mention it
        var optedOut = await PropertyAsync(x, "managedForOwner", owner, 5m, creditToOwner: false);
        var keep = await Ok(await x.C.PutAsJsonAsync($"{Re}/properties/{optedOut}", new { legalEntityId = x.Entity, name = U("Renamed "), propertyType = "residential", ownershipType = "managedForOwner", ownerPartyId = owner, managementFeePercent = 5m, totalUnits = 0, indicativeValue = 1m }));
        Assert.False((bool)keep["creditRentToOwner"]!);
        Assert.False((bool)keep["rentCreditsOwner"]!);
        var legacy = await InvoiceFirstAsync(x, (await LeaseAsync(x, optedOut, await PartyAsync(x, "Customer"))).Lease);
        Assert.Equal(-10_000m, legacy.Ledger[Leasing]);

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = x.Entity, name = U("Bad "), propertyType = "residential", ownershipType = "owned", creditRentToOwner = true, totalUnits = 0, indicativeValue = 1m })).StatusCode);
    }

    [Fact]
    public async Task A_workspace_that_enables_the_module_later_has_the_owner_payable()
    {
        // the admin workspace was not created from the real estate template: enabling the module brings its accounts
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/realestate/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Contains((await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray(), a => (string?)a!["systemRole"] == Payable);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = U("Mgr "), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var x = new Ctx(c, entity, "", "", []);
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var (lease, _) = await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant, taxed: false);
        var instalment = (string)(await Ok(await c.GetAsync($"{Re}/leases/{lease}/schedule"))).AsArray()[0]!["id"]!;
        await Ok(await c.PostAsync($"{Re}/billing/instalments/{instalment}/invoice", null));
        Assert.Contains((await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray(), a => (string?)a!["systemRole"] == Payable);
    }

    [Fact]
    public async Task The_owner_statement_and_the_fee_are_net_of_tax()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var inv = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant)).Lease);
        await ReceiptAsync(x, tenant, inv.InvoiceId, 10_500m);

        var st = await Ok(await x.C.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={owner}&from=2026-01-01&to=2026-01-31"));
        Assert.Equal(10_000m, (decimal)st["rentCollected"]!);  // not 10,500
        Assert.Equal(500m, (decimal)st["taxCollected"]!);
        Assert.Equal(500m, (decimal)st["managementFee"]!);     // 5% of the rent, not of rent and tax
        Assert.Equal(9_500m, (decimal)st["netToOwner"]!);
        Assert.Equal(10_000m, (decimal)st["rows"]![0]!["rentCollected"]!);

        // a part payment splits the same way
        var part = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant)).Lease);
        await ReceiptAsync(x, tenant, part.InvoiceId, 5_250m, "2026-02-10");
        var feb = await Ok(await x.C.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={owner}&from=2026-02-01&to=2026-02-28"));
        Assert.Equal((5_000m, 250m), ((decimal)feb["rentCollected"]!, (decimal)feb["managementFee"]!));
    }

    [Fact]
    public async Task A_payment_payout_clears_the_payable_and_takes_the_fee_and_maintenance_as_company_income()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var property = await PropertyAsync(x, "managedForOwner", owner, 5m);
        var inv = await InvoiceFirstAsync(x, (await LeaseAsync(x, property, tenant)).Lease);
        await ReceiptAsync(x, tenant, inv.InvoiceId, 10_500m);

        // nothing collected in the period: nothing to pay, and the bank is required
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2025-01-01", to = "2025-01-31", date = "2026-02-01", method = "payment", bankAccountId = x.Bank })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = "2026-01-31", date = "2026-02-01", method = "payment" })).StatusCode);

        var payout = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = "2026-01-31", date = "2026-02-01", method = "payment", bankAccountId = x.Bank }), HttpStatusCode.Created);
        Assert.Equal(9_500m, (decimal)payout["amount"]!);
        Assert.Equal("posted", (string)payout["status"]!);
        var ledger = await JournalAsync(x, (string)payout["journalEntryId"]!);
        Assert.Equal(10_000m, ledger[Payable]);                     // the whole credit from billing is cleared
        Assert.Equal(-500m, ledger[FeeIncome]);
        Assert.Equal(-9_500m, ledger["bank"]);
        Assert.Equal(0m, ledger.Values.Sum());

        var st = await Ok(await x.C.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={owner}&from=2026-01-01&to=2026-01-31"));
        Assert.Equal(9_500m, (decimal)st["paidToOwner"]!);

        // once per period, until it is voided
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-15", to = "2026-02-15", date = "2026-02-20", method = "payment", bankAccountId = x.Bank })).StatusCode);
        var voided = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts/{(string)payout["id"]!}/void", new { reason = "Wrong bank" }));
        Assert.Equal("voided", (string)voided["status"]!);
        Assert.Equal("reversed", (string)(await Ok(await x.C.GetAsync($"{Fin}/journals/{(string)payout["journalEntryId"]!}")))["status"]!, ignoreCase: true);

        // a job the owner bears comes out of the next payout through the recharge account
        var wo = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders", new { propertyId = property, issue = "Repaint", priority = "normal", technician = "Crew", estimatedCost = 900m }), HttpStatusCode.Created))["id"]!;
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{wo}/status", new { status = "assigned" }));
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{wo}/status", new { status = "inProgress" }));
        await Ok(await x.C.PostAsJsonAsync($"{Re}/work-orders/{wo}/status", new { status = "completed", actualCost = 1_000m }));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var again = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = today.ToString("yyyy-MM-dd"), date = today.ToString("yyyy-MM-dd"), method = "payment", bankAccountId = x.Bank }), HttpStatusCode.Created);
        Assert.Equal(8_500m, (decimal)again["amount"]!);
        var withJob = await JournalAsync(x, (string)again["journalEntryId"]!);
        Assert.Equal(-1_000m, withJob[Recharge]);
        Assert.Equal(10_000m, withJob[Payable]);
    }

    [Fact]
    public async Task A_bill_payout_clears_the_payable_through_accounts_payable_and_goes_when_the_bill_is_voided()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var inv = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant)).Lease);
        await ReceiptAsync(x, tenant, inv.InvoiceId, 10_500m);

        var payout = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = "2026-01-31", date = "2026-02-01", method = "bill", dueDate = "2026-02-15" }), HttpStatusCode.Created);
        var payoutId = (string)payout["id"]!;
        Assert.Equal(9_500m, (decimal)payout["amount"]!);

        var fee = await JournalAsync(x, (string)payout["journalEntryId"]!);
        Assert.Equal((500m, -500m), (fee[Payable], fee[FeeIncome]));

        var bill = await Ok(await x.C.GetAsync($"{Fin}/bills/{(string)payout["billId"]!}"));
        Assert.Equal(owner, (string)bill["partyId"]!);
        Assert.Equal(9_500m, (decimal)bill["total"]!);
        var billJournal = await JournalAsync(x, (string)bill["journalEntryId"]!);
        Assert.Equal(9_500m, billJournal[Payable]);        // 500 + 9,500 = the 10,000 credited at billing
        Assert.Equal(-9_500m, billJournal["ap.control"]);

        await Ok(await x.C.PostAsJsonAsync($"{Fin}/bills/{(string)bill["id"]!}/void", new { reason = "Owner asked for a transfer" }));
        var after = await Ok(await x.C.GetAsync($"{Re}/owner-payouts/{payoutId}"));
        Assert.Equal("voided", (string)after["status"]!);
        Assert.Equal("reversed", (string)(await Ok(await x.C.GetAsync($"{Fin}/journals/{(string)payout["journalEntryId"]!}")))["status"]!, ignoreCase: true);

        // and from the payout side, which voids the bill
        var second = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = "2026-01-31", date = "2026-02-01", method = "bill" }), HttpStatusCode.Created);
        var voided = await Ok(await x.C.PostAsJsonAsync($"{Re}/owner-payouts/{(string)second["id"]!}/void", new { reason = "Entered twice" }));
        Assert.Equal(("voided", "Entered twice"), ((string)voided["status"]!, (string)voided["voidReason"]!));
        Assert.Equal("voided", (string)(await Ok(await x.C.GetAsync($"{Fin}/bills/{(string)second["billId"]!}")))["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Rent_kept_as_company_income_is_not_paid_out_again()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var inv = await InvoiceFirstAsync(x, (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m, creditToOwner: false), tenant)).Lease);
        await ReceiptAsync(x, tenant, inv.InvoiceId, 10_500m);

        // the statement still reports it, but the ledger holds nothing for the owner
        Assert.Equal(10_000m, (decimal)(await Ok(await x.C.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={owner}&from=2026-01-01&to=2026-01-31")))["rentCollected"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/owner-payouts", new { legalEntityId = x.Entity, ownerPartyId = owner, from = "2026-01-01", to = "2026-01-31", date = "2026-02-01", method = "payment", bankAccountId = x.Bank })).StatusCode);
    }

    [Fact]
    public async Task Voiding_a_rent_invoice_takes_it_back_out_of_the_owner_payable()
    {
        var x = await CtxAsync();
        var owner = await PartyAsync(x, "Vendor"); var tenant = await PartyAsync(x, "Customer");
        var lease = (await LeaseAsync(x, await PropertyAsync(x, "managedForOwner", owner, 5m), tenant)).Lease;
        var inv = await InvoiceFirstAsync(x, lease);
        await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices/{inv.InvoiceId}/void", new { reason = "Wrong tenant" }));
        var invoice = await Ok(await x.C.GetAsync($"{Fin}/invoices/{inv.InvoiceId}"));
        Assert.Equal("voided", (string)invoice["status"]!, ignoreCase: true);
        Assert.Equal("scheduled", (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{lease}/schedule"))).AsArray()[0]!["status"]!, ignoreCase: true);
        var rebilled = await InvoiceFirstAsync(x, lease);
        Assert.Equal(-10_000m, rebilled.Ledger[Payable]);
    }

    [Fact]
    public async Task Portfolio_collection_is_net_of_tax_like_the_billed_figure_it_is_compared_with()
    {
        var x = await CtxAsync();
        var property = await PropertyAsync(x, "owned");
        var tenant = await PartyAsync(x, "Customer");
        var inv = await InvoiceFirstAsync(x, (await LeaseAsync(x, property, tenant)).Lease);
        await ReceiptAsync(x, tenant, inv.InvoiceId, 5_250m, "2026-01-15");

        var row = (await Ok(await x.C.GetAsync($"{Re}/reports/portfolio-performance?from=2026-01-01&to=2026-01-31")))["rows"]!.AsArray().Single(r => (string)r!["propertyId"]! == property)!;
        Assert.Equal(10_000m, (decimal)row["billed"]!);
        Assert.Equal(5_000m, (decimal)row["collected"]!);
        Assert.Equal(50m, (decimal)row["collectionRatePercent"]!);
    }

    [Fact]
    public async Task A_tenant_with_two_leases_is_not_counted_twice_on_the_rent_roll()
    {
        var x = await CtxAsync();
        var property = await PropertyAsync(x, "owned");
        var tenant = await PartyAsync(x, "Customer");
        var (leaseA, _) = await LeaseAsync(x, property, tenant, taxed: false);
        var (leaseB, _) = await LeaseAsync(x, property, tenant, taxed: false);
        await InvoiceFirstAsync(x, leaseA);
        await InvoiceFirstAsync(x, leaseB);
        // a further open invoice of the tenant, not from any lease: on one row only
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = tenant, invoiceDate = "2026-01-05", dueDate = "2026-01-05", lines = new[] { new { description = "Key replacement", quantity = 1m, price = 300m } } }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));

        var roll = await Ok(await x.C.GetAsync($"{Re}/reports/rent-roll?asOf=2026-06-30&propertyIds={property}"));
        var rows = roll["rows"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Equal(20_300m, (decimal)roll["totalOutstanding"]!); // what the tenant really owes; it was 40,600
        Assert.Equal(20_300m, rows.Sum(r => (decimal)r!["outstanding"]!));
        Assert.Contains(rows, r => (decimal)r!["outstanding"]! == 10_000m);
        Assert.Contains(rows, r => (decimal)r!["outstanding"]! == 10_300m);
    }

    [Fact]
    public async Task A_renewal_activated_after_the_nightly_job_ended_the_old_lease_works_and_reoccupies_the_unit()
    {
        var x = await CtxAsync();
        var property = await PropertyAsync(x, "owned");
        var tenant = await PartyAsync(x, "Customer");
        var type = await Ok(await x.C.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var unit = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/units", new { propertyId = property, unitNumber = "1", unitTypeId = (string)type["id"]!, areaSqft = 900, askingRent = 120_000m }), HttpStatusCode.Created))["id"]!;
        var old = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/leases", new { unitId = unit, lesseePartyId = tenant, startDate = "2024-01-01", endDate = "2024-12-31", annualRent = 120_000m, paymentFrequency = 4, activateNow = true }), HttpStatusCode.Created))["id"]!;
        var draft = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{old}/renew", new { annualRent = 130_000m }), HttpStatusCode.Created))["id"]!;

        await f.Services.GetRequiredService<ERP.Infrastructure.Services.ScheduledJobRunner>().RunOnceAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        Assert.Equal("ended", (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{old}")))["status"]!);
        Assert.Equal("vacant", (string)(await Ok(await x.C.GetAsync($"{Re}/units/{unit}")))["status"]!);

        var active = await Ok(await x.C.PostAsync($"{Re}/leases/{draft}/activate", null));
        Assert.Equal("active", (string)active["status"]!);
        Assert.NotEmpty(active["schedule"]!.AsArray());
        Assert.Equal("renewed", (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{old}")))["status"]!);
        Assert.Equal("occupied", (string)(await Ok(await x.C.GetAsync($"{Re}/units/{unit}")))["status"]!);
    }

    [Fact]
    public async Task A_renewal_of_a_terminated_lease_stays_refused()
    {
        var x = await CtxAsync();
        var (lease, _) = await LeaseAsync(x, await PropertyAsync(x, "owned"), await PartyAsync(x, "Customer"), taxed: false);
        var draft = (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{lease}/renew", new { }), HttpStatusCode.Created))["id"]!;
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{lease}/terminate", new { date = "2026-05-15", reason = "Moved out" }));
        var res = await x.C.PostAsync($"{Re}/leases/{draft}/activate", null);
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("draft", (string)(await Ok(await x.C.GetAsync($"{Re}/leases/{draft}")))["status"]!);
    }
}
