using StaticWebEpiserverPlugin.RequiredCssOnly.Interfaces;
using StaticWebEpiserverPlugin.RequiredCssOnly.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using static StaticWebEpiserverPlugin.RequiredCssOnly.Models.RequiredCssOnlyService;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Services
{
    public partial class RequiredCssOnlyService : IRequiredCssOnlyService
    {
        const string REGEX_FIND_COMMENTS = @"(?<comment>\/\*.*\*\/)";
        const string REGEX_FIND_ALL_STATEMENTS = @"(?<ruleset>(?<selectorList>[^;{}]+)(?<declarationBlock>{(?<declarations>[^}{]+)}))";
        const string REGEX_FIND_SELECTORS = @"(?<selector>[^,]+)";
        const string REGEX_FIND_SELECTOR_SECTION = @"(?<section>[^>~+|| ]+)";
        const string REGEX_FIND_SELECTOR_SUB_SECTION = @"(?<section>[.#\[]{0,1}[^.#\[]+)";
        const string REGEX_FIND_TYPE_SELECTOR = @"^([a-zA-Z])";
        const string REGEX_FIND_ID = @"id=[""|'](?<id>[^""|']+)[""|']";
        const string REGEX_FIND_CLASS = @"class=[""|'](?<classNames>[^""|']+)[""|']";

        protected IEnumerable<CssRuleset> GetRulesets(string cssContent)
        {
            RegexOptions options = RegexOptions.Multiline;

            var rulesetMatches = Regex.Matches(cssContent, REGEX_FIND_ALL_STATEMENTS, options);
            foreach (Match rulesetMatch in rulesetMatches)
            {
                var rulesetGroup = rulesetMatch.Groups["ruleset"];
                if (!rulesetGroup.Success)
                {
                    continue;
                }
                var selectorListGroup = rulesetMatch.Groups["selectorList"];
                if (!selectorListGroup.Success)
                {
                    continue;
                }

                var declarationBlockGroup = rulesetMatch.Groups["declarationBlock"];
                if (!declarationBlockGroup.Success)
                {
                    continue;
                }

                var declarationsGroup = rulesetMatch.Groups["declarations"];

                var selectorList = selectorListGroup.Value;
                selectorList = selectorList.Trim(new[] { ' ', '\t', '\r', '\n' });

                CssRuleset ruleset = new CssRuleset()
                {
                    Value = rulesetGroup.Value,
                    SelectorList = selectorList,
                    DeclarationBlock = declarationBlockGroup.Value,
                    Declarations = declarationsGroup.Success ? declarationsGroup.Value : null
                };

                yield return ruleset;
            }
        }

        protected IEnumerable<string> GetSelectors(string cssSelectoList)
        {
            RegexOptions options = RegexOptions.Multiline;

            var selectorMatches = Regex.Matches(cssSelectoList, REGEX_FIND_SELECTORS, options);
            foreach (Match selectorMatch in selectorMatches)
            {
                var selectorGroup = selectorMatch.Groups["selector"];
                if (!selectorGroup.Success)
                {
                    continue;
                }

                var selector = selectorGroup.Value;
                selector = selector.Trim(new[] { ' ', '\t', '\r', '\n' });

                yield return selector;
            }
        }

        protected IEnumerable<CssSection> GetSections(string cssSelector)
        {
            RegexOptions options = RegexOptions.Multiline;

            var sectionMatches = Regex.Matches(cssSelector, REGEX_FIND_SELECTOR_SECTION, options);
            foreach (Match sectionMatch in sectionMatches)
            {
                var sectionGroup = sectionMatch.Groups["section"];
                if (!sectionGroup.Success)
                {
                    continue;
                }

                var subSectionMatches = Regex.Matches(sectionGroup.Value, REGEX_FIND_SELECTOR_SUB_SECTION, options);
                foreach (Match subSectionMatch in subSectionMatches)
                {
                    var subSectionGroup = subSectionMatch.Groups["section"];
                    if (!subSectionGroup.Success)
                    {
                        continue;
                    }

                    yield return GetSection(subSectionGroup.Value);
                }
            }
        }

        private static CssSection GetSection(string section)
        {
            if (string.IsNullOrEmpty(section))
                return null;

            section = section.Trim(new[] { ' ', '\t', '\r', '\n' });

            var pseudoElement = "";
            var pseudoClass = "";

            var pseudoElementIndex = section.IndexOf("::");
            var pseudoClassIndex = section.IndexOf(":");

            var hasPseudoElement = pseudoElementIndex != -1;
            var hasPseudoClass = pseudoClassIndex != -1;

            if (hasPseudoElement)
            {
                // Maybe we want to use this later, store it just in case
                pseudoElement = section.Substring(pseudoElementIndex + 2);
                section = section.Substring(0, pseudoElementIndex);
            }
            else if (hasPseudoClass)
            {
                // Maybe we want to use this later, store it just in case
                pseudoClass = section.Substring(pseudoClassIndex + 1);
                section = section.Substring(0, pseudoClassIndex);
            }

            // universal selector (*)
            if (section.Contains("*"))
            {
                return new CssSection
                {
                    Type = CssSelectorType.UniversalSelector,
                    Value = section
                };
            }

            // class selector (.)
            if (section.StartsWith("."))
            {
                var className = section.Substring(1);
                return new CssSection
                {
                    Type = CssSelectorType.ClassSelector,
                    Value = section.Substring(1)
                };
            }

            // ID selector (#)
            if (section.StartsWith("#"))
            {
                // id
                var idName = section.Substring(1);
                return new CssSection
                {
                    Type = CssSelectorType.IdSelector,
                    Value = idName
                };
            }

            // attribute selector ([])
            if (section.StartsWith("["))
            {
                return new CssSection
                {
                    Type = CssSelectorType.AttributeSelector,
                    Value = section
                };
            }

            // ignore at-rules
            if (section.StartsWith("@"))
            {
                return new CssSection
                {
                    Type = CssSelectorType.Unknown,
                    Value = section
                };
            }



            if (section.Equals("from", System.StringComparison.OrdinalIgnoreCase)
                || section.Equals("to", System.StringComparison.OrdinalIgnoreCase))
            {
                // This is probably keyframes, ignore them
                return new CssSection
                {
                    Type = CssSelectorType.Unknown,
                    Value = section
                };
            }

            // type selector (element selector)
            // NOTE: We will treat all selectors starting with chars as type selectors
            if (Regex.IsMatch(section, REGEX_FIND_TYPE_SELECTOR))
            {
                return new CssSection
                {
                    Type = CssSelectorType.TypeSelector,
                    Value = section
                };
            }

            return new CssSection
            {
                Type = CssSelectorType.Unknown,
                Value = section
            };
        }

        public string RemoveUnusedRules(string cssContent, string htmlContent)
        {
            var availableClasses = GetAvailableClassesFromHtml(htmlContent);
            var availableIds = GetAvailableIdsFromHtml(htmlContent);

            string resultContent = cssContent;

            resultContent = RemoveComments(resultContent);

            var rulesets = GetRulesets(cssContent);

            foreach (CssRuleset ruleset in rulesets)
            {
                var ignoreRuleSet = false;
                var removeRuleSet = false;
                var selectors = GetSelectors(ruleset.SelectorList).ToList();
                var hasAnySelector = false;

                var selectorIndexsToRemove = new List<int>();

                var selectorIndex = 0;
                foreach (string selector in selectors)
                {
                    var hasSelector = true;
                    var sections = GetSections(selector);
                    foreach (CssSection section in sections)
                    {
                        var hasSection = false;
                        switch (section.Type)
                        {
                            case CssSelectorType.Unknown:
                                hasSection = true;
                                break;
                            case CssSelectorType.UniversalSelector:
                                hasSection = true;
                                break;
                            case CssSelectorType.TypeSelector:
                                if (HasElement(section.Value, htmlContent))
                                {
                                    hasSection = true;
                                }
                                break;
                            case CssSelectorType.ClassSelector:
                                if (HasCssClass(section.Value, availableClasses))
                                {
                                    hasSection = true;
                                }
                                break;
                            case CssSelectorType.IdSelector:
                                if (HasId(section.Value, availableIds))
                                {
                                    hasSection = true;
                                }
                                break;
                            case CssSelectorType.AttributeSelector:
                                hasSection = true;
                                break;
                            default:
                                ignoreRuleSet = true;
                                break;
                        }

                        if (ignoreRuleSet)
                        {
                            break;
                        }

                        // No match found, remove ruleset
                        if (!hasSection)
                        {
                            hasSelector = false;
                            break;
                        }
                    }

                    if (hasSelector)
                    {
                        hasAnySelector = true;
                    }
                    else
                    {
                        selectorIndexsToRemove.Add(selectorIndex);
                    }

                    if (ignoreRuleSet || removeRuleSet || hasSelector)
                    {
                        break;
                    }

                    selectorIndex++;
                }

                if (!hasAnySelector && !ignoreRuleSet)
                {
                    removeRuleSet = true;
                }

                if (removeRuleSet)
                {
                    resultContent = resultContent.Replace(ruleset.Value, "");
                }
                else
                {
                    // remove one or more selectors only
                    if (selectorIndexsToRemove.Count > 0)
                    {
                        selectorIndexsToRemove.Reverse();
                        foreach (int index in selectorIndexsToRemove)
                        {
                            selectors.RemoveAt(index);
                        }
                        var onlyRequiredSelectors = string.Join(",", selectors);

                        var selectorListIndex = ruleset.Value.IndexOf(ruleset.SelectorList);

                        var newRuleSet = ruleset.Value.Remove(selectorListIndex, ruleset.SelectorList.Length);
                        newRuleSet = newRuleSet.Insert(selectorListIndex, onlyRequiredSelectors);


                        resultContent = resultContent.Replace(ruleset.Value, newRuleSet);

                    }
                }
            }


            // clean up empty rulesets
            var cleanupRulesets = GetRulesets(resultContent);
            foreach (CssRuleset ruleset in cleanupRulesets)
            {
                var cleanedDeclaration = ruleset.Declarations.Trim(new[] { '\r', '\n', '\t', ' ' });
                if (string.IsNullOrEmpty(cleanedDeclaration))
                {
                    resultContent = resultContent.Replace(ruleset.Value, "");
                }
            }

            resultContent = resultContent.Replace("\r", "").Replace("\n", "").Replace("  ", "").Replace(": ", ":").Replace(" {", "{").Replace(" (", "(").Replace(", ", ",").Replace(" + ", "+");

            return resultContent;
        }

        private static string RemoveComments(string resultContent)
        {
            RegexOptions options = RegexOptions.Multiline;
            var commentMatches = Regex.Matches(resultContent, REGEX_FIND_COMMENTS, options);
            foreach (Match commentMatch in commentMatches)
            {
                var commentGroup = commentMatch.Groups["comment"];
                if (commentGroup.Success)
                {
                    resultContent = resultContent.Replace(commentGroup.Value, "");
                }
            }

            return resultContent;
        }

        private static List<string> GetAvailableIdsFromHtml(string htmlContent)
        {
            var matchIds = Regex.Matches(htmlContent, REGEX_FIND_ID);
            var availableIds = matchIds.Cast<Match>().Select(match => match.Groups["id"]).Where(group => group.Success).Select(group => group.Value).ToList();
            return availableIds;
        }

        private static List<string> GetAvailableClassesFromHtml(string htmlContent)
        {
            var matchClasses = Regex.Matches(htmlContent, REGEX_FIND_CLASS);
            return matchClasses.Cast<Match>().Select(match => match.Groups["classNames"]).Where(group => group.Success).Select(group => group.Value.Replace("&#32;", " ")).ToList();
        }

        private bool HasElement(string elementName, string htmlContent)
        {
            return (htmlContent.IndexOf("<" + elementName) != -1);
        }

        private bool HasId(string idName, List<string> availableIds)
        {
            return availableIds.Any(id => id == idName);
        }

        private bool HasCssClass(string className, List<string> availableClasses)
        {
            return availableClasses.Any(classNames => classNames.Equals(className) || classNames.StartsWith(className + " ") || classNames.Contains(" " + className + " ") || classNames.EndsWith(" " + className));
        }
    }
}
