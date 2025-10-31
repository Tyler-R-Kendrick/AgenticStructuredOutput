using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgenticStructuredOutput.Tests;

public class AgentServerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AgentServerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("healthy", content);
        Assert.Contains("DataMappingExpert", content);
    }

    [Fact]
    public async Task InfoEndpoint_ReturnsAgentInformation()
    {
        var response = await _client.GetAsync("/");
        
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("DataMappingExpert", content);
        Assert.Contains("Microsoft Agent Framework", content);
        Assert.Contains("Azure AI Inference", content);
        Assert.Contains("GITHUB_TOKEN", content);
        Assert.Contains("Dynamic JSON schema support", content);
    }

    [Fact]
    public async Task AgentEndpoint_RequiresBothInputAndSchema()
    {
        // Test missing schema
        var testPayload1 = new { input = "{\"name\": \"test\"}" };
        var response1 = await _client.PostAsJsonAsync("/agent", testPayload1);
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        
        // Test missing input
        var testPayload2 = new { schema = "{\"type\": \"object\"}" };
        var response2 = await _client.PostAsJsonAsync("/agent", testPayload2);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }
}
