using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreneticUtilities.FreneticExtensions;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Helper to generate automatic logic for ScriptEvent#couldMatch.</summary>
    public class ScriptEventCouldMatcher
    {
        /// <summary>Switches that are globally available.</summary>
        public static HashSet<string> GlobalSwitches = new() { "cancelled", "ignorecancelled", "priority", "server_flagged", "in" };

        /// <summary>The raw format string used to construct this couldMatcher.</summary>
        public string Format;

        /// <summary>The raw parts list used to construct this couldMatcher.</summary>
        public string[] Parts;

        /// <summary>The array of validator objects for this couldMatcher. The path length should equal the array length, and each argument match the validator.</summary>
        public Func<string, bool, int>[] Validators;

        /// <summary>Special optimization trick: an array of argument indices to control testing order. The simplest tests are run first.</summary>
        public int[] ArgOrder;

        /// <summary>Properly log an error message.</summary>
        public Action<string> Error;

        /// <summary>Registry of validators, as a map from name to instance object.</summary>
        public Dictionary<string, Func<string, bool, int>> KnownValidatorTypes;

        /// <summary>Construct the could matcher from the given reference format and switch set.</summary>
        public ScriptEventCouldMatcher(string _format, Action<string> _error, Dictionary<string, Func<string, bool, int>> validatorTypes)
        {
            KnownValidatorTypes = validatorTypes;
            Error = _error;
            Format = _format;
            List<Func<string, bool, int>> validatorList = new();
            Parts = Format.Split(' ');
            List<int> argOrderList = new();
            List<int> secondaryArgList = new();
            int index = 0;
            foreach (string arg in Parts)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    Error($"Event matcher format error: '{Format}' has a double space?");
                    continue;
                }
                if (arg.StartsWithFast('<'))
                {
                    if (!arg.EndsWithFast('>'))
                    {
                        Error($"Event matcher format error: '{Format}' has an unclosed fill-in part.");
                        continue;
                    }
                    string toUse = arg[1..^1];
                    if (toUse.StartsWithFast('\'') && toUse.EndsWithFast('\''))
                    {
                        string rawCopy = arg;
                        validatorList.Add((word, precise) => 1);
                        secondaryArgList.Add(index++);
                    }
                    else
                    {
                        if (!KnownValidatorTypes.TryGetValue(toUse, out Func<string, bool, int> validator))
                        {
                            Error($"Event matcher format error: '{Format}' has an unrecognized input type '{toUse}'");
                            continue;
                        }
                        validatorList.Add(validator);
                        secondaryArgList.Add(index++);
                    }
                }
                else if (arg.Contains('|'))
                {
                    HashSet<string> rawValues = new(arg.SplitFast('|'));
                    validatorList.Add((word, price) => rawValues.Contains(word) ? 10 : 0);
                    argOrderList.Add(index++);
                }
                else
                {
                    string rawCopy = arg;
                    validatorList.Add((word, precise) => rawCopy == word ? 10 : 0);
                    argOrderList.Add(index++);
                }
            }
            Validators = validatorList.ToArray();
            argOrderList.AddRange(secondaryArgList);
            ArgOrder = argOrderList.ToArray();
        }

        /// <summary>Returns 0 for no match, 1 for bare minimum match, up to 10 for best match.</summary>
        /// <param name="pathBaseParts">The array of words to match events against.</param>
        /// <param name="allowPartial">If false: full event must match. If true: can just be first few words.</param>
        /// <param name="precise">If true: object matchers must be valid. If false: object matchers must look vaguely close to correct.</param>
        public int TryMatch(string[] pathBaseParts, bool allowPartial, bool precise)
        {
            if (pathBaseParts.Length != Validators.Length)
            {
                if (!allowPartial || pathBaseParts.Length > Validators.Length)
                {
                    return 0;
                }
            }
            int max = 0;
            foreach (int i in ArgOrder)
            {
                if (i < pathBaseParts.Length)
                {
                    int match = Validators[i](pathBaseParts[i], precise);
                    if (match == 0)
                    {
                        return 0;
                    }
                    max = Math.Max(max, match);
                }
            }
            if (pathBaseParts.Length != Validators.Length)
            {
                return 1;
            }
            return max;
        }

        /// <summary>Returns true if this matcher matches better than the second matcher.</summary>
        public bool IsBetterMatchThan(string[] pathBaseParts, bool precise, ScriptEventCouldMatcher matcher2)
        {
            if (Validators.Length != matcher2.Validators.Length)
            {
                return Validators.Length > matcher2.Validators.Length;
            }
            int betterMatches = 0;
            foreach (int i in ArgOrder)
            {
                if (i < pathBaseParts.Length)
                {
                    int match = Validators[i](pathBaseParts[i], precise);
                    int match2 = matcher2.Validators[i](pathBaseParts[i], precise);
                    betterMatches += (match > match2) ? 1 : -1;
                }
            }
            return betterMatches >= 0;
        }
    }
}
