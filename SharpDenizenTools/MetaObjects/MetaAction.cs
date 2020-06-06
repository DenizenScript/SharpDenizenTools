using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A documented action.
    /// </summary>
    public class MetaAction : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_ACTION;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => Actions[0];

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Actions.Add(CleanName, this);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => CleanActions;

        /// <summary>
        /// The names of the action.
        /// </summary>
        public string[] Actions = new string[0];

        /// <summary>
        /// The names of the actions, autocleaned.
        /// </summary>
        public string[] CleanActions = new string[0];

        /// <summary>
        /// The trigger reason.
        /// </summary>
        public string Triggers;

        /// <summary>
        /// A hacked-in regex matcher.
        /// </summary>
        public Regex RegexMatcher = null;

        /// <summary>
        /// Context tags. One tag per string.
        /// </summary>
        public string[] Context = new string[0];

        /// <summary>
        /// Determination options. One Determination per string.
        /// </summary>
        public string[] Determinations = new string[0];

        /// <summary><see cref="MetaObject.ApplyValue(string, string)"/></summary>
        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "actions":
                    Actions = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    CleanActions = Actions.Select(s => s.ToLowerFast()).ToArray();
                    HasMultipleNames = Actions.Length > 1;
                    string regexable = Actions.FirstOrDefault(s => s.Contains("<")) ?? Actions[0];
                    if (regexable.Contains("<"))
                    {
                        int start = regexable.IndexOf('<');
                        int end = regexable.IndexOf('>');
                        regexable = regexable.Substring(0, start) + "[^\\s]+" + regexable.Substring(end + 1);
                    }
                    RegexMatcher = new Regex(regexable, RegexOptions.Compiled);
                    return true;
                case "triggers":
                    Triggers = value;
                    return true;
                case "context":
                    Context = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "determine":
                    Determinations = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, Actions[0], Triggers);
            PostCheckLinkableText(docs, Triggers);
            foreach (string context in Context)
            {
                PostCheckLinkableText(docs, context);
            }
            foreach (string determine in Determinations)
            {
                PostCheckLinkableText(docs, determine);
            }
        }

        /// <summary><see cref="MetaObject.GetAllSearchableText"/></summary>
        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allActions = string.Join('\n', Actions);
            string allContexts = string.Join('\n', Context);
            string allDeterminations = string.Join('\n', Determinations);
            return $"{baseText}\n{allActions}\n{Triggers}\n{allContexts}\n{allDeterminations}";
        }
    }
}
