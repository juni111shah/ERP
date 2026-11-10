using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>Internal: the name of a customer record.</summary>
public sealed record LookupCustomerNameQuery(Guid PartyId) : IQuery<string?>;
