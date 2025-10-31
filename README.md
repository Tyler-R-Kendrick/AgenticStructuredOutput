# AgenticStructuredOutput

Applies structured output to agent interactions using **Microsoft Agent Framework** (Semantic Kernel), mapping inputs to output schemas through AI-powered fuzzy logic and intelligent inference.

## Overview

This is a minimal C# agent-framework that uses **Microsoft Semantic Kernel** to create an AI agent that intelligently maps JSON input to a target schema. Unlike deterministic field-name matching, this agent uses **fuzzy logic** and **inference** to understand semantic relationships between fields.

## Key Features

- **AI-Powered Mapping**: Uses Microsoft Agent Framework (Semantic Kernel) with OpenAI
- **Fuzzy Logic**: Intelligently maps fields like "fullName" → "name", "yearsOld" → "age"
- **Agent Instructions**: Expert mapping agent with specific instructions for data transformation
- **Flexible Input**: Accepts input as file path or string literal
- **Nested Structures**: Handles complex nested objects and arrays
- **Type Inference**: Automatically infers appropriate data types

## Agent Architecture

The application creates an AI agent with explicit instructions:
```
You are an expert in data mapping and structured output transformation.
Your task is to intelligently map JSON input to a target schema using fuzzy logic and inference.
```

The agent uses:
- **Microsoft Semantic Kernel**: For agent orchestration
- **OpenAI ChatCompletion**: For intelligent field mapping
- **Structured Output**: Generates JSON conforming to target schema

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

### Example 1: Person Schema

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

**Agent Output** (intelligently mapped):
```json
{
  "name": "John Doe",
  "age": 30,
  "email": "john@example.com"
}
```

### Example 2: Nested Schema

**Schema** expects:
```json
{
  "properties": {
    "user": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "contact": { ... }
      }
    },
    "tags": { "type": "array", "items": { "type": "string" } }
  }
}
```

**Input** with different structure:
```json
{
  "person": {
    "fullName": "Jane Smith",
    "contactInfo": { ... }
  },
  "labels": ["developer", "engineer"]
}
```

**Agent Output** (semantically mapped):
- `person` → `user`
- `fullName` → `name`
- `contactInfo` → `contact`
- `labels` → `tags`

## Building

```bash
dotnet build
```

## Testing

The project includes comprehensive evaluation tests covering:

- **Fuzzy Mapping**: Tests field name inference (fullName → name)
- **Nested Structures**: Tests intelligent mapping of nested objects
- **String Literals**: Tests direct JSON string input
- **Error Handling**: Tests invalid JSON and missing API keys
- **Performance**: Validates completion within reasonable time
- **Output Validation**: Ensures output is valid JSON

Run tests with:
```bash
export OPENAI_API_KEY="your-api-key-here"
dotnet test
```

**Note**: Tests that require API calls will be skipped if `OPENAI_API_KEY` is not set.

## Evaluation Criteria

The agent demonstrates validity across common evaluation criteria:

1. **Correctness** - Uses AI to produce accurate mappings with fuzzy logic
2. **Robustness** - Handles errors gracefully (invalid JSON, missing keys)
3. **Agent Intelligence** - Applies semantic understanding to map fields
4. **Performance** - Completes within 30 seconds (includes AI API latency)
5. **Flexibility** - Accepts both file and string inputs
6. **Schema Conformance** - Output always conforms to target schema

## Dependencies

- .NET 9.0
- **Microsoft.SemanticKernel** 1.66.0 - Agent framework
- **Microsoft.SemanticKernel.Connectors.OpenAI** 1.66.0 - OpenAI integration
- **Microsoft.Extensions.AI** 9.10.1 - AI abstractions

All dependencies are free from known vulnerabilities.

## Architecture

```
User Input (JSON) 
    ↓
Schema (JSON) 
    ↓
AI Mapping Agent (Semantic Kernel + OpenAI)
    - Expert Instructions
    - Fuzzy Logic Inference
    - Semantic Understanding
    ↓
Structured Output (JSON)
```

## License

See LICENSE file for details.
