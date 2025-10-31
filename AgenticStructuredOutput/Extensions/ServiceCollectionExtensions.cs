using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using AgenticStructuredOutput.Services;

namespace AgenticStructuredOutput.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddScoped<IAgentFactory, AgentFactory>();
        
        return services;
    }

    public static IServiceCollection AddInferenceClient(this IServiceCollection services)
    {
        // Get API key from environment (GITHUB_TOKEN for GitHub Models)
        var apiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN") 
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
            ?? "";

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Warning: No API key found. Set GITHUB_TOKEN or OPENAI_API_KEY environment variable.");
        }

        // Only create and register the real chat client if we have an API key
        // Tests can override this by providing their own IChatClient implementation
        if (!string.IsNullOrEmpty(apiKey))
        {
            // Create Azure AI Inference client for GitHub Models
            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var credential = new AzureKeyCredential(apiKey);
            var chatClient = new ChatCompletionsClient(endpoint, credential);

            // Register chat client as singleton (we'll create agents dynamically per request)
            services.AddSingleton(chatClient.AsIChatClient());
        }
        else
        {
            // Register a no-op chat client for testing/development when no API key is available
            services.AddSingleton<IChatClient>(sp => throw new InvalidOperationException("No API key configured and no test mock provided"));
        }

        return services;
    }
}