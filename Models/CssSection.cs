namespace StaticWebEpiserverPlugin.RequiredCssOnly.Models
{
    public enum CssSelectorType
    {
        Unknown = 0,
        // Universal selector (*)
        UniversalSelector,
        // Type selector (element selector)
        TypeSelector,
        // class selector (.)
        ClassSelector,
        // ID selector (#)
        IdSelector,
        // attribute selector ([])
        AttributeSelector
    }

    public class CssSection
    {
        public CssSelectorType Type { get; set; }
        public string Value { get; set; }
    }
}
