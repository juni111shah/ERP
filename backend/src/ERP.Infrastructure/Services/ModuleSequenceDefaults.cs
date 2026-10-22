using ERP.Application.Common;
using ERP.Application.Templates;
using ERP.Modularity;

namespace ERP.Infrastructure.Services;

/// <summary>Default number sequences declared by installed modules, so a module enabled after onboarding can still number its documents.</summary>
public sealed class ModuleSequenceDefaults(ModuleRegistry modules) : ISequenceDefaults
{
    public SequenceTemplate? Find(string documentType)
        => modules.Modules.SelectMany(m => m.IndustryTemplates).SelectMany(t => t.Sequences ?? []).FirstOrDefault(s => s.DocumentType == documentType);
}
