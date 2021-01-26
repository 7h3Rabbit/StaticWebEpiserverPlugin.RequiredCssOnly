using System.Collections.Generic;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public class ContentRuleset : ContentPart
    {
        public string SelectorList { get; set; }
        public List<ContentRuleset> Parts { get; set; }
    }
}
