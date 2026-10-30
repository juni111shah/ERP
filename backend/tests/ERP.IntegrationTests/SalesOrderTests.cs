using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

public class SalesOrderTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Sales = "/api/v1/m/sales";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string Tax5Id);

    private async Task<Setup> NewAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/sales/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "SalesLE " + Guid.NewGuid().ToString("N")[..6],
            countryCode = "AE",
            baseCurrency = "AED",
            isTaxRegistered = true
        }), HttpStatusCode.Created))["id"]!;

        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new
        {
            name = "Customer " + Guid.NewGuid().ToString("N")[..6],
            kind = "Company",
            roles = "Customer",
            paymentTermsDays = 15
        }), HttpStatusCode.Created))["id"]!;

        var tax5 = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new
        {
            code = "TX" + Guid.NewGuid().ToString("N")[..6],
            name = "VAT 5%",
            kind = "vat",
            rate = 5,
            isDefault = true,
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        return new Setup(c, entity, customer, tax5);
    }

    [Fact]
    public async Task Taxed_sales_order_conversion_preserves_tax_source_and_total()
    {
        var s = await NewAsync();

        // 1. Create 5% taxed order
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Taxed Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemCode = "SVC-01",
                    description = "Consulting Hour",
                    quantity = 2m,
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 5m
                }
            }
        }), HttpStatusCode.Created);

        var orderId = (string)orderRes["id"]!;
        var orderTaxTotal = (decimal)orderRes["taxTotal"]!;
        var orderTotalAmount = (decimal)orderRes["totalAmount"]!;
        Assert.Equal(10m, orderTaxTotal);
        Assert.Equal(210m, orderTotalAmount);

        // 2. Confirm order
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        // 3. Convert to invoice
        var invoiceRes = await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));

        var invoiceId = (string)invoiceRes["id"]!;
        var invoiceTax = (decimal)invoiceRes["tax"]!;
        var invoiceTotal = (decimal)invoiceRes["total"]!;
        var sourceType = (string)invoiceRes["sourceType"]!;
        var sourceId = (string)invoiceRes["sourceId"]!;

        Assert.Equal(orderTaxTotal, invoiceTax);
        Assert.Equal(orderTotalAmount, invoiceTotal);
        Assert.Equal("salesorder", sourceType);
        Assert.Equal(orderId, sourceId);

        // 4. Verify order is marked invoiced with reference
        var updatedOrder = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("invoiced", (string)updatedOrder["status"]!, ignoreCase: true);
        Assert.Equal(invoiceId, (string)updatedOrder["invoicedInvoiceId"]!);
    }

    [Fact]
    public async Task Sales_order_with_unmatched_tax_rate_returns_conflict_and_creates_no_invoice()
    {
        var s = await NewAsync();

        // 1. Create order with 12.5% tax rate (no matching tax code in workspace)
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Unmatched Tax Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemCode = "SVC-02",
                    description = "Special Service",
                    quantity = 1m,
                    unitPrice = 200m,
                    discountPercent = 0m,
                    taxRate = 12.5m
                }
            }
        }), HttpStatusCode.Created);

        var orderId = (string)orderRes["id"]!;

        // 2. Confirm order
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        // 3. Convert to invoice -> expected 409 Conflict
        var convertRes = await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null);
        Assert.Equal(HttpStatusCode.Conflict, convertRes.StatusCode);
        var err = await convertRes.Content.ReadAsStringAsync();
        Assert.Contains("No active tax code found", err);

        // 4. Verify order is still Confirmed and has no invoice id
        var orderAfter = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("confirmed", (string)orderAfter["status"]!, ignoreCase: true);
        Assert.Null(orderAfter["invoicedInvoiceId"]);

        // 5. Verify no invoice with sourceId == orderId exists
        var invoicesList = await Ok(await s.C.GetAsync($"{Fin}/invoices"));
        if (invoicesList is JsonArray items)
        {
            Assert.DoesNotContain(items, item => string.Equals((string?)item?["sourceId"], orderId, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Foreign_currency_sales_order_returns_conflict_and_creates_no_invoice()
    {
        var s = await NewAsync();

        // Legal entity base currency is AED. Create order in USD.
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "USD Buyer",
            orderDate = "2026-06-01",
            currency = "USD",
            lines = new[]
            {
                new
                {
                    itemCode = "SVC-03",
                    description = "USD Service",
                    quantity = 1m,
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 5m
                }
            }
        }), HttpStatusCode.Created);

        var orderId = (string)orderRes["id"]!;

        // Confirm order
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        // Convert to invoice -> expected 409 Conflict
        var convertRes = await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null);
        Assert.Equal(HttpStatusCode.Conflict, convertRes.StatusCode);
        var err = await convertRes.Content.ReadAsStringAsync();
        Assert.Contains("Foreign-currency sales orders cannot be invoiced yet", err);

        // Verify order is still Confirmed and has no invoice id
        var orderAfter = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("confirmed", (string)orderAfter["status"]!, ignoreCase: true);
        Assert.Null(orderAfter["invoicedInvoiceId"]);
    }

    [Fact]
    public async Task Editing_delivered_cancelled_or_invoiced_order_returns_conflict_and_changes_nothing()
    {
        var s = await NewAsync();

        // 1. Order to be Delivered
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Original Name",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 2m, unitPrice = 50m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;
        var lineId = (string)orderRes["lines"]!.AsArray()[0]!["id"]!;

        // Confirm then fulfill all -> status becomes Delivered
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId, deliveredQuantity = 2m } }
        }));
        var deliveredOrder = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("delivered", (string)deliveredOrder["status"]!, ignoreCase: true);

        // Edit on Delivered order -> 409
        var editDelivered = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Hacked Name",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 2m, unitPrice = 50m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, editDelivered.StatusCode);

        // Verify order was not changed
        var orderCheck1 = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("Original Name", (string)orderCheck1["customerName"]!);

        // 2. Cancelled order
        var orderRes2 = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Original Name 2",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 1m, unitPrice = 50m } }
        }), HttpStatusCode.Created);
        var orderId2 = (string)orderRes2["id"]!;
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId2}/status", new { status = "Cancelled" }));

        // Edit on Cancelled order -> 409
        var editCancelled = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId2}", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Hacked Name 2",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 1m, unitPrice = 50m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, editCancelled.StatusCode);

        // 3. Invoiced order
        var orderRes3 = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Original Name 3",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 1m, unitPrice = 50m, taxRate = 5m } }
        }), HttpStatusCode.Created);
        var orderId3 = (string)orderRes3["id"]!;
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId3}/status", new { status = "Confirmed" }));
        await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId3}/convert-to-invoice", null));

        // Edit on Invoiced order -> 409
        var editInvoiced = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId3}", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Hacked Name 3",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ITM-01", description = "Item 1", quantity = 1m, unitPrice = 50m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, editInvoiced.StatusCode);
    }

    [Fact]
    public async Task Fulfillment_on_draft_or_cancelled_order_returns_conflict_and_issues_no_stock()
    {
        var s = await NewAsync();
        await s.C.PostAsync("/api/v1/modules/inventory/enable", null);

        // Create product item
        var itemRes = await Ok(await s.C.PostAsJsonAsync("/api/v1/items", new
        {
            sku = "PROD-" + Guid.NewGuid().ToString("N")[..6],
            name = "Test Product",
            type = "product",
            uom = "EA",
            isActive = true
        }), HttpStatusCode.Created);
        var itemId = (string)itemRes["id"]!;

        // 1. Draft order
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Draft Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemId, itemCode = "PROD-01", description = "Prod 1", quantity = 5m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;
        var lineId = (string)orderRes["lines"]!.AsArray()[0]!["id"]!;

        var movesBefore = (await Ok(await s.C.GetAsync($"/api/v1/m/inventory/movements?pageSize=100&filter=itemId:eq:{itemId}"))).AsArray().Count;

        // Fulfillment on Draft -> 409
        var draftFul = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId, deliveredQuantity = 3m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, draftFul.StatusCode);

        var movesAfterDraft = (await Ok(await s.C.GetAsync($"/api/v1/m/inventory/movements?pageSize=100&filter=itemId:eq:{itemId}"))).AsArray().Count;
        Assert.Equal(movesBefore, movesAfterDraft);

        // 2. Cancelled order
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Cancelled" }));
        var cancelFul = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId, deliveredQuantity = 3m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, cancelFul.StatusCode);

        var movesAfterCancel = (await Ok(await s.C.GetAsync($"/api/v1/m/inventory/movements?pageSize=100&filter=itemId:eq:{itemId}"))).AsArray().Count;
        Assert.Equal(movesBefore, movesAfterCancel);
    }

    [Fact]
    public async Task Lowering_delivered_quantity_returns_bad_request_and_issues_no_stock()
    {
        var s = await NewAsync();
        await s.C.PostAsync("/api/v1/modules/inventory/enable", null);

        var itemRes = await Ok(await s.C.PostAsJsonAsync("/api/v1/items", new
        {
            sku = "PROD-" + Guid.NewGuid().ToString("N")[..6],
            name = "Test Product 2",
            type = "product",
            uom = "EA",
            isActive = true
        }), HttpStatusCode.Created);
        var itemId = (string)itemRes["id"]!;

        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Fulfillment Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemId, itemCode = "PROD-02", description = "Prod 2", quantity = 10m, unitPrice = 50m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;
        var lineId = (string)orderRes["lines"]!.AsArray()[0]!["id"]!;

        // Confirm
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        // Initial fulfillment of 4 units -> 200
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId, deliveredQuantity = 4m } }
        }));

        var movesCount = (await Ok(await s.C.GetAsync($"/api/v1/m/inventory/movements?pageSize=100&filter=itemId:eq:{itemId}"))).AsArray().Count;

        // Attempt lowering delivered quantity to 2 -> 400 Bad Request
        var lowerFul = await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId, deliveredQuantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, lowerFul.StatusCode);

        // Verify stock movements count is unchanged and deliveredQuantity remains 4
        var movesAfter = (await Ok(await s.C.GetAsync($"/api/v1/m/inventory/movements?pageSize=100&filter=itemId:eq:{itemId}"))).AsArray().Count;
        Assert.Equal(movesCount, movesAfter);

        var orderCheck = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        var lineCheck = orderCheck["lines"]!.AsArray()[0]!;
        Assert.Equal(4m, (decimal)lineCheck["deliveredQuantity"]!);
    }

    [Fact]
    public async Task Invoice_due_date_uses_customer_payment_terms_days()
    {
        var s = await NewAsync();

        // Customer was created with paymentTermsDays = 15 in NewAsync()
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Terms Buyer",
            orderDate = "2026-06-01",
            expectedDeliveryDate = "2026-06-05", // Delivery date should NOT be used as invoice due date
            currency = "AED",
            lines = new[] { new { itemCode = "SRV-10", description = "Consulting", quantity = 1m, unitPrice = 100m, taxRate = 5m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        var invoiceRes = await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));

        var invoiceDateStr = (string)invoiceRes["invoiceDate"]!;
        var dueDateStr = (string)invoiceRes["dueDate"]!;

        var invoiceDate = DateOnly.Parse(invoiceDateStr);
        var dueDate = DateOnly.Parse(dueDateStr);

        // Due date must equal invoice date + 15 days
        Assert.Equal(invoiceDate.AddDays(15), dueDate);
    }

    [Fact]
    public async Task Voiding_the_generated_invoice_reopens_the_order_and_allows_invoicing_again()
    {
        var s = await NewAsync();
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Reopen Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "SRV-20", description = "Consulting", quantity = 1m, unitPrice = 100m, taxRate = 5m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));

        var first = await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));
        var firstId = (string)first["id"]!;
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{firstId}/issue", null));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{firstId}/void", new { reason = "Wrong price" }));

        var reopened = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.NotEqual("invoiced", ((string)reopened["status"]!).ToLowerInvariant());
        Assert.Null(reopened["invoicedInvoiceId"]);
        Assert.All(reopened["lines"]!.AsArray(), l => Assert.Equal(0m, (decimal)l!["invoicedQuantity"]!));

        var second = await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));
        Assert.NotEqual(firstId, (string)second["id"]!);
        var again = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("invoiced", (string)again["status"]!, ignoreCase: true);
        Assert.Equal((string)second["id"]!, (string)again["invoicedInvoiceId"]!);
    }

    [Fact]
    public async Task Voiding_an_invoice_without_a_source_leaves_orders_alone()
    {
        var s = await NewAsync();
        var orderRes = await Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = s.Entity,
            customerPartyId = s.Customer,
            customerName = "Other Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "SRV-21", description = "Consulting", quantity = 1m, unitPrice = 100m, taxRate = 5m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;
        await Ok(await s.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/status", new { status = "Confirmed" }));
        var inv = await Ok(await s.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));

        var manual = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new
        {
            legalEntityId = s.Entity,
            partyId = s.Customer,
            invoiceDate = "2026-06-01",
            lines = new[] { new { description = "Manual", quantity = 1m, price = 50m } }
        }), HttpStatusCode.Created);
        var manualId = (string)manual["id"]!;
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{manualId}/issue", null));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{manualId}/void", new { reason = "Entered twice" }));

        var order = await Ok(await s.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("invoiced", (string)order["status"]!, ignoreCase: true);
        Assert.Equal((string)inv["id"]!, (string)order["invoicedInvoiceId"]!);
    }
}
