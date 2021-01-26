using System.Collections.Generic;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public class ContentSelectorList : ContentPart
    {
        public ContentSelectorList()
        {
            Selectors = new List<ContentSelector>();
        }
        public string CleanedContent { get; set; }
        public List<ContentSelector> Selectors { get; set; }
        public bool Used { get; set; }
    }
}
