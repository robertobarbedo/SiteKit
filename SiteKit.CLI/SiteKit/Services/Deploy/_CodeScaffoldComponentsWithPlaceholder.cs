using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Deploy
{
    internal class _CodeScaffoldComponentsWithPlaceholder : IRun
    {
        private readonly ILogger _logger;

        public _CodeScaffoldComponentsWithPlaceholder(ILogger logger)
        {
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            // Validate if args.SiteConfig.Site.Code.ComponentsPath exists in the file system
            var componentsPath = args.SiteConfig?.Site?.Code?.ComponentsPath;

            if (string.IsNullOrWhiteSpace(componentsPath))
            {
                _logger.LogDebug("ComponentsPath is not configured in site code settings");
                return;
            }

            if (!Directory.Exists(componentsPath))
            {
                _logger.LogWarning($"Components path does not exist in the file system: {componentsPath}");
                return;
            }

            _logger.LogDebug($"Components path exists: {componentsPath}");

            try
            {
                // Process each component from composition
                if (args.CompositionConfig?.Composition?.Components != null)
                {
                    foreach (var compositionComponent in args.CompositionConfig.Composition.Components)
                    {
                        var componentName = compositionComponent.Key;
                        var placeholders = compositionComponent.Value;

                        // Find the component definition to get the category
                        var componentDef = args.ComponentConfig?.Components?.FirstOrDefault(c => c.Name == componentName);
                        if (componentDef == null)
                        {
                            _logger.LogWarning($"Component definition not found for: {componentName}");
                            continue;
                        }

                        // Generate file name: ComponentName + ".tsx"
                        var fileName = componentName.Replace(" ", "") + ".tsx";

                        // Create category folder path (lowercase, spaces to hyphens)
                        var categoryFolder = componentDef.Category?.ToLower().Replace(" ", "-") ?? "uncategorized";
                        var categoryPath = Path.Combine(componentsPath, categoryFolder);

                        // Create category folder if it doesn't exist
                        if (!Directory.Exists(categoryPath))
                        {
                            Directory.CreateDirectory(categoryPath);
                            _logger.LogDebug($"Created category folder: {categoryPath}");
                        }

                        var filePath = Path.Combine(categoryPath, fileName);

                        // Check if file already exists (search recursively in componentsPath)
                        if (FileExistsRecursively(componentsPath, fileName))
                        {
                            _logger.LogDebug($"File already exists somewhere in components path, skipping: {fileName}");
                            continue;
                        }

                        // Generate placeholder keys: component name + placeholder name
                        var phKeys = new Dictionary<string, string>();
                        foreach (var placeholder in placeholders)
                        {
                            var placeholderName = placeholder.Key;
                            var phKey = componentName.ToLowerInvariant().Replace(" ","-") + "-" + placeholderName;
                            phKeys[placeholderName] = phKey;
                        }

                        if (!phKeys.Any())
                        {
                            _logger.LogDebug($"No placeholders found for component: {componentName}");
                            continue;
                        }

                        // Generate the TypeScript React component content
                        var componentContent = GenerateComponentContent(phKeys);

                        // Write the file
                        File.WriteAllText(filePath, componentContent);
                        _logger.LogInformation($"Created component file: {filePath}");
                    }
                }
                else
                {
                    _logger.LogDebug("No composition components found to process");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating component placeholder files");
                args.IsValid = false;
                args.ValidationMessage = $"Error creating component placeholder files: {ex.Message}";
            }
        }

        private bool FileExistsRecursively(string rootPath, string fileName)
        {
            try
            {
                return Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories).Any();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error searching for file {fileName} in {rootPath}: {ex.Message}");
                return false;
            }
        }

        private string GenerateComponentContent(Dictionary<string, string> phKeys)
        {
            var sb = new StringBuilder();
            sb.AppendLine("import { JSX } from 'react';");
            sb.AppendLine("import React from 'react';");
            sb.AppendLine("import {");
            sb.AppendLine("  ComponentParams,");
            sb.AppendLine("  ComponentRendering,");
            sb.AppendLine("  Placeholder,");
            sb.AppendLine("} from '@sitecore-jss/sitecore-jss-nextjs';");
            sb.AppendLine();
            sb.AppendLine("interface ComponentProps {");
            sb.AppendLine("  rendering: ComponentRendering & { params: ComponentParams };");
            sb.AppendLine("  params: ComponentParams;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("export const Default = (props: ComponentProps): JSX.Element => {");
            sb.AppendLine("  const { params } = props;");
            sb.AppendLine("  // Dynamic placeholder keys - component-name + placeholder-name + DynamicPlaceholderId");
            
            foreach (var kvp in phKeys)
            {
                var placeholderName = kvp.Key;
                var phKey = kvp.Value;
                var variableName = placeholderName.Replace(" ", "").Replace("-", "");
                sb.AppendLine($"  const phKey_{variableName} = `{phKey}-${{params?.DynamicPlaceholderId ?? ''}}`;");
            }
            
            sb.AppendLine("  return (");
            sb.AppendLine("    <>");
            
            foreach (var kvp in phKeys)
            {
                var placeholderName = kvp.Key;
                var variableName = placeholderName.Replace(" ", "").Replace("-", "");
                sb.AppendLine($"      <div>");
                sb.AppendLine($"        <Placeholder name={{phKey_{variableName}}} rendering={{props.rendering}} />");
                sb.AppendLine($"      </div>");
            }
            
            sb.AppendLine("    </>");
            sb.AppendLine("  );");
            sb.AppendLine("};");
            
            return sb.ToString();
        }
    }
}
