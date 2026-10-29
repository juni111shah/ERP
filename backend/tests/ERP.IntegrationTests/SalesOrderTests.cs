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
}
