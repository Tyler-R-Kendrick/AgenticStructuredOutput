using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.Inference;
using Json.Schema;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Get API key from environment (GITHUB_TOKEN for GitHub Models)
var apiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN") 
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? "";

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Warning: No API key found. Set GITHUB_TOKEN or OPENAI_API_KEY environment variable.");
}

// Create Azure AI Inference client for GitHub Models
var endpoint = new Uri("https://models.inference.ai.azure.com");
var credential = new AzureKeyCredential(apiKey);
var chatCompletionsClient = new ChatCompletionsClient(endpoint, credential);

// Create IChatClient wrapper for Azure AI Inference
IChatClient chatClient = new AzureInferenceChatClient(chatCompletionsClient, "gpt-4o-mini");

// Register chat client as singleton (we'll create agents dynamically per request)
builder.Services.AddSingleton(chatClient);

var app = builder.Build();

// A2A agent endpoint - accepts dynamic schema and executes agent
app.MapPost("/agent", async (HttpContext context, IChatClient chatClient) =>
{
    try
    {
        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<AgentRequest>(requestBody);
        
        if (request?.Input == null)
        {
            return Results.BadRequest(new { error = "Input is required" });
        }
        
        if (request?.Schema == null)
        {
            return Results.BadRequest(new { error = "Schema is required" });
        }
        
        // Parse the schema from the request
        JsonElement schemaElement;
        try
        {
            using var schemaDoc = JsonDocument.Parse(request.Schema);
            schemaElement = schemaDoc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = $"Invalid schema JSON: {ex.Message}" });
        }
        
        // Configure chat options with the dynamic schema
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schemaElement,
                schemaName: "DynamicOutput",
                schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
            )
        };
        
        // Create the AI agent with expert instructions and dynamic schema
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
        
        // Create prompt for the agent
        var prompt = $"""
        Map the following JSON input to the target schema using intelligent inference and fuzzy logic:
        
        Input Data:
        {request.Input}
        
        Target Schema:
        {request.Schema}
        
        Map the fields intelligently, using fuzzy matching for field names and type inference as needed.
        """;
        
        // Execute the agent with structured output
        var response = await agent.RunAsync(prompt);
        var outputText = response.ToString();
        
        // Parse as dynamic JSON
        JsonNode? outputNode = JsonNode.Parse(outputText);
        
        if (outputNode == null)
        {
            return Results.Problem("Agent produced invalid JSON output");
        }
        
        // Validate using the same schema
        var compiledSchema = JsonSchema.FromText(request.Schema);
        var validationResult = compiledSchema.Evaluate(outputNode);
        
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors?.Select(e => e.Value) ?? new[] { "Unknown validation error" });
            return Results.Problem($"Agent output failed schema validation: {errors}");
        }
        
        // Return the validated output as JSON
        return Results.Json(outputNode);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Agent execution failed: {ex.Message}");
    }
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new HealthResponse
{ 
    Status = "healthy", 
    Agent = "DataMappingExpert", 
    Framework = "Microsoft Agent Framework", 
    Inference = "Azure AI Inference (GitHub Models)",
    Authentication = "GITHUB_TOKEN"
}));

// Info endpoint
app.MapGet("/", () => Results.Ok(new AgentInfo
{ 
    Name = "DataMappingExpert",
    Description = "AI agent for intelligent JSON data mapping with fuzzy logic and dynamic schema support",
    Version = "1.0.0",
    Framework = "Microsoft Agent Framework",
    Hosting = "A2A AspNetCore",
    Inference = "Azure AI Inference (GitHub Models)",
    Authentication = "GITHUB_TOKEN environment variable",
    Endpoints = new[]
    {
        "POST /agent - A2A agent endpoint for structured output mapping with dynamic schema",
        "GET /health - Health check",
        "GET / - Agent information"
    },
    Features = new[]
    {
        "Dynamic JSON schema support",
        "Fuzzy logic field mapping",
        "Runtime schema validation with JsonSchema.Net",
        "AI-powered inference"
    }
}));

app.Run();

// Request model with dynamic schema
public class AgentRequest
{
    [JsonPropertyName("input")]
    public string? Input { get; set; }
    
    [JsonPropertyName("schema")]
    public string? Schema { get; set; }
}

// Response models
public class HealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("agent")]
    public string? Agent { get; set; }
    
    [JsonPropertyName("framework")]
    public string? Framework { get; set; }
    
    [JsonPropertyName("inference")]
    public string? Inference { get; set; }
    
    [JsonPropertyName("authentication")]
    public string? Authentication { get; set; }
}

public class AgentInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    
    [JsonPropertyName("framework")]
    public string? Framework { get; set; }
    
    [JsonPropertyName("hosting")]
    public string? Hosting { get; set; }
    
    [JsonPropertyName("inference")]
    public string? Inference { get; set; }
    
    [JsonPropertyName("authentication")]
    public string? Authentication { get; set; }
    
    [JsonPropertyName("endpoints")]
    public string[]? Endpoints { get; set; }
    
    [JsonPropertyName("features")]
    public string[]? Features { get; set; }
}

// IChatClient wrapper for Azure AI Inference
public class AzureInferenceChatClient : IChatClient
{
    private readonly ChatCompletionsClient _client;
    private readonly string _modelId;

    public AzureInferenceChatClient(ChatCompletionsClient client, string modelId)
    {
        _client = client;
        _modelId = modelId;
    }

    public ChatClientMetadata Metadata => new ChatClientMetadata(
        providerName: "AzureAIInference",
        providerUri: new Uri("https://models.inference.ai.azure.com")
    );

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatRequestMessage>();
        foreach (var msg in chatMessages)
        {
            messages.Add(new ChatRequestUserMessage(msg.Text ?? ""));
        }

        var completionOptions = new ChatCompletionsOptions
        {
            Messages = messages,
            Model = _modelId
        };

        var response = await _client.CompleteAsync(completionOptions, cancellationToken);
        
        var resultMessage = new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant,
            response.Value.Content
        );

        return new ChatResponse(resultMessage);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not implemented");
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public TService? GetService<TService>(object? key = null) where TService : class
    {
        return null;
    }

    public void Dispose() { }
}

public partial class Program { }
