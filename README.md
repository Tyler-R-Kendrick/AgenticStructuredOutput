# AgenticStructuredOutput

Applies structured output to agent interactions, mapping inputs to output schemas using JSON Schema validation.

## Overview

This is a minimal C# agent-framework that validates JSON input against a JSON schema and maps it to structured output using type inference. The agent uses top-level statements for a clean, minimal codebase.

## Features

- **JSON Schema Validation**: Validates input against JSON Schema Draft-07
- **Flexible Input**: Accepts input as file path or string literal
- **Type Inference**: Automatically infers and maps types from input to output
- **Nested Structures**: Supports nested objects and arrays
- **Error Handling**: Provides clear validation error messages

## Usage

```bash
dotnet run -- <schema.json> <input.json|json-string>
```

### Examples

**With file input:**
```bash
dotnet run -- schema.json input.json
```

**With string literal:**
```bash
dotnet run -- schema.json '{"name":"John","age":30}'
```

## Building

```bash
dotnet build
```

## Testing

The project includes comprehensive evaluation tests covering:

- **Correctness**: Validates accurate output generation
- **Robustness**: Tests edge cases and error handling
- **Schema Compliance**: Ensures JSON schema validation works correctly
- **Performance**: Verifies completion within time bounds
- **Array Handling**: Tests array validation and mapping
- **Type Inference**: Validates number type handling

Run tests with:
```bash
dotnet test
```

## Evaluation Criteria

The agent demonstrates validity across common evaluation criteria:

1. **Correctness** - Produces accurate outputs matching input data
2. **Robustness** - Handles errors gracefully (invalid JSON, missing fields)
3. **Schema Compliance** - Validates against JSON schemas correctly
4. **Performance** - Completes within reasonable time bounds (<5s)
5. **Flexibility** - Accepts both file and string inputs
6. **Type Safety** - Properly infers and handles different data types

## Example Schemas

### Simple Schema (person-schema.json)
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" },
    "email": { "type": "string", "format": "email" }
  },
  "required": ["name"]
}
```

### Nested Schema (nested-schema.json)
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "user": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "address": {
          "type": "object",
          "properties": {
            "street": { "type": "string" },
            "city": { "type": "string" }
          }
        }
      }
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" }
    }
  }
}
```

## Dependencies

- .NET 9.0
- NJsonSchema 11.5.1 - JSON Schema validation
- System.Text.Json - JSON parsing and serialization

## License

See LICENSE file for details.

