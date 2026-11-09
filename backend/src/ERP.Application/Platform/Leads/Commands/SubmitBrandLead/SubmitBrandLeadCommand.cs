using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>A visitor of the public assistant asks to be contacted. The founder gets an email.</summary>
public sealed record SubmitBrandLeadCommand(BrandLeadInput Input) : ICommand;
