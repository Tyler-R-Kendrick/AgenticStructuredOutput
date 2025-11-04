using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Services;

var builder = WebApplication.CreateBuilder(args);

// Register all services
builder.Services.AddAgentServices();

var app = builder.Build();

// Initialize agent execution service
using (var scope = app.Services.CreateScope())
{
    var executionService = scope.ServiceProvider.GetRequiredService<IAgentExecutionService>();
    await executionService.InitializeAsync();
}

// Get the execution service from DI to configure routes
var agentService = app.Services.GetRequiredService<IAgentExecutionService>();
app.MapAgentRoutes(agentService);

app.Run();

// This partial class declaration is for test project access to Program
public partial class Program { }