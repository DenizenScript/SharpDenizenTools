using FreneticUtilities.FreneticExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.ScriptAnalysis
{

    /// <summary>Represents a set of strings that may have only partial knowledge.</summary>
    public class MixedKnowledgeSet
    {
        /// <summary>The set of exactly-known strings.</summary>
        public HashSet<string> ExactKnown = new();

        /// <summary>The set of partially-known strings.</summary>
        public HashSet<string> PartialKnown = new();

        /// <summary>The lowest known length in the set.</summary>
        public int MinLength;

        /// <summary>The highest known length in the set.</summary>
        public int MaxLength;

        /// <summary>Merges in all data from another set.</summary>
        public void MergeIn(MixedKnowledgeSet set)
        {
            ExactKnown.UnionWith(set.ExactKnown);
            PartialKnown.UnionWith(set.PartialKnown);
            MinLength = Math.Min(MinLength, set.MinLength);
            MaxLength = Math.Max(MaxLength, set.MaxLength);
        }

        /// <summary>Adds a new string to the set.</summary>
        public void Add(string str)
        {
            if (str.Contains('<'))
            {
                str = str.Before('<');
                PartialKnown.Add(str);
            }
            else
            {
                ExactKnown.Add(str);
            }
            MinLength = Math.Min(MinLength, str.Length);
            MaxLength = Math.Max(MaxLength, str.Length);
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
            if (option.Length < MinLength)
            {
                return false;
            }
            if (option.Length <= MaxLength && (ExactKnown.Contains(option) || PartialKnown.Contains(option)))
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

        /// <summary>Gets an enumerable of every string in the set, including partial ones.</summary>
        public IEnumerable<string> EnumerateAll()
        {
            foreach (string exact in ExactKnown)
            {
                yield return exact;
            }
            foreach (string partial in PartialKnown)
            {
                yield return partial;
            }
        }
    }
}
