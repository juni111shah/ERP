namespace ERP.Api.Controllers.Finance;

/// <summary>Body of the clear endpoints of the cheque register (received and issued cheques): the date the bank cleared it.</summary>
public sealed record ClearChequeRequest(DateOnly Date);
