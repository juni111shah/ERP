using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.Crm.Domain;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Crm.Features;

/// <summary>Adds a note to a customer's activity log, with the same command as the Activity logs screen.</summary>
internal sealed class LogNoteAssistantAction(ISender sender, IValidator<LogInteractionCommand> validator, AssistantScope scope, IReadRepository<Party> parties, ICurrentUser user) : IAssistantAction
{
    public string Key => "log_customer_note";
    public string Description => "Add a note to a customer's activity log. Find the customer's id with list_records first.";
    public string ParametersJsonSchema => """{"type":"object","properties":{"customer_id":{"type":"string"},"subject":{"type":"string"},"details":{"type":"string"}},"required":["customer_id","subject"]}""";
    public string? ModuleKey => CrmModule.Key;
    public string Permission => Permissions.InteractionsManage;

    private async Task<(LogInteractionCommand Command, string CustomerName)> BuildAsync(JsonElement a, CancellationToken ct)
    {
        var party = a.Id("customer_id") is { } id ? await parties.FindAsync(id, ct) : null;
        if (party is null) throw new ConflictException("That customer could not be found.");
        var entity = await scope.LegalEntityAsync(ct);
        return (new LogInteractionCommand(entity.Id, "Customer", party.Id, InteractionType.Note, a.Text("subject") ?? "", a.Text("details"), null, user.UserId, null, null, true, null), party.Name);
    }

    public async Task<AssistantActionPreview> PreviewAsync(JsonElement arguments, CancellationToken ct)
    {
        var (command, customer) = await BuildAsync(arguments, ct);
        await validator.EnsureValidAsync(command, ct);
        var lines = new List<AssistantActionLine> { new("Customer", customer), new("Subject", command.Subject) };
        if (command.Details is { } details) lines.Add(new("Details", details));
        return new("Add a note to the activity log", lines);
    }

    public async Task<AssistantActionResult> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var (command, customer) = await BuildAsync(arguments, ct);
        await sender.Send(command, ct);
        return new($"The note was added for {customer}.", "/crm/interactions");
    }
}
