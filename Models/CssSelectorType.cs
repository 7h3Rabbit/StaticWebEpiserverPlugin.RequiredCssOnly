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
}
