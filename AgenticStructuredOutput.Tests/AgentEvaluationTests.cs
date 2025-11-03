using AgenticStructuredOutput.Tests.Harness;
using AgenticStructuredOutput.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

namespace AgenticStructuredOutput.Tests;

/// <summary>
/// Evaluation tests for the data mapping agent using LLM-based quality judges.
/// Tests validate agent output across multiple quality dimensions: Relevance, Coherence, Correctness, Completeness, Grounding.
/// Inherits common setup and invocation logic from AgentTestHarness.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public partial class AgentEvaluationTests : AgentTestHarness
{
    private IChatClient? _judgeModelClient;

    [OneTimeSetUp]
    public new void SetupDefaultSchema()
    {
        // Call base setup for schema loading
        base.SetupDefaultSchema();
        
        // Initialize the judge model client for evaluation
        _judgeModelClient = new AzureInferenceChatClientBuilder()
            .UseGitHubModelsEndpoint()
            .WithEnvironmentApiKey()
            .BuildIChatClient();
        
        LogSync("✓ Judge model client initialized");
    }

    [OneTimeTearDown]
    public void DisposeMockClients()
    {
        (_judgeModelClient as IDisposable)?.Dispose();
        LogSync("✓ Judge model client disposed");
    }

    private static IEnumerable<TestCaseData> LoadEvalTestCasesFromJsonL()
    {
        var filePath = GetTestResourcePath("test-cases-eval.jsonl");
        return TestDataLoader.LoadEvaluationTestCases(filePath);
    }

    private async Task EvaluateNumericScoreAsync<TEvaluator>(string evaluationType, string testScenario, string input, string response, TEvaluator evaluator) 
        where TEvaluator : IEvaluator
    {
        var judgeClient = _judgeModelClient ?? throw new InvalidOperationException("Judge model not initialized");
        var chatConfig = new ChatConfiguration(judgeClient);
        var userMessage = new ChatMessage(ChatRole.User, input);
        var assistantMessage = new ChatMessage(ChatRole.Assistant, response);

        var result = await evaluator.EvaluateAsync(userMessage, assistantMessage, chatConfig, cancellationToken: TestContext.CurrentContext.CancellationToken);

        if (result.Metrics.Count == 0)
            Assert.Fail($"{evaluationType} evaluation returned no metrics for {testScenario}");

        var metricKey = result.Metrics.Keys.FirstOrDefault() ?? throw new InvalidOperationException($"No metric key for {evaluationType}");
        var metric = result.Metrics[metricKey];

        if (metric is NumericMetric numericMetric && numericMetric.Value != null)
        {
            double score = (double)numericMetric.Value;
            var assessment = score switch { >= 4 => "Excellent", >= 3 => "Good", >= 2 => "Fair", _ => "Poor" };
            await LogAsync($"[{evaluationType}/LLM-Judge] {testScenario}: Score={score:F1}/5 - {assessment}");
            Assert.That(score, Is.GreaterThanOrEqualTo(3),
                $"LLM judge determined {evaluationType.ToLower()} score is too low for {testScenario}: {score}/5");
        }
        else
            Assert.Fail($"{evaluationType} metric not numeric or has no value for {testScenario}");
    }

    [Test]
    [CancelAfter(MaxTestTimeoutMs)]
    [TestCaseSource(nameof(LoadEvalTestCasesFromJsonL))]
    public async Task Evaluation_WithLLMJudge(string evaluationType, string testScenario, string input)
    {
        await LogAsync($"[{evaluationType}/LLM-Judge] Starting: {testScenario}");
        try
        {
            var prompt = $"Map the following input to the standard schema:\nInput:\n{input}";
            var response = await InvokeAgentAsync(prompt);

            switch (evaluationType)
            {
                case "Relevance":
                    await EvaluateNumericScoreAsync("Relevance", testScenario, input, response, new RelevanceEvaluator());
                    break;

                case "Coherence":
                    await EvaluateNumericScoreAsync("Coherence", testScenario, input, response, new CoherenceEvaluator());
                    break;

                case "Fluency":
                    await EvaluateNumericScoreAsync("Fluency", testScenario, input, response, new FluencyEvaluator());
                    break;

                case "Correctness":
                    // Use EquivalenceEvaluator to assess if output matches expected schema structure
                    await EvaluateNumericScoreAsync("Correctness", testScenario, input, response, new EquivalenceEvaluator());
                    break;

                case "Completeness":
                    // Use built-in CompletenessEvaluator to assess data capture
                    await EvaluateNumericScoreAsync("Completeness", testScenario, input, response, new CompletenessEvaluator());
                    break;

                case "Grounding":
                    // Use built-in GroundednessEvaluator to verify output is grounded in input
                    await EvaluateNumericScoreAsync("Grounding", testScenario, input, response, new GroundednessEvaluator());
                    break;

                default:
                    throw new InvalidOperationException($"Unknown evaluation type: {evaluationType}");
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.Status == 401 || ex.Status == 403)
        {
            Assert.Ignore($"Skipping LLM-based evaluation - GitHub Models API not accessible (HTTP {ex.Status})");
        }
    }
}
