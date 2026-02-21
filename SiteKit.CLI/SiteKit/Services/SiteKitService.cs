using SiteKit.CLI.Services.Content;
using SiteKit.CLI.Services.Deploy;
using SiteKit.CLI.Services.Init;
using SiteKit.CLI.Services.Validate;
using SiteKit.CLI.Services.Navigation;

namespace SiteKit.CLI.Services;

public interface ISiteKitService
{
    Task DeployAsync(string siteName, string environment, bool verbose);
    Task ValidateAsync(string siteName, string environment, bool verbose);
    Task InitializeAsync(string site, string environment, bool verbose);
    Task NavigationAsync(string siteName, string environment, bool verbose);
    Task ContentAsync(string siteName, string environment, bool verbose);
}

public class SiteKitService : ISiteKitService
{
    private readonly IInitService _initService;
    private readonly IDeployService _deployService;
    private readonly IValidateService _validateService;
    private readonly INavigationService _navigationService;
    private readonly IContentService _contentService;

    public SiteKitService(
        IInitService initService,
        IDeployService deployService,
        IValidateService validateService,
        INavigationService navigationService,
        IContentService contentService)
    {
        _initService = initService;
        _deployService = deployService;
        _validateService = validateService;
        _navigationService = navigationService;
        _contentService = contentService;
    }

    public async Task DeployAsync(string siteName, string environment, bool verbose)
    {
        await _deployService.DeployAsync(siteName, environment, verbose);
    }

    public async Task ValidateAsync(string siteName, string environment, bool verbose)
    {
        await _validateService.ValidateAsync(siteName, environment, verbose);
    }

    public async Task InitializeAsync(string site, string environment, bool verbose)
    {
        await _initService.InitializeAsync(site, environment, verbose);
    }

    public async Task NavigationAsync(string siteName, string environment, bool verbose)
    {
        await _navigationService.NavigationAsync(siteName, environment, verbose);
    }

    public async Task ContentAsync(string siteName, string environment, bool verbose)
    {
        await _contentService.ContentAsync(siteName, environment, verbose);
    }
}
