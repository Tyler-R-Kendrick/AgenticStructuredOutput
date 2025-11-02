using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.AI;
using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput;
using A2A.AspNetCore;
using AgenticStructuredOutput.Services;
using Json.More;
using A2A;

var builder = WebApplication.CreateBuilder(args);

// Register agent services
builder.Services.AddAgentServices();

var app = builder.Build();

var agentFactory = app.Services.GetRequiredService<IAgentFactory>();

// Load schema from embedded resource
var assembly = typeof(Program).Assembly;
var resourceName = "AgenticStructuredOutput.Resources.schema.json";
string schemaJson;
using (var stream = assembly.GetManifestResourceStream(resourceName))
{
    if (stream == null)
    {
        throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
    }
    using var reader = new StreamReader(stream);
    schemaJson = reader.ReadToEnd();
}

var jsonSchema = JsonSchema.FromText(schemaJson);
var schemaJsonElement = jsonSchema.ToJsonDocument().RootElement;
// Create the AI agent using the factory
var agent = await agentFactory.CreateDataMappingAgentAsync(new()
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema(
        schema: schemaJsonElement,
        schemaName: "DynamicOutput",
        schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
    )
});
AgentCard agentCard = new()
{
    Name = "Agentic Structured Output Agent",
    Description = "An AI agent that produces structured JSON output based on a provided schema.",
    IconUrl = "https://example.com/agent-icon.png"
};

app.MapA2A(
    agent,
    path: "/",
    agentCard: agentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/"));

app.Run();

// Helper types for parsing results
record RequestParseResult(AgentRequest? Request, IResult? ErrorResult)
{
    public bool IsError => ErrorResult != null;
}

record SchemaParseResult(JsonElement? SchemaElement, IResult? ErrorResult)
{
    public bool IsError => ErrorResult != null;
}

record ValidationResult(bool IsValid, IResult? ErrorResult)
{
    public bool IsError => ErrorResult != null;
}

record ResponseValidationResult(JsonNode? OutputNode, IResult? ErrorResult)
{
    public bool IsError => ErrorResult != null;
}

// Static helper class for request parsing
static class RequestParser
{
    public static async Task<RequestParseResult> ParseRequestBodyAsync(HttpContext context)
    {
        try
        {
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<AgentRequest>(requestBody);
            return new RequestParseResult(request, null);
        }
        catch (JsonException ex)
        {
            var errorResult = Results.BadRequest(new { error = $"Invalid request JSON: {ex.Message}" });
            return new RequestParseResult(null, errorResult);
        }
    }

    public static ValidationResult ValidateRequestFields(AgentRequest? request)
    {
        if (request?.Input == null)
        {
            return new ValidationResult(false, Results.BadRequest(new { error = "Input is required" }));
        }
        
        if (request.Schema == null)
        {
            return new ValidationResult(false, Results.BadRequest(new { error = "Schema is required" }));
        }
        
        return new ValidationResult(true, null);
    }

    public static SchemaParseResult ParseSchemaElement(string schemaJson)
    {
        try
        {
            using var schemaDoc = JsonDocument.Parse(schemaJson);
            var schemaElement = schemaDoc.RootElement.Clone();
            return new SchemaParseResult(schemaElement, null);
        }
        catch (JsonException ex)
        {
            var errorResult = Results.BadRequest(new { error = $"Invalid schema JSON: {ex.Message}" });
            return new SchemaParseResult(null, errorResult);
        }
    }
}

// Static helper class for response validation
static class ResponseValidator
{
    public static ResponseValidationResult ParseAndValidateResponse(string outputText, string schemaJson)
    {
        // Parse as dynamic JSON
        JsonNode? outputNode;
        try
        {
            outputNode = JsonNode.Parse(outputText);
        }
        catch (JsonException ex)
        {
            var errorResult = Results.Problem($"Agent produced invalid JSON output: {ex.Message}");
            return new ResponseValidationResult(null, errorResult);
        }

        if (outputNode == null)
        {
            var errorResult = Results.Problem("Agent produced invalid JSON output");
            return new ResponseValidationResult(null, errorResult);
        }

        // Validate using the same schema
        try
        {
            var compiledSchema = JsonSchema.FromText(schemaJson);
            var schemaValidation = compiledSchema.Evaluate(outputNode);
            if (!schemaValidation.IsValid)
            {
                var errors = string.Join(", ", schemaValidation.Errors?.Select(e => e.Value) ?? ["Unknown validation error"]);
                var errorResult = Results.Problem($"Agent output failed schema validation: {errors}");
                return new ResponseValidationResult(null, errorResult);
            }
        }
        catch (JsonException ex)
        {
            var errorResult = Results.Problem($"Schema validation failed: {ex.Message}");
            return new ResponseValidationResult(null, errorResult);
        }

        return new ResponseValidationResult(outputNode, null);
    }
}

// This hacky statement is for test project access to Program class
public partial class Program { }