using SiteKit.CLI.Services.Deploy;
using SiteKit.CLI.Services.Init;
using SiteKit.CLI.Services.Validate;

namespace SiteKit.CLI.Services;

public interface ISiteKitService
{
    Task DeployAsync(string siteName, string environment, bool verbose);
    Task ValidateAsync(string siteName, string environment, bool verbose);
    Task InitializeAsync(string site, string environment, bool verbose);
}

public class SiteKitService : ISiteKitService
{
    private readonly IInitService _initService;
    private readonly IDeployService _deployService;
    private readonly IValidateService _validateService;

    public SiteKitService(
        IInitService initService,
        IDeployService deployService,
        IValidateService validateService)
    {
        _initService = initService;
        _deployService = deployService;
        _validateService = validateService;
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
}
