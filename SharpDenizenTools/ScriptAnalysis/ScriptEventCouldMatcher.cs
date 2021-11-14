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
        public static HashSet<string> GlobalSwitches = new HashSet<string>() { "cancelled", "ignorecancelled", "priority", "server_flagged", "in" };

        /// <summary>The raw format string used to construct this couldMatcher.</summary>
        public string Format;

        /// <summary>The raw parts list used to construct this couldMatcher.</summary>
        public string[] Parts;

        /// <summary>The array of validator objects for this couldMatcher. The path length should equal the array length, and each argument match the validator.</summary>
        public Func<string, bool, bool>[] Validators;

        /// <summary>Special optimization trick: an array of argument indices to control testing order. The simplest tests are run first.</summary>
        public int[] ArgOrder;

        /// <summary>Properly log an error message.</summary>
        public Action<string> Error;

        /// <summary>Registry of validators, as a map from name to instance object.</summary>
        public Dictionary<string, Func<string, bool, bool>> KnownValidatorTypes;

        /// <summary>Construct the could matcher from the given reference format and switch set.</summary>
        public ScriptEventCouldMatcher(string _format, Action<string> _error, Dictionary<string, Func<string, bool, bool>> validatorTypes)
        {
            KnownValidatorTypes = validatorTypes;
            Error = _error;
            Format = _format;
            List<Func<string, bool, bool>> validatorList = new List<Func<string, bool, bool>>();
            Parts = Format.Split(' ');
            List<int> argOrderList = new List<int>();
            List<int> secondaryArgList = new List<int>();
            int index = 0;
            foreach (string arg in Parts)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    Error("Event matcher format error: '" + Format + "' has a double space?");
                    continue;
                }
                if (arg.StartsWithFast('<'))
                {
                    if (!arg.EndsWithFast('>'))
                    {
                        Error("Event matcher format error: '" + Format + "' has an unclosed fill-in part.");
                        continue;
                    }
                    string toUse = arg[1..^1];
                    if (toUse.StartsWithFast('\'') && toUse.EndsWithFast('\''))
                    {
                        string rawCopy = arg;
                        validatorList.Add((word, precise) => rawCopy == word);
                        argOrderList.Add(index++);
                    }
                    else
                    {
                        if (!KnownValidatorTypes.TryGetValue(toUse, out Func<string, bool, bool> validator))
                        {
                            Error("Event matcher format error: '" + Format + "' has an unrecognized input type '" + toUse + "'");
                            continue;
                        }
                        validatorList.Add(validator);
                        secondaryArgList.Add(index++);
                    }
                }
                else if (arg.Contains('|'))
                {
                    HashSet<string> rawValues = new HashSet<string>(arg.SplitFast('|'));
                    validatorList.Add((word, price) => rawValues.Contains(word));
                    argOrderList.Add(index++);
                }
                else
                {
                    string rawCopy = arg;
                    validatorList.Add((word, precise) => rawCopy == word);
                    argOrderList.Add(index++);
                }
            }
            Validators = validatorList.ToArray();
            argOrderList.AddRange(secondaryArgList);
            ArgOrder = argOrderList.ToArray();
        }

        /// <summary>Returns true if the path could-match this event.</summary>
        /// <param name="pathBaseParts">The array of words to match events against.</param>
        /// <param name="allowPartial">If false: full event must match. If true: can just be first few words.</param>
        /// <param name="precise">If true: object matchers must be valid. If false: object matchers must look vaguely close to correct.</param>
        public bool DoesMatch(string[] pathBaseParts, bool allowPartial, bool precise)
        {
            if (pathBaseParts.Length != Validators.Length)
            {
                if (!allowPartial || pathBaseParts.Length > Validators.Length)
                {
                    return false;
                }
            }
            foreach (int i in ArgOrder)
            {
                if (i < pathBaseParts.Length && !Validators[i](pathBaseParts[i], precise))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
