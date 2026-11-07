using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Requisitions and purchase orders through the approval engine, and the auto-approve threshold. Runs on its own host because policies apply to the whole workspace.</summary>
public class PurchasingApprovalTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string OrderDoc = "purchase_order";
    private const string RequisitionDoc = "purchase_requisition";

    private static async Task<string> PolicyAsync(SupplyChainKit k, string documentType, decimal? minAmount = null)
        => (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType, minAmount }), HttpStatusCode.Created))["id"]!;

    private static Task DropPolicyAsync(SupplyChainKit k, string id) => k.C.DeleteAsync($"/api/v1/approval-policies/{id}");

    private static async Task<(string Id, string LineId)> DraftOrderAsync(SupplyChainKit k, decimal price)
    {
        var po = await Ok(await k.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = k.Entity, vendorPartyId = k.Vendor, vendorName = "Vendor", orderDate = "2026-05-01", currency = "AED",
            lines = new[] { new { itemId = k.Item, itemCode = k.Sku, description = "Stocked item", quantity = 10m, unitOfMeasure = "pcs", unitPrice = price, discountPercent = 0m, taxRate = 0m } },
        }), HttpStatusCode.Created);
        return ((string)po["id"]!, (string)po["lines"]![0]!["id"]!);
    }

    private static Task<HttpResponseMessage> SetAsync(SupplyChainKit k, string id, string status) => k.C.PutAsJsonAsync($"{Pur}/orders/{id}/status", new { status });

    private static async Task<string> PendingRequestFor(SupplyChainKit k, string documentId)
        => (string)(await Ok(await k.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray().First(a => (string?)a!["documentId"] == documentId && (string?)a!["status"] == "pending")!["id"]!;

    [Fact]
    public async Task An_order_under_a_policy_waits_for_another_user_and_only_then_can_be_received()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, OrderDoc);
        var (po, line) = await DraftOrderAsync(k, 1000m); // 10,000 total, over the default threshold

        var asked = await Ok(await SetAsync(k, po, "Approved"));
        Assert.Equal("submitted", (string)asked["status"]!, ignoreCase: true);
        Assert.Equal(HttpStatusCode.Conflict, (await k.ReceiveAsync(po, line, 10m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SetAsync(k, po, "Approved")).StatusCode); // still waiting

        var request = await PendingRequestFor(k, po);

        // the requester cannot approve their own
        var own = await k.C.PostAsJsonAsync($"/api/v1/approvals/{request}/approve", new { comment = "mine" });
        Assert.False(own.IsSuccessStatusCode);
        Assert.Contains("own", await own.Content.ReadAsStringAsync());

        var approver = await k.NewApproverAsync(f);
        await Ok(await approver.PostAsJsonAsync($"/api/v1/approvals/{request}/approve", new { comment = "fine" }));

        Assert.Equal("approved", (string)(await Ok(await SetAsync(k, po, "Approved")))["status"]!, ignoreCase: true);
        await k.ReceivedAsync(po, line, 10m);
        Assert.Equal(10000m, await k.BalanceAsync("inventory"));

        await DropPolicyAsync(k, policy);
    }

    [Fact]
    public async Task An_order_below_the_auto_approve_threshold_skips_the_engine()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, OrderDoc);
        var (po, _) = await DraftOrderAsync(k, 100m); // 1,000 total, under the default 5,000

        var submitted = await Ok(await SetAsync(k, po, "Submitted"));

        Assert.Equal("approved", (string)submitted["status"]!, ignoreCase: true);
        Assert.DoesNotContain((await Ok(await k.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray(), a => (string?)a!["documentId"] == po);

        await DropPolicyAsync(k, policy);
    }

    [Fact]
    public async Task The_auto_approve_threshold_is_a_live_setting()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, OrderDoc);
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["purchasing.autoApproveOrdersBelow"] = 500 })).StatusCode);
        try
        {
            var (po, _) = await DraftOrderAsync(k, 100m); // 1,000 is now over the threshold
            Assert.Equal("submitted", (string)(await Ok(await SetAsync(k, po, "Approved")))["status"]!, ignoreCase: true);
            Assert.Contains((await Ok(await k.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray(), a => (string?)a!["documentId"] == po);
        }
        finally
        {
            await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["purchasing.autoApproveOrdersBelow"] = 5000 });
            await DropPolicyAsync(k, policy);
        }
    }

    [Fact]
    public async Task Without_a_policy_an_order_and_a_requisition_approve_as_before()
    {
        var k = await NewAsync(f);
        var (po, _) = await DraftOrderAsync(k, 1000m);
        Assert.Equal("approved", (string)(await Ok(await SetAsync(k, po, "Approved")))["status"]!, ignoreCase: true);

        var req = await Ok(await k.C.PostAsJsonAsync($"{Pur}/requisitions", new
        {
            legalEntityId = k.Entity, requesterName = "Alice", requisitionDate = "2026-05-01",
            lines = new[] { new { itemCode = "X", description = "Part", quantity = 1m, unitOfMeasure = "pcs", estimatedUnitPrice = 50000m } },
        }), HttpStatusCode.Created);
        Assert.Equal("approved", (string)(await Ok(await k.C.PutAsJsonAsync($"{Pur}/requisitions/{(string)req["id"]!}/status", new { status = "Approved" })))["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Editing_an_order_withdraws_its_pending_request_and_it_must_go_through_again()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, OrderDoc);
        var (po, _) = await DraftOrderAsync(k, 1000m);
        await Ok(await SetAsync(k, po, "Approved"));
        var request = await PendingRequestFor(k, po);

        await Ok(await k.C.PutAsJsonAsync($"{Pur}/orders/{po}", new
        {
            legalEntityId = k.Entity, vendorPartyId = k.Vendor, vendorName = "Vendor", orderDate = "2026-05-01", currency = "AED",
            lines = new[] { new { itemId = k.Item, itemCode = k.Sku, description = "Stocked item", quantity = 20m, unitOfMeasure = "pcs", unitPrice = 1000m, discountPercent = 0m, taxRate = 0m } },
        }));

        var mine = (await Ok(await k.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray();
        Assert.Equal("withdrawn", (string)mine.First(a => (string?)a!["id"] == request)!["status"]!);
        Assert.Equal("submitted", (string)(await Ok(await SetAsync(k, po, "Approved")))["status"]!, ignoreCase: true);
        Assert.NotEqual(request, await PendingRequestFor(k, po));

        await DropPolicyAsync(k, policy);
    }

    [Fact]
    public async Task A_rejected_order_goes_back_for_a_new_decision()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, OrderDoc);
        var (po, _) = await DraftOrderAsync(k, 1000m);
        await Ok(await SetAsync(k, po, "Approved"));
        var request = await PendingRequestFor(k, po);
        var approver = await k.NewApproverAsync(f);
        await Ok(await approver.PostAsJsonAsync($"/api/v1/approvals/{request}/reject", new { comment = "no" }));

        Assert.Equal("submitted", (string)(await Ok(await SetAsync(k, po, "Approved")))["status"]!, ignoreCase: true);
        Assert.NotEqual(request, await PendingRequestFor(k, po));

        await DropPolicyAsync(k, policy);
    }

    [Fact]
    public async Task A_requisition_under_a_policy_waits_for_another_user_before_it_can_become_an_order()
    {
        var k = await NewAsync(f);
        var policy = await PolicyAsync(k, RequisitionDoc, minAmount: 100m);
        JsonNode Lines(decimal price) => new JsonArray(new JsonObject { ["itemCode"] = "X", ["description"] = "Part", ["quantity"] = 1m, ["unitOfMeasure"] = "pcs", ["estimatedUnitPrice"] = price });

        var small = await Ok(await k.C.PostAsJsonAsync($"{Pur}/requisitions", new { legalEntityId = k.Entity, requesterName = "Alice", requisitionDate = "2026-05-01", lines = Lines(50m) }), HttpStatusCode.Created);
        Assert.Equal("approved", (string)(await Ok(await k.C.PutAsJsonAsync($"{Pur}/requisitions/{(string)small["id"]!}/status", new { status = "Approved" })))["status"]!, ignoreCase: true);

        var big = await Ok(await k.C.PostAsJsonAsync($"{Pur}/requisitions", new { legalEntityId = k.Entity, requesterName = "Alice", requisitionDate = "2026-05-01", lines = Lines(500m) }), HttpStatusCode.Created);
        var id = (string)big["id"]!;
        var asked = await Ok(await k.C.PutAsJsonAsync($"{Pur}/requisitions/{id}/status", new { status = "Approved" }));
        Assert.Equal("pendingApproval", (string)asked["status"]!, ignoreCase: true);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Pur}/requisitions/{id}/convert-to-order", new { vendorPartyId = k.Vendor, vendorName = "Vendor" })).StatusCode);

        var request = await PendingRequestFor(k, id);
        Assert.False((await k.C.PostAsJsonAsync($"/api/v1/approvals/{request}/approve", new { comment = "mine" })).IsSuccessStatusCode);
        var approver = await k.NewApproverAsync(f);
        await Ok(await approver.PostAsJsonAsync($"/api/v1/approvals/{request}/approve", new { comment = "fine" }));

        Assert.Equal("approved", (string)(await Ok(await k.C.PutAsJsonAsync($"{Pur}/requisitions/{id}/status", new { status = "Approved" })))["status"]!, ignoreCase: true);
        Assert.Equal("draft", (string)(await Ok(await k.C.PostAsJsonAsync($"{Pur}/requisitions/{id}/convert-to-order", new { vendorPartyId = k.Vendor, vendorName = "Vendor" })))["status"]!, ignoreCase: true);

        await DropPolicyAsync(k, policy);
    }
}
