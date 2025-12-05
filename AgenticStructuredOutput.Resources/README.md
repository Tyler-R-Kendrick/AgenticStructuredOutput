# AgenticStructuredOutput.Resources

This project contains shared embedded resources used across the AgenticStructuredOutput solution.

## Purpose

Centralizes common resources to:
- Avoid duplication across projects
- Provide single source of truth for schema, prompts, and evaluation data
- Simplify resource management and updates

## Resources Included

- **`schema.json`**: JSON schema defining the target output structure for data mapping
- **`agent-instructions.md`**: Agent prompt instructions for the data mapping agent
- **`test-cases-eval.jsonl`**: Evaluation test cases in JSONL format (one JSON object per line)

## Usage in Other Projects

Projects that need these resources should:

1. Add a project reference:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\AgenticStructuredOutput.Resources\AgenticStructuredOutput.Resources.csproj" />
   </ItemGroup>
   ```

2. Access resources at runtime using `ResourceLoader` utility or embedded resource APIs:
   ```csharp
   // Via ResourceLoader (preferred for solution-level resources)
   var schema = ResourceLoader.LoadSchema();
   var prompt = ResourceLoader.LoadPrompt();
   var testCases = ResourceLoader.LoadTestCases();
   
   // Via embedded resources (if needed directly)
   var assembly = typeof(AgenticStructuredOutput.Resources.Marker).Assembly;
   using var stream = assembly.GetManifestResourceStream("AgenticStructuredOutput.Resources.schema.json");
   ```

## Updating Resources

To update any resource:
1. Modify the file in this project directory
2. Rebuild the solution
3. All consuming projects will automatically use the updated version

## Resource Formats

### schema.json
Standard JSON Schema (draft-07 or later) defining the target schema for data mapping.

### agent-instructions.md
Markdown-formatted instructions for the agent. May contain placeholders for dynamic content.

### test-cases-eval.jsonl
JSONL format with one `EvalTestCase` per line:
```jsonl
{"evaluationType": "Relevance", "testScenario": "Description", "input": "{...}"}
{"evaluationType": "Correctness", "testScenario": "Description", "input": "{...}"}
```
