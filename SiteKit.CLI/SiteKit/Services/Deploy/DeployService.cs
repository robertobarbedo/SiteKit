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

        // Pre Parsing Validation
        new _ValidateSiteExistsInSitecore(graphQLService, _logger).Run(args);

        // Parsing
        new _ReadYaml().Run(args);
        new _LoadYaml().Run(args);
        new _CompositionResolver().Run(args);

        // Post Parsing Validation
        new _ValidateSiteSettingsPaths(graphQLService, _logger).Run(args);
        new _ValidatePageDefaultLayouts(_logger).Run(args);
        new _ValidatePartialsLayouts(_logger).Run(args);
        new _ValidateCompositionComponents(_logger).Run(args);
        new _ValidateInsertOptions(graphQLService, _logger).Run(args);
        new _ValidateComponentParameters(graphQLService, _logger).Run(args);

        //deploy
        if (args.IsValid)
        {
            WaitAndWrite("Building component category folders...");
            new _BuildComponentCategoryFolders(graphQLService, _logger).Run(args);
            WaitAndWrite("Building component datasources and standard values...");
            new _BuildComponentDatasources(graphQLService, _logger).Run(args);
            WaitAndWrite("Building component datasources standard values...");
            new _BuildComponentDatasourcesStdValues(graphQLService, _logger).Run(args);
            WaitAndWrite("Building page templates..."); 
            new _BuildPageTemplates(graphQLService, _logger).Run(args);
            WaitAndWrite("Building shared data folders...");  
            new _BuildSharedDataFolders(graphQLService, _logger).Run(args);
            WaitAndWrite("Building renderings..."); 
            new _BuildRenderings(graphQLService, _logger).Run(args);
            WaitAndWrite("Building renderings page containers..."); 
            new _BuildRenderingsPageContainers(graphQLService, _logger).Run(args);
            WaitAndWrite("Building page templates standard values and layout...");
            new _BuildPageTemplatesStdValuesLayout(graphQLService, _logger, _httpClient).Run(args);
            WaitAndWrite("Building placeholder settings for components...");
            new _BuildPlaceholderSettingsForComponents(graphQLService, _logger).Run(args);
            WaitAndWrite("Building placeholder settings for pages...");
            new _BuildPlaceholderSettingsForPages(graphQLService, _logger).Run(args);
            WaitAndWrite("Building the insert options of all pages...");
            new _BuildInsertOptions(graphQLService, _logger).Run(args);
            WaitAndWrite("Building partial designs...");
            new _BuildPartialDesigns(graphQLService, _logger).Run(args);
            WaitAndWrite("Building default page design...");
            new _BuildPageDesigns(graphQLService, _logger).Run(args);
            WaitAndWrite("Updating Page Designs to Default...");
            new _UpdatePageDesignsDefault(graphQLService, _logger).Run(args);
            WaitAndWrite("Building styles...");
            new _BuildStyles(graphQLService, _logger).Run(args);
            WaitAndWrite("Building variants...");
            new _BuildVariants(graphQLService, _logger).Run(args);
            WaitAndWrite("Building dictionary...");
            new _BuildDictionary(graphQLService, _logger).Run(args);
            WaitAndWrite("Building page types components with placeholder examples..." + Environment.NewLine + " - path:" + args.SiteConfig?.Site?.Code?.ComponentsPath);
            new _CodePageTypePlaceholderComponents(_logger).Run(args);
            WaitAndWrite("Scaffolding components with placeholder..." + Environment.NewLine + " - path:" + args.SiteConfig?.Site?.Code?.ComponentsPath);
            new _CodeScaffoldComponentsWithPlaceholder(_logger).Run(args);

        }

        if (args.IsValid)
        {
            Console.WriteLine("Deployment successfull.");
        }
        else
        {
            Console.WriteLine("Error:");
            Console.WriteLine(args.ValidationMessage);
        }
    }

    public void WaitAndWrite(string message)
    {
        Console.WriteLine(message);
        Thread.Sleep(250);
    }
}
