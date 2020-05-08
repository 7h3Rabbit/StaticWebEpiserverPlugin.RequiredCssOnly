namespace StaticWebEpiserverPlugin.RequiredCssOnly.Interfaces
{
    public interface IRequiredCssOnlyService
    {
        string RemoveUnusedRules(string cssContent, string htmlContent);
    }
}