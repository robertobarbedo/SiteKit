using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services;

public interface IGraphQLService
{
    Task<GraphQLItemResponse?> GetItemByPathAsync(string endpoint, string accessToken, string path, bool verbose = false);
    Task<string?> CreateItemAsync(string endpoint, string accessToken, string name, string templateId, string parentId, bool verbose = false);
    Task<GraphQLUpdateResponse?> UpdateItemAsync(string endpoint, string accessToken, string pathOrId, Dictionary<string, string> fields, bool verbose = false);
}

public class GraphQLService : IGraphQLService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public GraphQLService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GraphQLItemResponse?> GetItemByPathAsync(string endpoint, string accessToken, string path, bool verbose = false)
    {
        var query = $@"
query {{
    item(
        where: {{
            database: ""master"",
            path: ""{path}""
        }}) {{
        itemId
        name
        path
        fields(ownFields: true, excludeStandardFields: true) {{
            nodes {{
                name
                value
            }}
        }}
    }}
}}";

        var requestBody = new { query };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Set authorization header
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        if (verbose)
        {
            _logger.LogDebug($"Fetching item by path: {path}");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error: {errorMessage}");
                return null;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("item", out var item) &&
                item.ValueKind != JsonValueKind.Null)
            {
                var itemResponse = new GraphQLItemResponse
                {
                    ItemId = item.GetProperty("itemId").GetString() ?? string.Empty,
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Path = item.GetProperty("path").GetString() ?? string.Empty,
                    Fields = new List<GraphQLField>()
                };

                if (item.TryGetProperty("fields", out var fields) &&
                    fields.TryGetProperty("nodes", out var nodes) &&
                    nodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in nodes.EnumerateArray())
                    {
                        var fieldName = field.GetProperty("name").GetString() ?? string.Empty;
                        var fieldValue = field.GetProperty("value").GetString() ?? string.Empty;
                        
                        itemResponse.Fields.Add(new GraphQLField
                        {
                            Name = fieldName,
                            Value = fieldValue
                        });
                    }
                }

                if (verbose)
                {
                    _logger.LogDebug($"Successfully retrieved item: {itemResponse.Name} (ID: {itemResponse.ItemId})");
                }

                return itemResponse;
            }

            if (verbose)
            {
                _logger.LogWarning($"Item not found at path: {path}");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when fetching item by path: {path}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when fetching item by path: {path}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when fetching item by path: {path}");
            throw;
        }
    }

    public async Task<string?> CreateItemAsync(string endpoint, string accessToken, string name, string templateId, string parentId, bool verbose = false)
    {
        var mutation = $@"
mutation {{
  createItem(
    input: {{
      name: ""{name}""
      templateId: ""{templateId}""
      parent: ""{parentId}""
      language: ""en""
    }}
  ) {{
    item {{
      itemId
    }}
  }}
}}";

        var requestBody = new { query = mutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Set authorization header
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        if (verbose)
        {
            _logger.LogDebug($"Creating item: Name='{name}', TemplateId='{templateId}', ParentId='{parentId}'");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"Create item GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error creating item: {errorMessage}");
                return null;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("createItem", out var createItem) &&
                createItem.TryGetProperty("item", out var item) &&
                item.ValueKind != JsonValueKind.Null &&
                item.TryGetProperty("itemId", out var itemIdProperty))
            {
                var itemId = itemIdProperty.GetString();
                
                if (verbose)
                {
                    _logger.LogDebug($"Successfully created item '{name}' with ID: {itemId}");
                }

                return itemId;
            }

            if (verbose)
            {
                _logger.LogWarning($"Failed to create item '{name}' - unexpected response structure");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when creating item '{name}'");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when creating item '{name}'");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when creating item '{name}'");
            throw;
        }
    }

    public async Task<GraphQLUpdateResponse?> UpdateItemAsync(string endpoint, string accessToken, string pathOrId, Dictionary<string, string> fields, bool verbose = false)
    {
        if (fields == null || !fields.Any())
        {
            if (verbose)
            {
                _logger.LogWarning("No fields provided for update operation");
            }
            return null;
        }

        // Build the fields array for the GraphQL mutation
        var fieldsArray = string.Join(", ", fields.Select(f => $"{{ name: \"{f.Key}\", value: \"{f.Value.Replace("\"", "\\\"")}\" }}"));

        // Determine if pathOrId is a path or ID and construct the appropriate input
        string inputClause;
        if (pathOrId.StartsWith("/") || pathOrId.Contains("/"))
        {
            // It's a path
            inputClause = $"path: \"{pathOrId}\"";
        }
        else
        {
            // It's an ID
            inputClause = $"itemId: \"{pathOrId}\"";
        }

        var mutation = $@"
mutation {{
  updateItem(
    input: {{
      database: ""master""
      {inputClause}
      language: ""en""
      fields: [
        {fieldsArray}
      ]
    }}
  ) {{
    item {{
      itemId
      path
    }}
  }}
}}";

        var requestBody = new { query = mutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Set authorization header
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        if (verbose)
        {
            _logger.LogDebug($"Updating item: PathOrId='{pathOrId}', Fields={fields.Count}");
            _logger.LogDebug($"Update mutation: {mutation}");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"Update item GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error updating item: {errorMessage}");
                return null;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("updateItem", out var updateItem) &&
                updateItem.TryGetProperty("item", out var item) &&
                item.ValueKind != JsonValueKind.Null)
            {
                var updateResponse = new GraphQLUpdateResponse
                {
                    ItemId = item.TryGetProperty("itemId", out var itemIdProp) ? itemIdProp.GetString() ?? string.Empty : string.Empty,
                    Path = item.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? string.Empty : string.Empty
                };

                if (verbose)
                {
                    _logger.LogDebug($"Successfully updated item with ID: {updateResponse.ItemId}, Path: {updateResponse.Path}");
                }

                return updateResponse;
            }

            if (verbose)
            {
                _logger.LogWarning($"Failed to update item '{pathOrId}' - unexpected response structure");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when updating item '{pathOrId}'");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when updating item '{pathOrId}'");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when updating item '{pathOrId}'");
            throw;
        }
    }
}

public class GraphQLItemResponse
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<GraphQLField> Fields { get; set; } = new List<GraphQLField>();
}

public class GraphQLField
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class GraphQLUpdateResponse
{
    public string ItemId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
