using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpDenizenTools.MetaHandlers
{
    /// <summary>Helper class for logic to load meta documentation in from source.</summary>
    public static class MetaDocsLoader
    {
        /// <summary>Primary Denizen official sources.</summary>
        public static readonly string[] DENIZEN_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/Denizen/archive/dev.zip",
            "https://github.com/DenizenScript/Denizen-Core/archive/master.zip"
        };

        /// <summary>Denizen secondary addon sources.</summary>
        public static readonly string[] DENIZEN_ADDON_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/Depenizen/archive/master.zip",
            "https://github.com/DenizenScript/dDiscordBot/archive/master.zip",
            "https://github.com/DenizenScript/Webizen/archive/master.zip"
        };

        /// <summary>The actual source array to use, by default built from <see cref="DENIZEN_SOURCES"/> and <see cref="DENIZEN_ADDON_SOURCES"/>.</summary>
        public static string[] SourcesToUse = DENIZEN_SOURCES.JoinWith(DENIZEN_ADDON_SOURCES);

        /// <summary>Optional alternative source for zips (for things like caching).</summary>
        public static Func<string, byte[]> AlternateZipSourcer = null;

        /// <summary>Source link for the Denizen beginner's guide.</summary>
        public static string DENIZEN_GUIDE_SOURCE = "https://guide.denizenscript.com/";

        /// <summary>Download all docs.</summary>
        public static MetaDocs DownloadAll()
        {
            HttpClient webClient = new HttpClient
            {
                Timeout = new TimeSpan(0, 2, 0)
            };
            webClient.DefaultRequestHeaders.UserAgent.ParseAdd("DenizenMetaScanner/1.0");
            MetaDocs docs = new MetaDocs();
            ConcurrentDictionary<string, ZipArchive> zips = new ConcurrentDictionary<string, ZipArchive>();
            List<ManualResetEvent> resets = new List<ManualResetEvent>();
            foreach (string src in SourcesToUse)
            {
                ManualResetEvent evt = new ManualResetEvent(false);
                resets.Add(evt);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        ZipArchive zip = DownloadZip(webClient, src);
                        zips[src] = zip;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Zip download exception {ex}");
                        docs.LoadErrors.Add($"Zip download error: {ex.GetType().Name}: {ex.Message}");
                    }
                    evt.Set();
                });
            }
            foreach (ManualResetEvent evt in resets)
            {
                evt.WaitOne();
            }
            foreach (string src in SourcesToUse)
            {
                try
                {
                    (int, string, string)[] fullLines = ReadLines(zips[src]);
                    LoadDataFromLines(docs, src, fullLines);
                }
                catch (Exception ex)
                {
                    docs.LoadErrors.Add($"Internal exception - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error: {ex}");
                }
            }
            try
            {
                ReadGuides(docs);
            }
            catch (Exception ex)
            {
                docs.LoadErrors.Add($"Internal exception - {ex.GetType().FullName} ... see bot console for details.");
                Console.Error.WriteLine($"Error: {ex}");
            }
            foreach (MetaObject obj in docs.AllMetaObjects())
            {
                try
                {
                    obj.PostCheck(docs);
                    obj.Searchable = obj.GetAllSearchableText().ToLowerFast();
                }
                catch (Exception ex)
                {
                    docs.LoadErrors.Add($"Internal exception while checking {obj.Type.Name} '{obj.Name}' - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error with {obj.Type.Name} '{obj.Name}': {ex}");
                }
            }
            foreach (string str in docs.LoadErrors)
            {
                Console.Error.WriteLine($"Load error: {str}");
            }
            return docs;
        }

        /// <summary>
        /// Downloads guide source info.
        /// </summary>
        public static void ReadGuides(MetaDocs docs)
        {
            HttpClient client = new HttpClient
            {
                Timeout = new TimeSpan(0, 2, 0)
            };
            string page = client.GetStringAsync(DENIZEN_GUIDE_SOURCE).Result;
            int contentIndex = page.IndexOf("<div class=\"section\" id=\"contents\">");
            if (contentIndex == -1)
            {
                docs.LoadErrors.Add("Guide page did not match expected format (table of contents div missing).");
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
                guidePage.AddTo(docs);
                linkIndex = linkEndIndex;
            }
        }

        /// <summary>
        /// Download a zip file from a URL.
        /// </summary>
        public static ZipArchive DownloadZip(HttpClient webClient, string url)
        {
            byte[] zipDataBytes;
            if (AlternateZipSourcer != null)
            {
                zipDataBytes = AlternateZipSourcer(url);
            }
            else
            {
                zipDataBytes = webClient.GetByteArrayAsync(url).Result;
            }
            MemoryStream zipDataStream = new MemoryStream(zipDataBytes);
            return new ZipArchive(zipDataStream);
        }

        /// <summary>
        /// Read lines of meta docs from Java files in a zip.
        /// </summary>
        public static (int, string, string)[] ReadLines(ZipArchive zip, string folderLimit = null)
        {
            List<(int, string, string)> lines = new List<(int, string, string)>();
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
                int lineNum = 0;
                foreach (string line in entryStream.AllLinesOfText())
                {
                    lineNum++;
                    if (line.TrimStart().StartsWith("// "))
                    {
                        lines.Add((lineNum, entry.FullName, line.Trim()["// ".Length..].Replace("\r", "")));
                    }
                }
            }
            return lines.ToArray();
        }

        /// <summary>
        /// Load the meta doc data from lines.
        /// </summary>
        public static void LoadDataFromLines(MetaDocs docs, string websrc, (int, string, string)[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                (int lineNum, string file, string line) = lines[i];
                if (line.StartsWith("<--[") && line.EndsWith("]"))
                {
                    string objectType = line.Substring("<--[".Length, line.Length - "<--[]".Length);
                    List<string> objectData = new List<string>();
                    for (i++; i < lines.Length; i++)
                    {
                        if (lines[i].Item3 == "-->")
                        {
                            break;
                        }
                        else if (lines[i].Item3.StartsWith("<--["))
                        {
                            docs.LoadErrors.Add($"While processing {file} at line {i + 1} found the start of a meta block, while still processing the previous meta block.");
                            break;
                        }
                        else if (lines[i].Item2 != file)
                        {
                            docs.LoadErrors.Add($"While processing {file} was not able to find the end of an object's documentation!");
                            objectData = null;
                            break;
                        }
                        objectData.Add(lines[i].Item3);
                    }
                    if (objectData == null)
                    {
                        continue;
                    }
                    objectData.Add("@end_meta");
                    LoadInObject(docs, objectType, GetCorrectURL(websrc, file, lineNum), objectData.ToArray());
                }
                else if (line.StartsWith("<--"))
                {
                    docs.LoadErrors.Add($"While processing {file} at line {i + 1} found the '<--' meta starter, but not a valid meta start.");
                }
            }
        }

        /// <summary>
        /// Gets a clean proper URL for a file path, if possible.
        /// </summary>
        public static string GetCorrectURL(string webSource, string file, int line)
        {
            if (webSource.StartsWith("https://github"))
            {
                return webSource[..^(".zip".Length)].Replace("/archive/", "/blob/") + "/" + file.After('/') + "#L" + line;
            }
            return $"Web source {webSource} file {file} line {line}";
        }

        /// <summary>
        /// Load an object into the meta docs from the object's text definition.
        /// </summary>
        public static void LoadInObject(MetaDocs docs, string objectType, string file, string[] objectData)
        {
            try
            {
                if (!MetaDocs.MetaObjectGetters.TryGetValue(objectType.ToLowerFast(), out Func<MetaObject> getter))
                {
                    docs.LoadErrors.Add($"While processing {file} found unknown meta type '{objectType}'.");
                    return;
                }
                MetaObject obj = getter();
                obj.SourceFile = file;
                obj.Meta = docs;
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
                                docs.LoadErrors.Add($"While processing {file} in object type '{objectType}' for '{obj.Name}' could not apply key '{curKey}' with value '{curValue}'.");
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
                obj.AddTo(docs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in file {file} for object type {objectType}: {ex}");
                docs.LoadErrors.Add($"Error in file {file} for object type {objectType}: {ex.GetType().Name}: {ex.Message} ... see console for details.");
            }
        }
    }
}
