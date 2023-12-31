using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>Abstract base for a type of meta object.</summary>
    public abstract class MetaObject
    {
        /// <summary>Get the meta type of the object.</summary>
        public abstract MetaType Type { get; }

        /// <summary>Get the name of the object. May have capitals.</summary>
        public abstract string Name { get; }

        /// <summary>Whether this object has multiple names.</summary>
        public bool HasMultipleNames = false;

        /// <summary>If the object has multiple names, returns the full set of names.</summary>
        public virtual IEnumerable<string> MultiNames => new string[] { CleanName };

        /// <summary>Get the clean lowercase name of the object.</summary>
        public virtual string CleanName => Name.ToLowerFast();

        /// <summary>The name to display for searchability purposes.</summary>
        public virtual string SearchName => Name;

        /// <summary>Other words that mean the same thing.</summary>
        public List<string> Synonyms = [];

        /// <summary>What categorization group the object is in.</summary>
        public string Group;

        /// <summary>Any warnings applied to this object type.</summary>
        public List<string> Warnings = [];

        /// <summary>Required plugin(s) if applicable.</summary>
        public string Plugin;

        /// <summary>The file in source code that defined this meta object.</summary>
        public string SourceFile;

        /// <summary>A deprecation notice, if any.</summary>
        public string Deprecated;

        /// <summary>The backing <see cref="MetaDocs"/> instance.</summary>
        public MetaDocs Meta;

        /// <summary>The original raw values specified for the object</summary>
        public Dictionary<string, List<string>> RawValues = [];

        /// <summary>Apply a setting value to this meta object.</summary>
        /// <param name="docs">The relevant meta docs instance.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <returns>Whether the value was applied.</returns>
        public virtual bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "group":
                    Group = value;
                    return true;
                case "warning":
                    Warnings.Add(value);
                    return true;
                case "plugin":
                    Plugin = value;
                    return true;
                case "deprecated":
                    Deprecated = value;
                    return true;
                case "synonyms":
                    Synonyms.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerFast()));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Finds the closing tag mark, compensating for layered tags.</summary>
        /// <param name="text">The raw text.</param>
        /// <param name="startIndex">The index to start searching at.</param>
        /// <returns>The closing symbol index, or -1 if not found.</returns>
        public static int FindClosingTagMark(string text, int startIndex)
        {
            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    depth++;
                }
                if (c == '>')
                {
                    if (depth == 0)
                    {
                        return i;
                    }
                    depth--;
                }
            }
            return -1;
        }

        /// <summary>Adds the object to the meta docs set.</summary>
        /// <param name="docs">The docs set.</param>
        public abstract void AddTo(MetaDocs docs);

        /// <summary>Checks the object for validity, after all loading is done.</summary>
        /// <param name="docs">The relevant docs object.</param>
        public virtual void PostCheck(MetaDocs docs)
        {
        }

        /// <summary>Post-check handler to require specific values be set (not-null).</summary>
        /// <param name="docs">The relevant docs object.</param>
        /// <param name="requiredValues">The values that are required.</param>
        public void Require(MetaDocs docs, params object[] requiredValues)
        {
            foreach (object obj in requiredValues)
            {
                if (obj == null)
                {
                    docs.LoadErrors.Add($"{Type.Name} '{Name}' is missing a required meta key.");
                }
            }
        }

        /// <summary>Post-check handler to validate synonyms don't duplicate existing values.</summary>
        public void PostCheckSynonyms<T>(MetaDocs docs, Dictionary<string, T> objects) where T : MetaObject
        {
            IEnumerable<string> badSynonyms = Synonyms.Where(s => objects.ContainsKey(s));
            if (badSynonyms.Any())
            {
                docs.LoadErrors.Add($"Object {Name} has synonyms '{string.Join(',', badSynonyms)}' that match existing objects and will break meta searches.");
            }
            IEnumerable<string> dupSynonyms = Synonyms.Where(s => objects.Values.Any(t => t != this && t.Type == Type && t.Synonyms.Contains(s)));
            if (dupSynonyms.Any())
            {
                docs.LoadErrors.Add($"Object {Name} has synonyms '{string.Join(',', dupSynonyms)}' that match other objects existing synonyms and will be invalid for searches.");
            }
        }

        /// <summary>Post-check handler for linkable text, to find bad links.</summary>
        /// <param name="docs">The relevant docs object.</param>
        /// <param name="linkedtext">The relevant linkable list.</param>
        public void PostCheckLinkableText(MetaDocs docs, string linkedtext)
        {
            if (string.IsNullOrWhiteSpace(linkedtext))
            {
                return;
            }
            int nextLinkIndex = linkedtext.IndexOf("<@link");
            if (nextLinkIndex < 0)
            {
                return;
            }
            while (nextLinkIndex >= 0)
            {
                int endIndex = FindClosingTagMark(linkedtext, nextLinkIndex + 1);
                if (endIndex < 0)
                {
                    return;
                }
                int startOfMetaCommand = nextLinkIndex + "<@link ".Length;
                string metaCommand = linkedtext[startOfMetaCommand..endIndex];
                if (!metaCommand.StartsWith("url"))
                {
                    int firstSpace = metaCommand.IndexOf(' ');
                    if (firstSpace < 0)
                    {
                        docs.LoadErrors.Add($"{Type.Name} '{Name}' contains text link '{metaCommand}', which is formatted incorrectly.");
                        return;
                    }
                    string type = metaCommand[..firstSpace].ToLowerFast();
                    string searchText = metaCommand[(firstSpace + 1)..].ToLowerFast();
                    bool exists;
                    if (type.Equals("command"))
                    {
                        exists = docs.Commands.ContainsKey(searchText);
                    }
                    else if (type.Equals("tag"))
                    {
                        exists = docs.FindTag(searchText) != null;
                    }
                    else if (type.Equals("mechanism") || type.Equals("property"))
                    {
                        exists = docs.Mechanisms.ContainsKey(searchText);
                    }
                    else if (type.Equals("event"))
                    {
                        if (searchText.StartsWith("on "))
                        {
                            searchText = searchText["on ".Length..];
                        }
                        exists = docs.FindEventsFor(searchText, true, true) != null;
                        if (!exists)
                        {
                            exists = docs.Events.Values.Any(e => e.CleanEvents.Any(s => s.Contains(searchText)));
                        }
                    }
                    else if (type.Equals("action"))
                    {
                        if (searchText.StartsWith("on "))
                        {
                            searchText = searchText["on ".Length..];
                        }
                        exists = docs.Actions.Values.Any(a => a.CleanActions.Any(s => s.Contains(searchText)));
                    }
                    else if (type.Equals("language"))
                    {
                        exists = docs.Languages.Keys.Any(s => s.Contains(searchText));
                    }
                    else if (type.Equals("objecttype"))
                    {
                        exists = docs.ObjectTypes.Keys.Any(s => s.Contains(searchText));
                    }
                    else
                    {
                        docs.LoadErrors.Add($"{Type.Name} '{Name}' contains text link '{metaCommand}', which refers to an unknown meta type.");
                        return;
                    }
                    if (!exists)
                    {
                        docs.LoadErrors.Add($"{Type.Name} '{Name}' contains text link '{metaCommand}', which does not exist.");
                        return;
                    }
                }
                nextLinkIndex = linkedtext.IndexOf("<@link", endIndex + 1);
            }
        }

        /// <summary>Post-check handler for tags, used in <see cref="MetaCommand"/> and <see cref="MetaMechanism"/>.</summary>
        /// <param name="docs">The relevant docs object.</param>
        /// <param name="tags">The relevant tags list.</param>
        public void PostCheckTags(MetaDocs docs, string[] tags)
        {
            foreach (string tag in tags)
            {
                if (tag.EndsWith('>'))
                {
                    MetaTag realTag = docs.FindTag(tag);
                    if (realTag == null)
                    {
                        docs.LoadErrors.Add($"{Type.Name} '{Name}' references tag '{tag}', which doesn't exist.");
                    }
                }
                PostCheckLinkableText(docs, tag);
            }
        }

        /// <summary>Class that contains data to help searching. All strings are lowercase.</summary>
        public class SearchableHelpers
        {
            /// <summary>Names for search quality levels.</summary>
            public static readonly string[] SearchQualityName =
                [
                    "Not Matched", // 0
                    "Just Barely Matched", // 1
                    "Backup Match", // 2
                    "Semi-Decent Match", // 3
                    "Decent Match", // 4
                    "Semi-Strong Match", // 5
                    "Strong Match", // 6
                    "Partial Synonym Match", // 7
                    "Perfect Synonym Match", // 8
                    "Partial Name Match", // 9
                    "Perfect Name Match", // 10
                ];

            /// <summary>Perfect match text, like a name.</summary>
            public List<string> PerfectMatches = [];

            /// <summary>Hand-chosen additional semiperfect search terms.</summary>
            public List<string> Synonyms = [];

            /// <summary>Very important matchables.</summary>
            public List<string> Strongs = [];

            /// <summary>Other normal matchables.</summary>
            public List<string> Decents = [];

            /// <summary>Any/all remaining text.</summary>
            public List<string> Backups = [];

            private static bool Try(List<string> list, string search, int val, out int toUse)
            {
                if (list.Contains(search))
                {
                    toUse = val;
                    return true;
                }
                if (list.Any(s => s.Contains(search)))
                {
                    toUse = val - 1;
                    return true;
                }
                toUse = 0;
                return false;
            }

            /// <summary>Searches the object for text matches. Returns 0 for no match, 10 for a perfect match, 5 for an average match, etc.</summary>
            /// <param name="search">Search text (all lowercase).</param>
            public int GetMatchQuality(string search)
            {
                if (Try(PerfectMatches, search, 10, out int result)
                    || Try(Synonyms, search, 8, out result)
                    || Try(Strongs, search, 6, out result)
                    || Try(Decents, search, 4, out result)
                    || Try(Backups, search, 2, out result))
                {
                    return result;
                }
                return 0;
            }
        }

        /// <summary>Cleans up and validates search data.</summary>
        public void ValidateSearchables(MetaDocs docs)
        {
            if (SearchHelper.PerfectMatches.RemoveAll(s => s is null) + SearchHelper.Synonyms.RemoveAll(s => s is null)
                + SearchHelper.Strongs.RemoveAll(s => s is null) + SearchHelper.Decents.RemoveAll(s => s is null) + SearchHelper.Backups.RemoveAll(s => s is null) > 0)
            {
                docs.LoadErrors.Add($"{Type.Name} object {CleanName} contains null values in searchable data");
            }
            SearchHelper.PerfectMatches = SearchHelper.PerfectMatches.Select(s => s.ToLowerFast()).ToList();
            SearchHelper.Synonyms = SearchHelper.Synonyms.Select(s => s.ToLowerFast()).ToList();
            SearchHelper.Strongs = SearchHelper.Strongs.Select(s => s.ToLowerFast()).ToList();
            SearchHelper.Decents = SearchHelper.Decents.Select(s => s.ToLowerFast()).ToList();
            SearchHelper.Backups = SearchHelper.Backups.Select(s => s.ToLowerFast()).ToList();
        }

        /// <summary>Data to help object searches.</summary>
        public SearchableHelpers SearchHelper = new();

        /// <summary>Build the contents of <see cref="SearchHelper"/>.</summary>
        public virtual void BuildSearchables()
        {
            SearchHelper.PerfectMatches.Add(CleanName);
            SearchHelper.PerfectMatches.Add(Name);
            SearchHelper.Synonyms.AddRange(Synonyms);
            if (Group != null)
            {
                SearchHelper.Strongs.Add(Group);
            }
            SearchHelper.Decents.AddRange(Warnings);
            SearchHelper.Backups.Add(SourceFile);
        }
    }
}
