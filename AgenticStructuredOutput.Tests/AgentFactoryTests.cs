using AgenticStructuredOutput.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Moq;

namespace AgenticStructuredOutput.Tests;

public class AgentFactoryTests
{
    [Fact]
    public async Task CreateDataMappingAgentAsync_ShouldCreateAgentWithInstructions()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<AgentFactory>>();
        var factory = new AgentFactory(mockChatClient.Object, mockLogger.Object);

        // Act & Assert - This mainly tests that the factory can create an agent without throwing
        var exception = await Record.ExceptionAsync(async () =>
        {
            await factory.CreateDataMappingAgentAsync();
        });

        // The method should not throw an exception when creating the agent
        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateDataMappingAgentAsync_ShouldReturnInstructedAgent()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<AgentFactory>>();
        var factory = new AgentFactory(mockChatClient.Object, mockLogger.Object);

        // Act & Assert - This mainly tests that the factory returns an AIAgent without throwing
        var exception = await Record.ExceptionAsync(async () =>
        {
            var agent = await factory.CreateDataMappingAgentAsync();
            Assert.NotNull(agent);
            Assert.IsAssignableFrom<AIAgent>(agent);
        });

        // The method should not throw an exception when creating the agent
        Assert.Null(exception);
    }
}