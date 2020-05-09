namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public partial class RequiredCssOnlyService
    {
        public class CssRuleset
        {
            public string Value { get; set; }
            public string SelectorList { get; set; }
            public string DeclarationBlock { get; set; }
            public string Declarations { get; set; }
        }
    }
}
