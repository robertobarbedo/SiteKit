using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildComponentDatasources : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildComponentDatasources(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            // This is now async but IRun interface expects sync - we'll use Task.Run to handle it
            Task.Run(async () => await ProcessAsync(args)).Wait();
        }

        public async Task ProcessAsync(AutoArgs args)
        {
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                if (component.HasFields())
                    await CreateOrUpdateAsync(args, component);
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var templatePath = $"{site.DatasourceTemplatePath}/{component.Category}/{component.Name}";

                // Try to retrieve the template item by path
                var existingTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);

                string templateId;

                // Create the datasource template if it doesn't exist
                if (existingTemplate == null)
                {
                    _logger.LogDebug($"Creating datasource template: {component.Name} at path: {templatePath}");

                    // Get the parent folder
                    var parentFolderPath = $"{site.DatasourceTemplatePath}/{component.Category}";
                    var parentFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentFolderPath, verbose: true);

                    if (parentFolder == null)
                    {
                        _logger.LogError($"Parent folder not found at path: {parentFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Parent folder not found at path: {parentFolderPath}";
                        return;
                    }

                    // Create the template using CreateTemplateAsync
                    var templateResponse = await _graphQLService.CreateTemplateAsync(
                        args.Endpoint,
                        args.AccessToken,
                        component.Name,
                        parentFolder.ItemId,
                        null, // No sections initially, will be added later
                        verbose: true);

                    if (templateResponse == null)
                    {
                        _logger.LogError($"Failed to create datasource template: {component.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create datasource template: {component.Name}";
                        return;
                    }

                    // Get the actual item ID by path since CreateTemplateAsync doesn't return itemId
                    var createdTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
                    if (createdTemplate == null)
                    {
                        _logger.LogError($"Failed to retrieve created template: {component.Name} at path: {templatePath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to retrieve created template: {component.Name}";
                        return;
                    }

                    templateId = createdTemplate.ItemId;
                }
                else
                {
                    templateId = existingTemplate.ItemId;
                    _logger.LogDebug($"Using existing datasource template: {component.Name} (ID: {templateId})");
                }

                // Update template with icon if specified
                if (!string.IsNullOrWhiteSpace(component.Icon))
                {
                    var iconFields = new Dictionary<string, string>
                    {
                        ["__Icon"] = component.Icon
                    };

                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, templateId, iconFields, verbose: true);
                }

                // Prepare the sections and fields to update the template using UpdateTemplateAsync
                if (component.Fields != null && component.Fields.Any())
                {
                    // Set default section for fields without a section
                    foreach (var field in component.Fields)
                    {
                        if (string.IsNullOrWhiteSpace(field.Section))
                            field.Section = "Content"; // Default
                    }

                    // Group fields by section
                    var fieldsBySection = component.Fields.GroupBy(f => f.Section);
                    var templateSections = new List<UpdateTemplateSection>();

                    foreach (var sectionGroup in fieldsBySection)
                    {
                        var sectionName = sectionGroup.Key;
                        var templateFields = new List<UpdateTemplateField>();

                        foreach (var field in sectionGroup)
                        {
                            var templateField = new UpdateTemplateField
                            {
                                Name = field.Name,
                                Type = field.Type
                            };
                            templateFields.Add(templateField);
                        }

                        var templateSection = new UpdateTemplateSection
                        {
                            Name = sectionName,
                            Fields = templateFields
                        };
                        templateSections.Add(templateSection);
                    }

                    // Before calling update, remove from templateSections all fields that already exist in the template
                    if (existingTemplate != null && existingTemplate.Fields != null)
                    {
                        var existingFieldNames = new HashSet<string>(existingTemplate.Fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

                        foreach (var section in templateSections)
                        {
                            if (section.Fields != null)
                            {
                                section.Fields = section.Fields.Where(f => f.Name != null && !existingFieldNames.Contains(f.Name)).ToList();
                            }
                        }

                        // Remove sections that have no fields after filtering
                        templateSections = templateSections.Where(s => s.Fields != null && s.Fields.Any()).ToList();

                        if (templateSections.Any())
                        {
                            _logger.LogDebug($"After filtering existing fields, {templateSections.Sum(s => s.Fields?.Count ?? 0)} new fields will be added across {templateSections.Count} sections");
                        }
                        else
                        {
                            _logger.LogDebug($"All fields already exist in template: {component.Name}");
                        }
                    }

                    // Create or update template sections and fields individually
                    if (templateSections.Any())
                    {
                        var fieldsCreated = 0;
                        var sectionsCreated = 0;

                        foreach (var section in templateSections)
                        {
                            if (string.IsNullOrEmpty(section.Name))
                            {
                                _logger.LogDebug("Skipping section with null or empty name");
                                continue;
                            }

                            // Check if section exists
                            var sectionPath = $"{templatePath}/{section.Name}";
                            var existingSection = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sectionPath, verbose: true);

                            string sectionId;

                            if (existingSection == null)
                            {
                                // Create new section
                                _logger.LogDebug($"Creating template section: {section.Name}");
                                var createdSectionId = await _graphQLService.CreateItemAsync(
                                    args.Endpoint,
                                    args.AccessToken,
                                    section.Name,
                                    "{E269FBB5-3750-427A-9149-7AA950B49301}", // Template Section template ID
                                    templateId,
                                    verbose: true);

                                if (createdSectionId == null)
                                {
                                    _logger.LogError($"Failed to create template section: {section.Name}");
                                    args.IsValid = false;
                                    args.ValidationMessage = $"Failed to create template section: {section.Name}";
                                    return;
                                }

                                sectionId = createdSectionId;
                                sectionsCreated++;
                            }
                            else
                            {
                                sectionId = existingSection.ItemId;
                                _logger.LogDebug($"Using existing template section: {section.Name} (ID: {sectionId})");
                            }

                            // Create fields within this section
                            if (section.Fields != null)
                            {
                                foreach (var field in section.Fields)
                                {
                                    if (string.IsNullOrEmpty(field.Name))
                                    {
                                        _logger.LogDebug("Skipping field with null or empty name");
                                        continue;
                                    }

                                    var fieldPath = $"{sectionPath}/{field.Name}";
                                    var existingField = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, fieldPath, verbose: true);

                                    if (existingField == null)
                                    {
                                        // Create new field
                                        _logger.LogDebug($"Creating template field: {field.Name} in section {section.Name}");
                                        var fieldId = await _graphQLService.CreateItemAsync(
                                            args.Endpoint,
                                            args.AccessToken,
                                            field.Name,
                                            "{455A3E98-A627-4B40-8035-E683A0331AC7}", // Template Field template ID
                                            sectionId,
                                            verbose: true);

                                        if (fieldId == null)
                                        {
                                            _logger.LogError($"Failed to create template field: {field.Name}");
                                            args.IsValid = false;
                                            args.ValidationMessage = $"Failed to create template field: {field.Name}";
                                            return;
                                        }

                                        // Set field type
                                        if (!string.IsNullOrEmpty(field.Type))
                                        {
                                            var fieldFields = new Dictionary<string, string>
                                            {
                                                ["Type"] = field.Type
                                            };

                                            await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, fieldId, fieldFields, verbose: true);
                                        }

                                        fieldsCreated++;
                                    }
                                    else
                                    {
                                        // Update existing field type if needed
                                        _logger.LogDebug($"Updating existing template field: {field.Name}");
                                        if (!string.IsNullOrEmpty(field.Type))
                                        {
                                            var fieldFields = new Dictionary<string, string>
                                            {
                                                ["Type"] = field.Type
                                            };

                                            await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, existingField.ItemId, fieldFields, verbose: true);
                                        }
                                    }
                                }
                            }
                        }

                        _logger.LogDebug($"Successfully processed template: {component.Name} - Created {sectionsCreated} sections and {fieldsCreated} fields");
                    }
                    else
                    {
                        _logger.LogDebug($"No template sections to update for component: {component.Name}");
                    }
                }

                // Create standard values item
                await CreateStandardValuesAsync(args, component, templateId, site);

                // Create folder template for datasource organization
                await CreateFolderTemplateAsync(args, component, templateId, site);

                _logger.LogDebug($"Successfully processed datasource template: {component.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing component datasource: {component.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing component datasource: {component.Name} - {ex.Message}";
                throw;
            }
        }

        private async Task CreateStandardValuesAsync(AutoArgs args, ComponentDefinition component, string templateId, SiteDefinition site)
        {
            try
            {
                string? standardValuesId;
                var standardValuesPath = $"{site.DatasourceTemplatePath}/{component.Category}/{component.Name}/__Standard Values";
                var existingStandardValues = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, standardValuesPath, verbose: true);

                if (existingStandardValues == null)
                {
                    // Create standard values item
                    standardValuesId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        "__Standard Values",
                        templateId, // Use the template itself as the template for standard values
                        templateId, // Parent is the template
                        verbose: true);

                    if (standardValuesId != null)
                    {
                        // Update template to reference standard values
                        var templateFields = new Dictionary<string, string>
                        {
                            ["__Standard values"] = standardValuesId
                        };
                        await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, templateId, templateFields, verbose: true);


                        _logger.LogDebug($"Created standard values for template: {component.Name}");
                    }
                }
                else
                {
                    standardValuesId = existingStandardValues.ItemId;
                }
                // Set default field values and workflow
                var standardValuesFields = new Dictionary<string, string>
                {
                    ["__Default workflow"] = site.Defaults.DatasourceWorkflow,
                    ["__Enable item fallback"] = site.Defaults.LanguageFallback ? "1" : "0"
                };

                // Add field defaults from component configuration
                if (component.Fields != null)
                {
                    foreach (var field in component.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Default))
                            standardValuesFields[field.Name] = field.Default;
                    }
                }

                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, standardValuesId, standardValuesFields, verbose: true);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating standard values for component: {component.Name}");
                throw;
            }
        }

        private async Task CreateFolderTemplateAsync(AutoArgs args, ComponentDefinition component, string templateId, SiteDefinition site)
        {
            try
            {
                var folderTemplateName = $"{component.Name} Folder";
                var folderTemplatePath = $"{site.DatasourceTemplatePath}/{component.Category}/{folderTemplateName}";
                var existingFolderTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, folderTemplatePath, verbose: true);

                if (existingFolderTemplate == null)
                {
                    // Get parent folder
                    var parentFolderPath = $"{site.DatasourceTemplatePath}/{component.Category}";
                    var parentFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentFolderPath, verbose: true);

                    if (parentFolder != null)
                    {
                        // Create folder template
                        var folderTemplateId = await _graphQLService.CreateItemAsync(
                            args.Endpoint,
                            args.AccessToken,
                            folderTemplateName,
                            "{AB86861A-6030-46C5-B394-E8F99E8B87DB}", // Sitecore.TemplateIDs.Template
                            parentFolder.ItemId,
                            verbose: true);

                        if (folderTemplateId != null)
                        {
                            // Update folder template with icon
                            var folderTemplateFields = new Dictionary<string, string>
                            {
                                ["__Icon"] = "office/32x32/folder_window.png"
                            };
                            await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, folderTemplateId, folderTemplateFields, verbose: true);

                            // Create standard values for folder template
                            var folderStandardValuesId = await _graphQLService.CreateItemAsync(
                                args.Endpoint,
                                args.AccessToken,
                                "__Standard Values",
                                folderTemplateId,
                                folderTemplateId,
                                verbose: true);

                            if (folderStandardValuesId != null)
                            {
                                // Update folder template to reference its standard values
                                var folderTemplateStandardValuesFields = new Dictionary<string, string>
                                {
                                    ["__Standard values"] = folderStandardValuesId
                                };
                                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, folderTemplateId, folderTemplateStandardValuesFields, verbose: true);

                                // Set masters field on folder standard values
                                var folderStandardValuesFields = new Dictionary<string, string>
                                {
                                    ["__Masters"] = $"{templateId}|{folderTemplateId}"
                                };
                                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, folderStandardValuesId, folderStandardValuesFields, verbose: true);

                                _logger.LogDebug($"Created folder template: {folderTemplateName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating folder template for component: {component.Name}");
                throw;
            }
        }
    }
}
