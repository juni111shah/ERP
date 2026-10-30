using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

public class ServicesBillingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Svc = "/api/v1/m/services";

    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string EngagementId, string Bank);

    private async Task<Setup> NewAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/services/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "SvcLegal " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = true }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Client " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;

        var engRes = await Ok(await c.PostAsJsonAsync($"{Svc}/engagements", new
        {
            legalEntityId = entity,
            title = "Cloud Transformation " + Guid.NewGuid().ToString("N")[..6],
            clientId = customer,
            clientName = "Client",
            type = 1, // FixedPrice
            contractValue = 100000m,
            budgetedHours = 500m,
            startDate = "2026-01-01"
        }), HttpStatusCode.Created);
        var engagementId = (string)engRes["id"]!;

        return new Setup(c, entity, customer, engagementId, bank);
    }

    [Fact]
    public async Task Milestone_not_ReadyToInvoice_cannot_be_invoiced_and_cannot_be_manually_marked_paid()
    {
        var s = await NewAsync();

        // 1. Create milestone in Planned status
        var msRes = await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", new
        {
            legalEntityId = s.Entity,
            engagementId = s.EngagementId,
            title = "Deliverable Signoff",
            amount = 15000m,
            dueDate = "2026-08-01"
        }), HttpStatusCode.Created);
        var milestoneId = (string)msRes["id"]!;

        // 2. Cannot invoice while in Planned status
        var invoiceRes = await s.C.PostAsync($"{Svc}/milestones/{milestoneId}/invoice", null);
        Assert.Equal(HttpStatusCode.Conflict, invoiceRes.StatusCode);

        // 3. Cannot manually mark as Paid
        var paidRes = await s.C.PostAsJsonAsync($"{Svc}/milestones/{milestoneId}/status", new
        {
            id = milestoneId,
            status = "Paid"
        });
        Assert.Equal(HttpStatusCode.Conflict, paidRes.StatusCode);
    }

    [Fact]
    public async Task Milestone_invoice_sets_due_date_to_max_of_invoice_date_and_milestone_due()
    {
        var s = await NewAsync();

        // 1. Create milestone with due date in the past
        var pastDue = "2020-01-15";
        var msRes = await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", new
        {
            legalEntityId = s.Entity,
            engagementId = s.EngagementId,
            title = "Past Deliverable",
            amount = 10000m,
            dueDate = pastDue
        }), HttpStatusCode.Created);
        var milestoneId = (string)msRes["id"]!;

        // 2. Move to ReadyToInvoice
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones/{milestoneId}/status", new
        {
            id = milestoneId,
            status = "ReadyToInvoice"
        }));

        // 3. Invoice milestone
        var invRes = await Ok(await s.C.PostAsync($"{Svc}/milestones/{milestoneId}/invoice", null));
        var invoiceId = (string)invRes["id"]!;
        var invDate = (string)invRes["invoiceDate"]!;
        var dueDate = (string)invRes["dueDate"]!;

        // Due date must not precede invoice date; must be max(invoiceDate, milestoneDue)
        Assert.Equal(invDate, dueDate);
    }

    [Fact]
    public async Task Milestone_shows_paid_only_after_invoice_is_fully_settled()
    {
        var s = await NewAsync();

        // 1. Create milestone and move to ReadyToInvoice
        var msRes = await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", new
        {
            legalEntityId = s.Entity,
            engagementId = s.EngagementId,
            title = "System Go-Live",
            amount = 2000m,
            dueDate = "2026-09-01"
        }), HttpStatusCode.Created);
        var milestoneId = (string)msRes["id"]!;

        await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones/{milestoneId}/status", new
        {
            id = milestoneId,
            status = "ReadyToInvoice"
        }));

        // 2. Invoice milestone
        var invoice = await Ok(await s.C.PostAsync($"{Svc}/milestones/{milestoneId}/invoice", null));
        var invoiceId = (string)invoice["id"]!;
        var total = (decimal)invoice["total"]!;

        // Issue invoice
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{invoiceId}/issue", null));

        // Milestone shows Invoiced, not Paid
        var msCheck1 = await Ok(await s.C.GetAsync($"{Svc}/milestones/{milestoneId}"));
        Assert.Equal("invoiced", (string)msCheck1["status"]!, ignoreCase: true);

        // 3. Settle partially (half)
        var half = total / 2m;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity,
            partyId = s.Customer,
            date = "2026-06-10",
            method = "bankTransfer",
            bankAccountId = s.Bank,
            amount = half,
            allocations = new[] { new { invoiceId, amount = half } }
        }), HttpStatusCode.Created);

        // Milestone is still Invoiced
        var msCheck2 = await Ok(await s.C.GetAsync($"{Svc}/milestones/{milestoneId}"));
        Assert.Equal("invoiced", (string)msCheck2["status"]!, ignoreCase: true);

        // 4. Settle remaining
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity,
            partyId = s.Customer,
            date = "2026-06-11",
            method = "bankTransfer",
            bankAccountId = s.Bank,
            amount = half,
            allocations = new[] { new { invoiceId, amount = half } }
        }), HttpStatusCode.Created);

        // Milestone now shows Paid!
        var msCheck3 = await Ok(await s.C.GetAsync($"{Svc}/milestones/{milestoneId}"));
        Assert.Equal("paid", (string)msCheck3["status"]!, ignoreCase: true);
    }
}
