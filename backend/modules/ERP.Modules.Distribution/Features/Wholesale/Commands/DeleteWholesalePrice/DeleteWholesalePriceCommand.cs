using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Deletes a wholesale price tier.</summary>
public record DeleteWholesalePriceCommand(Guid Id) : ICommand;
