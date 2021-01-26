using System.Collections.Generic;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public class ContentSelector : ContentPart
    {
        public ContentSelector()
        {
            Sections = new List<ContentSection>();
        }
        public string CleanedContent { get; set; }
        public List<ContentSection> Sections { get; set; }
        public bool Used { get; set; }
    }
}
