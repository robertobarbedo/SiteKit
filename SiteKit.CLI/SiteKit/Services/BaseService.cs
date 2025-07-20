using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services;

public abstract class BaseService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    protected BaseService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    protected async Task<string> GetAccessTokenAsync(string dir, bool verbose)
    {
        string environment = "xmCloud";

        var tokenJsonPath = Path.Combine(dir, @".sitecore\user.json");

        if (!File.Exists(tokenJsonPath))
        {
            throw new FileNotFoundException($"Token file not found at: {tokenJsonPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(tokenJsonPath);
        var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var accessToken = root
            .GetProperty("endpoints")
            .GetProperty(environment)
            .GetProperty("accessToken")
            .GetString();

        if (verbose)
        {
            _logger.LogDebug($"Access token retrieved for environment: {environment}");
        }

        return accessToken ?? throw new InvalidOperationException($"No access token found for environment: {environment}");
    }

    protected async Task<string> GetEndpointForEnvironment(string dir, string environment, bool verbose)
    {
        var tokenJsonPath = Path.Combine(dir, @".sitecore\user.json");

        if (!File.Exists(tokenJsonPath))
        {
            throw new FileNotFoundException($"Token file not found at: {tokenJsonPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(tokenJsonPath);
        var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var host = root
            .GetProperty("endpoints")
            .GetProperty(environment)
            .GetProperty("host")
            .GetString();

        if (verbose)
        {
            _logger.LogDebug($"Environment Endpoint retrieved host: {host}");
        }

        string endpoint = host + "/sitecore/api/authoring/graphql/v1";

        return endpoint ?? throw new InvalidOperationException($"No endpoint found for environment: {environment}");
    }

    protected async Task<string> GetParentIdAsync(string endpoint, string parentPath, bool verbose)
    {
        var query = $@"
query {{
    item(
        where: {{
            database: ""master"",
            path: ""{parentPath.ToLowerInvariant()}""
        }}){{
        itemId
    }}
}}";

        var requestBody = new { query };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug($"Fetching parent ID for path: {parentPath}");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Parent ID response: {responseText}");
        }

        var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item) &&
            item.ValueKind != JsonValueKind.Null &&
            item.TryGetProperty("itemId", out var itemIdProperty))
        {
            var parentId = itemIdProperty.GetString();
            if (verbose)
            {
                _logger.LogDebug($"Found parent ID: {parentId}");
            }
            return parentId!;
        }

        return string.Empty;
    }

    protected async Task UpdateLogAsync(string endpoint, string siteName, string logValue, bool verbose)
    {
        var updateLogMutation = $@"
mutation {{
  updateItem(
    input: {{
      database: ""master""
      path: ""/sitecore/system/modules/sitekit/{siteName}""
      language: ""en""
      fields: [
        {{ name: ""Log"", value: ""{logValue}"" }}
      ]
    }}
  ) {{
    item {{
      itemId
      path
    }}
  }}
}}";

        var requestBody = new { query = updateLogMutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug($"Updating Log field to: {logValue}");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Update log response: {responseText}");
        }
    }

    protected async Task<string> GetLogValueAsync(string endpoint, string siteName, bool verbose)
    {
        var getLogQuery = $@"
query {{
    item(
        where: {{
            database: ""master"",
            path: ""/sitecore/system/modules/sitekit/{siteName}""
    }}){{
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

        var requestBody = new { query = getLogQuery };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug("Retrieving Log field value");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Get Log response: {responseText}");
        }

        var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item) &&
            item.ValueKind != JsonValueKind.Null &&
            item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in nodes.EnumerateArray())
            {
                if (field.TryGetProperty("name", out var fieldName) &&
                    fieldName.GetString() == "Log" &&
                    field.TryGetProperty("value", out var fieldValue))
                {
                    return fieldValue.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
