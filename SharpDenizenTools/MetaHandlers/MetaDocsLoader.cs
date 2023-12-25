using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
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
        public static readonly string[] DENIZEN_SOURCES =
        [
            "https://github.com/DenizenScript/Denizen/archive/dev.zip",
            "https://github.com/DenizenScript/Denizen-Core/archive/master.zip"
        ];

        /// <summary>Denizen secondary addon sources.</summary>
        public static readonly string[] DENIZEN_ADDON_SOURCES =
        [
            "https://github.com/DenizenScript/Depenizen/archive/master.zip",
            "https://github.com/DenizenScript/dDiscordBot/archive/master.zip"
        ];

        /// <summary>The actual source array to use, by default built from <see cref="DENIZEN_SOURCES"/> and <see cref="DENIZEN_ADDON_SOURCES"/>.</summary>
        public static string[] SourcesToUse = DENIZEN_SOURCES.JoinWith(DENIZEN_ADDON_SOURCES);

        /// <summary>Optional alternative source for zips (for things like caching).</summary>
        public static Func<string, HttpClient, byte[]> AlternateZipSourcer = null;

        /// <summary>Source link for the Denizen beginner's guide.</summary>
        public static string DENIZEN_GUIDE_SOURCE = "https://guide.denizenscript.com/";

        /// <summary>Guide sub-pages to scan headers from.</summary>
        public static string[] DENIZEN_GUIDE_SUBPAGES = ["guides/troubleshooting/common-mistakes.html"];

        /// <summary>Whether links to the guides need to be loaded. Defaults to false.</summary>
        public static bool LoadGuideData = false;

        /// <summary>Download all docs.</summary>
        public static MetaDocs DownloadAll()
        {
            return DownloadAll(SourcesToUse);
        }

        /// <summary>Download all docs.</summary>
        public static MetaDocs DownloadAll(string[] sources)
        {
            using HttpClient webClient = new()
            {
                Timeout = new TimeSpan(0, 2, 0)
            };
            webClient.DefaultRequestHeaders.UserAgent.ParseAdd("DenizenMetaScanner/1.0");
            MetaDocs docs = new();
            try
            {
                docs.Data = ExtraData.Load();
            }
            catch (Exception ex)
            {
                docs.LoadErrors.Add($"Internal exception while reading extra data - {ex.GetType().FullName} ... see bot console for details.");
                Console.Error.WriteLine($"Error: {ex}");
            }
            ConcurrentDictionary<string, object> files = new();
            List<ManualResetEvent> resets = [];
            foreach (string src in sources)
            {
                ManualResetEvent evt = new(false);
                resets.Add(evt);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        byte[] data = DownloadData(webClient, src);
                        // Note: backup check based on PKWARE zip header (PK followed by 2 control bytes, usually 0x03 0x04)
                        if (src.EndsWith(".zip") || (data[0] == 0x50 && data[1] == 0x4b && data[1] < 0x20 && data[2] < 0x20))
                        {
                            files[src] = new ZipArchive(new MemoryStream(data));
                        }
                        else
                        {
                            files[src] = StringConversionHelper.UTF8Encoding.GetString(data, 0, data.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Source download exception {ex}");
                        docs.LoadErrors.Add($"Source download error: {ex.GetType().Name}: {ex.Message}");
                    }
                    evt.Set();
                });
            }
            foreach (ManualResetEvent evt in resets)
            {
                evt.WaitOne();
            }
            foreach ((string src, object file) in files)
            {
                try
                {
                    (int, string, string)[] fullLines;
                    if (file is ZipArchive zip)
                    {
                        fullLines = ReadLines(zip);
                    }
                    else
                    {
                        List<(int, string, string)> lines = [];
                        SeparateDataLines(lines, src, (file as string).Split('\n'));
                        fullLines = [.. lines];
                    }
                    LoadDataFromLines(docs, src, fullLines);
                }
                catch (Exception ex)
                {
                    docs.LoadErrors.Add($"Internal exception while reading meta - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error: {ex}");
                }
            }
            try
            {
                if (LoadGuideData)
                {
                    ReadGuides(docs, webClient);
                }
            }
            catch (Exception ex)
            {
                docs.LoadErrors.Add($"Internal exception while loading guides - {ex.GetType().FullName} ... see bot console for details.");
                Console.Error.WriteLine($"Error: {ex}");
            }
            PostDownloadVerify(docs);
            foreach (string str in docs.LoadErrors)
            {
                Console.Error.WriteLine($"Load error: {str}");
            }
            return docs;
        }

        /// <summary>Verify all objects after <see cref="DownloadAll()"/>. Automatically called.</summary>
        public static void PostDownloadVerify(MetaDocs docs)
        {
            foreach (MetaObject obj in docs.AllMetaObjects())
            {
                try
                {
                    obj.PostCheck(docs);
                }
                catch (Exception ex)
                {
                    docs.LoadErrors.Add($"Internal exception while checking {obj.Type.Name} '{obj.Name}' - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error with {obj.Type.Name} '{obj.Name}': {ex}");
                }
            }
            foreach (MetaObject obj in docs.AllMetaObjects())
            {
                try
                {
                    obj.BuildSearchables();
                    obj.ValidateSearchables(docs);
                }
                catch (Exception ex)
                {
                    docs.LoadErrors.Add($"Internal exception while building searchables for {obj.Type.Name} '{obj.Name}' - {ex.GetType().FullName} ... see bot console for details.");
                    Console.Error.WriteLine($"Error with {obj.Type.Name} '{obj.Name}': {ex}");
                }
            }
            docs.RawAdjustables = docs.ObjectTypes.Values.Where(t => t.GeneratedExampleAdjust == t.Name && !t.CleanName.EndsWith("tag")).Select(t => t.Name).ToHashSet();
        }

        /// <summary>Downloads guide source info.</summary>
        public static void ReadGuides(MetaDocs docs, HttpClient client)
        {
            string page = StringConversionHelper.UTF8Encoding.GetString(client.GetByteArrayAsync(DENIZEN_GUIDE_SOURCE).Result);
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
                MetaGuidePage guidePage = new();
                guidePage.URL = linkBody.BeforeAndAfter("\">", out guidePage.PageName);
                guidePage.AddTo(docs);
                guidePage.BuildSearchables();
                guidePage.ValidateSearchables(docs);
                linkIndex = linkEndIndex;
            }
            foreach (string subPage in DENIZEN_GUIDE_SUBPAGES)
            {
                string subpageContent = StringConversionHelper.UTF8Encoding.GetString(client.GetByteArrayAsync(DENIZEN_GUIDE_SOURCE + subPage).Result);
                int pageTitleIndex = subpageContent.IndexOf("<title>");
                int pageTitleEndIndex = subpageContent.IndexOf("</title>");
                int tableIndex = subpageContent.IndexOf("<div class=\"contents local topic\" id=\"table-of-contents\">");
                if (pageTitleIndex == -1 || pageTitleEndIndex == -1 || tableIndex == -1)
                {
                    docs.LoadErrors.Add("Guide sub-page did not match expected format (title or table of contents div missing).");
                    return;
                }
                string pageTitle = subpageContent[(pageTitleIndex + "<title>".Length)..pageTitleEndIndex].Before(" &mdash");
                int tableEndIndex = subpageContent.IndexOf("</div>", tableIndex);
                if (tableEndIndex == -1)
                {
                    docs.LoadErrors.Add("Guide sub-page did not match expected format (table of contents div never ends).");
                    return;
                }
                string[] table = subpageContent[tableIndex..tableEndIndex].Replace('\r', '\n').Split('\n');
                foreach (string line in table.Where(line => line.StartsWith("<li><p><a class=") && line.Contains("</a></p>")))
                {
                    int hrefIndex = line.IndexOf("href=\"") + "href=\"".Length;
                    int hrefEndIndex = line.IndexOf('\"', hrefIndex);
                    int titleStartIndex = line.IndexOf('>', hrefEndIndex + 1) + 1;
                    int titleEndIndex = line.IndexOf("</a>", titleStartIndex);
                    if (hrefEndIndex < 10 || hrefEndIndex == -1 || titleStartIndex == 0 || titleEndIndex == -1)
                    {
                        docs.LoadErrors.Add("Guide sub-page table-of-contents did not match expected format (table of contents contains at least one invalid line).");
                        continue;
                    }
                    string link = DENIZEN_GUIDE_SOURCE + subPage + line[hrefIndex..hrefEndIndex];
                    string title = line[titleStartIndex..titleEndIndex].Replace("&quot;", "\"");
                    MetaGuidePage guidePage = new()
                    {
                        URL = link,
                        PageName = pageTitle + " - " + title,
                        IsSubPage = true
                    };
                    guidePage.AddTo(docs);
                    guidePage.BuildSearchables();
                    guidePage.ValidateSearchables(docs);
                }
            }
        }

        /// <summary>Download a zip file from a URL.</summary>
        public static byte[] DownloadData(HttpClient webClient, string url)
        {
            byte[] zipDataBytes;
            if (AlternateZipSourcer != null)
            {
                zipDataBytes = AlternateZipSourcer(url, webClient);
            }
            else
            {
                zipDataBytes = webClient.GetByteArrayAsync(url).Result;
            }
            return zipDataBytes;
        }

        /// <summary>Read lines of meta docs from Java files in a zip.</summary>
        public static (int, string, string)[] ReadLines(ZipArchive zip, string folderLimit = null)
        {
            List<(int, string, string)> lines = [];
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
                SeparateDataLines(lines, entry.FullName, entryStream.AllLinesOfText());
            }
            return [.. lines];
        }

        /// <summary>Internal call for <see cref="ReadLines(ZipArchive, string)"/>.</summary>
        public static void SeparateDataLines(List<(int, string, string)> outLines, string fName, IEnumerable<string> inLines)
        {
            int lineNum = 0;
            foreach (string line in inLines)
            {
                lineNum++;
                string trimmed = line.Trim().Replace("\r", "");
                if (trimmed.StartsWith("//"))
                {
                    string actualContent = trimmed.Length == "//".Length ? "" : trimmed["// ".Length..];
                    outLines.Add((lineNum, fName, actualContent));
                }
            }
        }

        /// <summary>Load the meta doc data from lines.</summary>
        public static void LoadDataFromLines(MetaDocs docs, string websrc, (int, string, string)[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                (int lineNum, string file, string line) = lines[i];
                if (line.StartsWith("<--[") && line.EndsWith("]"))
                {
                    string objectType = line.Substring("<--[".Length, line.Length - "<--[]".Length);
                    List<string> objectData = [];
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
                    LoadInObject(docs, objectType, GetCorrectURL(websrc, file, lineNum), [.. objectData]);
                }
                else if (line.StartsWith("<--"))
                {
                    docs.LoadErrors.Add($"While processing {file} at line {i + 1} found the '<--' meta starter, but not a valid meta start.");
                }
            }
        }

        /// <summary>Gets a clean proper URL for a file path, if possible.</summary>
        public static string GetCorrectURL(string webSource, string file, int line)
        {
            if (webSource.StartsWith("https://github"))
            {
                return webSource[..^(".zip".Length)].Replace("/archive/", "/blob/") + "/" + file.After('/') + "#L" + line;
            }
            return $"Web source {webSource} file {file} line {line}";
        }

        /// <summary>Load an object into the meta docs from the object's text definition.</summary>
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
                            if (!obj.ApplyValue(docs, curKey.ToLowerFast(), curValue.Trim(' ', '\t', '\n')))
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
