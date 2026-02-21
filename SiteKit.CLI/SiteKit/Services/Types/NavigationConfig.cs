using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SiteKit.Types
{
    public class NavigationConfig
    {
        public NavigationDefinition? Navigation { get; set; }
    }

    public class NavigationDefinition
    {
        public NavigationTemplates? Templates { get; set; }
        public Dictionary<string, List<MenuItem>>? Menus { get; set; }
    }

    public class NavigationTemplates
    {
        [YamlMember(Alias = "navigation_domain")]
        public string? NavigationDomain { get; set; }

        [YamlMember(Alias = "menu")]
        public string? Menu { get; set; }

        [YamlMember(Alias = "menuitem")]
        public string? MenuItem { get; set; }
    }

    public class MenuItem
    {
        [YamlMember(Alias = "label")]
        public string? Label { get; set; }

        [YamlMember(Alias = "link")]
        public string? Link { get; set; }

        [YamlMember(Alias = "children")]
        public List<MenuItem>? Children { get; set; }
    }
}
