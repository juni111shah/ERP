using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Emailing issued documents and statements: recipient rules, status rules, not-configured, and the message log.</summary>
public class DocumentEmailTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private sealed class FakeSender(bool enabled, bool fail = false) : IEmailSender
    {
        public bool IsEnabled => enabled;
        public List<(string To, string Subject, string Text, string? Html)> Sent { get; } = [];
        public Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
        {
            if (fail) throw new InvalidOperationException("550 mailbox unavailable");
            lock (Sent) Sent.Add((to, subject, bodyText, bodyHtml));
            return Task.CompletedTask;
        }
    }

    private WebApplicationFactory<Program> With(FakeSender fake) => f.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IEmailSender>(); s.AddSingleton<IEmailSender>(fake); }));

    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password });
        var body = ApiFactory.Unwrap(JsonNode.Parse(await login.Content.ReadAsStringAsync())!);
        var token = (string?)body["accessToken"];
        if (token is null)
        {
            var sel = await client.PostAsJsonAsync("/api/v1/auth/select-workspace", new { selectionToken = (string)body["selectionToken"]!, workspaceId = (string)body["workspaces"]![0]!["id"]! });
            token = (string)ApiFactory.Unwrap(JsonNode.Parse(await sel.Content.ReadAsStringAsync())!)["accessToken"]!;
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string PartyId, string Bank);

    private static async Task<Ctx> NewAsync(HttpClient c, string? email)
    {
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Mail " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var party = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Mail Customer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", email }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        return new Ctx(c, entity, party, (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!);
    }

    private static async Task<string> DraftAsync(Ctx x) => (string)(await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = x.PartyId, invoiceDate = "2026-06-01", lines = new[] { new { description = "Consulting <b>hours</b>", quantity = 2m, price = 100m } } }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> IssuedAsync(Ctx x)
    {
        var id = await DraftAsync(x);
        await Ok(await x.C.PostAsync($"{Fin}/invoices/{id}/issue", null));
        return id;
    }

    [Fact]
    public async Task An_issued_invoice_is_emailed_to_the_party_and_logged_with_escaped_html()
    {
        var fake = new FakeSender(true);
        var app = With(fake);
        var c = await AdminAsync(app);
        var x = await NewAsync(c, "billing@customer.example");
        var id = await IssuedAsync(x);

        var sent = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices/{id}/email", new { message = "Thanks for your business" }));
        Assert.Equal("billing@customer.example", (string)sent["to"]!);
        var mail = Assert.Single(fake.Sent);
        Assert.Contains("Invoice INV", mail.Subject);
        Assert.Contains("Thanks for your business", mail.Text);
        Assert.Contains("200.00 AED", mail.Text);
        Assert.Contains("Consulting &lt;b&gt;hours&lt;/b&gt;", mail.Html); // user text is HTML-encoded

        var log = (await Ok(await c.GetAsync("/api/v1/messages?pageSize=100"))).AsArray();
        Assert.Contains(log, m => (string)m!["channel"]! == "email" && (string)m["recipient"]! == "billing@customer.example" && (string)m["status"]! == "sent");

        // a different recipient can be chosen, and a second send is a second log row
        await Ok(await c.PostAsJsonAsync($"{Fin}/invoices/{id}/email", new { to = "other@customer.example" }));
        Assert.Equal(2, fake.Sent.Count);
    }

    [Fact]
    public async Task Recipient_status_and_configuration_rules_are_enforced()
    {
        var fake = new FakeSender(true);
        var app = With(fake);
        var c = await AdminAsync(app);
        var noMail = await NewAsync(c, null);
        var issued = await IssuedAsync(noMail);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Fin}/invoices/{issued}/email", new { })).StatusCode); // no party email, none supplied
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Fin}/invoices/{issued}/email", new { to = "not-an-email" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"{Fin}/invoices/{issued}/email", new { to = "typed@example.com" })).StatusCode);

        var draft = await DraftAsync(noMail);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Fin}/invoices/{draft}/email", new { to = "typed@example.com" })).StatusCode); // not issued
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"{Fin}/invoices/{Guid.NewGuid()}/email", new { to = "typed@example.com" })).StatusCode);

        var off = With(new FakeSender(false));
        var cOff = await AdminAsync(off);
        var conflict = await cOff.PostAsJsonAsync($"{Fin}/invoices/{issued}/email", new { to = "typed@example.com" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("not configured", await conflict.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().PostAsJsonAsync($"{Fin}/invoices/{issued}/email", new { to = "a@b.co" })).StatusCode);
    }

    [Fact]
    public async Task A_failed_send_is_reported_and_recorded_as_failed()
    {
        var app = With(new FakeSender(true, fail: true));
        var c = await AdminAsync(app);
        var x = await NewAsync(c, "down@customer.example");
        var id = await IssuedAsync(x);
        var res = await c.PostAsJsonAsync($"{Fin}/invoices/{id}/email", new { });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var log = (await Ok(await c.GetAsync("/api/v1/messages?pageSize=100"))).AsArray();
        Assert.Contains(log, m => (string)m!["recipient"]! == "down@customer.example" && (string)m["status"]! == "failed");
    }

    [Fact]
    public async Task Receipts_and_party_statements_can_be_emailed()
    {
        var fake = new FakeSender(true);
        var app = With(fake);
        var c = await AdminAsync(app);
        var x = await NewAsync(c, "ar@customer.example");
        var invoice = await IssuedAsync(x);
        var receipt = await Ok(await c.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = x.Entity, partyId = x.PartyId, date = "2026-06-05", method = "bankTransfer", bankAccountId = x.Bank, amount = 50m, autoAllocate = true, reference = "TRF-9" }), HttpStatusCode.Created);

        await Ok(await c.PostAsJsonAsync($"{Fin}/receipts/{(string)receipt["id"]!}/email", new { }));
        Assert.Contains(fake.Sent, m => m.Subject.StartsWith("Receipt ") && m.Text.Contains("TRF-9"));

        await Ok(await c.PostAsJsonAsync($"/api/v1/parties/{x.PartyId}/statement/email?asOf=2026-07-01", new { message = "Please settle" }));
        var statement = fake.Sent.Last();
        Assert.StartsWith("Statement of account as of 2026-07-01", statement.Subject);
        Assert.Contains("Balance", statement.Text);
        Assert.Contains("Please settle", statement.Text);
        _ = invoice;
    }
}
