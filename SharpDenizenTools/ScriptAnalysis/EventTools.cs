using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDenizenTools.MetaHandlers;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Helper tools related to scriptevent processing.</summary>
    public static class EventTools
    {
        /// <summary>Matcher for numerical digits 0-9 only.</summary>
        public static readonly AsciiMatcher NumbersMatcher = new(c => c >= '0' && c <= '9');

        /// <summary>Separates the switches from an event line.</summary>
        /// <param name="meta">The relevant meta docs.</param>
        /// <param name="eventLine">The original full event line.</param>
        /// <param name="switches">The output switch list.</param>
        /// <returns>The cleaned event line.</returns>
        public static string SeparateSwitches(MetaDocs meta, string eventLine, out List<KeyValuePair<string, string>> switches)
        {
            string[] parts = eventLine.SplitFast(' ');
            StringBuilder output = new();
            switches = new List<KeyValuePair<string, string>>();
            foreach (string part in parts)
            {
                if (part.Contains(':') && !meta.IsInDataValueSet("not_switches", part.Before(':')) && !NumbersMatcher.IsOnlyMatches(part.Before(':')))
                {
                    string switchName = part.BeforeAndAfter(':', out string switchVal);
                    switches.Add(new KeyValuePair<string, string>(switchName.ToLowerFast(), switchVal));
                }
                else
                {
                    output.Append(part).Append(' ');
                }
            }
            return output.ToString().Trim();
        }

        /// <summary>Parse some event format input into a set of could-matchers.</summary>
        public static List<ScriptEventCouldMatcher> ParseMatchers(string format, Dictionary<string, Func<string, bool, int>> validatorTypes, Action<string> error)
        {
            List<ScriptEventCouldMatcher> matcherList = new();
            BuildMainContent(matcherList, format, validatorTypes, (s) => error($"while parsing event '{format}': {s}"));
            return matcherList;
        }

        private static void BuildMainContent(List<ScriptEventCouldMatcher> output, string format, Dictionary<string, Func<string, bool, int>> validatorTypes, Action<string> error)
        {
            int paren = format.IndexOf('(');
            if (paren == -1)
            {
                output.Add(new ScriptEventCouldMatcher(format, error, validatorTypes));
                return;
            }
            int endParen = format.IndexOf(')', paren);
            if (endParen == -1)
            {
                error("Invalid couldMatcher registration '" + format + "': inconsistent parens");
                return;
            }
            string baseText = paren == 0 ? "" : format[..(paren - 1)];
            string afterText = endParen + 2 >= format.Length ? "" : format[(endParen + 2)..];
            string optional = format[(paren + 1)..endParen];
            BuildMainContent(output, baseText + (string.IsNullOrEmpty(afterText) || string.IsNullOrWhiteSpace(baseText) ? afterText : (" " + afterText)), validatorTypes, error);
            BuildMainContent(output, (string.IsNullOrEmpty(baseText) ? "" : (baseText + " ")) + optional + (string.IsNullOrEmpty(afterText) ? "" : (" " + afterText)), validatorTypes, error);
        }
    }
}
