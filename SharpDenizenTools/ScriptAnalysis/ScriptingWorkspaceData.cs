using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Represents a full workspace of scripts.</summary>
    public class ScriptingWorkspaceData
    {
        /// <summary>All server flag names set within the workspace.</summary>
        public MixedKnowledgeSet AllKnownServerFlagNames = new();

        /// <summary>All object flag names set within the workspace.</summary>
        public MixedKnowledgeSet AllKnownObjectFlagNames = new();

        /// <summary>All containers within the workspace.</summary>
        public Dictionary<string, ScriptContainerData> Scripts = new();

        /// <summary>AllMerges another workspace data into this one.</summary>
        public void MergeIn(ScriptingWorkspaceData other)
        {
            AllKnownServerFlagNames.MergeIn(other.AllKnownServerFlagNames);
            AllKnownObjectFlagNames.MergeIn(other.AllKnownObjectFlagNames);
            foreach ((string name, ScriptContainerData data) in Scripts)
            {
                Scripts[name] = data;
            }
        }
    }
}
