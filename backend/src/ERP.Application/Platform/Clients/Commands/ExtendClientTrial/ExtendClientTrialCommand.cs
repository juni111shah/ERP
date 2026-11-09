using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Gives a trial (or a lapsed trial) more days.</summary>
public sealed record ExtendClientTrialCommand(Guid OrgId, int Days) : ICommand<ClientDetail>;
