using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SharpDenizenTools.MetaHandlers;
using FreneticUtilities.FreneticExtensions;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A page of the beginner's guide.
    /// </summary>
    public class MetaGuidePage : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_GUIDEPAGE;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => PageName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.GuidePages.Add(CleanName, this);
        }

        /// <summary>The name of the page.</summary>
        public string PageName;

        /// <summary>The URL to the page.</summary>
        public string URL;

        /// <summary>If true, this is an entry within a page rather than its own page.</summary>
        public bool IsSubPage;

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            SourceFile = URL;
            base.BuildSearchables();
            SearchHelper.PerfectMatches.Add(URL);
        }
    }
}
