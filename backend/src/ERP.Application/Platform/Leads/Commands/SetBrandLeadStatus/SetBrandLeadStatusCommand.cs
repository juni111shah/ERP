using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Moves a lead along: new, contacted, won or lost.</summary>
public sealed record SetBrandLeadStatusCommand(Guid Id, BrandLeadStatus Status) : ICommand<BrandLeadRow>;
