using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A documented mechanism.
    /// </summary>
    public class MetaMechanism : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_MECHANISM;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => FullName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            FullName = $"{MechObject}.{MechName}";
            NameForms = new string[] { FullName, MechName };
            HasMultipleNames = true;
            docs.Mechanisms.Add(CleanName, this);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => NameForms;

        /// <summary>
        /// Both forms of the mech name (the full name, and the partial name).
        /// </summary>
        public string[] NameForms = Array.Empty<string>();

        /// <summary>
        /// The full mechanism name (Object.Name).
        /// </summary>
        public string FullName;

        /// <summary>
        /// The object the mechanism applies to.
        /// </summary>
        public string MechObject;

        /// <summary>
        /// The name of the mechanism.
        /// </summary>
        public string MechName;

        /// <summary>
        /// The input type.
        /// </summary>
        public string Input;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Tags documented for this mechanism. One tag per string.
        /// </summary>
        public string[] Tags = Array.Empty<string>();

        /// <summary><see cref="MetaObject.ApplyValue(string, string)"/></summary>
        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "object":
                    MechObject = value;
                    return true;
                case "name":
                    MechName = value;
                    return true;
                case "input":
                    Input = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "tags":
                    Tags = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, MechObject, MechName, Input, Description);
            PostCheckTags(docs, Tags);
            PostCheckLinkableText(docs, Description);
            if (Tags.IsEmpty())
            {
                if (docs.Tags.ContainsKey(CleanName))
                {
                    docs.LoadErrors.Add($"Mechanism '{Name}' has no Tags link, but has the same name as an existing tag. A link should be added.");
                }
            }
        }

        /// <summary><see cref="MetaObject.GetAllSearchableText"/></summary>
        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allTags = string.Join('\n', Tags);
            return $"{baseText}\n{allTags}\n{Input}\n{Description}\n{MechObject}\n{MechName}";
        }
    }
}
