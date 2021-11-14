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
        /// <summary>Registry of validators, as a map from name to instance object.</summary>
        public static Dictionary<string, Func<string, bool>> KnownValidatorTypes = new Dictionary<string, Func<string, bool>>()
        {
             // TODO: actual processing
            { "entity", s => true },
            { "projectile", s => true },
            { "hanging", s => true },
            { "vehicle", s => true },
            { "item", s => true },
            { "inventory", s => true },
            { "block", s => true },
            { "liquid_block", s => true },
            { "material", s => true },
            { "area", s => true },
            { "world", s => true },
            { "any", s => true }
        };

        /// <summary>Switches that are globally available.</summary>
        public static HashSet<string> GlobalSwitches = new HashSet<string>() { "cancelled", "ignorecancelled", "priority", "server_flagged", "in" };

        /// <summary>The raw format string used to construct this couldMatcher.</summary>
        public string Format;

        /// <summary>The raw parts list used to construct this couldMatcher.</summary>
        public string[] Parts;

        /// <summary>The array of validator objects for this couldMatcher. The path length should equal the array length, and each argument match the validator.</summary>
        public Func<string, bool>[] Validators;

        /// <summary>Special optimization trick: an array of argument indices to control testing order. The simplest tests are run first.</summary>
        public int[] ArgOrder;

        /// <summary>Properly log an error message.</summary>
        public Action<string> Error;

        /// <summary>Construct the could matcher from the given reference format and switch set.</summary>
        public ScriptEventCouldMatcher(string format, Action<string> error)
        {
            this.Error = error;
            this.Format = format;
            List<Func<string, bool>> validatorList = new List<Func<string, bool>>();
            Parts = format.Split(' ');
            List<int> argOrderList = new List<int>();
            List<int> secondaryArgList = new List<int>();
            int index = 0;
            foreach (string arg in Parts)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    Error("Event matcher format error: '" + format + "' has a double space?");
                    continue;
                }
                if (arg.StartsWithFast('<'))
                {
                    if (!arg.EndsWithFast('>'))
                    {
                        Error("Event matcher format error: '" + format + "' has an unclosed fill-in part.");
                        continue;
                    }
                    string toUse = arg[1..^1];
                    if (toUse.StartsWithFast('\'') && toUse.EndsWithFast('\''))
                    {
                        string rawCopy = arg;
                        validatorList.Add(s => rawCopy == s);
                        argOrderList.Add(index++);
                    }
                    else
                    {
                        if (!KnownValidatorTypes.TryGetValue(toUse, out Func<string, bool> validator))
                        {
                            Error("Event matcher format error: '" + format + "' has an unrecognized input type '" + toUse + "'");
                            continue;
                        }
                        validatorList.Add(validator);
                        secondaryArgList.Add(index++);
                    }
                }
                else if (arg.Contains('|'))
                {
                    HashSet<string> rawValues = new HashSet<string>(arg.SplitFast('|'));
                    validatorList.Add(rawValues.Contains);
                    argOrderList.Add(index++);
                }
                else
                {
                    string rawCopy = arg;
                    validatorList.Add(s => rawCopy == s);
                    argOrderList.Add(index++);
                }
            }
            Validators = validatorList.ToArray();
            argOrderList.AddRange(secondaryArgList);
            ArgOrder = argOrderList.ToArray();
        }

        /// <summary>Returns true if the path could-match this event.</summary>
        public bool DoesMatch(string[] pathBaseParts)
        {
            if (pathBaseParts.Length != Validators.Length)
            {
                return false;
            }
            foreach (int i in ArgOrder)
            {
                if (!Validators[i](pathBaseParts[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
