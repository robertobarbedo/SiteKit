using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteKit.CLI.Services.Shared
{
    public class _ReadYaml : IRun
    {
        public void Run(AutoArgs args)
        {
            // Get current directory and construct path to .sitekit/<sitename>
            var currentDirectory = Directory.GetCurrentDirectory();
            var siteKitDirectory = Path.Combine(currentDirectory, ".sitekit", args.SiteName);

            // Validate that the folder exists
            if (!Directory.Exists(siteKitDirectory))
            {
                args.IsValid = false;
                args.ValidationMessage = $"YAML directory not found: {siteKitDirectory}";
                return;
            }

            try
            {
                // Get all YAML files in the directory
                var yamlFiles = Directory.GetFiles(siteKitDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
                
                if (yamlFiles.Length == 0)
                {
                    args.IsValid = false;
                    args.ValidationMessage = $"No YAML files found in directory: {siteKitDirectory}";
                    return;
                }

                // Clear existing yamls dictionary
                args.Yamls.Clear();

                // Read each YAML file and add to the dictionary
                foreach (var filePath in yamlFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var fileContent = File.ReadAllText(filePath);
                        
                        // Add to the dictionary with filename as key and content as value
                        args.Yamls[fileName] = fileContent;
                    }
                    catch (Exception ex)
                    {
                        args.IsValid = false;
                        args.ValidationMessage = $"Error reading file {filePath}: {ex.Message}";
                        return;
                    }
                }

                args.IsValid = true;
                args.ValidationMessage = $"Successfully read {args.Yamls.Count} YAML files from {siteKitDirectory}";
            }
            catch (Exception ex)
            {
                args.IsValid = false;
                args.ValidationMessage = $"Error accessing directory {siteKitDirectory}: {ex.Message}";
            }
        }
    }
}
