﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A documented action.</summary>
    public class MetaAction : MetaObject
    {
        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => Actions[0];

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Actions.Add(CleanName, this);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => CleanActions;

        /// <summary>The names of the action.</summary>
        public string[] Actions = [];

        /// <summary>The names of the actions, autocleaned.</summary>
        public string[] CleanActions = [];

        /// <summary>The trigger reason.</summary>
        public string Triggers;

        /// <summary>A hacked-in regex matcher.</summary>
        public Regex RegexMatcher = null;

        /// <summary>Context tags. One tag per string.</summary>
        public string[] Context = [];

        /// <summary>Determination options. One Determination per string.</summary>
        public string[] Determinations = [];

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "actions":
                    Actions = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    CleanActions = [.. Actions.Select(s => s.ToLowerFast())];
                    HasMultipleNames = Actions.Length > 1;
                    string outRegex = "";
                    foreach (string action in Actions)
                    {
                        string regexable = action;
                        if (regexable.Contains('<'))
                        {
                            int start = regexable.IndexOf('<');
                            int end = regexable.IndexOf('>');
                            regexable = regexable[..start] + "[^\\s]+" + regexable[(end + 1)..];
                        }
                        outRegex += $"({regexable})|";
                    }
                    if (outRegex.EndsWith('|'))
                    {
                        outRegex = outRegex[0..^1];
                    }
                    RegexMatcher = new Regex($"^(on )?{outRegex}$", RegexOptions.Compiled);
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
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Actions);
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

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.PerfectMatches.AddRange(Actions);
            SearchHelper.Strongs.Add(Triggers);
            SearchHelper.Decents.AddRange(Context);
            SearchHelper.Decents.AddRange(Determinations);
        }
    }
}
