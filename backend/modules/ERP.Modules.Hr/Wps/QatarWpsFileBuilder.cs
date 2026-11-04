using ERP.Application.Common;

namespace ERP.Modules.Hr.Wps;

/// <summary>Placeholder for the Qatar salary file. The layout must come from the paying bank's template, so nothing is guessed here.</summary>
public sealed class QatarWpsFileBuilder : IWpsFileBuilder
{
    public string CountryCode => "QA";

    public string Build(WpsInput input) => throw new ConflictException("The Qatar salary file layout has not been configured. Provide the bank template.");
}
