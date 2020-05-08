using StaticWebEpiserverPlugin.RequiredCssOnly.Interfaces;
using System.Linq;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Services
{
    public class RequiredCssOnlyService : IRequiredCssOnlyService
    {
        const string REGEX_FIND_ALL_STATEMENTS = @"(?<statement>(?<selectors>[a-zA-Z0-9.*+""^,:#_\-= \(\)\[\]>]+){(?<changesToApply>[^}|{]+)})";
        const string REGEX_FIND_SELECTORS = @"(?<selector>[^,]+)";
        const string REGEX_FIND_SELECTOR_SECTION = @"(?<section>[#.]*(?>)[a-zA-Z0-9_\-]+)(?<states>[::](?>[a-zA-Z-(\[\])]+)*)*";
        const string REGEX_FIND_ID = @"id=[""|'](?<id>[^""|']+)[""|']";
        const string REGEX_FIND_CLASS = @"class=[""|'](?<classNames>[^""|']+)[""|']";

        public string RemoveUnusedRules(string cssContent, string htmlContent)
        {
            string resultContent = cssContent;

            RegexOptions options = RegexOptions.Multiline;

            var matchStatements = Regex.Matches(cssContent, REGEX_FIND_ALL_STATEMENTS, options);
            foreach (Match statement in matchStatements)
            {
                var hasStatement = false;
                var selectorsGroup = statement.Groups["selectors"];
                if (!selectorsGroup.Success)
                {
                    continue;
                }

                var matchSelectors = Regex.Matches(selectorsGroup.Value, REGEX_FIND_SELECTORS);
                foreach (Match selector in matchSelectors)
                {
                    var hasSelector = true;
                    var selectorGroup = selector.Groups["selector"];
                    if (!selectorGroup.Success)
                    {
                        // nothing todo here
                        break;
                    }

                    var matchSections = Regex.Matches(selectorGroup.Value, REGEX_FIND_SELECTOR_SECTION);
                    foreach (Match matchSection in matchSections)
                    {
                        var sectionGroup = matchSection.Groups["section"];
                        if (!sectionGroup.Success)
                        {
                            // nothing todo here
                            break;
                        }

                        var section = sectionGroup.Value;
                        if (string.IsNullOrEmpty(section))
                        {
                            // nothing todo here
                            break;
                        }

                        /***
                         * Special case logic for names with special meaning
                         * class    = attribute
                         * type     = attribute
                         * from     = animation start rule
                         * to       = animation end rule
                         ***/
                        if (section.Equals("class", System.StringComparison.OrdinalIgnoreCase)
                            || section.Equals("type", System.StringComparison.OrdinalIgnoreCase)
                            || section.Equals("from", System.StringComparison.OrdinalIgnoreCase)
                            || section.Equals("to", System.StringComparison.OrdinalIgnoreCase))
                        {
                            hasSelector = true;
                            break;
                        }

                        if (section.StartsWith("."))
                        {
                            var className = section.Substring(1);
                            // class
                            var matchClasses = Regex.Matches(htmlContent, REGEX_FIND_CLASS);
                            if (!matchClasses.Cast<Match>().Select(match => match.Groups["classNames"]).Where(group => group.Success).Select(group => group.Value).Any(classNames => classNames.Contains(className)))
                            {
                                hasSelector = false;
                                break;
                            }
                        }
                        else if (section.StartsWith("#"))
                        {
                            // id
                            var idName = section.Substring(1);
                            var matchIds = Regex.Matches(htmlContent, REGEX_FIND_ID);
                            if (!matchIds.Cast<Match>().Select(match => match.Groups["id"]).Where(group => group.Success).Select(group => group.Value).Any(id => id == idName))
                            {
                                hasSelector = false;
                                break;
                            }
                        }
                        else if (htmlContent.IndexOf("<" + section) == -1)
                        {
                            // element
                            hasSelector = false;
                            break;
                        }
                    }

                    if (hasSelector)
                    {
                        hasStatement = true;
                        break;
                    }
                }

                if (!hasStatement)
                {
                    resultContent = resultContent.Replace(statement.Groups["statement"].Value, "");
                }
            }

            return resultContent;
        }
    }
}
