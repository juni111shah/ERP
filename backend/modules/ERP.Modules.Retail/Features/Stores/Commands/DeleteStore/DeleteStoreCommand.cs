using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Deletes a retail store.</summary>
public record DeleteStoreCommand(Guid Id) : ICommand;
