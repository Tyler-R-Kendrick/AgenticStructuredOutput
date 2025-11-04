using Microsoft.Extensions.FileProviders;

namespace AgenticStructuredOutput.Services;

/// <summary>
/// Static agent instruction resources loaded from embedded markdown files.
/// These are embedded at compile time and used to configure agent behavior.
/// </summary>
public static class AgentInstructions
{
    private static readonly Lazy<string> _dataMappingExpertLazy = new(LoadDataMappingExpert);

    /// <summary>
    /// Instructions for the data mapping expert agent - handles intelligent JSON transformation.
    /// Loaded from embedded markdown resource: agent-instructions.md
    /// </summary>
    public static string DataMappingExpert => _dataMappingExpertLazy.Value;

    /// <summary>
    /// Loads the data mapping expert instructions from the embedded markdown resource.
    /// </summary>
    private static string LoadDataMappingExpert()
    {
        var assembly = typeof(AgentInstructions).Assembly;
        var fileProvider = new EmbeddedFileProvider(assembly);
        
        var fileInfo = fileProvider.GetFileInfo("Resources/agent-instructions.md");
        if (!fileInfo.Exists)
        {
            throw new InvalidOperationException("Could not find embedded resource: Resources/agent-instructions.md");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
