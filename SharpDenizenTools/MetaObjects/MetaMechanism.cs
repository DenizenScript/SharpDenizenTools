using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A documented mechanism.</summary>
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
            NameForms = [FullName.ToLowerFast(), MechName.ToLowerFast()];
            HasMultipleNames = true;
            docs.Mechanisms.Add(CleanName, this);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => NameForms;

        /// <summary>Both forms of the mech name (the full name, and the partial name).</summary>
        public string[] NameForms = [];

        /// <summary>The full mechanism name (Object.Name).</summary>
        public string FullName;

        /// <summary>The object the mechanism applies to.</summary>
        public string MechObject;

        /// <summary>The name of the mechanism.</summary>
        public string MechName;

        /// <summary>The input type.</summary>
        public string Input;

        /// <summary>The long-form description.</summary>
        public string Description;

        /// <summary>Tags documented for this mechanism. One tag per string.</summary>
        public string[] Tags = [];

        /// <summary>Manual examples of this tag. One full script per entry.</summary>
        public List<string> Examples = [];

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
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
                case "example":
                    Examples.Add(value);
                    return true;
                default:
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Mechanisms);
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

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.PerfectMatches.Add(MechName);
            SearchHelper.Strongs.Add(MechObject);
            SearchHelper.Decents.Add(Input);
            SearchHelper.Decents.Add(Description);
            SearchHelper.Backups.AddRange(Tags);
        }
    }
}
