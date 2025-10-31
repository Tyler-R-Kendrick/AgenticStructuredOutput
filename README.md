# AgenticStructuredOutput

A2A (Agent-to-Agent) hosted service using **Microsoft Agent Framework** for intelligent JSON data mapping with fuzzy logic and semantic inference, powered by **Azure AI Inference** with GitHub Models.

## Overview

This implementation uses the **Microsoft Agent Framework** with A2A (Agent-to-Agent) hosting to create a web service that intelligently maps JSON input to a target schema. The agent uses **Azure AI Inference** to access GitHub Models with **fuzzy logic** and **inference** for semantic field mapping.

## Key Features

- **Microsoft Agent Framework**: Uses official `Microsoft.Agents.AI` library
- **A2A Hosting**: ASP.NET Core web service with `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`
- **Azure AI Inference**: GitHub Models access via `Azure.AI.Inference`
- **GITHUB_TOKEN Authentication**: Uses GitHub token for model access (works in GitHub Actions)
- **AI-Powered Fuzzy Mapping**: Intelligently maps fields like "fullName" → "name"
- **Expert Agent Instructions**: Agent configured as data mapping expert
- **Structured Output**: JSON Schema enforcement for output conformance

## Architecture

```
Client (A2A Protocol)
    ↓
ASP.NET Core Server (A2A Hosting)
    ↓
AI Agent (Microsoft Agent Framework)
    ↓
Azure AI Inference (GitHub Models via GITHUB_TOKEN)
    ↓
Structured JSON Output
```

## Prerequisites

- .NET 9.0 SDK
- GITHUB_TOKEN environment variable (for GitHub Models) or OPENAI_API_KEY

## Usage

### Running the Server

```bash
export GITHUB_TOKEN="your-github-token-here"
dotnet run
```

The server will start and listen on `http://localhost:5000` (or configured port).

### Endpoints

- `POST /agent` - A2A agent endpoint for structured output mapping
- `GET /health` - Health check endpoint
- `GET /` - Agent information and capabilities

### Example Request

```bash
curl -X POST http://localhost:5000/agent \
  -H "Content-Type: application/json" \
  -d '{"input": "{\"fullName\":\"John\",\"yearsOld\":30}"}'
```

## Testing

```bash
dotnet test
```

The test suite includes:
- Health endpoint validation
- Agent information endpoint
- A2A agent endpoint functionality

All tests run without requiring an API key.

## Configuration

### Environment Variables

- `GITHUB_TOKEN` - GitHub personal access token for GitHub Models (preferred)
- `OPENAI_API_KEY` - Alternative API key (fallback)

### GitHub Actions

The agent is configured to work in GitHub Actions runners where `GITHUB_TOKEN` is automatically available, enabling:
- Automated testing
- CI/CD integration
- GitHub-hosted agent deployments

## Agent Configuration

The agent is configured as **DataMappingExpert** with these instructions:

```
You are an expert in data mapping and structured output transformation.
Your task is to intelligently map JSON input to a target schema using fuzzy logic and inference.

Key responsibilities:
- Use intelligent inference to map input fields to schema fields
- Apply fuzzy matching when field names don't exactly match
- Infer appropriate data types based on schema requirements
- Handle nested structures intelligently
- Always produce output that conforms to the schema
```

## Dependencies

- **.NET 9.0**
- **Microsoft.Agents.AI** 1.0.0-preview.251028.1 - Microsoft Agent Framework
- **Microsoft.Agents.AI.Hosting.A2A.AspNetCore** 1.0.0-preview.251028.1 - A2A hosting
- **Azure.AI.Inference** 1.0.0-beta.5 - Azure AI Inference for GitHub Models
- **Microsoft.Extensions.AI.AzureAIInference** 9.10.0-preview.1.25513.3 - Azure AI integration

All dependencies are free from known vulnerabilities.

## Key Differences from Previous Versions

This version uses:
1. **A2A Hosting** - Web service instead of CLI application
2. **Azure AI Inference** - GitHub Models instead of OpenAI directly
3. **GITHUB_TOKEN** - Works in GitHub Actions without additional configuration
4. **ASP.NET Core** - Production-ready hosting with health checks

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test --verbosity normal
```

### Running Locally

```bash
dotnet run --project AgenticStructuredOutput/AgenticStructuredOutput.csproj
```

## License

See LICENSE file for details.
