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
        const string REGEX_FIND_COMMENTS = @"(?<comment>\/\*[^*]*\*+([^\/*][^*]*\*+)*\/)";
        const string REGEX_FIND_ALL_STATEMENTS = @"(?<ruleset>(?<selectorList>[^;{}]+)(?<declarationBlock>{(?<declarations>[^}{]+)}))";
        const string REGEX_FIND_SELECTORS = @"(?<selector>[^,]+)";
        const string REGEX_FIND_SELECTOR_SECTION = @"(?<section>[^>~+|| ]+)";
        //const string REGEX_FIND_SELECTOR_SECTION = @"(?<section>(?>[^>~+|| \[\(\""]*)(?>[\[\(\""]+[^\]\)]+[\]\)\""]+(?>[^>~+|| \[\(\""]*))*)";
        const string REGEX_FIND_SELECTOR_SUB_SECTION = @"(?<section>[.#\[]{0,1}[^.#\[]+)";
        const string REGEX_FIND_TYPE_SELECTOR = @"^([a-zA-Z])";
        const string REGEX_FIND_TAGNAME = @"<(?<tagName>[^>| |\/]+)";
        const string REGEX_FIND_ID = @"id=[""|'](?<id>[^""|']+)[""|']";
        const string REGEX_FIND_CLASS = @"class=[""|'](?<classNames>[^""|']+)[""|']";

        protected List<ContentSelector> GetSelectors(string cssSelectoList)
        {
            List<ContentSelector> selectors = new List<ContentSelector>();
            RegexOptions options = RegexOptions.Multiline;

            var selectorMatches = Regex.Matches(cssSelectoList, REGEX_FIND_SELECTORS, options);
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
            RegexOptions options = RegexOptions.Multiline;

            var index = 0;
            var sectionMatches = Regex.Matches(selector.CleanedContent, REGEX_FIND_SELECTOR_SECTION, options);
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

                    if (string.IsNullOrEmpty(subSectionGroup.Value))
                        continue;

                    var section = GetSection(subSectionGroup.Value, index);
                    selector.Sections.Add(section);
                }
            }
        }

        private static ContentSection GetSection(string section, int index)
        {
            var orginnalSection = section;
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
                    Content = orginnalSection,
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
                    Content = orginnalSection,
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
                    Content = orginnalSection,
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
                    Content = orginnalSection,
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
                    Content = orginnalSection,
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
                    Content = orginnalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.Unknown
                };
            }

            // type selector (element selector)
            // NOTE: We will treat all selectors starting with chars as type selectors
            if (Regex.IsMatch(section, REGEX_FIND_TYPE_SELECTOR))
            {
                // CssSelectorType.TypeSelector,
                return new ContentSection
                {
                    Content = orginnalSection,
                    CleanedContent = section,
                    Index = index,
                    Type = CssSelectorType.TypeSelector
                };
            }

            // CssSelectorType.Unknown
            return new ContentSection
            {
                Content = orginnalSection,
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
            string resultContent = cssContent;

            resultContent = RemoveComments(resultContent, ref parts);
            string workingContent = resultContent;
            workingContent = RemoveQuote(workingContent);

            GetRulesets(workingContent, ref parts);

            var contentsToIgnore = GetIgnoreableRulesets(availableClasses, availableIds, availableTags, ref parts);

            if (contentsToIgnore.Count > 0)
            {
                contentsToIgnore.Reverse();
                foreach (var content in contentsToIgnore)
                {
                    if (content is ContentRuleset)
                    {
                        var ruleset = content as ContentRuleset;
                        var contentLength = ruleset.EndIndex - ruleset.StartIndex;
                        resultContent = resultContent.Remove(ruleset.StartIndex, contentLength).Insert(ruleset.StartIndex, "{}".PadRight(contentLength - 2));
                        //resultContent = resultContent.Remove(ruleset.StartIndex, ruleset.EndIndex - ruleset.StartIndex).Insert(ruleset.StartIndex, "{}");
                        //resultContent = resultContent.Replace(ruleset.Content, "{}");
                    }
                }
            }

            resultContent = resultContent.Replace("\r", "").Replace("\n", "").Replace("  ", "").Replace(": ", ":").Replace(" {", "{").Replace(" (", "(").Replace(", ", ",").Replace(" + ", "+");

            //resultContent = resultContent.Replace("{}{}", "{}");

            resultContent = RemoveEmptyRulesets(resultContent);

            var test = workingContent;
            return resultContent;
        }

        private static string RemoveQuote(string resultContent)
        {
            // temporary remove everything inside "" and '' (as it can contain "," and that will break our selector regexp)
            string pattern = @"(?<quote>""[^""]*"")";
            var matches = Regex.Matches(resultContent, pattern);
            foreach (Match match in matches)
            {
                var group = match.Groups["quote"];
                if (group.Success)
                {
                    var previousValue = group.Value;
                    var valueLength = previousValue.Length - 2;
                    resultContent = resultContent.Replace(previousValue, @"""" + "".PadRight(valueLength, 'X') + @"""");
                }
            }

            pattern = @"'[^']*'";
            matches = Regex.Matches(resultContent, pattern);
            foreach (Match match in matches)
            {
                var group = match.Groups["quote"];
                if (group.Success)
                {
                    var previousValue = group.Value;
                    var valueLength = previousValue.Length - 2;
                    resultContent = resultContent.Replace(previousValue, @"'" + "".PadRight(valueLength, 'X') + @"'");
                }
            }

            return resultContent;
        }

        private static string RemoveEmptyRulesets(string resultContent)
        {
            RegexOptions options = RegexOptions.Multiline;
            string patternFindEmptyRulesets = @"(?<emptyRulesets>[^{}]*{})";
            Regex regex = new Regex(patternFindEmptyRulesets, options);
            resultContent = regex.Replace(resultContent, @" ");
            //var isDirty = true;
            //while (isDirty)
            //{
            //    isDirty = false;
            //    string patternFindEmptyRulesets = @"(?<emptyRulesets>[^{}]*{})";
            //    var matches = Regex.Matches(resultContent, patternFindEmptyRulesets);
            //    foreach (Match match in matches)
            //    {
            //        var group = match.Groups["emptyRulesets"];
            //        if (group.Success)
            //        {
            //            resultContent = resultContent.Replace(group.Value, "");
            //            isDirty = true;
            //        }
            //    }
            //}

            return resultContent;
        }

        private List<ContentPart> GetIgnoreableRulesets(List<string> availableClasses, List<string> availableIds, List<string> availableTags, ref List<ContentPart> parts)
        {
            List<ContentPart> ignoreableRulesets = new List<ContentPart>();

            foreach (ContentPart part in parts)
            {
                var ruleset = part as ContentRuleset;
                if (ruleset == null)
                    continue;
                GetIgnoreableRules(availableClasses, availableIds, availableTags, ignoreableRulesets, part);
                if (ruleset.Parts != null && ruleset.Parts.Count > 0)
                {
                    foreach (var subPart in ruleset.Parts)
                    {
                        GetIgnoreableRules(availableClasses, availableIds, availableTags, ignoreableRulesets, subPart);
                    }
                }
            }

            return ignoreableRulesets;
        }

        private void GetIgnoreableRules(List<string> availableClasses, List<string> availableIds, List<string> availableTags, List<ContentPart> ignoreableRulesets, ContentPart part)
        {
            var ruleset = part as ContentRuleset;
            if (ruleset == null)
            {
                // Ignore everything that is not a ruleset, for example comments
                return;
            }

            // TODO: If ruleset.Parts is not null/empty, make sure we loop all of them as well
            // TOOD: make logic inside of this foreach a seperate function (so it can be called recursive)

            //if (ruleset.Parts != null && ruleset.Parts.Count > 0)
            //{
            //    foreach (var subPart in ruleset.Parts)
            //    {
            //        GetIgnoreableRules(availableClasses, availableIds, availableTags, ignoreableRulesets, subPart);
            //    }
            //}

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
            }
        }

        private void GetRulesets(string resultContent, ref List<ContentPart> parts)
        {
            int startIndex = 0;
            int prevStartIndex = 0;
            var parentStartIndex = -1;
            var isDirty = true;
            List<ContentRuleset> childParts = new List<ContentRuleset>();

            while (isDirty)
            {
                isDirty = false;
                startIndex = resultContent.IndexOf("{", startIndex);
                if (startIndex != -1)
                {
                    var endIndex = resultContent.IndexOf("}", startIndex + 1) + 1;
                    var nextStartIndex = resultContent.IndexOf("{", startIndex + 1);

                    if (nextStartIndex != -1 && nextStartIndex < endIndex)
                    {
                        // We have identified nested block, fix this
                        parentStartIndex = prevStartIndex;
                        prevStartIndex = startIndex + 1;
                        startIndex = nextStartIndex;
                        isDirty = true;
                        continue;
                    }

                    var content = resultContent.Substring(startIndex, endIndex - startIndex);
                    var selectorList = resultContent.Substring(prevStartIndex, startIndex - prevStartIndex);

                    if (parentStartIndex == -1)
                    {
                        var part = new ContentRuleset
                        {
                            StartIndex = startIndex,
                            EndIndex = endIndex,
                            SelectorList = selectorList,
                            Content = content,
                            Type = PartType.Block
                        };

                        if (parentStartIndex == -1 && childParts.Count > 0)
                        {
                            part.Parts = childParts;
                            childParts = new List<ContentRuleset>();
                        }

                        parts.Add(part);
                    }
                    else
                    {
                        childParts.Add(new ContentRuleset
                        {
                            StartIndex = startIndex,
                            EndIndex = endIndex,
                            SelectorList = selectorList,
                            Content = content,
                            Type = PartType.Block
                        });
                    }

                    var rulesetContent = selectorList + content;
                    var length = rulesetContent.Length;
                    //resultContent = resultContent.Replace(content, "".PadRight(length));
                    resultContent = resultContent.Replace(rulesetContent, "".PadRight(length));

                    //resultContent = resultContent.Replace(selectorList +  , startIndex - prevStartIndex).Trim();


                    var nextEndIndex = resultContent.IndexOf("}", endIndex);
                    if (nextEndIndex < nextStartIndex)
                    {
                        // We have reached end of parent block, restart from begining
                        isDirty = true;
                        prevStartIndex = parentStartIndex;
                        startIndex = parentStartIndex;
                        parentStartIndex = -1;
                        continue;
                    }


                    isDirty = true;
                    prevStartIndex = startIndex;
                    startIndex = endIndex;
                }

            }
        }

        private static string RemoveComments(string resultContent, ref List<ContentPart> parts)
        {
            var isDirty = true;
            while (isDirty)
            {
                isDirty = false;
                var startIndex = resultContent.IndexOf("/*");
                if (startIndex != -1)
                {
                    var endIndex = resultContent.IndexOf("*/", startIndex + 2) + 2;

                    var content = resultContent.Substring(startIndex, endIndex - startIndex);
                    parts.Add(new ContentPart
                    {
                        StartIndex = startIndex,
                        EndIndex = endIndex,
                        Content = content,
                        Type = PartType.Comment
                    });

                    var length = endIndex - startIndex;
                    resultContent = resultContent.Replace(content, "".PadRight(length));
                    isDirty = true;
                }

            }

            //RegexOptions options = RegexOptions.Multiline;
            //var commentMatches = Regex.Matches(resultContent, REGEX_FIND_COMMENTS, options);
            //foreach (Match commentMatch in commentMatches)
            //{
            //    var commentGroup = commentMatch.Groups["comment"];
            //    if (commentGroup.Success)
            //    {
            //        resultContent = resultContent.Replace(commentGroup.Value, "");
            //    }
            //}

            return resultContent;
        }

        private static List<string> GetAvailableTagsFromHtml(string htmlContent)
        {
            var matchTagNames = Regex.Matches(htmlContent, REGEX_FIND_TAGNAME);
            var availableTagNames = matchTagNames.Cast<Match>().Select(match => match.Groups["tagName"]).Where(group => group.Success && group.Value != null).Select(group => group.Value.ToLowerInvariant()).Distinct().ToList();
            return availableTagNames;
        }


        private static List<string> GetAvailableIdsFromHtml(string htmlContent)
        {
            var matchIds = Regex.Matches(htmlContent, REGEX_FIND_ID);
            var availableIds = matchIds.Cast<Match>().Select(match => match.Groups["id"]).Where(group => group.Success).Select(group => group.Value).Distinct().ToList();
            return availableIds;
        }

        private static List<string> GetAvailableClassesFromHtml(string htmlContent)
        {
            var matchClasses = Regex.Matches(htmlContent, REGEX_FIND_CLASS);
            return matchClasses.Cast<Match>().Select(match => match.Groups["classNames"]).Where(group => group.Success).Select(group => group.Value.Replace("&#32;", " ")).Distinct().ToList();
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
            return availableClasses.Any(classNames => classNames.Equals(className) || classNames.StartsWith(className + " ") || classNames.Contains(" " + className + " ") || classNames.EndsWith(" " + className));
        }
    }
}
