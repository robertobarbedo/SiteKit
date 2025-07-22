using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Deploy;
using SiteKit.CLI.Services.Shared;
using SiteKit.CLI.Services.Validate;
using System.Text.Json;

namespace SiteKit.CLI.Services.Validate;

public interface IValidateService
{
    Task ValidateAsync(string siteName, string environment, bool verbose);
}

public class ValidateService : BaseService, IValidateService
{
    public ValidateService(HttpClient httpClient, ILogger<ValidateService> logger)
        : base(httpClient, logger)
    {
    }

    public async Task ValidateAsync(string siteName, string environment, bool verbose)
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

        if (args.IsValid)
        {
            Console.WriteLine("Files all validate successfully.");
        }
        else
        {
            Console.WriteLine("Error:");
            Console.WriteLine(args.ValidationMessage);
        }
    }

    public void Validate(AutoArgs args)
    {

    }

}
