using AgenticStructuredOutput.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgenticStructuredOutput.CLI;

internal sealed class SchemaMappingRunner(
    IAgentFactory agentFactory,
    ILogger<SchemaMappingRunner> logger,
    IFileProvider schemaFileProvider)
{
    private readonly IAgentFactory _agentFactory = agentFactory;
    private readonly ILogger<SchemaMappingRunner> _logger = logger;
    private readonly IFileProvider _schemaFileProvider = schemaFileProvider;

    public async Task<int> RunAsync(string[] args)
    {
        if (MappingOptions.ShouldShowHelp(args))
        {
            MappingOptions.PrintUsage(_logger);
            return 0;
        }

        if (!MappingOptions.TryParse(args, out var options, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogError(error);
            }

            MappingOptions.PrintUsage(_logger);
            return 1;
        }

        try
        {
            _logger.LogInformation("Schema: {SchemaPath}", options!.SchemaPath);
            _logger.LogInformation("Input:  {InputPath}", options.InputPath);

            var schemaJson = await ReadSchemaAsync(options.SchemaPath, CancellationToken.None);
            if (schemaJson is null)
            {
                return 1;
            }
            var compiledSchema = JsonSchema.FromText(schemaJson);
            using var schemaDoc = JsonDocument.Parse(schemaJson);
            var schemaElement = schemaDoc.RootElement.Clone();

            var inputJson = await File.ReadAllTextAsync(options.InputPath);
            if (!TryNormalizeJson(inputJson, out var normalizedInput, out _))
            {
                _logger.LogError("Input file does not contain valid JSON");
                return 1;
            }

            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema: schemaElement,
                    schemaName: "DynamicOutput",
                    schemaDescription: "Intelligently mapped JSON output conforming to the provided schema")
            };

            var agent = await _agentFactory.CreateDataMappingAgentAsync(chatOptions);
            var response = await agent.RunAsync(BuildUserPrompt(normalizedInput, schemaJson));
            var rawOutput = response?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                _logger.LogError("Agent returned an empty response");
                return 1;
            }

            if (!TryNormalizeJson(rawOutput, out var normalizedOutput, out var outputNode))
            {
                _logger.LogError("Agent response was not valid JSON");
                return 1;
            }

            var evaluation = compiledSchema.Evaluate(outputNode!);
            if (!evaluation.IsValid)
            {
                var errorMessage = evaluation.Errors is { Count: > 0 }
                    ? string.Join("; ", evaluation.Errors.Select(pair => $"{pair.Key}: {pair.Value}"))
                    : "Schema validation failed";
                _logger.LogError("Agent response failed schema validation: {Error}", errorMessage);
                return 1;
            }

            var formattedOutput = JsonNode.Parse(normalizedOutput)!
                .ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                var outputDirectory = Path.GetDirectoryName(options.OutputPath);
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                await File.WriteAllTextAsync(options.OutputPath, formattedOutput);
                _logger.LogInformation("✓ Mapped output saved to {OutputPath}", options.OutputPath);
            }
            else
            {
                Console.WriteLine(formattedOutput);
            }

            return 0;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON content: {Message}", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mapping failed: {Message}", ex.Message);
            return 1;
        }
    }

    private static string BuildUserPrompt(string normalizedInput, string schemaJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Map the following input JSON to the target schema.");
        builder.AppendLine("Return only valid JSON that conforms to the schema. Do not include commentary.");
        builder.AppendLine();
        builder.AppendLine("Target schema:");
        builder.AppendLine(schemaJson);
        builder.AppendLine();
        builder.AppendLine("Input JSON:");
        builder.AppendLine(normalizedInput);
        return builder.ToString();
    }

    private static bool TryNormalizeJson(string json, out string normalized, out JsonNode? node)
    {
        normalized = string.Empty;
        node = null;

        try
        {
            node = JsonNode.Parse(json);
            if (node is null)
            {
                return false;
            }

            normalized = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record MappingOptions(string SchemaPath, string InputPath, string? OutputPath)
    {
        public static bool ShouldShowHelp(string[] args)
        {
            return args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryParse(string[] args, out MappingOptions? options, out string? error)
        {
            options = null;
            error = null;

            if (args.Length == 0)
            {
                error = "Missing arguments";
                return false;
            }

            var schemaPath = GetArgumentValue(args, "--schema", "-s");
            var inputPath = GetArgumentValue(args, "--input", "-i");
            var outputPath = GetArgumentValue(args, "--output", "-o");

            if (string.IsNullOrWhiteSpace(schemaPath) || string.IsNullOrWhiteSpace(inputPath))
            {
                error = "--schema and --input arguments are required";
                return false;
            }

            schemaPath = Path.GetFullPath(schemaPath);
            inputPath = Path.GetFullPath(inputPath);
            outputPath = string.IsNullOrWhiteSpace(outputPath) ? null : Path.GetFullPath(outputPath);

            if (!File.Exists(inputPath))
            {
                error = $"Input file not found: {inputPath}";
                return false;
            }

            options = new MappingOptions(schemaPath, inputPath, outputPath);
            return true;
        }

        public static void PrintUsage(ILogger logger)
        {
            logger.LogInformation("Usage: dotnet run --project AgenticStructuredOutput.CLI -- --schema <schema.json> --input <input.json> [--output <output.json>]");
            logger.LogInformation("  --schema, -s   Path to the target JSON schema file");
            logger.LogInformation("  --input,  -i   Path to the source JSON document to map");
            logger.LogInformation("  --output, -o   Optional path to save the mapped output (defaults to stdout)");
            logger.LogInformation("  --help,   -h   Show this help message");
        }

        private static string? GetArgumentValue(string[] args, string longName, string shortName)
        {
            var index = Array.FindIndex(args, arg => string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1];
            }

            return null;
        }
    }

    private async Task<string?> ReadSchemaAsync(string schemaPath, CancellationToken cancellationToken)
    {
        var providerPath = NormalizeForProvider(schemaPath);
        var fileInfo = _schemaFileProvider.GetFileInfo(providerPath);
        if (!fileInfo.Exists)
        {
            _logger.LogError("Schema file not found: {SchemaPath}", schemaPath);
            return null;
        }

        await using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string NormalizeForProvider(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root))
        {
            fullPath = fullPath[root.Length..];
        }

        return fullPath
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
    }
}
