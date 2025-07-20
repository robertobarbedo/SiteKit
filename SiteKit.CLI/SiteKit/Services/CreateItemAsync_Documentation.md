# CreateItemAsync Method Implementation

## Overview
Added a new method `CreateItemAsync` to the `IGraphQLService` interface and `GraphQLService` class to create Sitecore items using GraphQL mutations.

## Method Signature
```csharp
Task<string?> CreateItemAsync(string endpoint, string name, string templateId, string parentId, bool verbose = false)
```

## Parameters
- **endpoint**: GraphQL endpoint URL
- **name**: Name of the item to create
- **templateId**: Template ID (with or without braces)
- **parentId**: Parent item ID (with or without braces)
- **verbose**: Enable detailed logging (optional, defaults to false)

## GraphQL Mutation
The method executes this GraphQL mutation:
```graphql
mutation {
  createItem(
    input: {
      name: "Item Name"
      templateId: "{TEMPLATE-ID}"
      parent: "{PARENT-ID}"
      language: "en"
    }
  ) {
    item {
      itemId
    }
  }
}
```

## Key Features

### ✅ **Simplified Creation**
- Only requires essential parameters: name, templateId, parentId
- No fields are required during creation (as requested)
- Fixed language to "en" (as requested)

### ✅ **Return Value**
- Returns the newly created item's ID as a string
- Returns `null` if creation fails or encounters errors

### ✅ **Error Handling**
- Handles GraphQL errors (e.g., duplicate names, invalid templates)
- Handles HTTP errors and network issues
- Handles JSON parsing errors
- Comprehensive logging with error details

### ✅ **Logging Support**
- Detailed debug logging when verbose=true
- Logs creation attempt details
- Logs success/failure with item ID
- Error logging for all failure scenarios

## Usage Examples

### Basic Usage
```csharp
var itemId = await _graphQLService.CreateItemAsync(
    endpoint: "https://example.com/sitecore/api/authoring/graphql/v1",
    name: "My New Page",
    templateId: "{76036F5E-CBCE-46D1-AF0A-4143F9B557AA}",
    parentId: "{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}"
);

if (itemId != null)
{
    Console.WriteLine($"Created item with ID: {itemId}");
}
else
{
    Console.WriteLine("Failed to create item");
}
```

### With Verbose Logging
```csharp
var itemId = await _graphQLService.CreateItemAsync(
    endpoint: endpoint,
    name: "Test Item",
    templateId: templateId,
    parentId: parentId,
    verbose: true  // Enable detailed logging
);
```

## Integration with Existing Code
The method seamlessly integrates with the existing GraphQL service:

- Same HttpClient and Logger instances
- Same error handling patterns
- Same JSON serialization approach
- Same response parsing methodology

## Response Handling
The method parses the GraphQL response structure:
```json
{
  "data": {
    "createItem": {
      "item": {
        "itemId": "{NEWLY-CREATED-ID}"
      }
    }
  }
}
```

## Error Scenarios Handled
1. **GraphQL Errors**: When Sitecore returns errors (logged and returns null)
2. **Missing Response Data**: When response structure is unexpected (returns null)
3. **HTTP Errors**: Network/server issues (throws HttpRequestException)
4. **JSON Errors**: Response parsing issues (throws JsonException)
5. **General Exceptions**: Any other unexpected errors (throws Exception)

## Service Registration
The updated interface is already registered in the DI container:
```csharp
services.AddScoped<IGraphQLService, GraphQLService>();
```

This implementation provides a clean, robust way to create Sitecore items programmatically via GraphQL!
