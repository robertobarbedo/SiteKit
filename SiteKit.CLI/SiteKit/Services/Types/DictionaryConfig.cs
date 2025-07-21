using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SiteKit.Types
{
    public class DictionaryConfig
    {
        public DictionaryDefinition? Dictionary { get; set; }
    }

    public class DictionaryDefinition
    {
        public List<DictionaryEntry>? Entries { get; set; }
    }

    public class DictionaryEntry
    {
        [YamlMember(Alias = "key")]
        public string? Key { get; set; }

        [YamlMember(Alias = "phrase")]
        public string? Phrase { get; set; }
    }
}
