using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Pays the agent and team lead shares from a bank account (Dr commission expense, Cr bank). Only after the client has paid.</summary>
public sealed record PayDealAgentCommand(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<DealDto>, IRequirePermission { public string RequiredPermission => P.DealsPayAgent; }
