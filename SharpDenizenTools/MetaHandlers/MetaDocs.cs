﻿using System;
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
    /// <summary>
    /// Helper class to contain the full set of meta documentation, and the logic to load it in.
    /// </summary>
    public class MetaDocs
    {
        /// <summary>
        /// The current meta documentation (the instance will change if meta is reloaded).
        /// </summary>
        public static MetaDocs CurrentMeta = null;

        /// <summary>
        /// Primary Denizen official sources.
        /// </summary>
        public static readonly string[] DENIZEN_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/Denizen/archive/dev.zip",
            "https://github.com/DenizenScript/Denizen-Core/archive/master.zip"
        };

        /// <summary>
        /// Denizen secondary addon sources.
        /// </summary>
        public static readonly string[] DENIZEN_ADDON_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/Depenizen/archive/master.zip",
            "https://github.com/DenizenScript/dDiscordBot/archive/master.zip",
            "https://github.com/DenizenScript/Webizen/archive/master.zip",
            "https://github.com/DenizenScript/dIRCBot/archive/master.zip"
        };

        /// <summary>
        /// The actual source array to use, by default built from <see cref="DENIZEN_SOURCES"/> and <see cref="DENIZEN_ADDON_SOURCES"/>.
        /// </summary>
        public static string[] SourcesToUse = DENIZEN_SOURCES.JoinWith(DENIZEN_ADDON_SOURCES);

        /// <summary>
        /// Optional alternative source for zips (for things like caching).
        /// </summary>
        public static Func<string, byte[]> AlternateZipSourcer = null;

        /// <summary>
        /// Source link for the Denizen beginner's guide.
        /// </summary>
        public static string DENIZEN_GUIDE_SOURCE = "https://guide.denizenscript.com/";

        /// <summary>
        /// The "command" meta type.
        /// </summary>
        public static MetaType META_TYPE_COMMAND = new MetaType() { Name = "Command", WebPath = "cmds" };

        /// <summary>
        /// The "mechanism" meta type.
        /// </summary>
        public static MetaType META_TYPE_MECHANISM = new MetaType() { Name = "Mechanism", WebPath = "mecs" };

        /// <summary>
        /// The "event" meta type.
        /// </summary>
        public static MetaType META_TYPE_EVENT = new MetaType() { Name = "Event", WebPath = "evts" };

        /// <summary>
        /// The "action" meta type.
        /// </summary>
        public static MetaType META_TYPE_ACTION = new MetaType() { Name = "Action", WebPath = "acts" };

        /// <summary>
        /// The "language" meta type.
        /// </summary>
        public static MetaType META_TYPE_LANGUAGE = new MetaType() { Name = "Language", WebPath = "lngs" };

        /// <summary>
        /// The "tag" meta type.
        /// </summary>
        public static MetaType META_TYPE_TAG = new MetaType() { Name = "Tag", WebPath = "tags" };

        /// <summary>
        /// The "guide page" meta type.
        /// </summary>
        public static MetaType META_TYPE_GUIDEPAGE = new MetaType() { Name = "GuidePage", WebPath = null };

        /// <summary>
        /// All meta types.
        /// </summary>
        public static MetaType[] META_TYPES = new MetaType[] { META_TYPE_COMMAND, META_TYPE_MECHANISM,
            META_TYPE_EVENT, META_TYPE_ACTION, META_TYPE_LANGUAGE, META_TYPE_TAG, META_TYPE_GUIDEPAGE };

        /// <summary>
        /// Getters for standard meta object types.
        /// </summary>
        public static Dictionary<string, Func<MetaObject>> MetaObjectGetters = new Dictionary<string, Func<MetaObject>>()
        {
            { "command", () => new MetaCommand() },
            { "mechanism", () => new MetaMechanism() },
            { "tag", () => new MetaTag() },
            { "event", () => new MetaEvent() },
            { "action", () => new MetaAction() },
            { "language", () => new MetaLanguage() },
            { "GuidePage", () => new MetaGuidePage() }
        };

        /// <summary>
        /// All known commands.
        /// </summary>
        public Dictionary<string, MetaCommand> Commands = new Dictionary<string, MetaCommand>(512);

        /// <summary>
        /// All known mechanisms.
        /// </summary>
        public Dictionary<string, MetaMechanism> Mechanisms = new Dictionary<string, MetaMechanism>(1024);

        /// <summary>
        /// All known tags.
        /// </summary>
        public Dictionary<string, MetaTag> Tags = new Dictionary<string, MetaTag>(2048);

        /// <summary>
        /// All known events.
        /// </summary>
        public Dictionary<string, MetaEvent> Events = new Dictionary<string, MetaEvent>(1024);

        /// <summary>
        /// All known actions.
        /// </summary>
        public Dictionary<string, MetaAction> Actions = new Dictionary<string, MetaAction>(512);

        /// <summary>
        /// All known languages.
        /// </summary>
        public Dictionary<string, MetaLanguage> Languages = new Dictionary<string, MetaLanguage>(512);

        /// <summary>
        /// All known guide pages.
        /// </summary>
        public Dictionary<string, MetaGuidePage> GuidePages = new Dictionary<string, MetaGuidePage>(512);

        /// <summary>
        /// A set of all known tag bases.
        /// </summary>
        public HashSet<string> TagBases = new HashSet<string>(512) { "permission", "text", "name", "amount" };

        /// <summary>
        /// A set of all known tag bits.
        /// </summary>
        public HashSet<string> TagParts = new HashSet<string>(2048);

        /// <summary>
        /// A mapping of tag bits to deprecation notices.
        /// </summary>
        public Dictionary<string, string> TagDeprecations = new Dictionary<string, string>(32);

        /// <summary>
        /// Returns an enumerable of all objects in the meta documentation.
        /// </summary>
        /// <returns>All objects.</returns>
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
            string cleaned = MetaTag.CleanTag(tagText);
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
        /// Download all docs.
        /// </summary>
        public void DownloadAll()
        {
            ConcurrentBag<ZipArchive> zips = new ConcurrentBag<ZipArchive>();
            List<ManualResetEvent> resets = new List<ManualResetEvent>();
            foreach (string src in SourcesToUse)
            {
                ManualResetEvent evt = new ManualResetEvent(false);
                resets.Add(evt);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        ZipArchive zip = DownloadZip(src);
                        zips.Add(zip);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Zip download exception {ex}");
                        LoadErrors.Add($"Zip download error: {ex.GetType().Name}: {ex.Message}");
                    }
                    evt.Set();
                });
            }
            foreach (ManualResetEvent evt in resets)
            {
                evt.WaitOne();
            }
            foreach (ZipArchive zip in zips)
            {
                try
                {
                    string[] fullLines = ReadLines(zip);
                    LoadDataFromLines(fullLines);
                }
                catch (Exception ex)
                {
                    LoadErrors.Add($"Internal exception - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error: {ex}");
                }
            }
            try
            {
                ReadGuides();
            }
            catch (Exception ex)
            {
                LoadErrors.Add($"Internal exception - {ex.GetType().FullName} ... see bot console for details.");
                Console.Error.WriteLine($"Error: {ex}");
            }
            foreach (MetaObject obj in AllMetaObjects())
            {
                try
                {
                    obj.PostCheck(this);
                    obj.Searchable = obj.GetAllSearchableText().ToLowerFast();
                }
                catch (Exception ex)
                {
                    LoadErrors.Add($"Internal exception while checking {obj.Type.Name} '{obj.Name}' - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error with {obj.Type.Name} '{obj.Name}': {ex}");
                }
            }
            foreach (string str in LoadErrors)
            {
                Console.Error.WriteLine($"Load error: {str}");
            }
        }

        /// <summary>
        /// Downloads guide source info.
        /// </summary>
        public void ReadGuides()
        {
            HttpClient client = new HttpClient
            {
                Timeout = new TimeSpan(0, 2, 0)
            };
            string page = client.GetStringAsync(DENIZEN_GUIDE_SOURCE).Result;
            int contentIndex = page.IndexOf("<div class=\"section\" id=\"contents\">");
            if (contentIndex == -1)
            {
                LoadErrors.Add("Guide page did not match expected format (table of contents div missing).");
                return;
            }
            page = page[contentIndex..];
            int linkIndex = 0;
            const string link_reference_text = "<a class=\"reference internal\" href=\"";
            while ((linkIndex = page.IndexOf(link_reference_text, linkIndex)) >= 0)
            {
                int linkEndIndex = page.IndexOf("</a>", linkIndex);
                string linkBody = DENIZEN_GUIDE_SOURCE + page[(linkIndex + link_reference_text.Length)..linkEndIndex];
                MetaGuidePage guidePage = new MetaGuidePage();
                guidePage.URL = linkBody.BeforeAndAfter("\">", out guidePage.PageName);
                guidePage.AddTo(this);
                linkIndex = linkEndIndex;
            }
        }

        /// <summary>
        /// Download a zip file from a URL.
        /// </summary>
        public static ZipArchive DownloadZip(string url)
        {
            byte[] zipDataBytes;
            if (AlternateZipSourcer != null)
            {
                zipDataBytes = AlternateZipSourcer(url);
            }
            else
            {
                HttpClient client = new HttpClient
                {
                    Timeout = new TimeSpan(0, 2, 0)
                };
                zipDataBytes = client.GetByteArrayAsync(url).Result;
            }
            MemoryStream zipDataStream = new MemoryStream(zipDataBytes);
            return new ZipArchive(zipDataStream);
        }

        /// <summary>
        /// End of file marker.
        /// </summary>
        public const string END_OF_FILE_MARK = "\0END_OF_FILE";

        /// <summary>
        /// Start of file marker prefix.
        /// </summary>
        public const string START_OF_FILE_PREFIX = "\0START_OF_FILE ";

        /// <summary>
        /// Read lines of meta docs from Java files in a zip.
        /// </summary>
        public static string[] ReadLines(ZipArchive zip, string folderLimit = null)
        {
            List<string> lines = new List<string>();
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (folderLimit != null && !entry.FullName.StartsWith(folderLimit))
                {
                    continue;
                }
                if (!entry.FullName.EndsWith(".java"))
                {
                    continue;
                }
                using Stream entryStream = entry.Open();
                lines.Add(START_OF_FILE_PREFIX + entry.FullName);
                lines.AddRange(entryStream.AllLinesOfText().Where((s) => s.TrimStart().StartsWith("// ")).Select((s) => s.Trim()["// ".Length..].Replace("\r", "")));
                lines.Add(END_OF_FILE_MARK);
            }
            return lines.ToArray();
        }

        /// <summary>
        /// Load the meta doc data from lines.
        /// </summary>
        public void LoadDataFromLines(string[] lines)
        {
            string file = "<unknown>";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(START_OF_FILE_PREFIX))
                {
                    file = line[START_OF_FILE_PREFIX.Length..];
                }
                else if (line.StartsWith("<--[") && line.EndsWith("]"))
                {
                    string objectType = line.Substring("<--[".Length, line.Length - "<--[]".Length);
                    List<string> objectData = new List<string>();
                    for (i++; i < lines.Length; i++)
                    {
                        if (lines[i] == "-->")
                        {
                            break;
                        }
                        else if (lines[i].StartsWith("<--["))
                        {
                            LoadErrors.Add($"While processing {file} at line {i + 1} found the start of a meta block, while still processing the previous meta block.");
                            break;
                        }
                        else if (lines[i] == END_OF_FILE_MARK || lines[i].StartsWith(START_OF_FILE_PREFIX))
                        {
                            LoadErrors.Add($"While processing {file} was not able to find the end of an object's documentation!");
                            objectData = null;
                            break;
                        }
                        objectData.Add(lines[i]);
                    }
                    if (objectData == null)
                    {
                        continue;
                    }
                    objectData.Add("@end_meta");
                    LoadInObject(objectType, file, objectData.ToArray());
                }
                else if (line.StartsWith("<--"))
                {
                    LoadErrors.Add($"While processing {file} at line {i + 1} found the '<--' meta starter, but not a valid meta start.");
                }
            }
        }

        /// <summary>
        /// Load an object into the meta docs from the object's text definition.
        /// </summary>
        public void LoadInObject(string objectType, string file, string[] objectData)
        {
            try
            {
                if (!MetaObjectGetters.TryGetValue(objectType.ToLowerFast(), out Func<MetaObject> getter))
                {
                    LoadErrors.Add($"While processing {file} found unknown meta type '{objectType}'.");
                    return;
                }
                MetaObject obj = getter();
                string curKey = null;
                string curValue = null;
                foreach (string line in objectData)
                {
                    if (line.StartsWith("@"))
                    {
                        if (curKey != null && curValue != null)
                        {
                            if (!obj.ApplyValue(curKey.ToLowerFast(), curValue.Trim(' ', '\t', '\n')))
                            {
                                LoadErrors.Add($"While processing {file} in object type '{objectType}' for '{obj.Name}' could not apply key '{curKey}' with value '{curValue}'.");
                            }
                            curKey = null;
                            curValue = null;
                        }
                        int space = line.IndexOf(' ');
                        if (space == -1)
                        {
                            curKey = line[1..];
                            if (curKey == "end_meta")
                            {
                                break;
                            }
                            continue;
                        }
                        curKey = line[1..space];
                        curValue = line[(space + 1)..];
                    }
                    else
                    {
                        curValue += "\n" + line;
                    }
                }
                obj.AddTo(this);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in file {file} for object type {objectType}: {ex}");
                LoadErrors.Add($"Error in file {file} for object type {objectType}: {ex.GetType().Name}: {ex.Message} ... see console for details.");
            }
        }

        /// <summary>
        /// A list of load-time errors, if any.
        /// </summary>
        public List<string> LoadErrors = new List<string>();
    }
}
