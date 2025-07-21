using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildDictionary : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildDictionary(IGraphQLService graphQLService, ILogger logger)
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
            if (args.DictionaryConfig?.Dictionary?.Entries == null || !args.DictionaryConfig.Dictionary.Entries.Any())
            {
                _logger.LogInformation("No dictionary entries found to process");
                return;
            }

            var dictionaryPath = args.SiteConfig?.Site?.DictionaryPath;
            if (string.IsNullOrEmpty(dictionaryPath))
            {
                _logger.LogError("Dictionary path not configured in site settings");
                return;
            }

            _logger.LogInformation($"Processing {args.DictionaryConfig.Dictionary.Entries.Count} dictionary entries");

            foreach (var entry in args.DictionaryConfig.Dictionary.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Phrase))
                {
                    _logger.LogWarning($"Skipping invalid dictionary entry: Key='{entry.Key}', Phrase='{entry.Phrase}'");
                    continue;
                }

                await CreateOrUpdateDictionaryEntryAsync(args, dictionaryPath, entry);
            }

            _logger.LogInformation("Dictionary processing completed");
        }

        private async Task CreateOrUpdateDictionaryEntryAsync(AutoArgs args, string dictionaryPath, DictionaryEntry entry)
        {
            try
            {
                // Validate required parameters
                if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
                {
                    _logger.LogError("Endpoint or AccessToken is missing");
                    return;
                }

                if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Phrase))
                {
                    _logger.LogWarning($"Entry Key or Phrase is null/empty");
                    return;
                }

                // Get the first letter of the key to determine the folder
                string firstLetter = entry.Key.Substring(0, 1).ToUpper();
                string letterFolderPath = $"{dictionaryPath}/{firstLetter}";
                string entryPath = $"{letterFolderPath}/{entry.Key}";

                // Ensure the letter folder exists
                await EnsureLetterFolderExistsAsync(args, letterFolderPath, firstLetter);

                // Check if the dictionary entry already exists
                var existingEntry = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, entryPath, verbose: true);

                if (existingEntry != null)
                {
                    // Update existing entry
                    await UpdateDictionaryEntryAsync(args, existingEntry.ItemId, entry);
                }
                else
                {
                    // Create new entry
                    await CreateDictionaryEntryAsync(args, letterFolderPath, entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing dictionary entry '{entry.Key}'");
            }
        }

        private async Task EnsureLetterFolderExistsAsync(AutoArgs args, string letterFolderPath, string firstLetter)
        {
            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                _logger.LogError("Endpoint or AccessToken is missing");
                return;
            }

            // Check if the letter folder exists
            var existingFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, letterFolderPath, verbose: true);

            if (existingFolder == null)
            {
                // Get the parent dictionary folder to create the letter folder under it
                var parentPath = letterFolderPath.Substring(0, letterFolderPath.LastIndexOf('/'));
                var parentFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentPath, verbose: true);

                if (parentFolder != null)
                {
                    _logger.LogDebug($"Creating dictionary letter folder: {firstLetter}");
                    var folderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        firstLetter,
                        "{267D9AC7-5D85-4E9D-AF89-99AB296CC218}", // Dictionary folder template
                        parentFolder.ItemId,
                        verbose: true);

                    if (folderId != null)
                    {
                        _logger.LogInformation($"Successfully created dictionary letter folder: {firstLetter}");
                        await SetDefaultFieldsAsync(args, folderId);
                    }
                    else
                    {
                        _logger.LogError($"Failed to create dictionary letter folder: {firstLetter}");
                    }
                }
                else
                {
                    _logger.LogError($"Parent dictionary folder not found at path: {parentPath}");
                }
            }
        }

        private async Task CreateDictionaryEntryAsync(AutoArgs args, string letterFolderPath, DictionaryEntry entry)
        {
            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                _logger.LogError("Endpoint or AccessToken is missing");
                return;
            }

            if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Phrase))
            {
                _logger.LogWarning($"Entry Key or Phrase is null/empty");
                return;
            }

            // Get the letter folder
            var letterFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, letterFolderPath, verbose: true);

            if (letterFolder == null)
            {
                _logger.LogError($"Letter folder not found at path: {letterFolderPath}");
                return;
            }

            _logger.LogDebug($"Creating dictionary entry: {entry.Key}");

            // Create the dictionary entry item
            var entryId = await _graphQLService.CreateItemAsync(
                args.Endpoint,
                args.AccessToken,
                entry.Key,
                "{6D1CD897-1936-4A3A-A511-289A94C2A7B1}", // Dictionary entry template
                letterFolder.ItemId,
                verbose: true);

            if (entryId != null)
            {
                _logger.LogInformation($"Successfully created dictionary entry: {entry.Key}");

                // Update the entry with Key and Phrase values
                var fields = new Dictionary<string, string>
                {
                    { "Key", entry.Key },
                    { "Phrase", entry.Phrase }
                };

                var updateResult = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, entryId, fields, verbose: true);

                if (updateResult != null)
                {
                    _logger.LogDebug($"Successfully updated dictionary entry fields for: {entry.Key}");
                    await SetDefaultFieldsAsync(args, entryId);
                }
                else
                {
                    _logger.LogError($"Failed to update dictionary entry fields for: {entry.Key}");
                }
            }
            else
            {
                _logger.LogError($"Failed to create dictionary entry: {entry.Key}");
            }
        }

        private async Task UpdateDictionaryEntryAsync(AutoArgs args, string entryId, DictionaryEntry entry)
        {
            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                _logger.LogError("Endpoint or AccessToken is missing");
                return;
            }

            if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Phrase))
            {
                _logger.LogWarning($"Entry Key or Phrase is null/empty");
                return;
            }

            _logger.LogDebug($"Updating dictionary entry: {entry.Key}");

            var fields = new Dictionary<string, string>
            {
                { "Key", entry.Key },
                { "Phrase", entry.Phrase }
            };

            var updateResult = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, entryId, fields, verbose: true);

            if (updateResult != null)
            {
                _logger.LogInformation($"Successfully updated dictionary entry: {entry.Key}");
            }
            else
            {
                _logger.LogError($"Failed to update dictionary entry: {entry.Key}");
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Validate required parameters
                if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
                {
                    _logger.LogError("Endpoint or AccessToken is missing");
                    return;
                }

                // Set any default fields that might be needed for dictionary items
                var defaultFields = new Dictionary<string, string>();
                
                // Add any default fields if specified in site configuration
                var site = args.SiteConfig?.Site;
                if (site?.Defaults != null)
                {
                    if (!string.IsNullOrEmpty(site.Defaults.DatasourceWorkflow))
                    {
                        defaultFields["__Default workflow"] = site.Defaults.DatasourceWorkflow;
                    }
                    
                    if (site.Defaults.LanguageFallback)
                    {
                        defaultFields["__Enable item fallback"] = "1";
                    }
                }

                if (defaultFields.Any())
                {
                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, itemId, defaultFields, verbose: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting default fields for dictionary item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }
    }
}
