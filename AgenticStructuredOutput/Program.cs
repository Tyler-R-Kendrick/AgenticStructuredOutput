using System.Text.Json;
using NJsonSchema;

// Parse command line arguments
if (args.Length < 2)
{
    Console.WriteLine("Usage: AgenticStructuredOutput <schema.json> <input.json|json-string>");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json input.json");
    Console.WriteLine("Example: AgenticStructuredOutput schema.json '{\"name\":\"John\"}'");
    return 1;
}

var schemaPath = args[0];
var inputArg = args[1];

// Load the JSON schema
JsonSchema schema;
try
{
    schema = await JsonSchema.FromFileAsync(schemaPath);
}
catch (FileNotFoundException)
{
    Console.WriteLine($"Error: Schema file not found: {schemaPath}");
    return 1;
}
catch (JsonException ex)
{
    Console.WriteLine($"Error: Invalid JSON schema - {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Failed to load schema - {ex.Message}");
    return 1;
}

// Load input JSON (from file or string literal)
string inputJson;
if (File.Exists(inputArg))
{
    inputJson = await File.ReadAllTextAsync(inputArg);
}
else
{
    inputJson = inputArg;
}

// Parse and validate the input
JsonDocument inputDoc;
try
{
    inputDoc = JsonDocument.Parse(inputJson);
}
catch (JsonException ex)
{
    Console.WriteLine($"Error: Invalid JSON input - {ex.Message}");
    return 1;
}

// Validate against schema
var validationErrors = schema.Validate(inputJson);

if (validationErrors.Count > 0)
{
    Console.WriteLine("Validation failed with the following errors:");
    foreach (var error in validationErrors)
    {
        Console.WriteLine($"  - {error.Path}: {error.Kind}");
    }
    return 1;
}

// Map the input to structured output
var output = MapToStructuredOutput(inputDoc.RootElement, schema);

// Serialize and output the result
var options = new JsonSerializerOptions { WriteIndented = true };
Console.WriteLine(JsonSerializer.Serialize(output, options));

return 0;

// Helper function to map input to structured output using inference
static object? MapToStructuredOutput(JsonElement input, JsonSchema schema)
{
    return input.ValueKind switch
    {
        JsonValueKind.Object => MapObject(input, schema),
        JsonValueKind.Array => MapArray(input, schema),
        JsonValueKind.String => input.GetString(),
        JsonValueKind.Number => input.TryGetInt32(out var i32) ? i32 : 
                                input.TryGetInt64(out var i64) ? i64 : 
                                input.TryGetDecimal(out var dec) ? dec : 
                                input.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new ArgumentException($"Unsupported JSON value kind: {input.ValueKind}")
    };
}

static Dictionary<string, object?> MapObject(JsonElement input, JsonSchema schema)
{
    var result = new Dictionary<string, object?>();
    
    foreach (var prop in input.EnumerateObject())
    {
        var propSchema = schema.Properties.ContainsKey(prop.Name) 
            ? schema.Properties[prop.Name] 
            : null;
        
        result[prop.Name] = propSchema != null 
            ? MapToStructuredOutput(prop.Value, propSchema) 
            : MapToStructuredOutput(prop.Value, schema);
    }
    
    return result;
}

static List<object?> MapArray(JsonElement input, JsonSchema schema)
{
    var result = new List<object?>();
    var itemSchema = schema.Item ?? schema;
    
    foreach (var item in input.EnumerateArray())
    {
        result.Add(MapToStructuredOutput(item, itemSchema));
    }
    
    return result;
}
