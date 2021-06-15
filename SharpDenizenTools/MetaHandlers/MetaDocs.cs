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
        public static MetaType META_TYPE_COMMAND = new MetaType() { Name = "Command", WebPath = "Commands" };

        /// <summary>The "mechanism" meta type.</summary>
        public static MetaType META_TYPE_MECHANISM = new MetaType() { Name = "Mechanism", WebPath = "Mechanisms" };

        /// <summary>The "event" meta type.</summary>
        public static MetaType META_TYPE_EVENT = new MetaType() { Name = "Event", WebPath = "Events" };

        /// <summary>The "action" meta type.</summary>
        public static MetaType META_TYPE_ACTION = new MetaType() { Name = "Action", WebPath = "Actions" };

        /// <summary>The "language" meta type.</summary>
        public static MetaType META_TYPE_LANGUAGE = new MetaType() { Name = "Language", WebPath = "Languages" };

        /// <summary>The "tag" meta type.</summary>
        public static MetaType META_TYPE_TAG = new MetaType() { Name = "Tag", WebPath = "Tags" };

        /// <summary>The "object type" meta type.</summary>
        public static MetaType META_TYPE_OBJECT = new MetaType() { Name = "ObjectType", WebPath = "ObjectTypes" };

        /// <summary>The "guide page" meta type.</summary>
        public static MetaType META_TYPE_GUIDEPAGE = new MetaType() { Name = "GuidePage", WebPath = null };

        /// <summary>All meta types.</summary>
        public static MetaType[] META_TYPES = new MetaType[] { META_TYPE_COMMAND, META_TYPE_MECHANISM,
            META_TYPE_EVENT, META_TYPE_ACTION, META_TYPE_LANGUAGE, META_TYPE_TAG, META_TYPE_GUIDEPAGE };

        /// <summary>Getters for standard meta object types.</summary>
        public static Dictionary<string, Func<MetaObject>> MetaObjectGetters = new Dictionary<string, Func<MetaObject>>()
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
        public Dictionary<string, MetaCommand> Commands = new Dictionary<string, MetaCommand>(512);

        /// <summary>All known mechanisms.</summary>
        public Dictionary<string, MetaMechanism> Mechanisms = new Dictionary<string, MetaMechanism>(1024);

        /// <summary>All known tags.</summary>
        public Dictionary<string, MetaTag> Tags = new Dictionary<string, MetaTag>(2048);

        /// <summary>All known object types.</summary>
        public Dictionary<string, MetaObjectType> ObjectTypes = new Dictionary<string, MetaObjectType>(512);

        /// <summary>All known events.</summary>
        public Dictionary<string, MetaEvent> Events = new Dictionary<string, MetaEvent>(1024);

        /// <summary>All known actions.</summary>
        public Dictionary<string, MetaAction> Actions = new Dictionary<string, MetaAction>(512);

        /// <summary>All known languages.</summary>
        public Dictionary<string, MetaLanguage> Languages = new Dictionary<string, MetaLanguage>(512);

        /// <summary>All known guide pages.</summary>
        public Dictionary<string, MetaGuidePage> GuidePages = new Dictionary<string, MetaGuidePage>(512);

        /// <summary>A set of all known tag bases.</summary>
        public HashSet<string> TagBases = new HashSet<string>(512) { "permission", "text", "name", "amount" };

        /// <summary>A set of all known tag bits.</summary>
        public HashSet<string> TagParts = new HashSet<string>(2048);

        /// <summary>A mapping of tag bits to deprecation notices.</summary>
        public Dictionary<string, string> TagDeprecations = new Dictionary<string, string>(32);

        /// <summary>The "ObjectTag" meta type.</summary>
        public MetaObjectType ObjectTagType;

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
                string tagBase = cleaned.Substring(0, dotIndex);
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

        /// <summary>
        /// A list of load-time errors, if any.
        /// </summary>
        public List<string> LoadErrors = new List<string>();
    }
}
