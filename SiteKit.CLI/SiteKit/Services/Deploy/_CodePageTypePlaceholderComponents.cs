using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Deploy
{
    internal class _CodePageTypePlaceholderComponents : IRun
    {
        private readonly ILogger _logger;

        public _CodePageTypePlaceholderComponents(ILogger logger)
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
                // Create "page-types" folder if it doesn't exist
                var pageTypesFolder = Path.Combine(componentsPath, "page-types");
                if (!Directory.Exists(pageTypesFolder))
                {
                    Directory.CreateDirectory(pageTypesFolder);
                    _logger.LogDebug($"Created page-types folder: {pageTypesFolder}");
                }
                else
                {
                    _logger.LogDebug($"Page-types folder already exists: {pageTypesFolder}");
                }

                // Process each page type from composition
                if (args.CompositionConfig?.Composition?.Pages != null)
                {
                    foreach (var pageType in args.CompositionConfig.Composition.Pages)
                    {
                        var pageTypeName = pageType.Key;
                        var placeholders = pageType.Value;

                        // Get the first placeholder name (as requested)
                        var firstPlaceholderName = placeholders.Keys.FirstOrDefault();

                        if (string.IsNullOrWhiteSpace(firstPlaceholderName))
                        {
                            _logger.LogDebug($"No placeholders found for page type: {pageTypeName}");
                            continue;
                        }

                        // Generate file name: "_" + pagetype.replace(" ","") + ".tsx"
                        var fileName = "_" + pageTypeName.Replace(" ", "") + ".tsx";
                        var filePath = Path.Combine(pageTypesFolder, fileName);

                        // Skip if file already exists
                        if (File.Exists(filePath))
                        {
                            _logger.LogDebug($"File already exists, skipping: {filePath}");
                            continue;
                        }

                        // Generate placeholder key: page type in lowercase, replacing " " by "-", plus placeholder name
                        var phKey = pageTypeName.ToLower().Replace(" ", "-") + "-" + firstPlaceholderName.ToLower();

                        // Generate the TypeScript React component content
                        var componentContent = GenerateComponentContent(phKey);

                        // Write the file
                        File.WriteAllText(filePath, componentContent);
                        _logger.LogInformation($"Created page type component file: {filePath}");
                    }
                }
                else
                {
                    _logger.LogDebug("No composition pages found to process");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating page type placeholder components");
                args.IsValid = false;
                args.ValidationMessage = $"Error creating page type placeholder components: {ex.Message}";
            }
        }

        private string GenerateComponentContent(string phKey)
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
            sb.AppendLine("  // Dynamic placeholder keys - component-name + placeholder-composition + DynamicPlaceholderId");
            sb.AppendLine($"  const phKey = `{phKey}-${{params?.DynamicPlaceholderId ?? ''}}`;");
            sb.AppendLine("  return <Placeholder name={phKey} rendering={props.rendering} />;");
            sb.AppendLine("};");
            return sb.ToString();
        }
    }
}
