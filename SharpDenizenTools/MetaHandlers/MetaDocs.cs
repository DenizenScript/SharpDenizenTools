using System.Collections.Generic;
using System.Linq;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaObjects;

namespace SharpDenizenTools.MetaHandlers
{
    /// <summary>Helper class to contain the full set of meta documentation.</summary>
    public class MetaDocs
    {
        /// <summary>The current meta documentation (the instance will change if meta is reloaded).</summary>
        public static MetaDocs CurrentMeta = null;

        /// <summary>The "command" meta type.</summary>
        public static MetaType META_TYPE_COMMAND = new() { Name = "Command", WebPath = "Commands" };

        /// <summary>The "mechanism" meta type.</summary>
        public static MetaType META_TYPE_MECHANISM = new() { Name = "Mechanism", WebPath = "Mechanisms" };

        /// <summary>The "event" meta type.</summary>
        public static MetaType META_TYPE_EVENT = new() { Name = "Event", WebPath = "Events" };

        /// <summary>The "action" meta type.</summary>
        public static MetaType META_TYPE_ACTION = new() { Name = "Action", WebPath = "Actions" };

        /// <summary>The "language" meta type.</summary>
        public static MetaType META_TYPE_LANGUAGE = new() { Name = "Language", WebPath = "Languages" };

        /// <summary>The "tag" meta type.</summary>
        public static MetaType META_TYPE_TAG = new() { Name = "Tag", WebPath = "Tags" };

        /// <summary>The "object type" meta type.</summary>
        public static MetaType META_TYPE_OBJECT = new() { Name = "ObjectType", WebPath = "ObjectTypes" };

        /// <summary>The "property" meta type.</summary>
        public static MetaType META_TYPE_PROPERTY = new() { Name = "Property", WebPath = "Properties" };

        /// <summary>The "guide page" meta type.</summary>
        public static MetaType META_TYPE_GUIDEPAGE = new() { Name = "GuidePage", WebPath = null };

        /// <summary>The "extension" meta type.</summary>
        public static MetaType META_TYPE_EXTENSION = new() { Name = "Extension", WebPath = null };

        /// <summary>Data for all meta types, by name.</summary>
        public Dictionary<string, IMetaTypeData> MetaTypesData = [];

        /// <summary>All known commands.</summary>
        public Dictionary<string, MetaCommand> Commands = new(512);

        /// <summary>All known mechanisms.</summary>
        public Dictionary<string, MetaMechanism> Mechanisms = new(1024);

        /// <summary>All known tags.</summary>
        public Dictionary<string, MetaTag> Tags = new(2048);

        /// <summary>All known object types.</summary>
        public Dictionary<string, MetaObjectType> ObjectTypes = new(512);

        /// <summary>All known properties.</summary>
        public Dictionary<string, MetaProperty> Properties = new(512);

        /// <summary>All known events.</summary>
        public Dictionary<string, MetaEvent> Events = new(1024);

        /// <summary>All known actions.</summary>
        public Dictionary<string, MetaAction> Actions = new(512);

        /// <summary>All known languages.</summary>
        public Dictionary<string, MetaLanguage> Languages = new(512);

        /// <summary>All known guide pages.</summary>
        public Dictionary<string, MetaGuidePage> GuidePages = new(512);

        /// <summary>All known meta extensions.</summary>
        public Dictionary<string, MetaExtension> Extensions = new(256);

        /// <summary>A set of all known tag bases.</summary>
        public HashSet<string> TagBases = new(512) { "context", "entry" };

        /// <summary>A set of all known tag bits.</summary>
        public HashSet<string> TagParts = new(2048);

        /// <summary>A mapping of tag bits to deprecation notices.</summary>
        public Dictionary<string, string> TagDeprecations = new(32);

        /// <summary>Core object types.</summary>
        public MetaObjectType ObjectTagType, ElementTagType;

        /// <summary>Special mapping of first-word to contents for event lookup optimization.</summary>
        public Dictionary<string, List<MetaEvent>> EventLookupOpti = [];

        /// <summary>Events that can't fit into <see cref="EventLookupOpti"/>.</summary>
        public List<MetaEvent> LegacyCouldMatchEvents = [];

        /// <summary>Internal data value sets.</summary>
        public Dictionary<string, HashSet<string>> DataValueSets = new(32);

        /// <summary>Set of raw adjustable keys.</summary>
        public HashSet<string> RawAdjustables = [];

        /// <summary>Creates a new instance of <see cref="MetaDocs"/> and registers it's <see cref="MetaTypeData{T}"/>.</summary>
        public MetaDocs()
        {
            // Extensions explicitly first
            IMetaTypeData.Register(this, META_TYPE_EXTENSION, Extensions, () => new MetaExtension());
            IMetaTypeData.Register(this, META_TYPE_COMMAND, Commands, () => new MetaCommand());
            IMetaTypeData.Register(this, META_TYPE_MECHANISM, Mechanisms, () => new MetaMechanism());
            IMetaTypeData.Register(this, META_TYPE_TAG, Tags, () => new MetaTag());
            IMetaTypeData.Register(this, META_TYPE_OBJECT, ObjectTypes, () => new MetaObjectType());
            IMetaTypeData.Register(this, META_TYPE_PROPERTY, Properties, () => new MetaProperty());
            IMetaTypeData.Register(this, META_TYPE_EVENT, Events, () => new MetaEvent());
            IMetaTypeData.Register(this, META_TYPE_ACTION, Actions, () => new MetaAction());
            IMetaTypeData.Register(this, META_TYPE_LANGUAGE, Languages, () => new MetaLanguage());
            IMetaTypeData.Register(this, META_TYPE_GUIDEPAGE, GuidePages, () => new MetaGuidePage());
            IMetaTypeData.Register(this, "data", null, () => new MetaDataValue());
        }

        /// <summary>Returns whether the given text value is in the named data set.</summary>
        public bool IsInDataValueSet(string set, string text)
        {
            return DataValueSets.TryGetValue(set, out HashSet<string> values) && values.Contains(text);
        }

        /// <summary>Associated ExtraData instance, if any.</summary>
        public ExtraData Data;

        /// <summary>Returns the event that best matches the input text.</summary>
        /// <param name="text">The text to match events against.</param>
        /// <param name="allowPartial">If false: full event must match. If true: can just be first few words.</param>
        /// <param name="precise">If true: object matchers must be valid. If false: object matchers must look vaguely close to correct.</param>
        public List<MetaEvent> FindEventsFor(string text, bool allowPartial, bool precise)
        {
            List<(MetaEvent, int)> matches = GetEventMatchesFor(text, allowPartial, precise);
            if (matches is null)
            {
                return null;
            }
            return matches.Select(p => p.Item1).ToList();
        }

        /// <summary>Returns event match details for the given input text, as pair of event and match quality.</summary>
        /// <param name="text">The text to match events against.</param>
        /// <param name="allowPartial">If false: full event must match. If true: can just be first few words.</param>
        /// <param name="precise">If true: object matchers must be valid. If false: object matchers must look vaguely close to correct.</param>
        public List<(MetaEvent, int)> GetEventMatchesFor(string text, bool allowPartial, bool precise)
        {
            text = text.ToLowerFast();
            if (text.StartsWith("on "))
            {
                text = text["on ".Length..];
            }
            else if (text.StartsWith("after "))
            {
                text = text["after ".Length..];
            }
            if (Events.TryGetValue(text, out MetaEvent evt))
            {
                return [(evt, 10)];
            }
            List<(MetaEvent, int)> result = [];
            string[] parts = text.SplitFast(' ');
            if (EventLookupOpti.TryGetValue(parts[0], out List<MetaEvent> possible))
            {
                foreach (MetaEvent evt2 in possible)
                {
                    int max = evt2.CouldMatchers.Select(c => c.TryMatch(parts, allowPartial, precise)).Max();
                    if (max > 0)
                    {
                        result.Add((evt2, max));
                        if (!allowPartial)
                        {
                            return result;
                        }
                    }
                }
            }
            foreach (MetaEvent evt2 in LegacyCouldMatchEvents)
            {
                int max = evt2.CouldMatchers.Select(c => c.TryMatch(parts, allowPartial, precise)).Max();
                if (max > 0)
                {
                    result.Add((evt2, max));
                    if (!allowPartial)
                    {
                        return result;
                    }
                }
            }
            return result.IsEmpty() ? null : result;
        }

        /// <summary>Returns an enumerable of all objects in the meta documentation.</summary>
        public IEnumerable<MetaObject> AllMetaObjects()
        {
            return MetaTypesData.SelectMany(type => type.Value.Meta?.Values ?? Enumerable.Empty<MetaObject>());
        }

        /// <summary>
        /// Finds the exact tag for the input text.
        /// Does not perform partial matching or chain parsing.
        /// </summary>
        /// <param name="tagText">The input text to search for.</param>
        /// <returns>The matching tag, or null if not found.</returns>
        public MetaTag FindTag(string tagText)
        {
            string cleaned = MetaTag.CleanTag(tagText).ToLowerFast();
            if (Tags.TryGetValue(cleaned, out MetaTag result))
            {
                return result;
            }
            // TODO: Chain searching
            int dotIndex = cleaned.IndexOf('.');
            if (dotIndex > 0)
            {
                string tagBase = cleaned[..dotIndex];
                string secondarySearch;
                if (tagBase == "playertag" || tagBase == "npctag")
                {
                    // TODO: Object meta, to inform of down-typing like this?
                    secondarySearch = "entitytag" + cleaned[dotIndex..];
                }
                else if (!tagBase.EndsWith("tag"))
                {
                    secondarySearch = tagBase + "tag" + cleaned[dotIndex..];
                }
                else
                {
                    return null;
                }
                return FindTag(secondarySearch);
            }
            return null;
        }

        /// <summary>A list of load-time errors, if any.</summary>
        public List<string> LoadErrors = [];
    }
}
