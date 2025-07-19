# UpdateItemAsync Method Implementation

## Overview
Added a new method `UpdateItemAsync` to the `IGraphQLService` interface and `GraphQLService` class to update Sitecore items using GraphQL mutations.

## Method Signature
```csharp
Task<GraphQLUpdateResponse?> UpdateItemAsync(string endpoint, string pathOrId, Dictionary<string, string> fields, bool verbose = false)
```

## Parameters
- **endpoint**: GraphQL endpoint URL
- **pathOrId**: Item path (e.g., "/sitecore/content/Home") or item ID (e.g., "{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}")
- **fields**: Dictionary of field names and values to update
- **verbose**: Enable detailed logging (optional, defaults to false)

## GraphQL Mutation
The method generates this GraphQL mutation structure:

### For Path-based Updates:
```graphql
mutation {
  updateItem(
    input: {
      database: "master"
      path: "/sitecore/content/Home"
      language: "en"
      fields: [
        { name: "Title", value: "New Title" },
        { name: "Text", value: "New Content" }
      ]
    }
  ) {
    item {
      itemId
      path
    }
  }
}
```

### For ID-based Updates:
```graphql
mutation {
  updateItem(
    input: {
      database: "master"
      itemId: "{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}"
      language: "en"
      fields: [
        { name: "Title", value: "New Title" },
        { name: "Text", value: "New Content" }
      ]
    }
  ) {
    item {
      itemId
      path
    }
  }
}
```

## Key Features

### ✅ **Flexible Item Identification**
- Accepts either item path or item ID
- Automatically detects format (paths contain "/" characters)
- Supports both formats seamlessly

### ✅ **Multiple Field Updates**
- Updates multiple fields in a single operation
- Uses simple `Dictionary<string, string>` for field values
- Automatically escapes quotes in field values

### ✅ **Fixed Parameters**
- Database: Always "master"
- Language: Always "en"
- These are consistent with the existing methods

### ✅ **Comprehensive Error Handling**
- Validates fields dictionary (null/empty check)
- Handles GraphQL errors from Sitecore
- Handles HTTP and network errors
- Handles JSON parsing errors

### ✅ **Return Information**
- Returns `GraphQLUpdateResponse` with updated item's ID and path
- Returns `null` on failure for easy error checking

## Usage Examples

### Basic Path-based Update
```csharp
var fields = new Dictionary<string, string>
{
    { "Title", "New Page Title" },
    { "MetaDescription", "Updated meta description" },
    { "Text", "New content for the page" }
};

var result = await _graphQLService.UpdateItemAsync(
    endpoint: "https://example.com/sitecore/api/authoring/graphql/v1",
    pathOrId: "/sitecore/content/Home/About",
    fields: fields
);

if (result != null)
{
    Console.WriteLine($"Updated item: {result.ItemId} at {result.Path}");
}
```

### Basic ID-based Update
```csharp
var fields = new Dictionary<string, string>
{
    { "Title", "Updated via ID" },
    { "Text", "Content updated using item ID" }
};

var result = await _graphQLService.UpdateItemAsync(
    endpoint: endpoint,
    pathOrId: "{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}",
    fields: fields,
    verbose: true  // Enable detailed logging
);
```

### Single Field Update
```csharp
var fields = new Dictionary<string, string>
{
    { "Title", "Quick Title Update" }
};

var result = await _graphQLService.UpdateItemAsync(endpoint, itemPath, fields);
```

## Response Model

```csharp
public class GraphQLUpdateResponse
{
    public string ItemId { get; set; } = string.Empty;  // Updated item's ID
    public string Path { get; set; } = string.Empty;    // Updated item's path
}
```

## Error Scenarios

### 1. **No Fields Provided**
- Input: `fields` is `null` or empty
- Result: Returns `null`
- Logging: Warning message if verbose=true

### 2. **GraphQL Errors**
- Cause: Item not found, field validation errors, permission issues
- Result: Returns `null`
- Logging: Error message with GraphQL error details

### 3. **HTTP Errors**
- Cause: Network issues, server errors, authentication problems
- Result: Throws `HttpRequestException`
- Logging: Error details before throwing

### 4. **JSON Parsing Errors**
- Cause: Invalid response format from server
- Result: Throws `JsonException`
- Logging: Error details before throwing

## Smart Path/ID Detection

The method automatically determines whether the input is a path or ID:

```csharp
// These are detected as PATHS:
"/sitecore/content/Home"
"/sitecore/system/Languages/en"
"sitecore/content/site/page"  // Even without leading slash

// These are detected as IDs:
"{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}"
"110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9"
"some-item-id-without-slashes"
```

## Integration

The method integrates seamlessly with the existing GraphQL service:
- Same HttpClient and Logger instances
- Consistent error handling patterns
- Same JSON serialization approach
- Compatible logging methodology

## Security Considerations

- **Quote Escaping**: Automatically escapes quotes in field values to prevent injection
- **Database Fixed**: Always targets "master" database (no user input)
- **Language Fixed**: Always uses "en" language (no user input)
- **Input Validation**: Validates field dictionary before processing

This implementation provides a robust, flexible way to update Sitecore items with comprehensive error handling and logging!
