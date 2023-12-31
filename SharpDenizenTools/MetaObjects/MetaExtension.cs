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
        public override MetaType Type => MetaDocs.META_TYPE_EXTENSION;

        /// <inheritdoc/>
        public override string Name => ExtensionName;

        /// <summary>The name of the extension.</summary>
        public string ExtensionName;

        /// <summary>The type and name of the object this extension meta extends.</summary>
        public string Extend;

        /// <summary>Whether the existing values from the extended meta should be included when adding the new values.</summary>
        public bool IncludeExisting = true;

        /// <inheritdoc/>
        public override void AddTo(MetaDocs docs)
        {
            docs.Extensions.Add(CleanName, this);
        }

        /// <inheritdoc/>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "extend":
                    Extend = value;
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
            Require(docs, Extend, ExtensionName);
            string metaName;
            string metaType = Extend.ToLowerFast().BeforeAndAfter(' ', out metaName);
            MetaObject extended = metaType switch
            {
                "command" => docs.Commands.GetValueOrDefault(metaName, null),
                "mechanism" => docs.Mechanisms.GetValueOrDefault(metaName, null),
                "tag" => docs.Tags.GetValueOrDefault(metaName, null),
                "objecttype" => docs.ObjectTypes.GetValueOrDefault(metaName, null),
                "property" => docs.Properties.GetValueOrDefault(metaName, null),
                "event" => docs.Events.GetValueOrDefault(metaName, null),
                "action" => docs.Actions.GetValueOrDefault(metaName, null),
                "language" => docs.Languages.GetValueOrDefault(metaName, null),
                _ => null
            };
            if (extended is null)
            {
                docs.LoadErrors.Add($"Extension '{ExtensionName}' has invalid target meta to extend '{Extend}'.");
                return;
            }
            foreach ((string key, List<string> values) in RawValues.ExceptBy(["extend", "name", "include_existing"], pair => pair.Key))
            {
                string currentValue = IncludeExisting && extended.RawValues.TryGetValue(key, out List<string> currentValues) ? currentValues.Last() : null;
                foreach (string value in values)
                {
                    string newValue = currentValue is not null ? currentValue + "\n\n" + value : value;
                    if (!extended.ApplyValue(docs, key, newValue))
                    {
                        docs.LoadErrors.Add($"Extension '{ExtensionName}' could not extend {extended.Type.Name.ToLowerFast()} meta '{extended.Name}', key/value pair '{key}' -> '{value}' is invalid.");
                    }
                }
            }
        }
    }
}
