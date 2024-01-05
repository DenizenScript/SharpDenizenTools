using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;
using System.Collections.Generic;
using System.Linq;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>An extension to an existing meta object.</summary>
    public class MetaExtension : MetaObject
    {
        /// <inheritdoc/>
        public override string Name => ExtensionName;

        /// <summary>The name of the extension.</summary>
        public string ExtensionName;

        /// <summary>The type of the meta object this extension meta extends.</summary>
        public string ExtendType;

        /// <summary>The name of the meta object this extension meta extends.</summary>
        public string ExtendName;

        /// <summary>Whether the existing values from the extended meta should be included when adding the new values.</summary>
        public bool IncludeExisting = true;

        /// <inheritdoc/>
        public override void AddTo(MetaDocs docs)
        {
            docs.META_TYPE_EXTENSION.Meta.Add(CleanName, this);
        }

        /// <inheritdoc/>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "target_type":
                    ExtendType = value;
                    return true;
                case "target_name":
                    ExtendName = value;
                    return true;
                case "name":
                    ExtensionName = value;
                    return true;
                case "include_existing":
                    return bool.TryParse(value, out IncludeExisting);
            };
            return true;
        }

        /// <inheritdoc/>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, ExtendType, ExtendName, ExtensionName);
            MetaObject extended = docs.MetaTypes.TryGetValue(ExtendType.ToLowerFast(), out IMetaType type) ? type.Meta.GetValueOrDefault(ExtendName.ToLowerFast()) : null;
            if (extended is null)
            {
                docs.LoadErrors.Add($"Extension '{ExtensionName}' has invalid target meta type/name to extend: '{ExtendType}'/'{ExtendName}'.");
                return;
            }
            foreach ((string key, List<string> values) in RawValues.ExceptBy(["target_type", "target_name", "name", "include_existing"], pair => pair.Key))
            {
                string currentValue = IncludeExisting && extended.RawValues.TryGetValue(key, out List<string> currentValues) ? currentValues.Last() : null;
                foreach (string value in values)
                {
                    string newValue = currentValue is not null ? currentValue + "\n\n" + value : value;
                    if (!extended.ApplyValue(docs, key, newValue))
                    {
                        docs.LoadErrors.Add($"Extension '{ExtensionName}' could not extend {ExtendType} meta '{ExtendName}', key/value pair '{key}' -> '{value}' is invalid.");
                    }
                }
            }
        }
    }
}
