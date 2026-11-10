using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>Deletes unapproved time and takes it off the engagement's consumption.</summary>
public record DeleteTimeEntryCommand(Guid Id) : ICommand;
