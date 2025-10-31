using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

// Parse command line arguments
if (args.Length < 2)
{
    Console.WriteLine("Usage: AgenticStructuredOutput <schema.json> <input.json|json-string>");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json input.json");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json '{\"name\":\"John\"}'");
    Console.WriteLine();
    Console.WriteLine("Note: Set OPENAI_API_KEY environment variable");
    return 1;
}

var schemaPath = args[0];
var inputArg = args[1];

// Load the JSON schema
string schemaJson;
try
{
    schemaJson = await File.ReadAllTextAsync(schemaPath);
}
catch (FileNotFoundException)
{
    Console.WriteLine($"Error: Schema file not found: {schemaPath}");
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Failed to load schema - {ex.Message}");
    return 1;
}

// Load input JSON (from file or string literal)
string inputJson;
if (File.Exists(inputArg))
{
    inputJson = await File.ReadAllTextAsync(inputArg);
}
else
{
    inputJson = inputArg;
}

// Validate JSON inputs
try
{
    JsonDocument.Parse(schemaJson);
    JsonDocument.Parse(inputJson);
}
catch (JsonException ex)
{
    Console.WriteLine($"Error: Invalid JSON - {ex.Message}");
    return 1;
}

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Error: No API key found. Set OPENAI_API_KEY environment variable.");
    return 1;
}

// Create Semantic Kernel with the mapping agent
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini",
    apiKey: apiKey
);

var kernel = builder.Build();
var chatService = kernel.GetRequiredService<IChatCompletionService>();

// Create the mapping agent with expert instructions  
var agentInstruction = """
You are an expert in data mapping and structured output transformation.
Your task is to intelligently map JSON input to a target schema using fuzzy logic and inference.

Instructions:
- Use intelligent inference to map input fields to schema fields
- Apply fuzzy matching when field names don't exactly match (e.g., "fullName" could map to "name")
- Infer appropriate data types based on schema requirements
- Fill in reasonable defaults or null values for missing required fields when possible
- Handle nested structures intelligently
- Preserve data structure and relationships
- Return ONLY valid JSON that conforms to the schema, with no additional explanation
""";

var userPrompt = $$$"""
Target Schema:
{{{schemaJson}}}

Input Data:
{{{inputJson}}}

Output the mapped JSON structure now:
""";

try
{
    // Create chat history with agent instruction
    var chatHistory = new ChatHistory(agentInstruction);
    chatHistory.AddUserMessage(userPrompt);
    
    // Get response from the mapping agent
    var response = await chatService.GetChatMessageContentAsync(chatHistory);
    var content = response.Content?.Trim() ?? "";
    
    // Clean up potential markdown code blocks
    if (content.StartsWith("```json"))
    {
        content = content.Substring(7);
    }
    if (content.StartsWith("```"))
    {
        content = content.Substring(3);
    }
    if (content.EndsWith("```"))
    {
        content = content.Substring(0, content.Length - 3);
    }
    content = content.Trim();
    
    // Validate the output is valid JSON
    JsonDocument.Parse(content);
    
    // Pretty print the output
    var jsonDoc = JsonDocument.Parse(content);
    var options = new JsonSerializerOptions { WriteIndented = true };
    Console.WriteLine(JsonSerializer.Serialize(jsonDoc, options));
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Agent failed to map data - {ex.Message}");
    return 1;
}
