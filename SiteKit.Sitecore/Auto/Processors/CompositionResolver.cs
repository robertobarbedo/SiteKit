using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class CompositionResolver
    {
        public void Process(AutoArgs args)
        {
            ExpandGroups(args.CompositionConfig);
        }

        public void ExpandGroups(CompositionConfig config)
        {
            var groups = config.Composition.Groups;

            config.Composition.Pages = ExpandNestedSection(config.Composition.Pages, groups);
            config.Composition.Components = ExpandNestedSection(config.Composition.Components, groups);
        }

        private Dictionary<string, Dictionary<string, List<string>>> ExpandNestedSection(
            Dictionary<string, Dictionary<string, List<string>>> source,
            Dictionary<string, List<string>> groups)
        {
            var expanded = new Dictionary<string, Dictionary<string, List<string>>>();

            foreach (var entry in source)
            {
                var expandedPlaceholders = new Dictionary<string, List<string>>();

                foreach (var placeholder in entry.Value)
                {
                    var result = new List<string>();

                    foreach (var item in placeholder.Value)
                    {
                        if (groups.TryGetValue(item, out var groupItems))
                        {
                            result.AddRange(groupItems);
                        }
                        else
                        {
                            result.Add(item);
                        }
                    }

                    expandedPlaceholders[placeholder.Key] = result;
                }

                expanded[entry.Key] = expandedPlaceholders;
            }

            return expanded;
        }

        private Dictionary<string, List<string>> ExpandSection(
            Dictionary<string, List<string>> source,
            Dictionary<string, List<string>> groups)
        {
            var expanded = new Dictionary<string, List<string>>();

            foreach (var entry in source)
            {
                var result = new List<string>();

                foreach (var item in entry.Value)
                {
                    if (groups.TryGetValue(item, out var groupItems))
                    {
                        result.AddRange(groupItems);
                    }
                    else
                    {
                        result.Add(item);
                    }
                }

                expanded[entry.Key] = result;
            }

            return expanded;
        }
    }
}
