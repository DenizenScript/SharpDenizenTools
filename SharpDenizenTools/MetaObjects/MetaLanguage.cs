using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SharpDenizenTools.MetaHandlers;
using FreneticUtilities.FreneticExtensions;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A language documentation.
    /// </summary>
    public class MetaLanguage : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_LANGUAGE;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => LangName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Languages.Add(CleanName, this);
        }

        /// <summary>
        /// The name of the language.
        /// </summary>
        public string LangName;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "name":
                    LangName = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                default:
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Languages);
            Require(docs, LangName, Description);
            PostCheckLinkableText(docs, Description);
        }

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.Decents.Add(Description);
        }
    }
}
