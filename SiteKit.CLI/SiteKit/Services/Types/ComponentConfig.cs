using System.Collections.Generic;

namespace SiteKit.Types
{
    public class ComponentConfig
    {
        public List<ComponentDefinition> Components { get; set; }
    }

    public class ComponentDefinition
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public List<string> Variants { get; set; }
        public List<FieldDefinition> Fields { get; set; }
        public List<ParameterDefinition> Parameters { get; set; }
        public List<RenderingDefinition> Rendering { get; set; }

        public bool HasFields()
        {
            return this.Fields != null && this.Fields.Count > 0;
        }
    }

    public class ParameterDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<StyleDefinition> Styles { get; set; }
    }

    public class StyleDefinition
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class RenderingDefinition
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
