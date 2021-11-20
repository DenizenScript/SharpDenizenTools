using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>The "guide page" meta type.</summary>
        public static MetaType META_TYPE_GUIDEPAGE = new() { Name = "GuidePage", WebPath = null };

        /// <summary>All meta types.</summary>
        public static MetaType[] META_TYPES = new MetaType[] { META_TYPE_COMMAND, META_TYPE_MECHANISM,
            META_TYPE_EVENT, META_TYPE_ACTION, META_TYPE_LANGUAGE, META_TYPE_TAG, META_TYPE_GUIDEPAGE };

        /// <summary>Getters for standard meta object types.</summary>
        public static Dictionary<string, Func<MetaObject>> MetaObjectGetters = new()
        {
            { "command", () => new MetaCommand() },
            { "mechanism", () => new MetaMechanism() },
            { "tag", () => new MetaTag() },
            { "objecttype", () => new MetaObjectType() },
            { "event", () => new MetaEvent() },
            { "action", () => new MetaAction() },
            { "language", () => new MetaLanguage() }
        };

        /// <summary>All known commands.</summary>
        public Dictionary<string, MetaCommand> Commands = new(512);

        /// <summary>All known mechanisms.</summary>
        public Dictionary<string, MetaMechanism> Mechanisms = new(1024);

        /// <summary>All known tags.</summary>
        public Dictionary<string, MetaTag> Tags = new(2048);

        /// <summary>All known object types.</summary>
        public Dictionary<string, MetaObjectType> ObjectTypes = new(512);

        /// <summary>All known events.</summary>
        public Dictionary<string, MetaEvent> Events = new(1024);

        /// <summary>All known actions.</summary>
        public Dictionary<string, MetaAction> Actions = new(512);

        /// <summary>All known languages.</summary>
        public Dictionary<string, MetaLanguage> Languages = new(512);

        /// <summary>All known guide pages.</summary>
        public Dictionary<string, MetaGuidePage> GuidePages = new(512);

        /// <summary>A set of all known tag bases.</summary>
        public HashSet<string> TagBases = new(512) { "context", "entry" };

        /// <summary>A set of all known tag bits.</summary>
        public HashSet<string> TagParts = new(2048);

        /// <summary>A mapping of tag bits to deprecation notices.</summary>
        public Dictionary<string, string> TagDeprecations = new(32);

        /// <summary>Core object types.</summary>
        public MetaObjectType ObjectTagType, ElementTagType;

        /// <summary>Special mapping of first-word to contents for event lookup optimization.</summary>
        public Dictionary<string, List<MetaEvent>> EventLookupOpti = new();

        /// <summary>Events that can't fit into <see cref="EventLookupOpti"/>.</summary>
        public List<MetaEvent> LegacyCouldMatchEvents = new();

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
                return new List<(MetaEvent, int)>() { (evt, 10) };
            }
            List<(MetaEvent, int)> result = new();
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
            foreach (MetaCommand command in Commands.Values)
            {
                yield return command;
            }
            foreach (MetaMechanism mechanism in Mechanisms.Values)
            {
                yield return mechanism;
            }
            foreach (MetaTag tag in Tags.Values)
            {
                yield return tag;
            }
            foreach (MetaObjectType objType in ObjectTypes.Values)
            {
                yield return objType;
            }
            foreach (MetaEvent evt in Events.Values)
            {
                yield return evt;
            }
            foreach (MetaAction action in Actions.Values)
            {
                yield return action;
            }
            foreach (MetaLanguage language in Languages.Values)
            {
                yield return language;
            }
            foreach (MetaGuidePage guidePage in GuidePages.Values)
            {
                yield return guidePage;
            }
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
        public List<string> LoadErrors = new();
    }
}
