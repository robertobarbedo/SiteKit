using System;
using System.Text.Json;
using SiteKit.Types;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


namespace SiteKit.Processors
{
    public class LoadTypes
    {
        public void Process(AutoArgs args)
        {
            args.SiteConfig = Parse<SiteConfig>(args.Yamls["sitesettings"]);
            args.ComponentConfig = Parse<ComponentConfig>(args.Yamls["components"]);
            args.PageTypesConfig = Parse<PageTypesConfig>(args.Yamls["pagetypes"]);
            args.CompositionConfig = Parse<CompositionConfig>(args.Yamls["composition"]);
        }

        public T Parse<T>(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<T>(yaml);
        }
    }
}
