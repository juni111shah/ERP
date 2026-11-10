using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>One retainer; null when it does not exist.</summary>
public record GetRetainerByIdQuery(Guid Id) : IQuery<RetainerDto?>;
