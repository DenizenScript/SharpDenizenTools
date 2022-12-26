using FreneticUtilities.FreneticExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Represents the data of a script container.</summary>
    public class ScriptContainerData
    {
        /// <summary>The name of the script.</summary>
        public string Name;

        /// <summary>The line number in-file the script is found on (specifically the line of its title key).</summary>
        public int LineNumber;

        /// <summary>The name of the file the script is from (sometimes will full or partial path data).</summary>
        public string FileName;

        /// <summary>What type the script is (eg "task", "world", ...).</summary>
        public string Type;

        /// <summary>The type of the container, if its real type is known.</summary>
        public ScriptChecker.KnownScriptType KnownType;

        /// <summary>Any/all definitions established within command sections of this script.</summary>
        public MixedKnowledgeSet DefNames = new();

        /// <summary>Any/all "save:" arg names established within command sections of this script.</summary>
        public MixedKnowledgeSet SaveEntryNames = new();

        /// <summary>Any/all sub-scripts injected within command sections of this script.</summary>
        public MixedKnowledgeSet InjectedPaths = new();

        /// <summary>The list of all script names that are known to actually be injected by this script.</summary>
        public HashSet<string> RealInjects = new();

        /// <summary>Any/all server flags set by command sections of this script.</summary>
        public MixedKnowledgeSet ServerFlags = new();

        /// <summary>Any/all object flags set by command sections of this script.</summary>
        public MixedKnowledgeSet ObjectFlags = new();

        /// <summary>Data keys on the script.</summary>
        public Dictionary<ScriptChecker.LineTrackedString, object> Keys = new();
    }

    /// <summary>Represents a set of strings that may have only partial knowledge.</summary>
    public class MixedKnowledgeSet
    {
        /// <summary>The set of exactly-known strings.</summary>
        public HashSet<string> ExactKnown = new();

        /// <summary>The set of partially-known strings.</summary>
        public HashSet<string> PartialKnown = new();
        
        /// <summary>Merges in all data from another set.</summary>
        public void MergeIn(MixedKnowledgeSet set)
        {
            ExactKnown.UnionWith(set.ExactKnown);
            PartialKnown.UnionWith(set.PartialKnown);
        }

        /// <summary>Adds a new string to the set.</summary>
        public void Add(string str)
        {
            if (str.Contains('<'))
            {
                PartialKnown.Add(str.Before('<'));
            }
            else
            {
                ExactKnown.Add(str);
            }
        }

        /// <summary>Adds all new strings to the set.</summary>
        public void AddAll(params string[] options)
        {
            foreach (string str in options)
            {
                Add(str);
            }
        }

        /// <summary>Returns true if there are any entries in the set.</summary>
        public bool Any()
        {
            return ExactKnown.Any() || PartialKnown.Any();
        }

        /// <summary>Returns true if the input option string matches this set.</summary>
        public bool Contains(string option)
        {
            if (ExactKnown.Contains(option))
            {
                return true;
            }
            foreach (string partial in PartialKnown)
            {
                if (option.StartsWith(partial))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Gets the set of all matches for this set in another string set.</summary>
        public IEnumerable<string> GetAllMatchesIn(IEnumerable<string> options)
        {
            return options.Where(o => Contains(o));
        }
    }
}
