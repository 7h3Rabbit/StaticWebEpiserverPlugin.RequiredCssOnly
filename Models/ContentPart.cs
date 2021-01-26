namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public class ContentPart
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string Content { get; set; }
        public PartType Type { get; set; }
    }
}
