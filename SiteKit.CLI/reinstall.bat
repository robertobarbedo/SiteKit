dotnet tool uninstall -g SiteKit.CLI
dotnet tool install -g SiteKit.CLI --add-source "C:\AI\SiteKit\SiteKit.CLI\SiteKit\bin\Debug"
copy "C:\AI\SiteKit\SiteKit.CLI\SiteKit\bin\Debug\SiteKit.CLI.1.0.0.nupkg" "C:\AI\SiteKit\Releases\SiteKit.CLI.1.0.0.nupkg"

REM dotnet tool install -g SiteKit.CLI --add-source "https://github.com/robertobarbedo/SiteKit/raw/refs/heads/main/Releases/SiteKit.CLI.1.0.0.nupkg"