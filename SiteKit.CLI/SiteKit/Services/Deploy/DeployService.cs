using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Deploy;
using SiteKit.CLI.Services.Shared;
using SiteKit.CLI.Services.Validate;
using System.Text.Json;

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

        AutoArgs args = new AutoArgs(siteName);
        args.Endpoint = endpoint;
        args.AccessToken = accessToken; 
        args.Directory = dir;


        var graphQLService = new GraphQLService(_httpClient, _logger);

        //new _ValidateSchema(new HttpClient(), _logger).Run(args);
        new _ReadYaml().Run(args);
        new _LoadYaml().Run(args);
        new _CompositionResolver().Run(args);

        //deploy
        if (args.IsValid)
        {
            new _BuildComponentCategoryFolders(graphQLService, _logger).Run(args);
            new _BuildComponentDatasources(graphQLService, _logger).Run(args);
            new _BuildComponentDatasourcesStdValues(graphQLService, _logger).Run(args);
            new _BuildPageTemplates(graphQLService, _logger).Run(args);
            new _BuildPageTemplatesStdValuesLayout(graphQLService, _logger, _httpClient).Run(args);
            new _BuildSharedDataFolders(graphQLService, _logger).Run(args);
            new _BuildRenderings(graphQLService, _logger).Run(args);
            new _BuildRenderingsPageContainers(graphQLService, _logger).Run(args);
            new _BuildPlaceholderSettingsForComponents(graphQLService, _logger).Run(args);
            new _BuildPlaceholderSettingsForPages(graphQLService, _logger).Run(args);
            new _BuildStyles(graphQLService, _logger).Run(args);
            new _BuildVariants(graphQLService, _logger).Run(args);
        }

        if (args.IsValid)
        {
            Console.WriteLine("Deployment successfully.");
        }
        else
        {
            Console.WriteLine("Error:");
            Console.WriteLine(args.ValidationMessage);
        }
    }
}
