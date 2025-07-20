using System.Collections.Generic;

namespace SiteKit.Types
{
    public class CompositionConfig
    {
        public CompositionDefinition Composition { get; set; } 
    }

    public class CompositionDefinition
    {
        public Dictionary<string, List<string>> Groups { get; set; }  
        public Dictionary<string, Dictionary<string, List<string>>> Pages { get; set; }  
        public Dictionary<string, Dictionary<string, List<string>>> Components { get; set; }  
    }
}
