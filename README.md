# AgenticStructuredOutput

Applies structured output to agent interactions using **Microsoft Agent Framework**, mapping inputs to output schemas through AI-powered fuzzy logic and intelligent inference.

## Overview

This implementation uses the **Microsoft Agent Framework** (not Semantic Kernel) to create an AI agent that intelligently maps JSON input to a target schema. The agent uses **fuzzy logic** and **inference** to understand semantic relationships between fields, producing structured output that conforms to the target schema.

## Key Features

- **Microsoft Agent Framework**: Uses official `Microsoft.Agents.AI` library
- **AI-Powered Fuzzy Mapping**: Intelligently maps fields like "fullName" → "name", "yearsOld" → "age"
- **Expert Agent Instructions**: Agent configured as "expert in data mapping"
- **Structured Output**: Uses JSON Schema to enforce output structure
- **Flexible Input**: Accepts input as file path or string literal
- **Type Inference**: Automatically infers appropriate data types

## Agent Architecture

The application creates an AI agent with explicit expert instructions using Microsoft Agent Framework:

```csharp
var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    Name = "DataMappingExpert",
    Instructions = "You are an expert in data mapping and structured output transformation...",
    ChatOptions = chatOptions  // Includes JSON Schema for structured output
});
```

The agent uses:
- **Microsoft.Agents.AI**: Official Microsoft Agent Framework
- **Microsoft.Agents.AI.OpenAI**: OpenAI integration
- **Structured Output via JSON Schema**: Enforces output conformance
- **Fuzzy Logic Inference**: AI-powered field mapping

## Prerequisites

- .NET 9.0 SDK
- OpenAI API Key (set as `OPENAI_API_KEY` environment variable)

## Usage

```bash
export OPENAI_API_KEY="your-api-key-here"
dotnet run -- <schema.json> <input.json|json-string>
```

### Examples

**With file input:**
```bash
dotnet run -- schema.json input.json
```

**With string literal:**
```bash
dotnet run -- schema.json '{"firstName":"Alice","ageInYears":25}'
```

## Fuzzy Mapping Examples

### Example: Person Schema

**Schema** expects:
```json
{
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" },
    "email": { "type": "string" }
  }
}
```

**Input** with different field names:
```json
{
  "fullName": "John Doe",
  "yearsOld": 30,
  "emailAddress": "john@example.com"
}
```

**Agent Output** (intelligently mapped using AI inference):
```json
{
  "name": "John Doe",
  "age": 30,
  "email": "john@example.com"
}
```

The agent uses fuzzy logic to understand that:
- `fullName` semantically maps to `name`
- `yearsOld` semantically maps to `age`
- `emailAddress` semantically maps to `email`

## Building

```bash
dotnet build
```

## Testing

The project includes comprehensive evaluation tests:

- **Fuzzy Mapping**: Tests AI-powered field name inference
- **String Literals**: Tests direct JSON string input
- **Error Handling**: Tests invalid JSON and missing API keys
- **Performance**: Validates completion within 30 seconds
- **Output Validation**: Ensures output is valid JSON

Run tests with:
```bash
export OPENAI_API_KEY="your-api-key-here"
dotnet test
```

**Note**: Tests that require API calls will be skipped if `OPENAI_API_KEY` is not set.

## Evaluation Criteria

The agent demonstrates validity across common evaluation criteria:

1. **Correctness** - AI agent produces accurate mappings with fuzzy logic
2. **Robustness** - Handles errors gracefully (invalid JSON, missing keys)
3. **Agent Intelligence** - Uses Microsoft Agent Framework with expert instructions
4. **Performance** - Completes within 30 seconds (includes AI API latency)
5. **Flexibility** - Accepts both file and string inputs
6. **Schema Conformance** - Output enforced via JSON Schema structured output

## Dependencies

- .NET 9.0
- **Microsoft.Agents.AI** 1.0.0-preview.251028.1 - Official Microsoft Agent Framework
- **Microsoft.Agents.AI.OpenAI** 1.0.0-preview.251028.1 - OpenAI integration for agents
- **Microsoft.Extensions.AI** 9.10.1 - AI abstractions

All dependencies are free from known vulnerabilities.

## Architecture

```
User Input (JSON) + Schema (JSON)
    ↓
AI Agent (Microsoft Agent Framework)
    - Expert Instructions
    - Fuzzy Logic Inference  
    - Structured Output via JSON Schema
    ↓
Conformant JSON Output
```

## Key Differences from Semantic Kernel

This implementation uses **Microsoft Agent Framework** (`Microsoft.Agents.AI`), not Semantic Kernel:
- Agent Framework is purpose-built for agentic AI patterns
- Includes structured output enforcement via JSON Schema
- Provides better agent orchestration capabilities
- Part of Microsoft's unified agent strategy

## License

See LICENSE file for details.
