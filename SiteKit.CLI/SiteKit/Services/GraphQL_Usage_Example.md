# GraphQL Service Usage Example

The `GraphQLService` provides a clean way to execute GraphQL queries against Sitecore. Here's how to use the `GetItemByPathAsync` method:

## Service Interface

```csharp
public interface IGraphQLService
{
    Task<GraphQLItemResponse?> GetItemByPathAsync(string endpoint, string accessToken, string path, bool verbose = false);
    Task<string?> CreateItemAsync(string endpoint, string accessToken, string name, string templateId, string parentId, bool verbose = false);
    Task<GraphQLUpdateResponse?> UpdateItemAsync(string endpoint, string accessToken, string pathOrId, Dictionary<string, string> fields, bool verbose = false);
}
```

## Usage Example

```csharp
// Inject the service through dependency injection
public class MyService
{
    private readonly IGraphQLService _graphQLService;
    
    public MyService(IGraphQLService graphQLService)
    {
        _graphQLService = graphQLService;
    }
    
    public async Task<GraphQLItemResponse?> GetHomeItem()
    {
        string endpoint = "https://your-sitecore-instance.com/sitecore/api/authoring/graphql/v1";
        string path = "/sitecore/content/MySiteCollection/NewSite/Home";
        
        var item = await _graphQLService.GetItemByPathAsync(endpoint, path, verbose: true);
        
        if (item != null)
        {
            Console.WriteLine($"Found item: {item.Name} at {item.Path}");
            Console.WriteLine($"Item ID: {item.ItemId}");
            
            foreach (var field in item.Fields)
            {
                Console.WriteLine($"Field: {field.Name} = {field.Value}");
            }
        }
        else
        {
            Console.WriteLine("Item not found");
        }
        
        return item;
    }
    
    public async Task<string?> CreateNewItem()
    {
        string endpoint = "https://your-sitecore-instance.com/sitecore/api/authoring/graphql/v1";
        string name = "My New Item";
        string templateId = "{76036F5E-CBCE-46D1-AF0A-4143F9B557AA}";
        string parentId = "{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}";
        
        var itemId = await _graphQLService.CreateItemAsync(endpoint, name, templateId, parentId, verbose: true);
        
        if (itemId != null)
        {
            Console.WriteLine($"Successfully created item with ID: {itemId}");
        }
        else
        {
            Console.WriteLine("Failed to create item");
        }
        
        return itemId;
    }
    
    public async Task<GraphQLUpdateResponse?> UpdateItemFields()
    {
        string endpoint = "https://your-sitecore-instance.com/sitecore/api/authoring/graphql/v1";
        string itemPath = "/sitecore/content/MySiteCollection/NewSite/Home";
        
        var fields = new Dictionary<string, string>
        {
            { "Title", "Updated Title" },
            { "Text", "Updated content text" },
            { "MetaDescription", "Updated meta description" }
        };
        
        var updateResponse = await _graphQLService.UpdateItemAsync(endpoint, itemPath, fields, verbose: true);
        
        if (updateResponse != null)
        {
            Console.WriteLine($"Successfully updated item with ID: {updateResponse.ItemId}");
            Console.WriteLine($"Item path: {updateResponse.Path}");
        }
        else
        {
            Console.WriteLine("Failed to update item");
        }
        
        return updateResponse;
    }
}
```

## Response Model

### GetItemByPathAsync Response

The method returns a `GraphQLItemResponse` object with the following structure:

```csharp
public class GraphQLItemResponse
{
    public string ItemId { get; set; }      // Sitecore item ID
    public string Name { get; set; }        // Item name
    public string Path { get; set; }        // Full item path
    public List<GraphQLField> Fields { get; set; }  // Custom fields (excludes standard fields)
}

public class GraphQLField
{
    public string Name { get; set; }        // Field name
    public string Value { get; set; }       // Field value
}
```

### CreateItemAsync Response

The method returns a `string?` containing the newly created item's ID, or `null` if the creation failed.

### UpdateItemAsync Response

The method returns a `GraphQLUpdateResponse?` object with the following structure:

```csharp
public class GraphQLUpdateResponse
{
    public string ItemId { get; set; }      // Updated item's ID
    public string Path { get; set; }        // Updated item's path
}
```

Returns `null` if the update failed.

## Features

### GetItemByPathAsync
- ✅ **Error Handling**: Handles HTTP errors, JSON parsing errors, and GraphQL errors
- ✅ **Logging**: Detailed logging with verbose option for debugging
- ✅ **Type Safety**: Strongly typed response models
- ✅ **Null Safety**: Returns null when item is not found
- ✅ **Standard Compliant**: Uses proper GraphQL query format
- ✅ **Fixed Database**: Always queries the "master" database as requested

### CreateItemAsync
- ✅ **Simple Creation**: Creates items with just name, templateId, and parentId
- ✅ **Fixed Language**: Always uses "en" language as requested
- ✅ **No Fields Required**: Creates items without any field values
- ✅ **Error Handling**: Comprehensive error handling for creation failures
- ✅ **ID Return**: Returns the newly created item's ID on success
- ✅ **Logging**: Detailed logging with verbose option

### UpdateItemAsync
- ✅ **Flexible Input**: Accepts either item path or item ID
- ✅ **Multiple Fields**: Updates multiple fields in a single operation
- ✅ **Field Dictionary**: Simple Dictionary<string, string> for field values
- ✅ **Fixed Language**: Always uses "en" language
- ✅ **Fixed Database**: Always targets "master" database
- ✅ **Error Handling**: Comprehensive error handling for update failures
- ✅ **Response Data**: Returns updated item ID and path
- ✅ **Logging**: Detailed logging with verbose option

## Error Scenarios

### GetItemByPathAsync
The method handles these error scenarios gracefully:

1. **Item Not Found**: Returns `null`
2. **GraphQL Errors**: Logs error message and returns `null`
3. **HTTP Errors**: Throws `HttpRequestException`
4. **JSON Parsing Errors**: Throws `JsonException`
5. **Network Issues**: Throws appropriate exceptions

### CreateItemAsync
The method handles these error scenarios gracefully:

1. **Creation Failed**: Returns `null` (e.g., duplicate name, invalid template, etc.)
2. **GraphQL Errors**: Logs error message and returns `null`
3. **HTTP Errors**: Throws `HttpRequestException`
4. **JSON Parsing Errors**: Throws `JsonException`
5. **Network Issues**: Throws appropriate exceptions

### UpdateItemAsync
The method handles these error scenarios gracefully:

1. **No Fields Provided**: Returns `null` if fields dictionary is null or empty
2. **Update Failed**: Returns `null` (e.g., item not found, field validation errors, etc.)
3. **GraphQL Errors**: Logs error message and returns `null`
4. **HTTP Errors**: Throws `HttpRequestException`
5. **JSON Parsing Errors**: Throws `JsonException`
6. **Network Issues**: Throws appropriate exceptions

## Integration

The service is automatically registered in the DI container in `Program.cs`:

```csharp
services.AddScoped<IGraphQLService, GraphQLService>();
```

You can inject it into any service constructor that needs GraphQL functionality.
