using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// Parse command line arguments
if (args.Length < 2)
{
    Console.WriteLine("Usage: AgenticStructuredOutput <schema.json> <input.json|json-string>");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json input.json");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json '{\"firstName\":\"Alice\"}'");
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
    Console.WriteLine($"Error: No API key found. Set OPENAI_API_KEY environment variable.");
    return 1;
}

// Parse schema to get JSON schema element
var schemaDoc = JsonDocument.Parse(schemaJson);
JsonElement schemaElement = schemaDoc.RootElement;

// Create chat options with structured output using JSON schema
var chatOptions = new ChatOptions
{
    ResponseFormat = ChatResponseFormatJson.ForJsonSchema(
        schema: schemaElement,
        schemaName: "MappedOutput",
        schemaDescription: "Intelligently mapped JSON output conforming to the target schema"
    )
};

// Create the OpenAI client and agent using Microsoft Agent Framework
var openAIClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey));
var chatClient = openAIClient.GetChatClient(Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini");

// Create the AI Agent using Microsoft Agent Framework
var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    Name = "DataMappingExpert",
    Instructions = """
    You are an expert in data mapping and structured output transformation.
    Your task is to intelligently map JSON input to a target schema using fuzzy logic and inference.
    
    Key responsibilities:
    - Use intelligent inference to map input fields to schema fields
    - Apply fuzzy matching when field names don't exactly match (e.g., "fullName" → "name", "yearsOld" → "age")
    - Infer appropriate data types based on schema requirements
    - Fill in reasonable defaults or null values for missing fields when appropriate
    - Handle nested structures intelligently
    - Preserve data structure and relationships
    
    Always produce output that exactly conforms to the provided schema.
    """,
    ChatOptions = chatOptions
});

// Create the user prompt
var userPrompt = $"""
Map the following input JSON to the target schema using intelligent inference and fuzzy logic:

Input Data:
{inputJson}

Target Schema:
{schemaJson}

Map the fields intelligently, using fuzzy matching for field names and type inference as needed.
""";

try
{
    // Run the agent to get structured output
    var response = await agent.RunAsync(userPrompt);
    
    // Extract the content from the agent response
    var content = response.ToString().Trim();
    
    // Parse and validate the output
    var outputDoc = JsonDocument.Parse(content);
    
    // Pretty print the output
    var options = new JsonSerializerOptions { WriteIndented = true };
    Console.WriteLine(JsonSerializer.Serialize(outputDoc, options));
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Agent failed to map data - {ex.Message}");
    return 1;
}
