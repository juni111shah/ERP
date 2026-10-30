using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Credit notes cannot credit more of an invoice line than was invoiced, counting the invoice's other non-voided credit notes (TASK-130).</summary>
public class CreditNoteLineCapTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Invoice, string Cheap, string Dear);

    /// <summary>An issued invoice with a cheap line (5 x 10) and an expensive line (5 x 100).</summary>
    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Cap " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new
        {
            legalEntityId = entity, partyId = customer, invoiceDate = "2026-06-01",
            lines = new[] { new { description = "Cheap", quantity = 5m, price = 10m }, new { description = "Dear", quantity = 5m, price = 100m } },
        }), HttpStatusCode.Created);
        var issued = await Ok(await c.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null), HttpStatusCode.OK);
        var lines = issued["lines"]!.AsArray();
        string Line(string description) => (string)lines.First(l => (string?)l!["description"] == description)!["id"]!;
        return new Setup(c, (string)issued["id"]!, Line("Cheap"), Line("Dear"));
    }

    private static object Credit(string line, decimal quantity) => new { lines = new[] { new { invoiceLineId = line, quantity } } };

    [Fact]
    public async Task Crediting_a_line_in_full_twice_is_rejected_the_second_time()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 5m)), HttpStatusCode.Created);
        var second = await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 5m));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("Cheap", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Partial_credits_add_up_to_the_line_quantity_and_no_further()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 2m)), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 3m)), HttpStatusCode.Created);
        var over = await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 4m));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
    }

    [Fact]
    public async Task Voiding_a_credit_note_frees_its_quantity_again()
    {
        var s = await NewAsync();
        var first = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 5m)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 1m))).StatusCode);

        var id = (string)first["id"]!;
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{id}/issue", null), HttpStatusCode.OK);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{id}/void", new { reason = "Entered in error" }), HttpStatusCode.OK);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 5m)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task With_no_lines_given_only_the_uncredited_remainder_is_proposed()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 2m)), HttpStatusCode.Created);
        var rest = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", new { }), HttpStatusCode.Created);
        var quantities = rest["lines"]!.AsArray().ToDictionary(l => (string)l!["description"]!, l => (decimal)l!["quantity"]!);
        Assert.Equal(3m, quantities["Cheap"]);
        Assert.Equal(5m, quantities["Dear"]);
    }

    [Fact]
    public async Task When_everything_is_credited_a_full_credit_note_is_refused()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", new { }), HttpStatusCode.Created);
        var again = await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", new { });
        Assert.True(again.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, again.StatusCode.ToString());
    }

    [Fact]
    public async Task Editing_a_draft_credit_note_cannot_get_around_the_line_cap()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 4m)), HttpStatusCode.Created);
        var second = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{s.Invoice}/credit-note", Credit(s.Cheap, 1m)), HttpStatusCode.Created);
        var id = (string)second["id"]!;
        var body = new Dictionary<string, object?>
        {
            ["legalEntityId"] = (string)second["legalEntityId"]!, ["partyId"] = (string)second["partyId"]!, ["invoiceDate"] = "2026-06-02", ["isCreditNote"] = true,
            ["lines"] = new[] { new { description = "Cheap", quantity = 3m, price = 10m } },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PutAsJsonAsync($"{Fin}/invoices/{id}", body)).StatusCode);
    }
}
