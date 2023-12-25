﻿using FreneticUtilities.FreneticExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Holder class for a replica of the Denizen advanced matcher engine.</summary>
    public class AdvancedMatcher
    {
        /// <summary>Entry point for the replica of the Denizen advanced matcher engine.</summary>
        public abstract class MatchHelper
        {
            /// <summary>True if the text is a match, otherwise false.</summary>
            public abstract bool DoesMatch(string input);
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class AlwaysMatchHelper : MatchHelper
        {
            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => true;
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class ExactMatchHelper(string _text) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public string Text = _text.ToLowerFast();

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => Text == input.ToLowerFast();
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class PrefixAsteriskMatchHelper(string _text) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public string Text = _text.ToLowerFast();

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => input.ToLowerFast().EndsWith(Text);
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class PostfixAsteriskMatchHelper(string _text) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public string Text = _text.ToLowerFast();

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => input.ToLowerFast().StartsWith(Text);
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class MultipleAsteriskMatchHelper(string[] _texts) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public string[] Texts = _texts;

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input)
            {
                int index = 0;
                input = input.ToLowerFast();
                if (!input.StartsWith(Texts[0]) || !input.EndsWith(Texts[^1]))
                {
                    return false;
                }
                foreach (string text in Texts)
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }
                    index = input.IndexOf(text, index);
                    if (index == -1)
                    {
                        return false;
                    }
                    index += text.Length;
                }
                return true;
            }
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class RegexMatchHelper(string _regex) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public Regex Pattern = new(_regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => Pattern.IsMatch(input);
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class MultipleMatchesHelper(MatchHelper[] _matches) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public MatchHelper[] Matches = _matches;

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input)
            {
                foreach (MatchHelper match in Matches)
                {
                    if (match.DoesMatch(input))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Implements <see cref="MatchHelper"/>.</summary>
        public class InverseMatchHelper(MatchHelper _matcher) : MatchHelper
        {

            /// <summary>Required data.</summary>
            public MatchHelper Matcher = _matcher;

            /// <summary>Implements <see cref="MatchHelper.DoesMatch(string)"/>.</summary>
            public override bool DoesMatch(string input) => !Matcher.DoesMatch(input);
        }

        /// <summary>Returns true if the text uses the advanced matcher system.</summary>
        public static bool IsAdvancedMatchable(string input)
        {
            return input.StartsWith("regex:") || input.Contains('|') || input.Contains('*') || input.StartsWithFast('!');
        }

        /// <summary>Creates a valid matcher out of the given text.</summary>
        public static MatchHelper CreateMatcher(string input)
        {
            MatchHelper result;
            int asterisk;
            if (input.StartsWith('!'))
            {
                result = new InverseMatchHelper(CreateMatcher(input[1..]));
            }
            else if (input.StartsWith("regex:"))
            {
                result = new RegexMatchHelper(input["regex:".Length..]);
            }
            else if (input.Contains('|'))
            {
                string[] split = input.SplitFast('|');
                MatchHelper[] matchers = new MatchHelper[split.Length];
                for (int i = 0; i < split.Length; i++)
                {
                    matchers[i] = CreateMatcher(split[i]);
                }
                result = new MultipleMatchesHelper(matchers);
            }
            else if ((asterisk = input.IndexOf('*')) != -1)
            {
                if (input.Length == 1)
                {
                    result = new AlwaysMatchHelper();
                }
                else if (asterisk == 0 && input.IndexOf('*', 1) == -1)
                {
                    result = new PrefixAsteriskMatchHelper(input[1..]);
                }
                else if (asterisk == input.Length - 1)
                {
                    result = new PostfixAsteriskMatchHelper(input[..^1]);
                }
                else
                {
                    result = new MultipleAsteriskMatchHelper(input.ToLowerFast().SplitFast('*'));
                }
            }
            else
            {
                result = new ExactMatchHelper(input);
            }
            return result;
        }
    }
}
