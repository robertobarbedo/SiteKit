using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SiteKit.Types
{
    public class SiteConfig
    {
        public SiteDefinition Site { get; set; }
    }

    public class SiteDefinition
    {
        public string Name { get; set; }

        [YamlMember(Alias = "site_path")]
        public string SitePath { get; set; }

        [YamlMember(Alias = "default_language")]
        public string DefaultLanguage { get; set; }

        [YamlMember(Alias = "dictionary_path")]
        public string DictionaryPath { get; set; }

        [YamlMember(Alias = "site_template_path")]
        public string SiteTemplatePath { get; set; }

        [YamlMember(Alias = "datasource_template_path")]
        public string DatasourceTemplatePath { get; set; }

        [YamlMember(Alias = "rendering_path")]
        public string RenderingPath { get; set; }

        [YamlMember(Alias = "placeholder_path")]
        public string PlaceholderPath { get; set; }

        [YamlMember(Alias = "available_renderings_path")]
        public string AvailableRenderingsPath { get; set; }

        [YamlMember(Alias = "site_placeholder_path")]
        public string SitePlaceholderPath { get; set; }

        public SiteDefaults Defaults { get; set; }
    }

    public class SiteDefaults
    {
        [YamlMember(Alias = "page_workflow")]
        public string PageWorkflow { get; set; }
        [YamlMember(Alias = "datasource_workflow")]
        public string DatasourceWorkflow { get; set; }

        [YamlMember(Alias = "language_fallback")]
        public bool LanguageFallback { get; set; }
    }
}