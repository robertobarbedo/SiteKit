using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildPageTemplates : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildPageTemplates(IGraphQLService graphQLService, ILogger logger)
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
            var pageTypes = args.PageTypesConfig.PageTypes;
            foreach (var pageType in pageTypes)
            {
                await CreateOrUpdateAsync(args, pageType);
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, PageType pageType)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var templatePath = $"{site.SiteTemplatePath}/{pageType.Name}";

                // Try to retrieve the template item by path
                var existingTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);

                string templateId;

                // Create the page template if it doesn't exist
                if (existingTemplate == null)
                {
                    _logger.LogInformation($"Creating page template: {pageType.Name} at path: {templatePath}");

                    // Get the parent folder (site template path)
                    var parentFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, site.SiteTemplatePath, verbose: true);

                    if (parentFolder == null)
                    {
                        _logger.LogError($"Parent folder not found at path: {site.SiteTemplatePath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Parent folder not found at path: {site.SiteTemplatePath}";
                        return;
                    }

                    // Create the template using CreateTemplateAsync
                    var templateResponse = await _graphQLService.CreateTemplateAsync(
                        args.Endpoint,
                        args.AccessToken,
                        pageType.Name,
                        parentFolder.ItemId,
                        null, // No sections initially, will be added later
                        verbose: true);

                    if (templateResponse == null)
                    {
                        _logger.LogError($"Failed to create page template: {pageType.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create page template: {pageType.Name}";
                        return;
                    }

                    // Get the actual item ID by path since CreateTemplateAsync doesn't return itemId
                    var createdTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
                    if (createdTemplate == null)
                    {
                        _logger.LogError($"Failed to retrieve created template: {pageType.Name} at path: {templatePath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to retrieve created template: {pageType.Name}";
                        return;
                    }

                    templateId = createdTemplate.ItemId;
                }
                else
                {
                    templateId = existingTemplate.ItemId;
                    _logger.LogInformation($"Using existing page template: {pageType.Name} (ID: {templateId})");
                }

                // Update template with base template and icon
                var templateFields = new Dictionary<string, string>();

                var baseTemplateId = await GetBaseTemplateIdAsync(args, pageType);
                if (!string.IsNullOrEmpty(baseTemplateId))
                {
                    templateFields["__Base template"] = baseTemplateId;
                }

                templateFields["__Icon"] = "Office/32x32/document_text.png";

                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, templateId, templateFields, verbose: true);

                // Process fields if they exist
                if (pageType.Fields != null && pageType.Fields.Any())
                {
                    // Set default section for fields without a section
                    foreach (var field in pageType.Fields)
                    {
                        if (string.IsNullOrWhiteSpace(field.Section))
                            field.Section = "Content"; // Default
                    }

                    // Group fields by section
                    var fieldsBySection = pageType.Fields.GroupBy(f => f.Section);
                    var templateSections = new List<UpdateTemplateSection>();

                    foreach (var sectionGroup in fieldsBySection)
                    {
                        var sectionName = sectionGroup.Key;
                        var templateFields2 = new List<UpdateTemplateField>();

                        foreach (var field in sectionGroup)
                        {
                            var templateField = new UpdateTemplateField
                            {
                                Name = field.Name,
                                Type = field.Type
                            };
                            templateFields2.Add(templateField);
                        }

                        var templateSection = new UpdateTemplateSection
                        {
                            Name = sectionName,
                            Fields = templateFields2
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
                            _logger.LogInformation($"After filtering existing fields, {templateSections.Sum(s => s.Fields?.Count ?? 0)} new fields will be added across {templateSections.Count} sections");
                        }
                        else
                        {
                            _logger.LogInformation($"All fields already exist in template: {pageType.Name}");
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
                                _logger.LogWarning("Skipping section with null or empty name");
                                continue;
                            }

                            // Check if section exists
                            var sectionPath = $"{templatePath}/{section.Name}";
                            var existingSection = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sectionPath, verbose: true);

                            string sectionId;

                            if (existingSection == null)
                            {
                                // Create new section
                                _logger.LogInformation($"Creating template section: {section.Name}");
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
                                _logger.LogInformation($"Using existing template section: {section.Name} (ID: {sectionId})");
                            }

                            // Create fields within this section
                            if (section.Fields != null)
                            {
                                foreach (var field in section.Fields)
                                {
                                    if (string.IsNullOrEmpty(field.Name))
                                    {
                                        _logger.LogWarning("Skipping field with null or empty name");
                                        continue;
                                    }

                                    var fieldPath = $"{sectionPath}/{field.Name}";
                                    var existingField = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, fieldPath, verbose: true);

                                    if (existingField == null)
                                    {
                                        // Create new field
                                        _logger.LogInformation($"Creating template field: {field.Name} in section {section.Name}");
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

                                        // Set field type and source
                                        var fieldFields = new Dictionary<string, string>();

                                        if (!string.IsNullOrEmpty(field.Type))
                                        {
                                            fieldFields["Type"] = field.Type;
                                        }

                                        // Find the original field to get the source
                                        var originalField = pageType.Fields?.FirstOrDefault(f => f.Name == field.Name);
                                        if (originalField != null && !string.IsNullOrEmpty(originalField.Source))
                                        {
                                            fieldFields["Source"] = originalField.Source;
                                        }

                                        if (fieldFields.Any())
                                        {
                                            await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, fieldId, fieldFields, verbose: true);
                                        }

                                        fieldsCreated++;
                                    }
                                    else
                                    {
                                        // Update existing field type and source if needed
                                        _logger.LogInformation($"Updating existing template field: {field.Name}");
                                        var fieldFields = new Dictionary<string, string>();

                                        if (!string.IsNullOrEmpty(field.Type))
                                        {
                                            fieldFields["Type"] = field.Type;
                                        }

                                        // Find the original field to get the source
                                        var originalField = pageType.Fields?.FirstOrDefault(f => f.Name == field.Name);
                                        if (originalField != null && !string.IsNullOrEmpty(originalField.Source))
                                        {
                                            fieldFields["Source"] = originalField.Source;
                                        }

                                        if (fieldFields.Any())
                                        {
                                            await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, existingField.ItemId, fieldFields, verbose: true);
                                        }
                                    }
                                }
                            }
                        }

                        _logger.LogInformation($"Successfully processed template: {pageType.Name} - Created {sectionsCreated} sections and {fieldsCreated} fields");
                    }
                    else
                    {
                        _logger.LogInformation($"No template sections to update for page type: {pageType.Name}");
                    }
                }

                // Create standard values item
                await CreateStandardValuesAsync(args, pageType, templateId, site);

                _logger.LogInformation($"Successfully processed page template: {pageType.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing page template: {pageType.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing page template: {pageType.Name} - {ex.Message}";
                throw;
            }
        }

        private async Task CreateStandardValuesAsync(AutoArgs args, PageType pageType, string templateId, SiteDefinition site)
        {
            try
            {
                string? standardValuesId;
                var standardValuesPath = $"{site.SiteTemplatePath}/{pageType.Name}/__Standard Values";
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


                        _logger.LogInformation($"Created standard values for template: {pageType.Name}");
                    }
                }
                else
                {
                    standardValuesId = existingStandardValues.ItemId;
                }

                // Set default field values and workflow
                var standardValuesFields = new Dictionary<string, string>
                {
                    ["__Default workflow"] = site.Defaults.PageWorkflow ?? site.Defaults.DatasourceWorkflow,
                    ["__Enable item fallback"] = site.Defaults.LanguageFallback ? "1" : "0"
                };

                // Add field defaults from page type configuration
                if (pageType.Fields != null)
                {
                    foreach (var field in pageType.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Default))
                            standardValuesFields[field.Name] = field.Default;
                    }
                }

                if (standardValuesId != null)
                {
                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, standardValuesId, standardValuesFields, verbose: true);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating standard values for page type: {pageType.Name}");
                throw;
            }
        }

        private async Task<string> GetBaseTemplateIdAsync(AutoArgs args, PageType pageType)
        {
            try
            {
                string baseTemplatePath;
                
                if (!string.IsNullOrWhiteSpace(args.SiteConfig.Site.Defaults.PageBaseTemplate))
                {
                    baseTemplatePath = args.SiteConfig.Site.Defaults.PageBaseTemplate;
                }
                else
                {
                    // Use default base template path
                    baseTemplatePath = args.SiteConfig.Site.SiteTemplatePath + "/Page";
                }

                // If it's already an ID (starts with { or is a GUID format), return it directly
                if (baseTemplatePath.StartsWith("{") || Guid.TryParse(baseTemplatePath, out _))
                {
                    return baseTemplatePath;
                }

                // Otherwise, treat it as a path and get the item to retrieve its ID
                var baseTemplateItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, baseTemplatePath, verbose: true);
                
                if (baseTemplateItem != null)
                {
                    return baseTemplateItem.ItemId;
                }
                else
                {
                    _logger.LogWarning($"Base template not found at path: {baseTemplatePath}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving base template ID for page type: {pageType.Name}");
                return "";
            }
        }
    }
}