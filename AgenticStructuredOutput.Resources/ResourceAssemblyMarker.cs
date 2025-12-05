using System.Reflection;

namespace AgenticStructuredOutput.Resources;

/// <summary>
/// Provides access to the AgenticStructuredOutput.Resources assembly for embedded assets.
/// </summary>
public static class ResourceAssemblyMarker
{
    /// <summary>
    /// Gets the assembly that contains all embedded resources.
    /// </summary>
    public static Assembly Assembly { get; } = typeof(ResourceAssemblyMarker).Assembly;
}
