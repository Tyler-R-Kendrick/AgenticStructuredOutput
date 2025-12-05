using Microsoft.Extensions.FileProviders;

namespace AgenticStructuredOutput.Services;

/// <summary>
/// Provides access to the embedded default schema using the FileProviders abstraction.
/// </summary>
public static class EmbeddedSchemaLoader
{
    private const string ResourceNamespace = "AgenticStructuredOutput.Resources";
    private const string SchemaFileName = "schema.json";
    private static readonly EmbeddedFileProvider FileProvider =
        new(typeof(EmbeddedSchemaLoader).Assembly, ResourceNamespace);

    public static string LoadSchemaJson()
    {
        var schemaFile = FileProvider.GetFileInfo(SchemaFileName);
        if (!schemaFile.Exists)
        {
            throw new InvalidOperationException(
                $"Embedded schema file not found: {ResourceNamespace}.{SchemaFileName}");
        }

        using var stream = schemaFile.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
