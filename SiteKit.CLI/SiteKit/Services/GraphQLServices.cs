using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services;

public interface IGraphQLService
{
    Task<GraphQLItemResponse?> GetItemByPathAsync(string endpoint, string accessToken, string path, bool verbose = false);
    Task<string?> CreateItemAsync(string endpoint, string accessToken, string name, string templateId, string parentId, bool verbose = false);
    Task<GraphQLUpdateResponse?> UpdateItemAsync(string endpoint, string accessToken, string pathOrId, Dictionary<string, string> fields, bool verbose = false);
    Task<GraphQLTemplateResponse?> CreateTemplateAsync(string endpoint, string accessToken, string name, string parent, List<TemplateSection>? sections = null, bool verbose = false);
    Task<GraphQLTemplateResponse?> UpdateTemplateAsync(string endpoint, string accessToken, string templateId, string? name = null, List<UpdateTemplateSection>? sections = null, bool verbose = false);
    Task<bool> DeleteItemAsync(string endpoint, string accessToken, string path, bool permanently = false, bool verbose = false);
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

    public async Task<GraphQLTemplateResponse?> CreateTemplateAsync(string endpoint, string accessToken, string name, string parent, List<TemplateSection>? sections = null, bool verbose = false)
    {
        // Build sections string if provided
        string sectionsString = "";
        if (sections != null && sections.Any())
        {
            var sectionsArray = sections.Select(section =>
            {
                var fieldsArray = string.Join(", ", section.Fields.Select(field =>
                    $"{{ name: \"{field.Name}\", type: \"{field.Type}\" }}"));
                return $"{{ name: \"{section.Name}\", fields: [{fieldsArray}] }}";
            });
            sectionsString = $", sections: [{string.Join(", ", sectionsArray)}]";
        }

        var mutation = $@"
mutation {{
  createItemTemplate(
    input: {{
      name: ""{name}""
      parent: ""{parent}""{sectionsString}
    }}
  ) {{
    itemTemplate {{
      name
      ownFields {{
        nodes {{
          name
          type
        }}
      }}
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
            _logger.LogDebug($"Creating template: Name='{name}', Parent='{parent}'");
            _logger.LogDebug($"Template mutation: {mutation}");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"Create template GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error creating template: {errorMessage}");
                return null;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("createItemTemplate", out var createTemplate) &&
                createTemplate.TryGetProperty("itemTemplate", out var template) &&
                template.ValueKind != JsonValueKind.Null)
            {
                var templateResponse = new GraphQLTemplateResponse
                {
                    ItemId = string.Empty, // ItemId not available from createItemTemplate
                    Name = template.GetProperty("name").GetString() ?? string.Empty,
                    Fields = new List<GraphQLTemplateField>()
                };

                if (template.TryGetProperty("ownFields", out var ownFields) &&
                    ownFields.TryGetProperty("nodes", out var nodes) &&
                    nodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in nodes.EnumerateArray())
                    {
                        var fieldName = field.GetProperty("name").GetString() ?? string.Empty;
                        var fieldType = field.GetProperty("type").GetString() ?? string.Empty;
                        
                        templateResponse.Fields.Add(new GraphQLTemplateField
                        {
                            Name = fieldName,
                            Type = fieldType
                        });
                    }
                }

                if (verbose)
                {
                    _logger.LogDebug($"Successfully created template: {templateResponse.Name} with {templateResponse.Fields.Count} fields");
                }

                return templateResponse;
            }

            if (verbose)
            {
                _logger.LogWarning($"Failed to create template '{name}' - unexpected response structure");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when creating template '{name}'");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when creating template '{name}'");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when creating template '{name}'");
            throw;
        }
    }

    public async Task<GraphQLTemplateResponse?> UpdateTemplateAsync(string endpoint, string accessToken, string templateId, string? name = null, List<UpdateTemplateSection>? sections = null, bool verbose = false)
    {
        // Build the input object
        var inputParts = new List<string>();
        inputParts.Add($"templateId: \"{templateId}\"");

        if (!string.IsNullOrEmpty(name))
        {
            inputParts.Add($"name: \"{name}\"");
        }

        // Build sections array if provided
        if (sections != null && sections.Any())
        {
            var sectionsArray = sections.Select(section =>
            {
                var sectionParts = new List<string>();

                if (!string.IsNullOrEmpty(section.TemplateSectionId))
                {
                    sectionParts.Add($"templateSectionId: \"{section.TemplateSectionId}\"");
                }

                if (!string.IsNullOrEmpty(section.Name))
                {
                    sectionParts.Add($"name: \"{section.Name}\"");
                }

                if (section.Fields != null && section.Fields.Any())
                {
                    var fieldsArray = section.Fields.Select(field =>
                    {
                        var fieldParts = new List<string>();

                        if (!string.IsNullOrEmpty(field.FieldId))
                        {
                            fieldParts.Add($"fieldId: \"{field.FieldId}\"");
                        }

                        if (!string.IsNullOrEmpty(field.Name))
                        {
                            fieldParts.Add($"name: \"{field.Name}\"");
                        }

                        if (!string.IsNullOrEmpty(field.Type))
                        {
                            fieldParts.Add($"type: \"{field.Type}\"");
                        }

                        return "{ " + string.Join(", ", fieldParts) + " }";
                    });

                    sectionParts.Add($"fields: [{string.Join(", ", fieldsArray)}]");
                }

                return "{ " + string.Join(", ", sectionParts) + " }";
            });

            inputParts.Add($"sections: [{string.Join(", ", sectionsArray)}]");
        }

        var mutation = $@"
mutation {{
  updateItemTemplate(
    input: {{
      {string.Join(",\n      ", inputParts)}
    }}
  ) {{
    itemTemplate {{
      name
      ownFields {{
        nodes {{
          name
          type
        }}
      }}
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
            _logger.LogDebug($"Updating template: TemplateId='{templateId}', Name='{name ?? "unchanged"}'");
            _logger.LogDebug($"Update template mutation: {mutation}");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"Update template GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error updating template: {errorMessage}");
                return null;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("updateItemTemplate", out var updateTemplate) &&
                updateTemplate.TryGetProperty("itemTemplate", out var template) &&
                template.ValueKind != JsonValueKind.Null)
            {
                var templateResponse = new GraphQLTemplateResponse
                {
                    Name = template.GetProperty("name").GetString() ?? string.Empty,
                    Fields = new List<GraphQLTemplateField>()
                };

                if (template.TryGetProperty("ownFields", out var ownFields) &&
                    ownFields.TryGetProperty("nodes", out var nodes) &&
                    nodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in nodes.EnumerateArray())
                    {
                        var fieldName = field.GetProperty("name").GetString() ?? string.Empty;
                        var fieldType = field.GetProperty("type").GetString() ?? string.Empty;
                        
                        templateResponse.Fields.Add(new GraphQLTemplateField
                        {
                            Name = fieldName,
                            Type = fieldType
                        });
                    }
                }

                if (verbose)
                {
                    _logger.LogDebug($"Successfully updated template: {templateResponse.Name} with {templateResponse.Fields.Count} fields");
                }

                return templateResponse;
            }

            if (verbose)
            {
                _logger.LogWarning($"Failed to update template '{templateId}' - unexpected response structure");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when updating template '{templateId}'");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when updating template '{templateId}'");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when updating template '{templateId}'");
            throw;
        }
    }

    public async Task<bool> DeleteItemAsync(string endpoint, string accessToken, string path, bool permanently = false, bool verbose = false)
    {
        var mutation = $@"
mutation {{
  deleteItem(
    input: {{
      path: ""{path}""
      permanently: {permanently.ToString().ToLower()}
    }}
  ) {{
    successful
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
            _logger.LogDebug($"Deleting item: Path='{path}', Permanently='{permanently}'");
            _logger.LogDebug($"Delete mutation: {mutation}");
        }

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                _logger.LogDebug($"Delete item GraphQL response: {responseText}");
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            // Check for GraphQL errors
            if (root.TryGetProperty("errors", out var errors))
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError($"GraphQL error deleting item: {errorMessage}");
                return false;
            }

            // Parse the successful response
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("deleteItem", out var deleteItem) &&
                deleteItem.TryGetProperty("successful", out var successful))
            {
                var isSuccessful = successful.GetBoolean();
                
                if (verbose)
                {
                    _logger.LogDebug($"Delete item operation result: {(isSuccessful ? "Success" : "Failed")} for path '{path}'");
                }

                return isSuccessful;
            }

            if (verbose)
            {
                _logger.LogWarning($"Failed to delete item '{path}' - unexpected response structure");
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error when deleting item '{path}'");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON parsing error when deleting item '{path}'");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when deleting item '{path}'");
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

public class GraphQLTemplateResponse
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<GraphQLTemplateField> Fields { get; set; } = new List<GraphQLTemplateField>();
}

public class GraphQLTemplateField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class TemplateSection
{
    public string Name { get; set; } = string.Empty;
    public List<TemplateField> Fields { get; set; } = new List<TemplateField>();
}

public class TemplateField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class UpdateTemplateSection
{
    public string? TemplateSectionId { get; set; }
    public string? Name { get; set; }
    public List<UpdateTemplateField>? Fields { get; set; }
}

public class UpdateTemplateField
{
    public string? FieldId { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
}
