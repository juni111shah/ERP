using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Bounced cheques: the receivable reopens, the cheque team is told once, a follow-up can be logged, and the replacement settles the same invoices again.</summary>
public class ChequeFollowUpTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Party, string Bank, string InvoiceId);

    private async Task<Setup> NewAsync(decimal total = 1000m)
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "BFU " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var party = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Payer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = party, invoiceDate = "2026-03-01", lines = new[] { new { description = "Rent", quantity = 1m, price = total } } }), HttpStatusCode.Created);
        var invoice = (string)(await Ok(await c.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)))["id"]!;
        return new Setup(c, entity, party, bank, invoice);
    }

    private static object Cheque(Setup s, string no, decimal amount = 1000m, bool allocate = true)
        => new
        {
            legalEntityId = s.Entity, partyId = s.Party, kind = "cheque", instrumentNo = no, bankName = "Emirates NBD", amount, instrumentDate = "2026-03-05", receivedOn = "2026-03-02",
            allocations = allocate ? new object[] { new { invoiceId = s.InvoiceId, amount } } : Array.Empty<object>(),
        };

    private static async Task<int> NotificationsAsync(HttpClient c, string title)
        => (await Ok(await c.GetAsync("/api/v1/notifications")))["items"]!.AsArray().Count(n => (string?)n!["title"] == title);

    [Fact]
    public async Task A_bounce_reopens_the_invoice_stores_the_date_and_tells_the_cheque_team_once()
    {
        var s = await NewAsync();
        var no = "B" + Guid.NewGuid().ToString("N")[..7];
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, no)), HttpStatusCode.Created))["id"]!;
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}")))["status"]!);

        var bounced = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "Insufficient funds" }));
        Assert.Equal("2026-03-08", (string)bounced["bouncedOn"]!);
        Assert.Null((string?)bounced["followUpOn"]);
        var invoice = await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}"));
        Assert.Equal(("issued", 1000m), ((string)invoice["status"]!, (decimal)invoice["outstanding"]!));
        Assert.Equal(1, await NotificationsAsync(s.C, $"Cheque {no} bounced"));

        // a second bounce is refused and does not notify again
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-09", reason = "Again" })).StatusCode);
        Assert.Equal(1, await NotificationsAsync(s.C, $"Cheque {no} bounced"));
    }

    [Fact]
    public async Task A_cheque_cannot_bounce_before_it_was_received()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "E" + Guid.NewGuid().ToString("N")[..7])), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-02-20", reason = "Too early" })).StatusCode);
        Assert.Equal("inCustody", (string)(await Ok(await s.C.GetAsync($"{Fin}/cheques/{id}")))["status"]!);
    }

    [Fact]
    public async Task A_follow_up_is_logged_only_on_a_bounced_cheque()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "F" + Guid.NewGuid().ToString("N")[..7])), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "Called the customer" })).StatusCode); // not bounced yet
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "Signature mismatch" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "" })).StatusCode);

        var logged = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "Customer promised a new cheque on Friday", date = "2026-03-10" }));
        Assert.Equal(("2026-03-10", "Customer promised a new cheque on Friday"), ((string)logged["followUpOn"]!, (string)logged["followUpNote"]!));
        var row = (await Ok(await s.C.GetAsync($"{Fin}/cheques?filter=partyId:eq:{s.Party}"))).AsArray().Single()!;
        Assert.Equal("2026-03-10", (string)row["followUpOn"]!);
        Assert.Equal("2026-03-08", (string)row["bouncedOn"]!);
    }

    [Fact]
    public async Task A_replacement_without_allocations_settles_the_same_invoices_again_and_is_linked()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "R" + Guid.NewGuid().ToString("N")[..7])), HttpStatusCode.Created))["id"]!;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "Account closed" }));
        Assert.Equal("issued", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}")))["status"]!);

        var replacement = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/replace", Cheque(s, "R" + Guid.NewGuid().ToString("N")[..7], allocate: false)));
        Assert.Equal(id, (string)replacement["replacesInstrumentId"]!);
        var allocation = Assert.Single(replacement["allocations"]!.AsArray())!;
        Assert.Equal(s.InvoiceId, (string)allocation["invoiceId"]!);
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}")))["status"]!);

        var old = await Ok(await s.C.GetAsync($"{Fin}/cheques/{id}"));
        Assert.Equal("replaced", (string)old["status"]!);
        Assert.Equal((string)replacement["id"]!, (string)old["replacedByChequeId"]!);
        // a replaced cheque can no longer be followed up
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "Too late" })).StatusCode);
    }

    [Fact]
    public async Task A_smaller_replacement_re_settles_only_what_it_can_cover()
    {
        var s = await NewAsync(1000m);
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "S" + Guid.NewGuid().ToString("N")[..7])), HttpStatusCode.Created))["id"]!;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "Returned" }));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/replace", Cheque(s, "S" + Guid.NewGuid().ToString("N")[..7], amount: 400m, allocate: false)));
        var invoice = await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}"));
        Assert.Equal(("partlyPaid", 600m), ((string)invoice["status"]!, (decimal)invoice["outstanding"]!));
    }

    [Fact]
    public async Task An_unchased_bounce_triggers_one_reminder_and_a_logged_follow_up_stops_it()
    {
        var s = await NewAsync();
        var unchasedNo = "U" + Guid.NewGuid().ToString("N")[..7];
        var unchased = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, unchasedNo)), HttpStatusCode.Created))["id"]!;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{unchased}/bounce", new { date = "2026-03-08", reason = "Returned" })); // long ago: the nudge is due
        var chasedNo = "C" + Guid.NewGuid().ToString("N")[..7];
        var s2 = await NewAsync();
        var chased = (string)(await Ok(await s2.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s2, chasedNo)), HttpStatusCode.Created))["id"]!;
        await Ok(await s2.C.PostAsJsonAsync($"{Fin}/cheques/{chased}/bounce", new { date = "2026-03-08", reason = "Returned" }));
        await Ok(await s2.C.PostAsJsonAsync($"{Fin}/cheques/{chased}/follow-up", new { note = "Spoke to the accountant" }));

        await Ok(await s.C.PostAsync("/api/v1/messages/send-reminders", null));
        await Ok(await s.C.PostAsync("/api/v1/messages/send-reminders", null));
        var subjects = (await Ok(await s.C.GetAsync("/api/v1/messages?pageSize=200"))).AsArray().Select(m => (string)m!["subject"]!).ToList();
        Assert.Equal(1, subjects.Count(x => x == $"Follow up bounced cheque {unchasedNo}"));
        Assert.DoesNotContain($"Follow up bounced cheque {chasedNo}", subjects);
    }

    [Fact]
    public async Task Bouncing_and_following_up_need_the_right_permissions()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "P" + Guid.NewGuid().ToString("N")[..7])), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "x" })).StatusCode);

        var roles = (await Ok(await s.C.GetAsync("/api/v1/roles"))).AsArray();
        var viewer = (string)roles.First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var email = $"ch{Guid.NewGuid():N}"[..10] + "@demo.local";
        (await s.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "View Only", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { viewer } })).EnsureSuccessStatusCode();
        var (viewerClient, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.PostAsJsonAsync($"{Fin}/cheques/{id}/follow-up", new { note = "x" })).StatusCode);
    }
}
