using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Deletes a lead for good.</summary>
public sealed record DeleteBrandLeadCommand(Guid Id) : ICommand;
