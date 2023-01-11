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
}
