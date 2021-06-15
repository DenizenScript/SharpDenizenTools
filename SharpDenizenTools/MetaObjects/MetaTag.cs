using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A documented tag.
    /// </summary>
    public class MetaTag : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_TAG;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => TagFull;

        /// <summary><see cref="MetaObject.CleanName"/></summary>
        public override string CleanName => CleanedName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Tags.Add(CleanName, this);
            docs.TagBases.Add(CleanName.BeforeAndAfter('.', out string otherBits));
            foreach (string bit in otherBits.Split('.'))
            {
                docs.TagParts.Add(bit);
            }
            if (!string.IsNullOrWhiteSpace(Deprecated))
            {
                foreach (string bit in CleanName.Split('.'))
                {
                    docs.TagDeprecations[bit] = Deprecated;
                }
            }
        }

        /// <summary>
        /// Cleans tag text for searchability.
        /// </summary>
        public static string CleanTag(string text)
        {
            StringBuilder cleaned = new StringBuilder(text.Length);
            bool skipping = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<' || c == '>')
                {
                    continue;
                }
                if (c == '[')
                {
                    skipping = true;
                    continue;
                }
                if (c == ']')
                {
                    skipping = false;
                    continue;
                }
                if (skipping)
                {
                    continue;
                }
                cleaned.Append(c);
            }
            return cleaned.ToString();
        }

        /// <summary>
        /// The cleaned (searchable) name.
        /// </summary>
        public string CleanedName;

        /// <summary>
        /// The text before the first dot (with tag cleaning applied).
        /// Will have capitalized characters.
        /// </summary>
        public string BeforeDot;

        /// <summary>
        /// The text after the first dot (with tag cleaning applied).
        /// </summary>
        public string AfterDotCleaned;

        /// <summary>
        /// The full tag syntax text.
        /// </summary>
        public string TagFull;

        /// <summary>
        /// The return type.
        /// </summary>
        public string Returns;

        /// <summary>
        /// The return object type.
        /// </summary>
        public MetaObjectType ReturnType;

        /// <summary>
        /// The base tag type (if any).
        /// </summary>
        public MetaObjectType BaseType;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary>
        /// The associated mechanism, if any.
        /// </summary>
        public string Mechanism = "";

        /// <summary><see cref="MetaObject.ApplyValue(string, string)"/></summary>
        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "attribute":
                    TagFull = value;
                    CleanedName = CleanTag(TagFull);
                    if (CleanedName.Contains('.') && !CleanedName.StartsWith("&"))
                    {
                        BeforeDot = CleanedName.Before('.');
                    }
                    else
                    {
                        BeforeDot = "Base";
                    }
                    CleanedName = CleanedName.ToLowerFast();
                    AfterDotCleaned = CleanedName.After('.');
                    return true;
                case "returns":
                    Returns = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "mechanism":
                    Mechanism = value;
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Tags);
            Require(docs, TagFull, Returns, Description);
            if (TagFull.Contains(' '))
            {
                docs.LoadErrors.Add($"Tag '{TagFull}' contains spaces.");
            }
            if (!string.IsNullOrWhiteSpace(Mechanism))
            {
                if (!docs.Mechanisms.ContainsKey(Mechanism.ToLowerFast()))
                {
                    docs.LoadErrors.Add($"Tag '{Name}' references mechanism '{Mechanism}', which doesn't exist.");
                }
                PostCheckLinkableText(docs, Mechanism);
            }
            else
            {
                if (docs.Mechanisms.ContainsKey(CleanedName))
                {
                    docs.LoadErrors.Add($"Tag '{Name}' has no mechanism link, but has the same name as an existing mechanism. A link should be added.");
                }
            }
            ReturnType = docs.ObjectTypes.GetValueOrDefault(Returns.ToLowerFast().Before('('));
            if (ReturnType == null)
            {
                docs.LoadErrors.Add($"Tag '{Name}' specifies return type '{Returns}' which does not appear to be a valid object type.");
            }
            BaseType = docs.ObjectTypes.GetValueOrDefault(BeforeDot.ToLowerFast());
            PostCheckLinkableText(docs, Description);
        }

        /// <summary><see cref="MetaObject.GetAllSearchableText"/></summary>
        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            return $"{baseText}\n{TagFull}\n{Returns}\n{Description}\n{Mechanism}";
        }
    }
}
