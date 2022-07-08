using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>Internally tracked data values helper.</summary>
    public class MetaDataValue : MetaObject
    {
        /// <summary>Irrelevant.</summary>
        public override MetaType Type => null;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => DataKeyName;

        /// <summary>The name of the data key to add this to.</summary>
        public string DataKeyName;

        /// <summary>The value set to add.</summary>
        public string[] Values;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.DataValueSets.GetOrCreate(DataKeyName, () => new HashSet<string>()).UnionWith(Values);
        }

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "name":
                    DataKeyName = value.ToLowerFast();
                    return true;
                case "values":
                    Values = value.Split(',').Select(s => s.Trim().ToLowerFast()).ToArray();
                    return true;
            }
            return false;
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, DataKeyName, Values);
        }
    }
}
