using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Shared;

namespace SiteKit.CLI.Services.Content;

public interface IContentService
{
    Task ContentAsync(string siteName, string environment, bool verbose);
}

public class ContentService : BaseService, IContentService
{
    public ContentService(HttpClient httpClient, ILogger<ContentService> logger) 
        : base(httpClient, logger)
    {
    }

    public async Task ContentAsync(string siteName, string environment, bool verbose)
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

        if (args.IsValid)
        {
            WaitAndWrite("Building menus...");
            new _BuildMenus(graphQLService, _logger).Run(args);
        }

        if (args.IsValid)
        {
            Console.WriteLine("Content deployment successful.");
        }
        else
        {
            Console.WriteLine("Error:");
            Console.WriteLine(args.ValidationMessage);
        }
    }

    private void WaitAndWrite(string message)
    {
        Console.WriteLine(message);
        Thread.Sleep(250);
    }
}
