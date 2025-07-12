using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Linq;
using System.Text.Json;
using SiteKit.Types;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


namespace SiteKit.Processors
{
    public class BuildComponentCategoryFolders : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var types = args.ComponentConfig.Components.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();

            var site = GetSite(args);

            foreach (var type in types)
            {
                //datasource_template_path
                CreateOrUpdate(Sitecore.TemplateIDs.TemplateFolder, type, site.DatasourceTemplatePath, args.GetUniqueID("datasource_template_path", type));

                //rendering_path
                CreateOrUpdate(new ID("{7EE0975B-0698-493E-B3A2-0B2EF33D0522}"), type, site.RenderingPath, args.GetUniqueID("rendering_path", type));

                //available_renderings_path
                CreateOrUpdate(new ID("{76DA0A8D-FC7E-42B2-AF1E-205B49E43F98}"), type, site.AvailableRenderingsPath, args.GetUniqueID("available_renderings_path", type));

                //placeholder_settings_path
                CreateOrUpdate(new ID("{C3B037A0-46E5-4B67-AC7A-A144B962A56F}"), type, site.PlaceholderPath, args.GetUniqueID("placeholder_settings_path", type));

                //placeholder_settings_path_in_site
                CreateOrUpdate(new ID("{52288E39-7830-4694-B62D-32A54C6EF7BA}"), type, site.SitePath + "/Presentation/Placeholder Settings", args.GetUniqueID("placeholder_settings_path_in_site", type));

            }
        }

        private void CreateOrUpdate(ID template,  string type, string folderPath, ID id)
        {
            Item item = Database.GetItem(id);
            if (item == null)
            {
                var folder = Database.GetItem(folderPath);
                item = folder.Add(type, new TemplateID(template), id);
            }
            SetDefaultFields(item);
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
