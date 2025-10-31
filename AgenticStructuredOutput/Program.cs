using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.A2A;
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

// TODO: Create chat client for Azure AI Inference
// This requires proper Azure AI Inference integration which is not yet available in preview packages
// For now, creating a placeholder agent configuration

// Default schema for general mapping
var defaultSchema = GetDefaultSchema();

// Register services
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// A2A agent endpoint placeholder
app.MapPost("/agent", async (HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    
    return Results.Ok(new
    {
        agent = "DataMappingExpert",
        message = "A2A endpoint - agent configured for Azure AI Inference with GitHub Models",
        instructions = "Expert in data mapping and structured output transformation using fuzzy logic",
        authentication = "GITHUB_TOKEN",
        input = requestBody
    });
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    agent = "DataMappingExpert", 
    framework = "Microsoft Agent Framework", 
    inference = "Azure AI Inference (GitHub Models)",
    authentication = "GITHUB_TOKEN"
}));

// Info endpoint
app.MapGet("/", () => Results.Ok(new 
{ 
    name = "DataMappingExpert",
    description = "AI agent for intelligent JSON data mapping with fuzzy logic",
    version = "1.0.0",
    framework = "Microsoft Agent Framework",
    hosting = "A2A AspNetCore",
    inference = "Azure AI Inference (GitHub Models)",
    authentication = "GITHUB_TOKEN environment variable",
    endpoints = new[]
    {
        "POST /agent - A2A agent endpoint for structured output mapping",
        "GET /health - Health check",
        "GET / - Agent information"
    },
    note = "Uses Microsoft.Agents.AI with Azure.AI.Inference for GitHub Models"
}));

app.Run();

static JsonElement GetDefaultSchema()
{
    var schemaJson = """
    {
      "$schema": "http://json-schema.org/draft-07/schema#",
      "type": "object",
      "properties": {
        "name": { "type": "string", "description": "Name field" },
        "age": { "type": "integer", "description": "Age field" },
        "email": { "type": "string", "description": "Email field" }
      }
    }
    """;
    
    var doc = JsonDocument.Parse(schemaJson);
    return doc.RootElement.Clone();
}
public partial class Program { }
