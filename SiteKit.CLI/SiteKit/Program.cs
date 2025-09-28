using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Deploy;
using SiteKit.CLI.Services.Init;
using SiteKit.CLI.Services.Validate;

namespace SiteKit.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        //Console.WriteLine("Waiting for debugger... press Enter to continue");
        //Console.ReadLine();

        var services = new ServiceCollection();
        ConfigureServices(services, false); // Default to non-verbose

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Define global options
        var siteOption = new Option<string>(
            aliases: new[] { "-s", "--site" },
            description: "Site name (required)")
        {
            IsRequired = true
        };

        var environmentOption = new Option<string>(
            aliases: new[] { "-n", "--environment" },
            description: "Environment name (default: default)")
        {
            IsRequired = false
        };
        environmentOption.SetDefaultValue("default");

        var verboseOption = new Option<bool>(
            aliases: new[] { "-v", "--verbose" },
            description: "Enable verbose logging")
        {
            IsRequired = false
        };

        var rootCommand = new RootCommand("SiteKit CLI - Sitecore YAML deployment tool");

        // Add deploy command
        rootCommand.AddCommand(CreateSiteKitCommand(serviceProvider, logger, siteOption, environmentOption, verboseOption));

        // Add validate command
        rootCommand.AddCommand(CreateValidateCommand(serviceProvider, logger, siteOption, environmentOption, verboseOption));

        // Add init command
        rootCommand.AddCommand(CreateInitCommand(serviceProvider, logger, environmentOption, verboseOption));

        //debug
        //args = (new List<String>() { "deploy", "-s", "SUGBR" }).ToArray();

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services, bool verbose)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            
            // When verbose is true, log everything (Debug and above)
            // When verbose is false, log warnings and errors (Warning and above)
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);

            // Suppress HTTP client logs unless verbose
            if (!verbose)
            {
                builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Error);
            }
        });

        // Register non-generic ILogger for BaseService compatibility
        services.AddSingleton<ILogger>(provider => provider.GetService<ILoggerFactory>()!.CreateLogger("SiteKit.CLI"));

        services.AddHttpClient();

        // Register service interfaces
        services.AddScoped<IGraphQLService, GraphQLService>();
        services.AddScoped<IInitService, InitService>();
        services.AddScoped<IDeployService, DeployService>();
        services.AddScoped<IValidateService, ValidateService>();
        services.AddScoped<ISiteKitService, SiteKitService>();
    }

    private static Command CreateSiteKitCommand(ServiceProvider serviceProvider, ILogger<Program> logger,
        Option<string> siteOption, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var command = new Command("deploy", "Deploy YAML files to Sitecore")
        {
            siteOption,
            environmentOption,
            verboseOption
        };

        command.SetHandler(async (site, environment, verbose) =>
        {
            // Create service provider with correct verbose setting
            var services = new ServiceCollection();
            ConfigureServices(services, verbose);
            var verboseServiceProvider = services.BuildServiceProvider();
            var verboseLogger = verboseServiceProvider.GetRequiredService<ILogger<Program>>();

            var siteKitService = verboseServiceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                verboseLogger.LogDebug("Verbose mode enabled");
                verboseLogger.LogDebug($"Starting deployment for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.DeployAsync(site, environment, verbose);
                if (verbose)
                {
                    verboseLogger.LogInformation("Deployment Finished");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Deployment failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        return command;
    }

    private static Command CreateValidateCommand(ServiceProvider serviceProvider, ILogger<Program> logger,
        Option<string> siteOption, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var command = new Command("validate", "Validate YAML files against Sitecore")
        {
            siteOption,
            environmentOption,
            verboseOption
        };

        command.SetHandler(async (site, environment, verbose) =>
        {
            // Create service provider with correct verbose setting
            var services = new ServiceCollection();
            ConfigureServices(services, verbose);
            var verboseServiceProvider = services.BuildServiceProvider();
            var verboseLogger = verboseServiceProvider.GetRequiredService<ILogger<Program>>();

            var siteKitService = verboseServiceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                verboseLogger.LogInformation("Verbose mode enabled");
                verboseLogger.LogInformation($"Starting validation for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.ValidateAsync(site, environment, verbose);
                if (verbose)
                {
                    verboseLogger.LogInformation("Validation Finished");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Validation failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        return command;
    }

    private static Command CreateInitCommand(ServiceProvider serviceProvider, ILogger<Program> logger, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var siteOption = new Option<string>(
            aliases: new[] { "-s", "--site" },
            description: "Site name (required)")
        {
            IsRequired = true
        };

        var command = new Command("init", "Initialize SiteKit project with sample YAML files")
        {
            siteOption,
            environmentOption,
            verboseOption
        };

        command.SetHandler(async (site, environment, verbose) =>
        {
            // Create service provider with correct verbose setting
            var services = new ServiceCollection();
            ConfigureServices(services, verbose);
            var verboseServiceProvider = services.BuildServiceProvider();
            var verboseLogger = verboseServiceProvider.GetRequiredService<ILogger<Program>>();

            var siteKitService = verboseServiceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                verboseLogger.LogInformation("Verbose mode enabled");
                verboseLogger.LogInformation($"Initializing SiteKit project for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.InitializeAsync(site, environment, verbose);
                if (!verbose)
                {
                    // Only show essential completion message in non-verbose mode
                    Console.WriteLine("SiteKit project initialized successfully");
                }
                else
                {
                    verboseLogger.LogInformation("SiteKit project initialized successfully");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Initialization failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        return command;
    }
}
