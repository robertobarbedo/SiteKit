using System.Text.Json;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy;

public interface IDeployService
{
    Task DeployAsync(string siteName, string environment, bool verbose);
}

public class DeployService : BaseService, IDeployService
{
    public DeployService(HttpClient httpClient, ILogger<DeployService> logger) 
        : base(httpClient, logger)
    {
    }

    public async Task DeployAsync(string siteName, string environment, bool verbose)
    {
        var dir = Directory.GetCurrentDirectory();
        string accessToken = await GetAccessTokenAsync(dir, verbose);
        string endpoint = await GetEndpointForEnvironment(dir, environment, verbose);

        //_httpClient.DefaultRequestHeaders.Clear();
        //_httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        AutoArgs args = new AutoArgs(siteName);
        args.Endpoint = endpoint;
        args.AccessToken = accessToken; 
        args.Directory = dir;


        var graphQLService = new GraphQLService(_httpClient, _logger);

        new _ReadYaml().Run(args);
        new _LoadYaml().Run(args);
        new _CompositionResolver().Run(args);
        new _BuildComponentCategoryFolders(graphQLService, _logger).Run(args);

    }
}
