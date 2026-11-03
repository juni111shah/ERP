using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary><c>GET /api/v1/print/{kind}/{id}/pdf</c>: a real PDF for every print document, with the print data's auth and 404 rules.</summary>
public class PrintPdfTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private static readonly string[] Kinds = ["invoice", "receipt", "bill", "payment", "journal"];

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string Vendor, string Bank, string Expense);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Pdf " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Pdf Customer " + tag, kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Pdf Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var expense = (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!;
        return new Setup(c, entity, customer, vendor, bank, expense);
    }

    private static async Task<(byte[] Bytes, HttpResponseMessage Res)> PdfAsync(HttpClient c, string kind, string id)
    {
        var res = await c.GetAsync($"/api/v1/print/{kind}/{id}/pdf");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"{kind}: expected 200 but got {res.StatusCode}: {Encoding.UTF8.GetString(bytes)}");
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        return (bytes, res);
    }

    [Fact]
    public async Task Each_document_downloads_as_a_pdf_named_after_its_number()
    {
        var s = await NewAsync();

        var invoice = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = s.Entity, partyId = s.Customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Consulting", quantity = 2m, price = 100m } } }), HttpStatusCode.Created);
        var invoiceId = (string)invoice["id"]!;
        var issued = await Ok(await s.C.PostAsync($"{Fin}/invoices/{invoiceId}/issue", null));
        var (_, invRes) = await PdfAsync(s.C, "invoice", invoiceId);
        var disposition = invRes.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal($"Invoice-{(string)issued["number"]!}.pdf", disposition.FileNameStar ?? disposition.FileName?.Trim('"'));

        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-05", method = "bankTransfer", bankAccountId = s.Bank, amount = 200m, autoAllocate = true }), HttpStatusCode.Created);
        var (_, rctRes) = await PdfAsync(s.C, "receipt", (string)receipt["id"]!);
        Assert.StartsWith("Receipt-", rctRes.Content.Headers.ContentDisposition!.FileNameStar ?? rctRes.Content.Headers.ContentDisposition.FileName);

        var bill = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = s.Entity, partyId = s.Vendor, vendorReference = "PDF-" + Guid.NewGuid().ToString("N")[..6], billDate = "2026-06-01", lines = new[] { new { description = "Supplies", quantity = 1m, price = 80m, accountId = s.Expense } } }), HttpStatusCode.Created);
        await PdfAsync(s.C, "bill", (string)bill["id"]!);

        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 50m }), HttpStatusCode.Created);
        await PdfAsync(s.C, "payment", (string)payment["id"]!);

        var journal = await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date = "2026-06-12", memo = "Pdf test", lines = new[] { new { accountId = s.Expense, debit = 10m, credit = 0m }, new { accountId = s.Bank, debit = 0m, credit = 10m } } }), HttpStatusCode.Created);
        await PdfAsync(s.C, "journal", (string)journal["id"]!);
    }

    [Fact]
    public async Task An_unknown_id_is_a_404_for_every_kind()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        foreach (var kind in Kinds)
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/print/{kind}/{Guid.NewGuid()}/pdf")).StatusCode);
    }

    [Fact]
    public async Task A_signed_out_caller_gets_a_401()
    {
        var anonymous = f.CreateClient();
        foreach (var kind in Kinds)
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/print/{kind}/{Guid.NewGuid()}/pdf")).StatusCode);
    }

    [Fact]
    public void Each_pdf_route_requires_the_same_policies_as_its_print_data_route()
    {
        var endpoints = f.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        static string Policies(RouteEndpoint e) => string.Join(",", e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? "").Order());
        foreach (var kind in Kinds)
        {
            var data = endpoints.Single(e => e.RoutePattern.RawText == $"/api/v1/print/{kind}/{{id:guid}}");
            var pdf = endpoints.Single(e => e.RoutePattern.RawText == $"/api/v1/print/{kind}/{{id:guid}}/pdf");
            Assert.NotEmpty(Policies(pdf));
            Assert.Equal(Policies(data), Policies(pdf));
        }
    }
}
