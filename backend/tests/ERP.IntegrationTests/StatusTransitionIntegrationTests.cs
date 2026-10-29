using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

public class StatusTransitionIntegrationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<(HttpClient Client, string EntityId, string CustomerId, string VendorId)> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/sales/enable", null);
        await c0.PostAsync("/api/v1/modules/purchasing/enable", null);
        await c0.PostAsync("/api/v1/modules/crm/enable", null);
        await c0.PostAsync("/api/v1/modules/services/enable", null);
        await c0.PostAsync("/api/v1/modules/projects/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "TestCorp " + tag,
            countryCode = "AE",
            baseCurrency = "AED",
            isTaxRegistered = true
        }), HttpStatusCode.Created))["id"]!;

        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new
        {
            name = "Customer " + tag,
            kind = "Company",
            roles = "Customer"
        }), HttpStatusCode.Created))["id"]!;

        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new
        {
            name = "Vendor " + tag,
            kind = "Company",
            roles = "Vendor"
        }), HttpStatusCode.Created))["id"]!;

        return (c, entity, customer, vendor);
    }

    [Fact]
    public async Task Sales_quote_and_order_enforce_transitions()
    {
        var (c, entity, customer, _) = await SetupAsync();

        // 1. Quote
        var quoteRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/sales/quotes", new
        {
            legalEntityId = entity,
            customerName = "Acme Corp",
            quoteDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "SRV-01", description = "Consulting", quantity = 1m, unitPrice = 500m } }
        }), HttpStatusCode.Created);
        var quoteId = (string)quoteRes["id"]!;

        // Illegal: Draft -> Converted via status endpoint returns 409
        var illegalQuote = await c.PutAsJsonAsync($"/api/v1/m/sales/quotes/{quoteId}/status", new { status = "Converted" });
        Assert.Equal(HttpStatusCode.Conflict, illegalQuote.StatusCode);
        var illegalQuoteBody = await illegalQuote.Content.ReadAsStringAsync();
        Assert.Contains("system conversion process", illegalQuoteBody);

        // Legal: Draft -> Sent returns 200
        var legalQuote = await Ok(await c.PutAsJsonAsync($"/api/v1/m/sales/quotes/{quoteId}/status", new { status = "Sent" }));
        Assert.Equal("sent", (string)legalQuote["status"]!, ignoreCase: true);

        // 2. Order
        var orderRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/sales/orders", new
        {
            legalEntityId = entity,
            customerPartyId = customer,
            customerName = "Acme Corp",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "WIDGET-01", description = "Widget", quantity = 2m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        // Illegal: Draft -> Invoiced via status endpoint returns 409
        var illegalOrder = await c.PutAsJsonAsync($"/api/v1/m/sales/orders/{orderId}/status", new { status = "Invoiced" });
        Assert.Equal(HttpStatusCode.Conflict, illegalOrder.StatusCode);
        var illegalOrderBody = await illegalOrder.Content.ReadAsStringAsync();
        Assert.Contains("system conversion process", illegalOrderBody);

        // Legal: Draft -> Confirmed returns 200
        var legalOrder = await Ok(await c.PutAsJsonAsync($"/api/v1/m/sales/orders/{orderId}/status", new { status = "Confirmed" }));
        Assert.Equal("confirmed", (string)legalOrder["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Purchasing_order_and_requisition_enforce_transitions()
    {
        var (c, entity, _, vendor) = await SetupAsync();

        // 1. Purchase Order
        var poRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/purchasing/orders", new
        {
            legalEntityId = entity,
            vendorPartyId = vendor,
            vendorName = "Supplier Ltd",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "RAW-01", description = "Raw Steel", quantity = 10m, unitPrice = 200m } }
        }), HttpStatusCode.Created);
        var poId = (string)poRes["id"]!;

        // Illegal: Draft -> Billed via status endpoint returns 409
        var illegalPo = await c.PutAsJsonAsync($"/api/v1/m/purchasing/orders/{poId}/status", new { status = "Billed" });
        Assert.Equal(HttpStatusCode.Conflict, illegalPo.StatusCode);
        var illegalPoBody = await illegalPo.Content.ReadAsStringAsync();
        Assert.Contains("system conversion process", illegalPoBody);

        // Legal: Draft -> Approved returns 200
        var legalPo = await Ok(await c.PutAsJsonAsync($"/api/v1/m/purchasing/orders/{poId}/status", new { status = "Approved" }));
        Assert.Equal("approved", (string)legalPo["status"]!, ignoreCase: true);

        // 2. Requisition
        var reqRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/purchasing/requisitions", new
        {
            legalEntityId = entity,
            requesterName = "John Doe",
            department = "IT",
            requisitionDate = "2026-06-01",
            lines = new[] { new { itemCode = "IT-01", description = "Monitors", quantity = 5m, estimatedUnitPrice = 300m } }
        }), HttpStatusCode.Created);
        var reqId = (string)reqRes["id"]!;

        // Illegal: Draft -> Ordered via status endpoint returns 409
        var illegalReq = await c.PutAsJsonAsync($"/api/v1/m/purchasing/requisitions/{reqId}/status", new { status = "Ordered" });
        Assert.Equal(HttpStatusCode.Conflict, illegalReq.StatusCode);
        var illegalReqBody = await illegalReq.Content.ReadAsStringAsync();
        Assert.Contains("system conversion process", illegalReqBody);

        // Legal: Draft -> Approved returns 200
        var legalReq = await Ok(await c.PutAsJsonAsync($"/api/v1/m/purchasing/requisitions/{reqId}/status", new { status = "Approved" }));
        Assert.Equal("approved", (string)legalReq["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Crm_opportunity_enforces_pipeline_stages()
    {
        var (c, entity, _, _) = await SetupAsync();

        var oppRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/crm/opportunities", new
        {
            legalEntityId = entity,
            title = "Big Enterprise Deal",
            customerName = "Global Logistics",
            stage = 1, // Qualification
            probabilityPercent = 20,
            expectedRevenue = 500000m
        }), HttpStatusCode.Created);
        var oppId = (string)oppRes["id"]!;

        // Illegal: Qualification -> ClosedWon directly returns 409
        var illegalOpp = await c.PostAsJsonAsync($"/api/v1/m/crm/opportunities/{oppId}/stage", new
        {
            id = oppId,
            stage = 4, // ClosedWon
            probabilityPercent = 100
        });
        Assert.Equal(HttpStatusCode.Conflict, illegalOpp.StatusCode);
        var illegalOppBody = await illegalOpp.Content.ReadAsStringAsync();
        Assert.Contains("Proposal, ClosedLost", illegalOppBody);

        // Legal: Qualification -> Proposal returns 200
        var legalOpp = await Ok(await c.PostAsJsonAsync($"/api/v1/m/crm/opportunities/{oppId}/stage", new
        {
            id = oppId,
            stage = 2, // Proposal
            probabilityPercent = 40
        }));
        Assert.Equal("proposal", (string)legalOpp["stage"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Services_milestone_enforces_transitions()
    {
        var (c, entity, customer, _) = await SetupAsync();

        var engRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/services/engagements", new
        {
            legalEntityId = entity,
            title = "Advisory Project",
            clientId = customer,
            clientName = "Advisory Client",
            contractValue = 50000m,
            startDate = "2026-06-01"
        }), HttpStatusCode.Created);
        var engId = (string)engRes["id"]!;

        var msRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/services/milestones", new
        {
            legalEntityId = entity,
            engagementId = engId,
            title = "Phase 1 Delivery",
            amount = 25000m,
            dueDate = "2026-06-30"
        }), HttpStatusCode.Created);
        var msId = (string)msRes["id"]!;

        // Illegal: Planned -> Invoiced via status endpoint returns 409
        var illegalMs = await c.PostAsJsonAsync($"/api/v1/m/services/milestones/{msId}/status", new
        {
            id = msId,
            status = 3 // Invoiced
        });
        Assert.Equal(HttpStatusCode.Conflict, illegalMs.StatusCode);
        var illegalMsBody = await illegalMs.Content.ReadAsStringAsync();
        Assert.Contains("system conversion process", illegalMsBody);

        // Legal: Planned -> ReadyToInvoice returns 200
        var legalMs = await Ok(await c.PostAsJsonAsync($"/api/v1/m/services/milestones/{msId}/status", new
        {
            id = msId,
            status = 2 // ReadyToInvoice
        }));
        Assert.Equal("readytoinvoice", (string)legalMs["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Projects_enforce_transitions()
    {
        var (c, entity, customer, _) = await SetupAsync();

        var prjRes = await Ok(await c.PostAsJsonAsync("/api/v1/m/projects/projects", new
        {
            legalEntityId = entity,
            customerId = customer,
            name = "ERP Modernization",
            startDate = "2026-06-01",
            billingType = 0 // TimeAndMaterials
        }), HttpStatusCode.Created);
        var prjId = (string)prjRes["id"]!;

        // Illegal: Planning -> Completed returns 409
        var illegalPrj = await c.PutAsJsonAsync($"/api/v1/m/projects/projects/{prjId}/status", new
        {
            status = 3 // Completed
        });
        Assert.Equal(HttpStatusCode.Conflict, illegalPrj.StatusCode);
        var illegalPrjBody = await illegalPrj.Content.ReadAsStringAsync();
        Assert.Contains("Active, Cancelled", illegalPrjBody);

        // Legal: Planning -> Active returns 200
        var legalPrj = await Ok(await c.PutAsJsonAsync($"/api/v1/m/projects/projects/{prjId}/status", new
        {
            status = 1 // Active
        }));
        Assert.Equal("active", (string)legalPrj["status"]!, ignoreCase: true);
    }
}
