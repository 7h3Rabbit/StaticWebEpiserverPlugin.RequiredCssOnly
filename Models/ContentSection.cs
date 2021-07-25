namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public class ContentSection
    {
        public int Index { get; set; }
        public string Content { get; set; }
        public CssSelectorType Type { get; set; }
        public string CleanedContent { get; set; }
        public bool Used { get; set; }
    }
}
