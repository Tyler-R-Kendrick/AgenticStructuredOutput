using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Services;

var builder = WebApplication.CreateBuilder(args);

// Expand configuration sources so secrets can come from appsettings, user secrets, or environment
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Register all services
builder.Services.AddAgentServices(builder.Configuration);
builder.Services.AddSingleton<IAgentExecutionService, AgentExecutionService>();

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