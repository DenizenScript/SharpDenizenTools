using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// Abstract base for a type of meta object.
    /// </summary>
    public abstract class MetaObject
    {
        /// <summary>
        /// Get the meta type of the object.
        /// </summary>
        public abstract MetaType Type { get; }

        /// <summary>
        /// Get the name of the object. May have capitals.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Whether this object has multiple names.
        /// </summary>
        public bool HasMultipleNames = false;

        /// <summary>
        /// If the object has multiple names, returns the full set of names.
        /// </summary>
        public virtual IEnumerable<string> MultiNames => new string[] { CleanName };

        /// <summary>
        /// Get the clean lowercase name of the object.
        /// </summary>
        public virtual string CleanName => Name.ToLowerFast();

        /// <summary>
        /// What categorization group the object is in.
        /// </summary>
        public string Group;

        /// <summary>
        /// Any warnings applied to this object type.
        /// </summary>
        public List<string> Warnings = new List<string>();

        /// <summary>
        /// Required plugin(s) if applicable.
        /// </summary>
        public string Plugin;

        /// <summary>
        /// The file in source code that defined this meta object.
        /// </summary>
        public string SourceFile;

        /// <summary>
        /// The searchable text pile.
        /// </summary>
        public string Searchable;

        /// <summary>
        /// A deprecation notice, if any.
        /// </summary>
        public string Deprecated;

        /// <summary>
        /// The backing <see cref="MetaDocs"/> instance.
        /// </summary>
        public MetaDocs Meta;

        /// <summary>
        /// Apply a setting value to this meta object.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <returns>Whether the value was applied.</returns>
        public virtual bool ApplyValue(string key, string value)
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
                default:
                    return false;
            }
        }

        /// <summary>
        /// Finds the closing tag mark, compensating for layered tags.
        /// </summary>
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

        /// <summary>
        /// Adds the object to the meta docs set.
        /// </summary>
        /// <param name="docs">The docs set.</param>
        public abstract void AddTo(MetaDocs docs);

        /// <summary>
        /// Checks the object for validity, after all loading is done.
        /// </summary>
        /// <param name="docs">The relevant docs object.</param>
        public virtual void PostCheck(MetaDocs docs)
        {
        }

        /// <summary>
        /// Post-check handler to require specific values be set (not-null).
        /// </summary>
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

        /// <summary>
        /// Post-check handler for linkable text, to find bad links.
        /// </summary>
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
                    string type = metaCommand.Substring(0, firstSpace).ToLowerFast();
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
                    else if (type.Equals("mechanism"))
                    {
                        exists = docs.Mechanisms.ContainsKey(searchText);
                    }
                    else if (type.Equals("event"))
                    {
                        if (searchText.StartsWith("on "))
                        {
                            searchText = searchText["on ".Length..];
                        }
                        exists = docs.Events.Values.Any(e => e.CleanEvents.Any(s => s.Contains(searchText)));
                        if (!exists)
                        {
                            exists = docs.Events.Values.Any(e => e.RegexMatcher.IsMatch(searchText));
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

        /// <summary>
        /// Post-check handler for tags, used in <see cref="MetaCommand"/> and <see cref="MetaMechanism"/>.
        /// </summary>
        /// <param name="docs">The relevant docs object.</param>
        /// <param name="tags">The relevant tags list.</param>
        public void PostCheckTags(MetaDocs docs, string[] tags)
        {
            foreach (string tag in tags)
            {
                if (tag.EndsWith(">"))
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

        /// <summary>
        /// Get all text for related to this object that may be useful for searches.
        /// </summary>
        public virtual string GetAllSearchableText()
        {
            string warningFlat = string.Join('\n', Warnings);
            return $"{Name}\n{CleanName}\n{Group}\n{warningFlat}\n{Plugin}\n{SourceFile}";
        }
    }
}
