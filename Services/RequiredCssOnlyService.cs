using StaticWebEpiserverPlugin.RequiredCssOnly.Interfaces;
using StaticWebEpiserverPlugin.RequiredCssOnly.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.RequiredCssOnly.Services
{
    public partial class RequiredCssOnlyService : IRequiredCssOnlyService
    {
        const string REGEX_FIND_COMMENTS = @"(?<comment>\/\*[^*]*\*+([^\/*][^*]*\*+)*\/)";
        static readonly Regex REGEX_FIND_EMPTY_RULESETS = new Regex(@"(?<emptyRulesets>[^{}]*{[\r\n\t ]*})", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex REGEX_FIND_SINGLE_QUOTE = new Regex(@"'[^']*'", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_QUOTE = new Regex(@"(?<quote>""[^""]*"")", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_ALL_STATEMENTS = new Regex(@"(?<ruleset>(?<selectorList>[^;{}]+)(?<declarationBlock>{(?<declarations>[^}{]+)}))", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex REGEX_FIND_SELECTORS = new Regex(@"(?<selector>[^,]+)", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_SELECTOR_SECTION = new Regex(@"(?<section>[^>~+|| ]+)", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex REGEX_FIND_SELECTOR_SUB_SECTION = new Regex(@"(?<section>[.#\[:]{0,1}[^.#\[:]+)", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex REGEX_FIND_TYPE_SELECTOR = new Regex(@"^([a-zA-Z])", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_TAGNAME = new Regex(@"<(?<tagName>[^>| |\/]+)", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_ID = new Regex(@"id=[""|'](?<id>[^""|']+)[""|']", RegexOptions.Compiled);
        static readonly Regex REGEX_FIND_CLASS = new Regex(@"class=[""|'](?<classNames>[^""|']+)[""|']", RegexOptions.Compiled);

        protected List<ContentSelector> GetSelectors(string cssSelectoList)
        {
            List<ContentSelector> selectors = new List<ContentSelector>();

            var selectorMatches = REGEX_FIND_SELECTORS.Matches(cssSelectoList);
            foreach (Match selectorMatch in selectorMatches)
            {
                var selectorGroup = selectorMatch.Groups["selector"];
                if (!selectorGroup.Success)
                {
                    continue;
                }

                var selectorValue = selectorGroup.Value;
                var cleanedSelectorValue = selectorValue.Trim(new[] { ' ', '\t', '\r', '\n' });

                selectors.Add(new ContentSelector
                {
                    Content = selectorValue,
                    CleanedContent = cleanedSelectorValue,
                    StartIndex = selectorGroup.Index,
                    EndIndex = selectorGroup.Length + selectorGroup.Index,
                    Type = PartType.Selector
                });
            }

            return selectors;
        }

        protected void GetSections(ContentSelector selector)
        {
            var index = 0;
            var sectionMatches = REGEX_FIND_SELECTOR_SECTION.Matches(selector.CleanedContent);
            foreach (Match sectionMatch in sectionMatches)
            {
                var sectionGroup = sectionMatch.Groups["section"];
                if (!sectionGroup.Success)
                {
                    continue;
                }

                var subSectionMatches = REGEX_FIND_SELECTOR_SUB_SECTION.Matches(sectionGroup.Value);
                foreach (Match subSectionMatch in subSectionMatches)
                {
                    var subSectionGroup = subSectionMatch.Groups["section"];
                    if (!subSectionGroup.Success)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(subSectionGroup.Value))
                        continue;

                    var section = GetSection(subSectionGroup.Value, index++);
                    selector.Sections.Add(section);
                }
            }
        }

        private static ContentSection GetSection(string section, int index)
        {
            var orginalSection = section;
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
                // CssSelectorType.UniversalSelector
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.UniversalSelector
                };
            }

            // class selector (.)
            if (section.StartsWith("."))
            {
                var className = section.Substring(1);
                // CssSelectorType.ClassSelector,
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section.Substring(1),
                    Index = index,
                    Type = CssSelectorType.ClassSelector
                };
            }

            // ID selector (#)
            if (section.StartsWith("#"))
            {
                // id
                var idName = section.Substring(1);
                // CssSelectorType.IdSelector
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = idName,
                    Index = index,
                    Type = CssSelectorType.IdSelector
                };
            }

            // attribute selector ([])
            if (section.StartsWith("["))
            {
                // CssSelectorType.AttributeSelector
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.AttributeSelector
                };
            }

            // ignore at-rules
            if (section.StartsWith("@"))
            {
                // CssSelectorType.Unknown
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.Unknown
                };
            }


            if (section.Equals("from", System.StringComparison.OrdinalIgnoreCase)
                || section.Equals("to", System.StringComparison.OrdinalIgnoreCase))
            {
                // This is probably keyframes, ignore them
                // CssSelectorType.Unknown
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.Unknown
                };
            }

            // type selector (element selector)
            // NOTE: We will treat all selectors starting with chars as type selectors
            if (REGEX_FIND_TYPE_SELECTOR.IsMatch(section))
            {
                // CssSelectorType.TypeSelector,
                return new ContentSection
                {
                    Content = orginalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.TypeSelector
                };
            }

            // CssSelectorType.Unknown
            return new ContentSection
            {
                Content = orginalSection,
                CleanedContent = section,
                Index = index,
                Type = CssSelectorType.Unknown
            };
        }

        public string RemoveUnusedRules(string cssContent, string htmlContent)
        {
            var availableClasses = GetAvailableClassesFromHtml(htmlContent);
            var availableIds = GetAvailableIdsFromHtml(htmlContent);
            var availableTags = GetAvailableTagsFromHtml(htmlContent);

            List<ContentPart> parts = new List<ContentPart>();
            string resultingCssContent = cssContent;

            resultingCssContent = RemoveComments(resultingCssContent, ref parts);
            string workingContent = RemoveQuote(resultingCssContent);

            GetRulesets(workingContent, ref parts);

            var contentToIgnore = GetIgnoreableOrModifiedContent(availableClasses, availableIds, availableTags, ref parts);

            if (contentToIgnore.Count > 0)
            {
                System.Text.StringBuilder sbContent = new System.Text.StringBuilder(resultingCssContent);
                contentToIgnore.Reverse();
                foreach (var content in contentToIgnore)
                {
                    if (content is ContentReplacedRuleset)
                    {
                        var ruleset = content as ContentReplacedRuleset;
                        var contentLength = ruleset.EndIndex - ruleset.StartIndex;
                        sbContent.Remove(ruleset.StartIndex, contentLength).Insert(ruleset.StartIndex, ruleset.ReplacedContent);
                    }
                    else if (content is ContentRuleset)
                    {
                        var ruleset = content as ContentRuleset;
                        var contentLength = ruleset.EndIndex - ruleset.StartIndex;
                        sbContent.Remove(ruleset.StartIndex, contentLength).Insert(ruleset.StartIndex, "{}".PadRight(contentLength - 2));
                    }
                }
                resultingCssContent = sbContent.ToString();
            }

            resultingCssContent = RemoveUselessSpaces(resultingCssContent);

            resultingCssContent = RemoveEmptyRulesets(resultingCssContent);

            resultingCssContent = resultingCssContent.Trim(' ');

            return resultingCssContent;
        }

        private static string RemoveUselessSpaces(string resultingCssContent)
        {
            return resultingCssContent.Replace("\r", "").Replace("\n", "").Replace("  ", "").Replace(": ", ":").Replace(" {", "{").Replace(" (", "(").Replace(", ", ",").Replace(" + ", "+");
        }

        private static string RemoveQuote(string content)
        {
            System.Text.StringBuilder resultContent = new System.Text.StringBuilder(content);
            // temporary remove everything inside "" and '' (as it can contain "," and that will break our selector regexp)
            var matches = REGEX_FIND_QUOTE.Matches(content);
            foreach (Match match in matches)
            {
                var group = match.Groups["quote"];
                if (group.Success)
                {
                    var previousValue = group.Value;
                    var valueLength = previousValue.Length - 2;
                    resultContent.Remove(group.Index, group.Length);
                    resultContent.Insert(group.Index, @"""" + "".PadRight(valueLength, 'X') + @"""");
                }
            }

            matches = REGEX_FIND_SINGLE_QUOTE.Matches(content);
            foreach (Match match in matches)
            {
                var group = match.Groups["quote"];
                if (group.Success)
                {
                    var previousValue = group.Value;
                    var valueLength = previousValue.Length - 2;
                    resultContent.Remove(group.Index, group.Length);
                    resultContent.Insert(group.Index, @"'" + "".PadRight(valueLength, 'X') + @"'");
                }
            }

            return resultContent.ToString();
        }

        private static string RemoveEmptyRulesets(string resultContent)
        {
            for (int i = 0; i < 2; i++)
            {
                resultContent = REGEX_FIND_EMPTY_RULESETS.Replace(resultContent, @"");
            }
            return resultContent;
        }

        private List<ContentPart> GetIgnoreableOrModifiedContent(List<string> availableClasses, List<string> availableIds, List<string> availableTags, ref List<ContentPart> parts)
        {
            List<ContentPart> ignoreableRulesets = new List<ContentPart>();

            foreach (ContentPart part in parts)
            {
                var ruleset = part as ContentRuleset;
                if (ruleset == null)
                    continue;
                GetIgnoreableOrModifiedRules(availableClasses, availableIds, availableTags, ignoreableRulesets, part);
                if (ruleset.Parts != null && ruleset.Parts.Count > 0)
                {
                    foreach (var subPart in ruleset.Parts)
                    {
                        GetIgnoreableOrModifiedRules(availableClasses, availableIds, availableTags, ignoreableRulesets, subPart);
                    }
                }
            }

            return ignoreableRulesets;
        }

        private void GetIgnoreableOrModifiedRules(List<string> availableClasses, List<string> availableIds, List<string> availableTags, List<ContentPart> ignoreableRulesets, ContentPart part)
        {
            var ruleset = part as ContentRuleset;
            if (ruleset == null)
            {
                // Ignore everything that is not a ruleset, for example comments
                return;
            }

            var selectorList = ruleset.SelectorList;

            ContentSelectorList contentSelectorList = new ContentSelectorList
            {
                Type = PartType.SelectorList,
                Content = ruleset.SelectorList,
                CleanedContent = selectorList,
            };

            var selectorListIsDirty = false;
            var selectors = GetSelectors(selectorList);
            contentSelectorList.Selectors = selectors;
            foreach (var selector in selectors)
            {
                var removeSelector = false;
                GetSections(selector);
                foreach (ContentSection section in selector.Sections)
                {
                    switch (section.Type)
                    {
                        case CssSelectorType.Unknown:
                            // We don't know what this is, make sure it is left untouched (by not doing anything)
                            section.Used = true;
                            break;
                        case CssSelectorType.UniversalSelector:
                            // We don't how to handle this, make sure it is left untouched (by not doing anything)
                            section.Used = true;
                            break;
                        case CssSelectorType.TypeSelector:
                            if (!HasElement(section.CleanedContent, availableTags))
                            {
                                // we don't have this element, add this selector to ignore list
                                removeSelector = true;
                            }
                            else
                            {
                                section.Used = true;
                            }
                            break;
                        case CssSelectorType.ClassSelector:
                            if (!HasCssClass(section.CleanedContent, availableClasses))
                            {
                                // we don't have this class, add this selector to ignore list
                                removeSelector = true;
                            }
                            else
                            {
                                section.Used = true;
                            }
                            break;
                        case CssSelectorType.IdSelector:
                            if (!HasId(section.CleanedContent, availableIds))
                            {
                                // we don't have this id, add this selector to ignore list
                                removeSelector = true;
                            }
                            else
                            {
                                section.Used = true;
                            }
                            break;
                        case CssSelectorType.AttributeSelector:
                            // We don't how to handle this, make sure it is left untouched (by not doing anything)
                            section.Used = true;
                            break;
                        default:
                            // We don't know what this is, make sure it is left untouched (by not doing anything)
                            section.Used = true;
                            break;
                    }
                }

                selector.Used = !removeSelector;
            }

            contentSelectorList.Used = selectors.Any(s => s.Used);
            if (!contentSelectorList.Used)
            {
                // remove selectorlist all together
                ignoreableRulesets.Add(ruleset);
            }
            else if (selectors.Any(s => !s.Used))
            {
                // remove specific selector
                var replacedRuleSet = new ContentReplacedRuleset();
                replacedRuleSet.ReplacedContent = replacedRuleSet.Content = ruleset.Content;
                replacedRuleSet.EndIndex = ruleset.EndIndex;
                replacedRuleSet.Parts = ruleset.Parts;
                replacedRuleSet.ReplacedSelectorList = replacedRuleSet.SelectorList = ruleset.SelectorList;
                replacedRuleSet.StartIndex = ruleset.StartIndex;
                replacedRuleSet.Type = ruleset.Type;

                var tmpSelectors = new List<ContentSelector>(selectors);
                tmpSelectors.Reverse();

                foreach (var selector in tmpSelectors)
                {
                    if (!selector.Used)
                    {
                        var length = selector.EndIndex - selector.StartIndex;
                        replacedRuleSet.ReplacedSelectorList = replacedRuleSet.ReplacedSelectorList.Remove(selector.StartIndex, length).Insert(selector.StartIndex, " ".PadRight(length));

                        var seperatorIndex = selector.StartIndex - 1;
                        if (selector.StartIndex == 0)
                        {
                            seperatorIndex = selector.StartIndex + length;
                        }
                        if (replacedRuleSet.ReplacedSelectorList.Length > seperatorIndex && replacedRuleSet.ReplacedSelectorList[seperatorIndex] == ',')
                        {
                            replacedRuleSet.ReplacedSelectorList = replacedRuleSet.ReplacedSelectorList.Remove(seperatorIndex, 1);
                        }
                    }
                }

                replacedRuleSet.ReplacedSelectorList = replacedRuleSet.ReplacedSelectorList.Trim();
                if (replacedRuleSet.ReplacedSelectorList.StartsWith(","))
                {
                    replacedRuleSet.ReplacedSelectorList = replacedRuleSet.ReplacedSelectorList.Remove(0, 1);
                }

                replacedRuleSet.ReplacedContent = replacedRuleSet.ReplacedContent.Replace(replacedRuleSet.SelectorList, replacedRuleSet.ReplacedSelectorList);
                ignoreableRulesets.Add(replacedRuleSet);
            }
        }

        protected void GetRulesets(string cssContent, ref List<ContentPart> parts)
        {
            var rulesetMatches = REGEX_FIND_ALL_STATEMENTS.Matches(cssContent);
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

                var part = new ContentRuleset
                {
                    StartIndex = rulesetGroup.Index,
                    EndIndex = rulesetGroup.Index + rulesetGroup.Length,
                    SelectorList = selectorList,
                    Content = rulesetGroup.Value,
                    Type = PartType.Block
                };

                parts.Add(part);
            }
        }

        private static string RemoveComments(string content, ref List<ContentPart> parts)
        {
            var resultContent = new System.Text.StringBuilder(content);
            var isDirty = true;
            var startIndex = 0;
            while (isDirty)
            {
                isDirty = false;
                startIndex = content.IndexOf("/*", startIndex);
                if (startIndex != -1)
                {
                    var endIndex = content.IndexOf("*/", startIndex + 2) + 2;

                    var commentContent = content.Substring(startIndex, endIndex - startIndex);
                    parts.Add(new ContentPart
                    {
                        StartIndex = startIndex,
                        EndIndex = endIndex,
                        Content = commentContent,
                        Type = PartType.Comment
                    });

                    var length = endIndex - startIndex;
                    resultContent = resultContent.Replace(commentContent, "".PadRight(length));
                    isDirty = true;
                    startIndex = endIndex + 2;
                }

            }

            return resultContent.ToString();
        }

        private static List<string> GetAvailableTagsFromHtml(string htmlContent)
        {
            var matchTagNames = REGEX_FIND_TAGNAME.Matches(htmlContent);
            var availableTagNames = matchTagNames.Cast<Match>().Select(match => match.Groups["tagName"]).Where(group => group.Success && group.Value != null).Select(group => group.Value.ToLowerInvariant()).Distinct().ToList();
            return availableTagNames;
        }


        private static List<string> GetAvailableIdsFromHtml(string htmlContent)
        {
            var matchIds = REGEX_FIND_ID.Matches(htmlContent);
            var availableIds = matchIds.Cast<Match>().Select(match => match.Groups["id"]).Where(group => group.Success).Select(group => group.Value).Distinct().ToList();
            return availableIds;
        }

        private static List<string> GetAvailableClassesFromHtml(string htmlContent)
        {
            var matchClasses = REGEX_FIND_CLASS.Matches(htmlContent);
            var classes = matchClasses.Cast<Match>().Select(match => match.Groups["classNames"]).Where(group => group.Success).Select(group => group.Value.Replace("&#32;", " "));
            var classNames = classes.SelectMany(list => list.Split(' ')).Distinct().ToList();
            return classNames;
        }

        private bool HasElement(string tagName, List<string> availableTags)
        {
            tagName = tagName.ToLowerInvariant();
            return availableTags.Any(name => name == tagName);
        }

        private bool HasId(string idName, List<string> availableIds)
        {
            return availableIds.Any(id => id == idName);
        }

        private bool HasCssClass(string className, List<string> availableClasses)
        {
            return availableClasses.Contains(className);
        }
    }
}
