dotnet tool uninstall -g SiteKit.CLI
dotnet tool install -g SiteKit.CLI --add-source "C:\AI\SiteKit\SiteKit.CLI\SiteKit\bin\Release"
copy "C:\AI\SiteKit\SiteKit.CLI\SiteKit\bin\Release\SiteKit.CLI.1.0.0.nupkg" "C:\AI\SiteKit\Releases\"

REM dotnet tool install -g SiteKit.CLI --add-source "https://github.com/robertobarbedo/SiteKit/raw/refs/heads/main/Releases/SiteKit.CLI.1.0.0.nupkg"