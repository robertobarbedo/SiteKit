using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Shared;

namespace SiteKit.CLI.Services.Navigation;

public interface INavigationService
{
    Task NavigationAsync(string siteName, string environment, bool verbose);
}

public class NavigationService : BaseService, INavigationService
{
    public NavigationService(HttpClient httpClient, ILogger<NavigationService> logger) 
        : base(httpClient, logger)
    {
    }

    public async Task NavigationAsync(string siteName, string environment, bool verbose)
    {
        var dir = Directory.GetCurrentDirectory();
        string accessToken = await GetAccessTokenAsync(dir, verbose);
        string endpoint = await GetEndpointForEnvironment(dir, environment, verbose);

        AutoArgs args = new AutoArgs(siteName);
        args.Endpoint = endpoint;
        args.AccessToken = accessToken; 
        args.Directory = dir;

        var graphQLService = new GraphQLService(_httpClient, _logger);

        // Parsing
        new _ReadYaml().Run(args);
        new _LoadYaml().Run(args);

        WaitAndWrite("Building navigation templates...");
        new _BuildNavigationTemplates(graphQLService, _logger).Run(args);
        if (!args.IsValid) { ShowError(args); return; }

        // Re-read YAML to pick up template IDs written by the previous step
        new _ReadYaml().Run(args);
        new _LoadYaml().Run(args);

        WaitAndWrite("Creating Menu Domain item...");
        new _CreateMenuDomain(graphQLService, _logger).Run(args);
        if (!args.IsValid) { ShowError(args); return; }

        Console.WriteLine("Navigation execution successful.");
    }

    private void ShowError(AutoArgs args)
    {
        Console.WriteLine("Error:");
        Console.WriteLine(args.ValidationMessage);
    }

    private void WaitAndWrite(string message)
    {
        Console.WriteLine(message);
        Thread.Sleep(250);
    }
}
