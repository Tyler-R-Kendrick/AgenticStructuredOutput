using AgenticStructuredOutput.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Moq;

namespace AgenticStructuredOutput.Tests;

[TestFixture]
public class AgentFactoryTests
{
    [Test]
    public async Task CreateDataMappingAgentAsync_ShouldReturnValidInstructedAgent()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<AgentFactory>>();
        var factory = new AgentFactory(mockChatClient.Object, mockLogger.Object);

        // Act
        var agent = await factory.CreateDataMappingAgentAsync();

        // Assert
        Assert.That(agent, Is.Not.Null);
        Assert.That(agent, Is.InstanceOf<AIAgent>());
    }
}