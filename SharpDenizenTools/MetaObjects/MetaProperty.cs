using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A documented object property.</summary>
    public class MetaProperty : MetaObject
    {

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => FullName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            FullName = $"{PropObject}.{PropName}";
            NameForms = [FullName.ToLowerFast(), PropName.ToLowerFast()];
            HasMultipleNames = true;
            docs.Properties.Add(CleanName, this);
            string asTag = $"<{FullName}>";
            string cleanedTag = MetaTag.CleanTag(asTag);
            bool hasControls = Description.StartsWithFast("Controls");
            string cleanedDescription = hasControls ? Description.Substring("Controls".Length) : Description;
            new MetaMechanism()
            {
                Type = MetaDocs.META_TYPE_MECHANISM,
                MechName = PropName,
                MechObject = PropObject,
                Input = Input,
                Description = "(Property) " + (hasControls ? "Sets" : "") + cleanedDescription + MechanismDescription,
                Group = Group ?? "Properties",
                Warnings = Warnings,
                Examples = MechanismExamples,
                Plugin = Plugin,
                SourceFile = SourceFile,
                Deprecated = Deprecated,
                Synonyms = Synonyms,
                Meta = Meta,
                Tags = [asTag]
            }.AddTo(docs);
            new MetaTag()
            {
                Type = MetaDocs.META_TYPE_TAG,
                TagFull = asTag,
                CleanedName = cleanedTag.ToLowerFast(),
                BeforeDot = cleanedTag.Before('.'),
                AfterDotCleaned = cleanedTag.ToLowerFast().After('.'),
                Returns = Input,
                Description = "(Property) " + (hasControls ? "Returns" : "") + cleanedDescription + TagDescription,
                Mechanism = FullName,
                Examples = TagExamples,
                Group = Group ?? "Properties",
                Warnings = Warnings,
                Plugin = Plugin,
                SourceFile = SourceFile,
                Deprecated = Deprecated,
                Meta = Meta,
                Synonyms = Synonyms
            }.AddTo(docs);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => NameForms;

        /// <summary>Both forms of the mech name (the full name, and the partial name).</summary>
        public string[] NameForms = [];

        /// <summary>The full mechanism name (Object.Name).</summary>
        public string FullName;

        /// <summary>The object the property applies to.</summary>
        public string PropObject;

        /// <summary>The name of the property.</summary>
        public string PropName;

        /// <summary>The data type.</summary>
        public string Input;

        /// <summary>The long-form description.</summary>
        public string Description;

        /// <summary>Optional description specific to the property's mechanism.</summary>
        public string MechanismDescription = "";

        /// <summary>Optional description specific to the property's tag.</summary>
        public string TagDescription = "";

        /// <summary>Manual examples for the property's tag. One full script per entry.</summary>
        public List<string> TagExamples = [];

        /// <summary>Manual examples for the property's mechanism. One full script per entry.</summary>
        public List<string> MechanismExamples = [];

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "object":
                    PropObject = value;
                    return true;
                case "name":
                    PropName = value;
                    return true;
                case "input":
                    Input = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "example":
                    TagExamples.Add(value);
                    MechanismExamples.Add(value);
                    return true;
                case "tag-example":
                    TagExamples.Add(value);
                    return true;
                case "mechanism-example":
                    MechanismExamples.Add(value);
                    return true;
                case "tag":
                    TagDescription = '\n' + value;
                    return true;
                case "mechanism":
                    MechanismDescription = '\n' + value;
                    return true;
                default:
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Mechanisms);
            Require(docs, PropObject, PropName, Input, Description);
            PostCheckLinkableText(docs, Description);
        }

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.PerfectMatches.Add(PropName);
            SearchHelper.Strongs.Add(PropObject);
            SearchHelper.Decents.Add(Input);
            SearchHelper.Decents.Add(Description);
        }
    }
}
