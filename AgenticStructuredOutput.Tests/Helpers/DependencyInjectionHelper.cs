using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticStructuredOutput.Tests.Helpers;

/// <summary>
/// Helper class for dependency injection setup patterns used in tests and applications.
/// Encapsulates common DI container configurations.
/// </summary>
public static class DependencyInjectionHelper
{
    /// <summary>
    /// Creates a new service collection with common test/app configurations.
    /// </summary>
    /// <returns>A configured IServiceCollection</returns>
    public static IServiceCollection CreateBaseServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// Registers an IChatClient in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="chatClient">The chat client to register</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection RegisterChatClient(
        this IServiceCollection services,
        IChatClient chatClient)
    {
        services.AddSingleton(chatClient);
        return services;
    }

    /// <summary>
    /// Registers a ChatCompletionsClient as an IChatClient in the service collection.
    /// Automatically converts the ChatCompletionsClient to IChatClient.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="chatCompletionsClient">The chat completions client to register</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection RegisterChatClient(
        this IServiceCollection services,
        Azure.AI.Inference.ChatCompletionsClient chatCompletionsClient)
    {
        services.AddSingleton(chatCompletionsClient.AsIChatClient());
        return services;
    }

    /// <summary>
    /// Builds a service provider from the service collection.
    /// Note: Use ServiceCollectionContainerBuilderExtensions.BuildServiceProvider() to avoid naming conflicts.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>A built IServiceProvider</returns>
    public static IServiceProvider BuildProvider(this IServiceCollection services)
    {
        return Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
    }

    /// <summary>
    /// Verifies that a service provider can resolve the IChatClient.
    /// Useful for validation after DI setup.
    /// </summary>
    /// <param name="serviceProvider">The service provider to check</param>
    /// <returns>True if IChatClient can be resolved, false otherwise</returns>
    public static bool CanResolveChatClient(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IChatClient>() != null;
    }

    /// <summary>
    /// Attempts to resolve the IChatClient from the service provider.
    /// Returns null if the service is not registered.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The resolved IChatClient or null</returns>
    public static IChatClient? TryResolveChatClient(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IChatClient>();
    }

    /// <summary>
    /// Resolves the IChatClient from the service provider.
    /// Throws if the service is not registered.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The resolved IChatClient</returns>
    /// <exception cref="InvalidOperationException">If IChatClient is not registered</exception>
    public static IChatClient ResolveChatClient(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IChatClient>();
    }
}
