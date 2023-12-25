using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using FreneticUtilities.FreneticExtensions;
using YamlDotNet.RepresentationModel;
using SharpDenizenTools.MetaHandlers;
using SharpDenizenTools.MetaObjects;
using FreneticUtilities.FreneticToolkit;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Utility class to check a script's validity.</summary>
    public class ScriptChecker
    {
        /// <summary>Action to log an internal message (defaults to <see cref="Console.WriteLine(string)"/>.</summary>
        public static Action<string> LogInternalMessage = Console.WriteLine;

        /// <summary>A set of all known script type names.</summary>
        public static readonly Dictionary<string, KnownScriptType> KnownScriptTypes = new()
        {
            // Denizen Core
            { "custom", new KnownScriptType() { LikelyBadKeys = ["script", "actions", "events", "steps"], ValueKeys = ["inherit", "*"], ScriptKeys = ["tags.*", "mechanisms.*"], Strict = false, CanHaveRandomScripts = false } },
            { "procedure", new KnownScriptType() { RequiredKeys = ["script"], LikelyBadKeys = ["events", "actions", "steps"], ValueKeys = ["definitions"], ScriptKeys = ["script"], Strict = true } },
            { "task", new KnownScriptType() { RequiredKeys = ["script"], LikelyBadKeys = ["events", "actions", "steps"], ValueKeys = ["definitions"], ScriptKeys = ["script"], Strict = false } },
            { "world", new KnownScriptType() { RequiredKeys = ["events"], ValueKeys = ["enabled"], LikelyBadKeys = ["script", "actions", "steps"], ScriptKeys = ["events.*"], Strict = false } },
            { "data", new KnownScriptType() { LikelyBadKeys = ["script", "actions", "steps", "events"], ValueKeys = ["*"], ListKeys = ["*"], Strict = false, CanHaveRandomScripts = false } },
            // Denizen-Bukkit
            { "assignment", new KnownScriptType() { RequiredKeys = ["actions"], LikelyBadKeys = ["script", "steps", "events"], ValueKeys = ["default constants.*", "constants.*", "enabled"], ListKeys = ["interact scripts"], ScriptKeys = ["actions.*"], Strict = true } },
            { "book", new KnownScriptType() { RequiredKeys = ["title", "author", "text"], LikelyBadKeys = ["script", "actions", "steps", "events"], ValueKeys = ["title", "author", "signed"], ListKeys = ["text"], Strict = true, CanHaveRandomScripts = false } },
            { "command", new KnownScriptType() { RequiredKeys = ["name", "description", "usage", "script"], LikelyBadKeys = ["steps", "actions", "events"], ValueKeys = ["name", "description", "usage", "permission", "permission message", "enabled"], ListKeys = ["aliases"], ScriptKeys = ["allowed help", "tab complete", "script"], Strict = false } },
            { "economy", new KnownScriptType() { RequiredKeys = ["priority", "name single", "name plural", "digits", "format", "balance", "has", "withdraw", "deposit"], LikelyBadKeys = ["script", "actions", "steps", "events"], ValueKeys = ["priority", "name single", "name plural", "digits", "format", "balance", "has", "enabled"], ScriptKeys = ["withdraw", "deposit"], Strict = true, CanHaveRandomScripts = false } },
            { "entity", new KnownScriptType() { RequiredKeys = ["entity_type"], LikelyBadKeys = ["script", "actions", "steps", "events"], ValueKeys = ["entity_type", "flags.*", "mechanisms.*"], ListKeys = ["flags.*", "mechanisms.*"], Strict = true, CanHaveRandomScripts = false } },
            { "format", new KnownScriptType() { RequiredKeys = ["format"], LikelyBadKeys = ["script", "actions", "steps", "events"], ValueKeys = ["format"], Strict = true, CanHaveRandomScripts = false } },
            { "interact", new KnownScriptType() { RequiredKeys = ["steps"], ValueKeys = ["enabled"], LikelyBadKeys = ["script", "actions", "events"], ScriptKeys = ["steps.*"], Strict = true } },
            { "inventory", new KnownScriptType() { RequiredKeys = ["inventory"], LikelyBadKeys = ["script", "steps", "actions", "events"], ValueKeys = ["inventory", "title", "size", "definitions.*", "gui"], ScriptKeys = ["procedural items"], ListKeys = ["slots"], Strict = true, CanHaveRandomScripts = false } },
            { "item", new KnownScriptType() { RequiredKeys = ["material"], LikelyBadKeys = ["script", "steps", "actions", "events"], ValueKeys = ["material", "mechanisms.*", "display name", "durability", "recipes.*", "no_id", "color", "book", "flags.*", "allow in material recipes"], ListKeys = ["mechanisms.*", "lore", "enchantments", "recipes.*", "flags.*"], Strict = true, CanHaveRandomScripts = false } },
            { "map", new KnownScriptType() { LikelyBadKeys = ["script", "steps", "actions", "events"], ValueKeys = ["original", "display name", "auto update", "objects.*", "contextual"], Strict = true, CanHaveRandomScripts = false } },
            { "enchantment", new KnownScriptType() { LikelyBadKeys = ["script", "steps", "actions", "events"], ScriptKeys = ["after attack", "after hurt"], ValueKeys = ["id", "rarity", "category", "full_name", "min_level", "max_level", "min_cost", "max_cost", "treasure_only", "is_curse", "is_tradable", "is_discoverable", "is_compatible", "can_enchant", "damage_bonus", "damage_protection", "enabled"], ListKeys = ["slots"], Strict = true, CanHaveRandomScripts = false } }
        };

        /// <summary>Keys that always mean a section is a script.</summary>
        public static string[] AlwaysScriptKeys = ["script", "scripts", "subscripts", "subtasks", "inject", "injects", "injectables", "subprocedures"];

        /// <summary>A non-complete set of Denizen commands that can end with a colon and contain arguments, for checking certain syntax errors.</summary>
        public static HashSet<string> CommandsWithColonsAndArguments =
        [
            "if", "else", "foreach", "while", "repeat", "choose", "case"
        ];

        /// <summary>A non-complete set of Denizen commands that can end with a colon and do not have to contain any arguments, for checking certain syntax errors.</summary>
        public static HashSet<string> CommandsWithColonsButNoArguments =
        [
            "else", "default", "random"
        ];

        /// <summary>The applicable MetaDocs set.</summary>
        public MetaDocs Meta;

        /// <summary>The full original script text.</summary>
        public string FullOriginalScript;

        /// <summary>All lines of the script.</summary>
        public string[] Lines;

        /// <summary>All lines, pre-trimmed and lowercased.</summary>
        public string[] CleanedLines;

        /// <summary>The number of lines that were comments.</summary>
        public int CommentLines = 0;

        /// <summary>The number of lines that were blank.</summary>
        public int BlankLines = 0;

        /// <summary>The number of lines that were structural (ending with a colon).</summary>
        public int StructureLines = 0;

        /// <summary>The number of lines that were code (starting with a dash).</summary>
        public int CodeLines = 0;

        /// <summary>The number of warnings that were ignored.</summary>
        public int IgnoredWarnings = 0;

        /// <summary>Represents a warning about a script.</summary>
        public class ScriptWarning
        {
            /// <summary>A unique key for this *type* of warning.</summary>
            public string WarningUniqueKey;

            /// <summary>The locally customized message form.</summary>
            public string CustomMessageForm;

            /// <summary>The line this applies to.</summary>
            public int Line;

            /// <summary>The starting character position.</summary>
            public int StartChar = 0;

            /// <summary>The ending character position.</summary>
            public int EndChar = 0;
        }

        /// <summary>A list of all errors about this script.</summary>
        public List<ScriptWarning> Errors = [];

        /// <summary>A list of all warnings about this script.</summary>
        public List<ScriptWarning> Warnings = [];

        /// <summary>A list of all minor warnings about this script.</summary>
        public List<ScriptWarning> MinorWarnings = [];

        /// <summary>A list of informational notices about this script.</summary>
        public List<ScriptWarning> Infos = [];

        /// <summary>A list of debug notices about this script, generally don't actually show to users.</summary>
        public List<string> Debugs = [];

        /// <summary>A track of all script names that appear to be injected, for false-warning reduction.</summary>
        public List<string> Injects = [];

        /// <summary>A user-specified list of warning types to ignore.</summary>
        public HashSet<string> IgnoredWarningTypes = [];

        /// <summary>Optional workspace this script exists within.</summary>
        public ScriptingWorkspaceData SurroundingWorkspace = null;

        /// <summary>Generated workspace data about this script file alone.</summary>
        public ScriptingWorkspaceData GeneratedWorkspace = new();

        /// <summary>Construct the ScriptChecker instance from a script string.</summary>
        /// <param name="script">The script contents string.</param>
        public ScriptChecker(string script)
        {
            FullOriginalScript = script;
            if (script.Contains('\r'))
            {
                script = script.Replace("\r\n", "\n").Replace('\r', '\n');
            }
            Lines = script.Split('\n');
            CleanedLines = Lines.Select(s => s.Trim().ToLowerFast()).ToArray();
        }

        /// <summary>Adds a warning to track.</summary>
        /// <param name="warnType">The warning type (the list object).</param>
        /// <param name="line">The zero-indexed line the warning is regarding.</param>
        /// <param name="key">The unique warning key, for compressing repeat warns.</param>
        /// <param name="message">The warning message.</param>
        /// <param name="start">The starting character index.</param>
        /// <param name="end">The ending character index.</param>
        public void Warn(List<ScriptWarning> warnType, int line, string key, string message, int start, int end)
        {
            if (IgnoredWarningTypes.Contains(key))
            {
                IgnoredWarnings++;
                return;
            }
            foreach (ScriptWarning warning in warnType)
            {
                if (warning.Line == line && warning.WarningUniqueKey == key)
                {
                    return;
                }
            }
            warnType.Add(new ScriptWarning() { Line = line, WarningUniqueKey = key, CustomMessageForm = message, StartChar = start, EndChar = end });
        }

        /// <summary>Adds a warning to track.</summary>
        /// <param name="warnType">The warning type (the list object).</param>
        /// <param name="key">The unique warning key, for compressing repeat warns.</param>
        /// <param name="message">The warning message.</param>
        /// <param name="line">The line to warn about.</param>
        public void Warn(List<ScriptWarning> warnType, string key, string message, LineTrackedString line)
        {
            Warn(warnType, line.Line, key, message, line.StartChar, line.StartChar + line.Text.Length);
        }

        /// <summary>Clears all comment lines.</summary>
        public void ClearCommentsFromLines()
        {
            for (int i = 0; i < CleanedLines.Length; i++)
            {
                if (CleanedLines[i].StartsWith("#"))
                {
                    if (Lines[i].StartsWith("##") && CleanedLines[i].StartsWith("##ignorewarning "))
                    {
                        IgnoredWarningTypes.Add(CleanedLines[i]["##ignorewarning ".Length..]);
                    }
                    string comment = CleanedLines[i][1..].Trim();
                    if (comment.ToLowerFast().StartsWith("todo"))
                    {
                        Warn(MinorWarnings, i, "todo_comment", $"TODO Line: {Lines[i].Trim()}", Lines[i].IndexOf('#'), Lines[i].Length);
                    }
                    CleanedLines[i] = "";
                    Lines[i] = "";
                    CommentLines++;
                }
                else if (CleanedLines[i] == "")
                {
                    BlankLines++;
                }
                else if (CleanedLines[i].StartsWith("-"))
                {
                    CodeLines++;
                }
                else if (CleanedLines[i].EndsWith(":"))
                {
                    StructureLines++;
                }
            }
        }

        /// <summary>Performs some minimal script cleaning, based on logic in DenizenCore, that matches a script load in as valid YAML, for use with <see cref="CheckYAML"/>.</summary>
        /// <returns>The cleaned YAML-friendly script.</returns>
        public string CleanScriptForYAMLProcessing()
        {
            StringBuilder result = new(FullOriginalScript.Length);
            for (int i = 0; i < Lines.Length; i++)
            {
                string line = CleanedLines[i];
                string deborkLine = Lines[i].Replace('*', 's').Replace('&', 'a').Replace('.', 'd');
                if (!line.EndsWithFast(':') && line.StartsWithFast('-'))
                {
                    int dashIndex = deborkLine.IndexOf('-');
                    result.Append(deborkLine[..(dashIndex + 1)]).Append(" ^1^");
                    result.Append(deborkLine[(dashIndex + 1)..].Replace(": ", "<&co>").Replace("#", "<&ns>")).Append('\n');
                }
                else if (!line.EndsWithFast(':'))
                {
                    int colonIndex = deborkLine.IndexOf(':');
                    result.Append(deborkLine[..(colonIndex + 1)]).Append(deborkLine[(colonIndex + 1)..].Replace(":", "<&co>")).Append('\n');
                }
                else
                {
                    result.Append(deborkLine).Append('\n');
                }
            }
            result.Append('\n');
            return result.ToString();
        }

        /// <summary>Checks if the script is even valid YAML (if not, critical error).</summary>
        public void CheckYAML()
        {
            try
            {
                new YamlStream().Load(new StringReader(CleanScriptForYAMLProcessing()));
            }
            catch (Exception ex)
            {
                string text = ex.Message;
                int line = 0;
                if (text is not null && text.StartsWith("(Line: "))
                {
                    int comma = text.IndexOf(',');
                    if (comma != -1)
                    {
                        if (int.TryParse(text["(Line: ".Length..comma], out line))
                        {
                            line--;
                        }
                    }
                }
                Warn(Errors, line, "yaml_load", $"Invalid YAML! Error message: {text}", 0, Lines[line].Length);
            }
        }

        /// <summary>Looks for injects, to prevent issues with later checks.</summary>
        public void LoadInjects()
        {
            for (int i = 0; i < CleanedLines.Length; i++)
            {
                if (CleanedLines[i].StartsWith("- inject "))
                {
                    string line = CleanedLines[i]["- inject ".Length..];
                    if (line.Contains("locally"))
                    {
                        for (int x = i; x >= 0; x--)
                        {
                            if (CleanedLines[x].Length > 0 && CleanedLines[x].EndsWith(":") && !Lines[x].Replace("\t", "    ").StartsWith(" "))
                            {
                                string scriptName = CleanedLines[x][0..^1];
                                Injects.Add(scriptName);
                                break;
                            }
                        }
                    }
                    else
                    {
                        string target = line.Before(" ");
                        string scriptTarget = target.Before(".");
                        Injects.Add(scriptTarget);
                        if (target.Contains('<'))
                        {
                            Injects.Add("*");
                        }
                    }
                }
            }
        }

        /// <summary>Checks the basic format of every line of the script, to locate stray text or useless lines.</summary>
        public void BasicLineFormatCheck()
        {
            for (int i = 0; i < Lines.Length; i++)
            {
                string line = Lines[i];
                if (line.EndsWith(" "))
                {
                    int endChar;
                    for (endChar = line.Length - 1; endChar >= 0; endChar--)
                    {
                        if (line[endChar] != ' ')
                        {
                            break;
                        }
                    }
                    endChar = Math.Max(0, endChar);
                    Warn(MinorWarnings, i, "stray_space_eol", "Stray space after end of line (possible copy/paste mixup. Enable View->Render Whitespace in VS Code).", endChar, Math.Max(endChar, line.Length - 1));
                }
                else if (CleanedLines[i].StartsWith("- ") && !CleanedLines[i].EndsWith(":"))
                {
                    int spaces = CountPreSpaces(line);
                    while (i + 1 < Lines.Length)
                    {
                        string line2 = Lines[i + 1].Replace("\t", "    ");
                        string cleaned2 = CleanedLines[i + 1];
                        if (CountPreSpaces(line2) > spaces && !cleaned2.StartsWith("- "))
                        {
                            i++;
                            if (cleaned2.EndsWith(':'))
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else if (CleanedLines[i].Length > 0 && !CleanedLines[i].Contains(':'))
                {
                    Warn(Warnings, i, "useless_invalid_line", "Useless/invalid line (possibly missing a `-` or a `:`, or just accidentally hit enter or paste).", Lines[i].IndexOf(CleanedLines[i][0]), Lines[i].Length - 1);
                }
                int sectionSymbol = line.IndexOf('§');
                if (sectionSymbol != -1)
                {
                    Warn(MinorWarnings, i, "color_code_misformat", "Don't use the section symbol for color codes, instead use tags: like <&c>, <red> or <&color[red]>.", sectionSymbol, sectionSymbol + 2);
                }
            }
        }

        /// <summary>Checks if "\t" tabs are used (instead of spaces). If so, warning.</summary>
        public void CheckForTabs()
        {
            if (!FullOriginalScript.Contains('\t'))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Contains('\t'))
                {
                    Warn(Warnings, i, "raw_tab_symbol", "This script uses the raw tab symbol. Please switch these out for 2 or 4 spaces.", Lines[i].IndexOf('\t'), Lines[i].LastIndexOf('\t'));
                    break;
                }
            }
        }

        private static readonly char[] BracesChars = ['{', '}'];

        /// <summary>Checks if { braces } are used (instead of modern "colon:" syntax). If so, error.</summary>
        public void CheckForBraces()
        {
            if (!FullOriginalScript.Contains('{'))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].EndsWith("{") || Lines[i].EndsWith("}"))
                {
                    int start = Lines[i].IndexOfAny(BracesChars);
                    int end = Lines[i].LastIndexOfAny(BracesChars);
                    Warn(Errors, i, "brace_syntax", "This script uses outdated { braced } syntax. Please update to modern 'colon:' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#colon-syntax> for more info.", start, end);
                    break;
                }
            }
        }

        /// <summary>Checks if &lt;def[oldDefs]&gt; are used (instead of modern "&lt;[defname]&gt;" syntax). If so, warning.</summary>
        public void CheckForOldDefs()
        {
            if (!FullOriginalScript.Contains("<def["))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Contains("<def["))
                {
                    int start = Lines[i].IndexOf("<def[");
                    int end = Lines[i].LastIndexOf("<def[");
                    Warn(Warnings, i, "old_defs", "This script uses <def[old-defs]>. Please update to modern '<[defname]>' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#definition-syntax> for more info.", start, end);
                    break;
                }
            }
        }

        /// <summary>Performs the necessary checks on a single tag.</summary>
        /// <param name="line">The line number.</param>
        /// <param name="startChar">The index of the character where this tag starts.</param>
        /// <param name="tag">The text of the tag.</param>
        /// <param name="context">The script checking context (if any).</param>
        public void CheckSingleTag(int line, int startChar, string tag, ScriptCheckContext context)
        {
            SingleTag parsed = TagHelper.Parse(tag, (s) =>
            {
                Warn(Warnings, line, "tag_format_break", $"Tag parse error: {s}", startChar, startChar + tag.Length);
            });
            void warnPart(SingleTag.Part part, string key, string message)
            {
                Warn(Warnings, line, key, message, startChar + part.StartChar, startChar + part.EndChar);
            }
            string tagName = parsed.Parts[0].Text.ToLowerFast();
            if (!Meta.TagBases.Contains(tagName) && tagName.Length > 0)
            {
                warnPart(parsed.Parts[0], "bad_tag_base", $"Invalid tag base `{tagName.Replace('`', '\'')}` (check `!tag ...` to find valid tags).");
            }
            else if (tagName.EndsWith("tag"))
            {
                warnPart(parsed.Parts[0], "xtag_notation", $"'XTag' notation is for documentation purposes, and is not to be used literally in a script. (replace the 'XTag' text with a valid real tagbase that returns a tag of that type).");
            }
            if (tagName == "" || tagName == "definition")
            {
                string param = parsed.Parts[0].Parameter;
                if (param is not null)
                {
                    param = param.ToLowerFast().Before('.');
                    if (context is not null && !context.Definitions.Contains(param) && !context.HasUnknowableDefinitions)
                    {
                        warnPart(parsed.Parts[0], "def_of_nothing", "Definition tag points to non-existent definition (typo, or bad copypaste?).");
                    }
                }
            }
            else if (tagName == "entry")
            {
                string param = parsed.Parts[0].Parameter;
                if (param is not null)
                {
                    param = param.ToLowerFast();
                    if (context is not null && !context.SaveEntries.Contains(param) && !context.HasUnknowableSaveEntries)
                    {
                        warnPart(parsed.Parts[0], "entry_of_nothing", "entry[...] tag points to non-existent save entry (typo, or bad copypaste?).");
                    }
                }
            }
            for (int i = 1; i < parsed.Parts.Count; i++)
            {
                SingleTag.Part part = parsed.Parts[i];
                if (!Meta.TagParts.Contains(part.Text))
                {
                    if (i != 1 || (tagName != "entry" && tagName != "context"))
                    {
                        warnPart(part, "bad_tag_part", $"Invalid tag part `{part.Text.Replace('`', '\'')}` (check `!tag ...` to find valid tags).");
                        if (part.Text.EndsWith("tag"))
                        {
                            warnPart(part, "xtag_notation", $"'XTag' notation is for documentation purposes, and is not to be used literally in a script. (replace the 'XTag' text with a valid real tagbase that returns a tag of that type).");
                        }
                    }
                }
            }
            foreach (SingleTag.Part part in parsed.Parts)
            {
                if (part.Parameter is not null)
                {
                    CheckSingleArgument(line, startChar + part.StartChar + part.Text.Length + 1, part.Parameter, context, false);
                }
            }
            if (parsed.Fallback is not null)
            {
                CheckSingleArgument(line, startChar + parsed.EndChar + 2, parsed.Fallback, context, false);
            }
            TagTracer tracer = new()
            {
                Docs = Meta,
                Tag = parsed,
                Error = (s) => { Warn(Warnings, line, "tag_trace_failure", $"Tag tracer: {s}", startChar, startChar + tag.Length); },
                DeprecationError = (s, part) => { Warn(MinorWarnings, line, "deprecated_tag_part", s, startChar + part.StartChar, startChar + part.StartChar + part.Text.Length); }
            };
            tracer.Trace();
            if (SurroundingWorkspace is not null)
            {
                foreach (SingleTag.Part part in parsed.Parts)
                {
                    if (part.PossibleTags.Count == 1)
                    {
                        MetaTag actualTag = part.PossibleTags[0];
                        if (actualTag.ParsedFormat.Parts.Count <= 2 && part.Parameter is not null && part.Parameter.Length > 0)
                        {
                            SingleTag.Part metaPart = actualTag.ParsedFormat.Parts[^1];
                            if (metaPart.Parameter is not null && metaPart.Parameter.StartsWithFast('<'))
                            {
                                string input = part.Parameter.Before('[').ToLowerFast();
                                if (!input.Contains('<'))
                                {
                                    CheckTagParam(part, metaPart, input, warnPart);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Helper to check a single tag parameter.</summary>
        public void CheckTagParam(SingleTag.Part part, SingleTag.Part metaPart, string input, Action<SingleTag.Part, string, string> warnPart)
        {
            switch (metaPart.Parameter)
            {
                case "<material>":
                    if (!Meta.Data.Items.Contains(input) && !Meta.Data.Blocks.Contains(input) && ContextValidatedGetScriptFor(input, "item") is null && ContextValidatedGetScriptFor(input, "book") is null)
                    {
                        warnPart(metaPart, "invalid_tag_material", $"Tag part `{part.Text}` has parameter `{part.Parameter}` which has to be a valid Material, but is not.");
                    }
                    break;
                case "<item>":
                    if (!Meta.Data.Items.Contains(input) && ContextValidatedGetScriptFor(input, "item") is null && ContextValidatedGetScriptFor(input, "book") is null)
                    {
                        warnPart(metaPart, "invalid_tag_item", $"Tag part `{part.Text}` has parameter `{part.Parameter}` which has to be a valid Item, but is not.");
                    }
                    break;
                case "<entity>":
                    if (!Meta.Data.Entities.Contains(input) && ContextValidatedGetScriptFor(input, "entity") is null)
                    {
                        warnPart(metaPart, "invalid_tag_entity", $"Tag part `{part.Text}` has parameter `{part.Parameter}` which has to be a valid Entity, but is not.");
                    }
                    break;
                case "<inventory>":
                    if (!ExtraData.InventoryMatchers.Contains(input) && ContextValidatedGetScriptFor(input, "inventory") is null)
                    {
                        warnPart(metaPart, "invalid_tag_inventory", $"Tag part `{part.Text}` has parameter `{part.Parameter}` which has to be a valid Inventory, but is not.");
                    }
                    break;
                case "<procedure_script_name>":
                    if (ContextValidatedGetScriptFor(input, "procedure") is null)
                    {
                        warnPart(metaPart, "invalid_tag_procedure", $"Tag part `{part.Text}` has parameter `{part.Parameter}` which has to be a valid Procedure script name, but is not.");
                    }
                    break;
            }
        }

        private static readonly char[] tagMarksChars = ['<', '>'];

        /// <summary>Performs the necessary checks on a single argument.</summary>
        /// <param name="line">The line number.</param>
        /// <param name="startChar">The index of the character where this argument starts.</param>
        /// <param name="argument">The text of the argument.</param>
        /// <param name="context">The script checking context (if any).</param>
        /// <param name="isCommand">Whether this is an argument to a command.</param>
        public void CheckSingleArgument(int line, int startChar, string argument, ScriptCheckContext context, bool isCommand)
        {
            if (argument.Contains('@') && !isCommand)
            {
                Range? range = ContainsObjectNotation(argument);
                if (range is not null)
                {
                    int start = startChar + range.Value.Start.Value;
                    int end = startChar + range.Value.End.Value;
                    Warn(Warnings, line, "raw_object_notation", "This line appears to contain raw object notation. There is almost always a better way to write a line than using raw object notation. Consider the relevant object constructor tags.", start, end);
                }
            }
            string argNoArrows = argument.Replace("<-", "al").Replace(":->", "arr");
            if (argument.Length > 2 && argNoArrows.CountCharacter('<') != argNoArrows.CountCharacter('>'))
            {
                int start = startChar + argument.IndexOfAny(tagMarksChars);
                int end = startChar + argument.LastIndexOfAny(tagMarksChars);
                Warn(Warnings, line, "uneven_tags", $"Uneven number of tag marks (forgot to close a tag?).", start, end);
            }
            int tagIndex = argNoArrows.IndexOf('<');
            while (tagIndex != -1)
            {
                int bracks = 0;
                int endIndex = -1;
                for (int i = tagIndex; i < argNoArrows.Length; i++)
                {
                    if (argNoArrows[i] == '<')
                    {
                        bracks++;
                    }
                    if (argNoArrows[i] == '>')
                    {
                        bracks--;
                        if (bracks == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                }
                if (endIndex == -1)
                {
                    break;
                }
                string tag = argNoArrows.Substring(tagIndex + 1, endIndex - tagIndex - 1);
                CheckSingleTag(line, startChar + tagIndex + 1, tag, context);
                tagIndex = argNoArrows.IndexOf('<', endIndex);
            }
        }
        /// <summary>Performs the necessary checks on a single data key line.</summary>
        /// <param name="line">The line number.</param>
        /// <param name="startChar">The index of the character where this argument starts.</param>
        /// <param name="argument">The text of the argument.</param>
        /// <param name="context">The script checking context (if any).</param>
        public void CheckSingleDataLine(int line, int startChar, string argument, ScriptCheckContext context)
        {
            if (argument.Contains('\"') || argument.StartsWith('\''))
            {
                Warn(MinorWarnings, line, "invalid_data_line_quotes", "Data lines should not be quoted. You can use '<empty>' to make an empty line, or '<&dq>' to make a raw double-quote symbol, or '<&sq>' to make a raw single-quote.", startChar, startChar + argument.Length);
            }
            CheckSingleArgument(line, startChar, argument, context, false);
        }

        /// <summary>A single argument to a command.</summary>
        public class CommandArgument
        {
            /// <summary>The character index that this argument starts at.</summary>
            public int StartChar;

            /// <summary>The text of the argument.</summary>
            public string Text;
        }

        /// <summary>Symbols that are allowed as the first character of a tag.</summary>
        public static AsciiMatcher VALID_TAG_FIRST_CHAR = new(AsciiMatcher.BothCaseLetters + AsciiMatcher.Digits + "&_[");

        /// <summary>Build args, as copied from Denizen Core -> ArgumentHelper.</summary>
        /// <param name="line">The line number.</param>
        /// <param name="startChar">The index of the character where this argument starts.</param>
        /// <param name="stringArgs">The raw arguments input.</param>
        /// <param name="checker">Optionally linked checker for warnings.</param>
        /// <returns>The argument array.</returns>
        public static CommandArgument[] BuildArgs(int line, int startChar, string stringArgs, ScriptChecker checker)
        {
            stringArgs = stringArgs.Trim().Replace('\r', ' ').Replace('\n', ' ');
            List<CommandArgument> matchList = new(stringArgs.CountCharacter(' '));
            int start = 0;
            int len = stringArgs.Length;
            char currentQuote = '\0';
            int firstQuote = 0;
            int inTags = 0, inTagParams = 0;
            bool currentTagHasFallback = false;
            for (int i = 0; i < len; i++)
            {
                char c = stringArgs[i];
                if (c == ' ' && currentQuote == '\0' && inTags == 0 && !currentTagHasFallback)
                {
                    if (i > start)
                    {
                        matchList.Add(new CommandArgument() { StartChar = startChar + start, Text = stringArgs[start..i] });
                    }
                    start = i + 1;
                }
                else if (c == '<')
                {
                    if (i + 1 < len && VALID_TAG_FIRST_CHAR.IsMatch(stringArgs[i + 1]))
                    {
                        inTags++;
                    }
                }
                else if (c == '>' && inTags > 0)
                {
                    inTags--;
                    if (inTags == 0)
                    {
                        currentTagHasFallback = false;
                    }
                }
                else if (c == '[' && inTags > 0)
                {
                    inTagParams++;
                }
                else if (c == ']' && inTagParams > 0)
                {
                    inTagParams--;
                }
                else if (c == '|' && i > 0 && stringArgs[i - 1] == '|' && inTags == 1)
                {
                    currentTagHasFallback = true;
                }
                else if (c == '"' || c == '\'')
                {
                    if (currentQuote == '\0' && inTagParams == 0)
                    {
                        if (firstQuote == 0)
                        {
                            firstQuote = i;
                        }
                        if (i == 0 || stringArgs[i - 1] == ' ')
                        {
                            currentQuote = c;
                            start = i + 1;
                        }
                    }
                    else if (currentQuote == c)
                    {
                        if (i + 1 >= len || stringArgs[i + 1] == ' ')
                        {
                            currentQuote = '\0';
                            if (i >= start)
                            {
                                string matched = stringArgs[start..i];
                                matchList.Add(new CommandArgument() { StartChar = startChar + start, Text = matched });
                                if (checker is not null)
                                {
                                    int tagMarks = 0;
                                    bool hasSpace = false;
                                    foreach (char subC in matched)
                                    {
                                        if (subC == '<')
                                        {
                                            tagMarks++;
                                        }
                                        else if (subC == '>')
                                        {
                                            tagMarks--;
                                        }
                                        else if (subC == ' ' && tagMarks == 0)
                                        {
                                            hasSpace = true;
                                        }
                                    }
                                    if (!(hasSpace || (tagMarks != 0 && matched.Contains(' '))) && !matched.EndsWith(":"))
                                    {
                                        checker.Warn(checker.MinorWarnings, line, "bad_quotes", "Pointless quotes (arguments quoted but do not contain spaces).", startChar + start, startChar + i);
                                    }
                                }
                            }
                            i++;
                            start = i + 1;
                        }
                    }
                }
            }
            if (currentQuote != '\0' && checker is not null)
            {
                checker.Warn(checker.Warnings, line, "missing_quotes", "Uneven quotes (forgot to close a quote?).", startChar + firstQuote, startChar + len);
            }
            if (start < len)
            {
                matchList.Add(new CommandArgument() { StartChar = startChar + start, Text = stringArgs[start..] });
            }
            return [.. matchList];
        }

        /// <summary>Context for checking a single script-container.</summary>
        public class ScriptCheckContext
        {
            /// <summary>Known definition names.</summary>
            public HashSet<string> Definitions = [];

            /// <summary>Known save-entry names.</summary>
            public HashSet<string> SaveEntries = [];

            /// <summary>If true, there are injects or other issues that make def names unknownable.</summary>
            public bool HasUnknowableDefinitions = false;

            /// <summary>If true, there are injects or other issues that make save entries names unknownable.</summary>
            public bool HasUnknowableSaveEntries = false;
        }

        /// <summary>Performs the necessary checks on a single command line.</summary>
        /// <param name="line">The line number.</param>
        /// <param name="startChar">The index of the character where this argument starts.</param>
        /// <param name="commandText">The text of the command line.</param>
        /// <param name="context">The script checking context.</param>
        /// <param name="script">The relevant script container.</param>
        public void CheckSingleCommand(int line, int startChar, string commandText, ScriptCheckContext context, ScriptContainerData script)
        {
            if (commandText.Contains('@'))
            {
                Range? range = ContainsObjectNotation(commandText);
                if (range is not null)
                {
                    int start = startChar + range.Value.Start.Value;
                    int end = startChar + range.Value.End.Value;
                    Warn(Warnings, line, "raw_object_notation", "This line appears to contain raw object notation. There is almost always a better way to write a line than using raw object notation. Consider the relevant object constructor tags.", start, end);
                }
            }
            commandText = commandText.Replace('\n', ' ');
            string[] parts = commandText.Split(' ', 2);
            string commandName = parts[0].ToLowerFast();
            int cmdLen = commandName.Length;
            if (commandName.StartsWith("~") || commandName.StartsWith("^"))
            {
                commandName = commandName[1..];
            }
            CommandArgument[] arguments = parts.Length == 1 ? [] : BuildArgs(line, startChar + parts[0].Length + 1, parts[1], this);
            if (!Meta.Commands.TryGetValue(commandName, out MetaCommand command))
            {
                if (commandName != "case" && commandName != "default")
                {
                    Warn(Errors, line, "unknown_command", $"Unknown command `{commandName.Replace('`', '\'')}` (typo? Use `!command [...]` to find a valid command).", startChar, startChar + cmdLen);
                }
                return;
            }
            int argCount = arguments.Count(s => !s.Text.StartsWith("save:") && !s.Text.StartsWith("if:") && !s.Text.StartsWith("player:") && !s.Text.StartsWith("npc:"));
            ScriptCheckerCommandSpecifics.CommandCheckDetails details = new()
            {
                StartChar = startChar,
                Line = line,
                CommandText = commandText,
                ArgCount = argCount,
                Arguments = arguments,
                CommandName = commandName,
                Context = context,
                Script = script,
                Checker = this
            };
            if (!string.IsNullOrWhiteSpace(command.Deprecated))
            {
                Warn(Errors, line, "deprecated_command", $"Command '{command.Name}' is deprecated: {command.Deprecated}", startChar, startChar + cmdLen);
            }
            if (commandText.Contains("parse_tag")) // TODO: Handle this locally to the tag, rather than globally pretending it exists
            {
                details.TrackDefinition("parse_value");
            }
            if (commandText.Contains("null_if_tag"))
            {
                details.TrackDefinition("null_if_value");
            }
            if (commandText.Contains("parse_value_tag"))
            {
                details.TrackDefinition("parse_value");
                details.TrackDefinition("parse_key");
            }
            if (commandText.Contains("filter_tag"))
            {
                details.TrackDefinition("filter_key");
                details.TrackDefinition("filter_value");
            }
            if (argCount < command.Required)
            {
                Warn(Errors, line, "too_few_args", $"Insufficient arguments... the `{command.Name}` command requires at least {command.Required} arguments, but you only provided {argCount}.", startChar, startChar + commandText.Length);
            }
            if (argCount > command.Maximum)
            {
                Warn(Errors, line, "too_many_args", $"Too many arguments... the `{command.Name}` command requires no more than {command.Maximum} arguments, but you provided {argCount}. Did you forget 'quotes'?", startChar, startChar + commandText.Length);
            }
            if (ScriptCheckerCommandSpecifics.CommandCheckers.TryGetValue(commandName, out Action<ScriptCheckerCommandSpecifics.CommandCheckDetails> checker))
            {
                checker(details);
            }
            string saveArgument = arguments.FirstOrDefault(s => s.Text.StartsWith("save:"))?.Text;
            if (saveArgument is not null)
            {
                context.SaveEntries.Add(saveArgument["save:".Length..].ToLowerFast());
                if (saveArgument.Contains('<'))
                {
                    context.HasUnknowableSaveEntries = true;
                }
            }
            foreach (CommandArgument argument in arguments)
            {
                CheckSingleArgument(line, argument.StartChar, argument.Text, context, true);
            }
        }

        /// <summary>Basic metadata about a known script type.</summary>
        public class KnownScriptType
        {
            /// <summary>Keys that must always be present.</summary>
            public string[] RequiredKeys = [];

            /// <summary>Keys that generally shouldn't be present unless something's gone wrong.</summary>
            public string[] LikelyBadKeys = [];

            /// <summary>Value-based keys.</summary>
            public string[] ValueKeys = [];

            /// <summary>Data list keys.</summary>
            public string[] ListKeys = [];

            /// <summary>Script keys.</summary>
            public string[] ScriptKeys = [];

            /// <summary>Whether to be strict in checks (if true, unrecognize keys will receive a warning).</summary>
            public bool Strict = false;

            /// <summary>Whether this type can have random extra scripts attached.</summary>
            public bool CanHaveRandomScripts = true;
        }

        /// <summary>A matcher for the set of characters that a script title is allowed to have.</summary>
        public static AsciiMatcher ScriptTitleCharactersAllowed = new("abcdefghijklmnopqrstuvwxyz0123456789_");

        /// <summary>Helper to check if a key matches a key-set (with asterisk support).</summary>
        public static bool MatchesSet(string key, string[] keySet)
        {
            return keySet.Contains(key) || keySet.Contains($"{key}.*") || keySet.Contains("*");
        }

        /// <summary>Checks a dictionary full of script containers, performing all checks on the scripts from there on.</summary>
        public void CheckAllContainers()
        {
            foreach (ScriptContainerData script in GeneratedWorkspace.Scripts.Values)
            {
                void warnScript(List<ScriptWarning> warns, int line, string key, string warning)
                {
                    Warn(warns, line, key, $"In script `{script.Name.Replace('`', '\'')}`: {warning}", 0, Lines[line].Length);
                }
                try
                {
                    if (script.Name.Contains(' '))
                    {
                        warnScript(MinorWarnings, script.LineNumber, "spaced_script_name", "Script titles should not contain spaces - consider the '_' underscore symbol instead.");
                    }
                    else if (!ScriptTitleCharactersAllowed.IsOnlyMatches(script.Name))
                    {
                        warnScript(MinorWarnings, script.LineNumber, "non_alphanumeric_script_name", "Script titles should be primarily alphanumeric, and shouldn't contain symbols other than '_' underscores.");
                    }
                    if (script.Name.Length < 4)
                    {
                        warnScript(Warnings, script.LineNumber, "short_script_name", "Overly short script title - script titles should be relatively long, unique text that definitely won't appear anywhere else.");
                    }
                    if (Meta.Data is not null && Meta.Data.All.Contains(script.Name))
                    {
                        warnScript(Warnings, script.LineNumber, "enumerated_script_name", "Dangerous script title - exactly matches a core keyword in Minecraft. Use a more unique name.");
                    }
                    if (Meta.Commands.ContainsKey(script.Name) || KnownScriptTypes.ContainsKey(script.Name))
                    {
                        warnScript(Warnings, script.LineNumber, "enumerated_script_name", "Dangerous script title - exactly matches a Denizen command or keyword. Use a more unique name.");
                    }
                    Dictionary<LineTrackedString, object> scriptSection = script.Keys;
                    if (!scriptSection.TryGetValue(new LineTrackedString(0, "type", 0), out object typeValue) || typeValue is not LineTrackedString typeString)
                    {
                        warnScript(Errors, script.LineNumber, "no_type_key", "Missing 'type' key!");
                        continue;
                    }
                    foreach (string key in script.KnownType.RequiredKeys)
                    {
                        if (!scriptSection.ContainsKey(new LineTrackedString(0, key, 0)))
                        {
                            warnScript(Warnings, typeString.Line, "missing_key_" + typeString.Text, $"Missing required key `{key}` (check `!lang {typeString.Text} script containers` for format rules)!");
                        }
                    }
                    foreach (string key in script.KnownType.LikelyBadKeys)
                    {
                        if (scriptSection.ContainsKey(new LineTrackedString(0, key, 0)))
                        {
                            warnScript(Warnings, typeString.Line, "bad_key_" + typeString.Text, $"Unexpected key `{key.Replace('`', '\'')}` (probably doesn't belong in this script type - check `!lang {typeString.Text} script containers` for format rules)!");
                        }
                    }
                    foreach ((LineTrackedString keyLine, object valueAtKey) in scriptSection)
                    {
                        string keyName = keyLine.Text;
                        if (keyName == "debug" || keyName == "speed" || keyName == "type")
                        {
                            continue;
                        }
                        void checkAsScript(List<object> list, ScriptCheckContext context = null)
                        {
                            if (context is null)
                            {
                                context = new ScriptCheckContext();
                            }
                            if (scriptSection.TryGetValue(new LineTrackedString(0, "definitions", 0), out object defList) && defList is LineTrackedString defListVal)
                            {
                                context.Definitions.UnionWith(defListVal.Text.ToLowerFast().Split('|').Select(s => s.Before('[').Trim()));
                            }
                            if (typeString.Text == "task")
                            {
                                // Workaround the weird way shoot command does things
                                context.Definitions.UnionWith(new[] { "shot_entities", "last_entity", "location", "hit_entities" });
                            }
                            else if (typeString.Text == "economy")
                            {
                                context.Definitions.UnionWith(new[] { "amount" });
                            }
                            // Default run command definitions get used sometimes
                            context.Definitions.UnionWith(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });
                            if (Injects.Contains(script.Name) || Injects.Contains("*"))
                            {
                                context.HasUnknowableDefinitions = true;
                                context.HasUnknowableSaveEntries = true;
                            }
                            foreach (object entry in list)
                            {
                                if (entry is LineTrackedString str)
                                {
                                    CheckSingleCommand(str.Line, str.StartChar, str.Text, context, script);
                                }
                                else if (entry is Dictionary<LineTrackedString, object> subMap)
                                {
                                    KeyValuePair<LineTrackedString, object> onlyEntry = subMap.First();
                                    CheckSingleCommand(onlyEntry.Key.Line, onlyEntry.Key.StartChar, onlyEntry.Key.Text, context, script);
                                    if (!onlyEntry.Key.Text.StartsWith("definemap"))
                                    {
                                        checkAsScript((List<object>)onlyEntry.Value, context);
                                    }
                                }
                            }
                        }
                        void checkBasicList(List<object> list, bool canBeScript)
                        {
                            foreach (object entry in list)
                            {
                                if (entry is LineTrackedString str)
                                {
                                    CheckSingleDataLine(str.Line, str.StartChar, str.Text, null);
                                }
                                else if (canBeScript)
                                {
                                    warnScript(Warnings, keyLine.Line, "script_should_be_list", $"Key `{keyName.Replace('`', '\'')}` appears to contain a script, when a data list was expected (check `!lang {typeString.Text} script containers` for format rules).");
                                }
                            }
                        }
                        if (valueAtKey is List<object> listAtKey)
                        {
                            if (MatchesSet(keyName, script.KnownType.ScriptKeys) || MatchesSet(keyName, AlwaysScriptKeys))
                            {
                                checkAsScript(listAtKey);
                            }
                            else if (MatchesSet(keyName, script.KnownType.ListKeys))
                            {
                                checkBasicList(listAtKey, true);
                            }
                            else if (MatchesSet(keyName, script.KnownType.ValueKeys))
                            {
                                warnScript(Warnings, keyLine.Line, "list_should_be_value", $"Bad key `{keyName.Replace('`', '\'')}` (was expected to be a direct Value, but was instead a list - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (typeString.Text == "data" || keyName == "data" || keyName == "description")
                            {
                                // Always allow 'data'
                                checkBasicList(listAtKey, false);
                            }
                            else if (script.KnownType.Strict)
                            {
                                warnScript(Warnings, keyLine.Line, "unknown_key_" + typeString.Text, $"Unexpected list key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (script.KnownType.CanHaveRandomScripts)
                            {
                                checkAsScript(listAtKey);
                            }
                            else
                            {
                                checkBasicList(listAtKey, true);
                            }

                        }
                        else if (valueAtKey is LineTrackedString lineAtKey)
                        {
                            ScriptCheckContext context = new();
                            if (typeString.Text == "economy" && (keyName == "format" || keyName == "has"))
                            {
                                context.Definitions.Add("amount");
                            }
                            else if (typeString.Text == "format" && keyName == "format")
                            {
                                context.Definitions.Add("text");
                                context.Definitions.Add("name");
                            }
                            else if (typeString.Text == "command" && keyName == "permission message")
                            {
                                context.Definitions.Add("permission");
                            }
                            else if (typeString.Text == "data")
                            {
                                context.HasUnknowableSaveEntries = true;
                                context.HasUnknowableDefinitions = true;
                            }
                            if (MatchesSet(keyName, script.KnownType.ValueKeys) || keyName == "description")
                            {
                                CheckSingleDataLine(keyLine.Line, lineAtKey.StartChar + 2, lineAtKey.Text, context);
                            }
                            else if (MatchesSet(keyName, script.KnownType.ListKeys) || MatchesSet(keyName, script.KnownType.ScriptKeys))
                            {
                                warnScript(Warnings, keyLine.Line, "bad_key_" + typeString.Text, $"Bad key `{keyName.Replace('`', '\'')}` (was expected to be a list or script, but was instead a direct Value - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (script.KnownType.Strict && keyName != "data")
                            {
                                warnScript(Warnings, keyLine.Line, "unknown_key_" + typeString.Text, $"Unexpected value key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                CheckSingleDataLine(keyLine.Line, keyLine.StartChar, lineAtKey.Text, context);
                            }
                        }
                        else if (valueAtKey is Dictionary<LineTrackedString, object> keyPairMap)
                        {
                            string keyText = keyName + ".*";
                            void checkSubMaps(Dictionary<LineTrackedString, object> subMap, bool canBeScript)
                            {
                                foreach (object subValue in subMap.Values)
                                {
                                    if (subValue is LineTrackedString textLine)
                                    {
                                        CheckSingleDataLine(textLine.Line, textLine.StartChar, textLine.Text, null);
                                    }
                                    else if (subValue is List<object> listKey)
                                    {
                                        if (canBeScript && (script.KnownType.ScriptKeys.Contains(keyText) || (!script.KnownType.ListKeys.Contains(keyText) && script.KnownType.CanHaveRandomScripts)))
                                        {
                                            checkAsScript(listKey);
                                        }
                                        else
                                        {
                                            checkBasicList(listKey, canBeScript);
                                        }
                                    }
                                    else if (subValue is Dictionary<LineTrackedString, object> mapWithin)
                                    {
                                        checkSubMaps(mapWithin, canBeScript);
                                    }
                                }
                            }
                            if (script.KnownType.ValueKeys.Contains(keyText) || script.KnownType.ListKeys.Contains(keyText) || script.KnownType.ScriptKeys.Contains(keyText) || AlwaysScriptKeys.Contains(keyName)
                                || script.KnownType.ValueKeys.Contains("*") || script.KnownType.ListKeys.Contains("*") || script.KnownType.ScriptKeys.Contains("*"))
                            {
                                checkSubMaps(keyPairMap, typeString.Text != "data" && keyName != "data");
                            }
                            else if (script.KnownType.Strict && keyName != "data")
                            {
                                warnScript(Warnings, keyLine.Line, "unknown_key_" + typeString.Text, $"Unexpected submapping key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                checkSubMaps(keyPairMap, typeString.Text != "data" && !keyName.StartsWith("definemap") && keyName != "data");
                            }
                        }
                    }
                    if (typeString.Text == "command")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "name", 0), out object nameValue) && nameValue is LineTrackedString nameString)
                        {
                            if (scriptSection.TryGetValue(new LineTrackedString(0, "usage", 0), out object usageValue) && usageValue is LineTrackedString usageString)
                            {
                                if (!usageString.Text.StartsWith($"/{nameString.Text} ") && usageString.Text != $"/{nameString.Text}")
                                {
                                    warnScript(MinorWarnings, usageString.Line, "command_script_usage", "Command script usage key doesn't match the name key (the name is the actual thing you need to type in-game, the usage is for '/help' - refer to `!lang command script containers`)!");
                                }
                            }
                            if (scriptSection.TryGetValue(new LineTrackedString(0, "aliases", 0), out object aliasValue) && aliasValue is List<object> aliasList && !aliasList.IsEmpty())
                            {
                                if (aliasList.FirstOrDefault(o => o is LineTrackedString s && s.Text == nameString.Text) is LineTrackedString badAlias)
                                {
                                    warnScript(Warnings, badAlias.Line, "command_script_aliasname", "A command script alias should not be the same as the command script's name.");
                                }
                            }
                        }
                    }
                    else if (typeString.Text == "assignment")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "actions", 0), out object actionsValue) && actionsValue is Dictionary<LineTrackedString, object> actionsMap)
                        {
                            foreach (LineTrackedString actionValue in actionsMap.Keys)
                            {
                                string actionName = actionValue.Text["on ".Length..];
                                if (actionName.Contains('@'))
                                {
                                    int start = actionValue.StartChar + actionValue.Text.IndexOf('@');
                                    int end = actionValue.StartChar + actionValue.Text.LastIndexOf('@');
                                    Warn(Warnings, actionValue.Line, "action_object_notation", "This action line appears to contain raw object notation. Object notation is not allowed in action lines.", start, end);
                                }
                                actionName = "on " + actionName;
                                if (!Meta.Actions.ContainsKey(actionName))
                                {
                                    bool exists = false;
                                    foreach (MetaAction action in Meta.Actions.Values)
                                    {
                                        if (action.RegexMatcher.IsMatch(actionName))
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (!exists)
                                    {
                                        warnScript(Warnings, actionValue.Line, "action_missing", $"Assignment script action listed doesn't exist. (Check `!act ...` to find proper action names)!");
                                    }
                                }
                            }
                        }
                    }
                    else if (typeString.Text == "world")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "events", 0), out object eventsValue) && eventsValue is Dictionary<LineTrackedString, object> eventsMap)
                        {
                            foreach (LineTrackedString eventValue in eventsMap.Keys)
                            {
                                string eventName = eventValue.Text[(eventValue.Text.StartsWith("on ") ? "on ".Length : (eventValue.Text.StartsWith("after ") ? "after ".Length : 0))..];
                                if (eventName.Contains('@'))
                                {
                                    Range? atRange = ContainsObjectNotation(eventName);
                                    if (atRange is not null)
                                    {
                                        int start = eventValue.StartChar + atRange.Value.Start.Value;
                                        int end = eventValue.StartChar + atRange.Value.End.Value;
                                        Warn(Warnings, eventValue.Line, "event_object_notation", "This event line appears to contain raw object notation. Object notation is not allowed in event lines.", start, end);
                                    }
                                }
                                eventName = EventTools.SeparateSwitches(Meta, eventName, out List<KeyValuePair<string, string>> switches);
                                string[] parts = eventName.SplitFast(' ');
                                MetaEvent matchedEvent = null;
                                ScriptEventCouldMatcher matched = null;
                                bool matchedSwitches = false;
                                foreach (MetaEvent evt in Meta.Events.Values)
                                {
                                    foreach (ScriptEventCouldMatcher matcher in evt.CouldMatchers)
                                    {
                                        if (matcher.TryMatch(parts, false, false) > 0)
                                        {
                                            if (matched == null || matcher.IsBetterMatchThan(parts, false, matched))
                                            {
                                                if (AllSwitchesValid(evt, switches))
                                                {
                                                    matched = matcher;
                                                    matchedEvent = evt;
                                                    matchedSwitches = true;
                                                }
                                                else if (!matchedSwitches)
                                                {
                                                    matched = matcher;
                                                    matchedEvent = evt;
                                                }
                                            }
                                            else if (!matchedSwitches && AllSwitchesValid(evt, switches))
                                            {
                                                matched = matcher;
                                                matchedEvent = evt;
                                                matchedSwitches = true;
                                            }
                                        }
                                    }
                                }
                                if (matchedEvent is null)
                                {
                                    foreach (MetaEvent evt in Meta.Events.Values)
                                    {
                                        if (evt.CouldMatchers.Any(c => c.TryMatch(parts, true, false) > 0))
                                        {
                                            matchedEvent = evt;
                                            break;
                                        }
                                    }
                                    if (matchedEvent is null)
                                    {
                                        warnScript(Warnings, eventValue.Line, "event_missing", $"Script Event listed doesn't exist. (Check `!event ...` to find proper event lines)!");
                                    }
                                    else
                                    {
                                        warnScript(Warnings, eventValue.Line, "event_missing", $"Script Event listed doesn't exist. Got partial match for '{matchedEvent.Name}' - might be incomplete? Check documentation.");
                                    }
                                }
                                else
                                {
                                    foreach (KeyValuePair<string, string> switchPair in switches)
                                    {
                                        if (switchPair.Key == "cancelled" || switchPair.Key == "ignorecancelled")
                                        {
                                            if (switchPair.Value.ToLowerFast() != "true" && switchPair.Value.ToLowerFast() != "false")
                                            {
                                                warnScript(Warnings, eventValue.Line, "bad_switch_value", $"'{switchPair.Key}' switch invalid: must be 'true' or 'false'.");
                                            }
                                        }
                                        else if (switchPair.Key == "priority" || switchPair.Key == "chance")
                                        {
                                            if (!double.TryParse(switchPair.Value, out _))
                                            {
                                                warnScript(Warnings, eventValue.Line, "bad_switch_value", $"'{switchPair.Key}' switch invalid: must be a decimal number.");
                                            }
                                        }
                                        else if (switchPair.Key == "in" || switchPair.Key == "location_flagged")
                                        {
                                            if (!matchedEvent.HasLocation)
                                            {
                                                warnScript(Warnings, eventValue.Line, "unknown_switch", $"'{switchPair.Key}' switch is only supported on events that have a known location.");
                                            }
                                        }
                                        else if (switchPair.Key == "flagged" || switchPair.Key == "permission")
                                        {
                                            if (string.IsNullOrWhiteSpace(matchedEvent.Player))
                                            {
                                                warnScript(Warnings, eventValue.Line, "unknown_switch", $"'{switchPair.Key}' switch is only supported on events that have a linked player.");
                                            }
                                        }
                                        else if (switchPair.Key == "assigned")
                                        {
                                            if (string.IsNullOrWhiteSpace(matchedEvent.NPC))
                                            {
                                                warnScript(Warnings, eventValue.Line, "unknown_switch", $"'{switchPair.Key}' switch is only supported on events that have a linked NPC.");
                                            }
                                        }
                                        else
                                        {
                                            if (!matchedEvent.IsValidSwitch(switchPair.Key))
                                            {
                                                warnScript(Warnings, eventValue.Line, "unknown_switch", $"Switch given is unrecognized.");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnScript(Warnings, script.LineNumber, "exception_internal", $"Internal exception (check internal debug console)!");
                    LogInternalMessage($"Script check exception: {ex}");
                }
            }
        }

        private static bool AllSwitchesValid(MetaEvent evt, List<KeyValuePair<string, string>> switches)
        {
            return switches.All(pair => evt.IsValidSwitch(pair.Key));
        }

        /// <summary>Matcher for A-Z only.</summary>
        public static readonly AsciiMatcher AlphabetMatcher = new(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        /// <summary>Matcher for the letter right before the '@' symbol in existing ObjectTag types.</summary>
        public static readonly AsciiMatcher OBJECT_NOTATION_LAST_LETTER_MATCHER = new("mdlipqsebhounwr");

        /// <summary>Checks whether a line contains object notation, and returns a range of matches if so.</summary>
        /// <param name="line">The line to check.</param>
        /// <returns>The match range, or null.</returns>
        public static Range? ContainsObjectNotation(string line)
        {
            int first = line.Length;
            int last = -1;
            int atIndex = -1;
            while ((atIndex = line.IndexOf('@', atIndex + 1)) != -1)
            {
                if (atIndex > 0 && OBJECT_NOTATION_LAST_LETTER_MATCHER.IsMatch(line[atIndex - 1]))
                {
                    first = Math.Min(first, atIndex - 1);
                    last = Math.Max(last, atIndex);
                }
            }
            if (last != -1)
            {
                return new Range(first, last);
            }
            return null;
        }

        /// <summary>Helper class for strings that remember where they came from.</summary>
        public class LineTrackedString(int line, string text, int start)
        {
            /// <summary>The text of the line.</summary>
            public string Text = text;

            /// <summary>The line number.</summary>
            public int Line = line;

            /// <summary>The character index of where this line starts.</summary>
            public int StartChar = start;

            /// <summary>HashCode impl, for Dictionary functionality.</summary>
            public override int GetHashCode()
            {
                return HashCode.Combine(Text);
            }

            /// <summary>Equals impl, for Dictionary functionality.</summary>
            public override bool Equals(object obj)
            {
                return (obj is LineTrackedString lts2) && Text == lts2.Text;
            }

            /// <summary>ToString override, returns <see cref="Text"/>.</summary>
            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>Counts the number of spaces in front of a line.</summary>
        public static int CountPreSpaces(string line)
        {
            int spaces;
            for (spaces = 0; spaces < line.Length; spaces++)
            {
                if (line[spaces] != ' ')
                {
                    break;
                }
            }
            return spaces;
        }

        /// <summary>Gathers a dictionary of all actual containers, checking for errors as it goes, and returning the dictionary.</summary>
        public Dictionary<LineTrackedString, object> GatherActualContainers()
        {
            Dictionary<LineTrackedString, object> rootScriptSection = [];
            Dictionary<int, Dictionary<LineTrackedString, object>> spacedsections = new() { { 0, rootScriptSection } };
            Dictionary<int, List<object>> spacedlists = [];
            Dictionary<LineTrackedString, object> currentSection = rootScriptSection;
            Dictionary<LineTrackedString, object> currentRootSection = null;
            int pspaces = 0;
            LineTrackedString secwaiting = null;
            List<object> clist = null;
            bool buildingSubList = false;
            for (int i = 0; i < Lines.Length; i++)
            {
                string line = Lines[i].Replace("\t", "    ");
                string cleaned = CleanedLines[i];
                int cleanStartCut = cleaned.Length == 0 ? 0 : line.IndexOf(cleaned[0]);
                if (cleaned.Length == 0)
                {
                    continue;
                }
                int spaces = CountPreSpaces(line);
                if (spaces < pspaces)
                {
                    if (spacedlists.TryGetValue(spaces, out List<object> tempList))
                    {
                        clist = tempList;
                    }
                    else if (spacedsections.TryGetValue(spaces, out Dictionary<LineTrackedString, object> temp))
                    {
                        currentSection = temp;
                    }
                    else
                    {
                        Warn(Warnings, i, "shrunk_spacing", $"Simple spacing error - shrunk unexpectedly to new space count, from {pspaces} down to {spaces}, while expecting any of: {string.Join(", ", spacedsections.Keys)}.", 0, spaces);
                        pspaces = spaces;
                        continue;
                    }
                    foreach (int test in new List<int>(spacedsections.Keys))
                    {
                        if (test > spaces)
                        {
                            spacedsections.Remove(test);
                        }
                    }
                    foreach (int test in new List<int>(spacedlists.Keys))
                    {
                        if (test > spaces)
                        {
                            spacedlists.Remove(test);
                        }
                    }
                }
                if (cleaned.StartsWith("- "))
                {
                    if (spaces > pspaces && clist is not null && !buildingSubList)
                    {
                        Warn(Warnings, i, "growing_spaces_in_script", "Spacing grew for no reason (missing a ':' on a command, or accidental over-spacing?).", 0, spaces);
                    }
                    if (secwaiting is not null)
                    {
                        if (clist is null)
                        {
                            clist = [];
                            spacedlists[spaces] = clist;
                            if (currentSection.Keys.Any(k => k.Text == secwaiting.Text))
                            {
                                Warn(Errors, "duplicate_key", "Duplicate key - a key of the same name already exists in this script section.", secwaiting);
                            }
                            currentSection[secwaiting] = clist;
                            secwaiting = null;
                        }
                        else if (buildingSubList)
                        {
                            if (spaces <= pspaces)
                            {
                                Warn(Errors, "empty_command_section", "Script section within command is empty (add contents, or remove the section).", secwaiting);
                            }
                            List<object> newclist = [];
                            clist.Add(new Dictionary<LineTrackedString, object>() { { secwaiting, newclist } });
                            secwaiting = null;
                            buildingSubList = false;
                            clist = newclist;
                            spacedlists[spaces] = newclist;
                        }
                        else
                        {
                            Warn(Warnings, i, "growing_spacing_impossible", "Line grew when that isn't possible (spacing error?).", 0, spaces);
                            pspaces = spaces;
                            continue;
                        }
                    }
                    else if (clist is null)
                    {
                        Warn(Warnings, i, "weird_line_growth", "Line purpose unknown, attempted list entry when not building a list (likely line format error, perhaps missing or misplaced a `:` on lines above, or incorrect tabulation?).", 0, line.IndexOf('-'));
                        pspaces = spaces;
                        continue;
                    }
                    string text = cleaned["- ".Length..];
                    if (!cleaned.EndsWith(":"))
                    {
                        while (i + 1 < Lines.Length)
                        {
                            string line2 = Lines[i + 1].Replace("\t", "    ");
                            string cleaned2 = CleanedLines[i + 1];
                            if (CountPreSpaces(line2) > spaces && !cleaned2.StartsWith("- "))
                            {
                                text += "\n" + line2;
                                i++;
                                if (cleaned2.EndsWith(':'))
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    if (text.EndsWith(':'))
                    {
                        if (text.StartsWith("definemap "))
                        {
                            clist.Add(new LineTrackedString(i, text, cleanStartCut + 2));
                            while (i + 1 < Lines.Length)
                            {
                                string subLine = Lines[i + 1].Replace("\t", "    ");
                                if (string.IsNullOrWhiteSpace(subLine) || CountPreSpaces(subLine) > spaces)
                                {
                                    i++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            secwaiting = new LineTrackedString(i, text[..^1], cleanStartCut + 2);
                            buildingSubList = true;
                        }
                    }
                    else
                    {
                        clist.Add(new LineTrackedString(i, text, cleanStartCut + 2));
                    }
                    pspaces = spaces;
                    continue;
                }
                clist = null;
                spacedlists.Remove(spaces);
                string startofline;
                string endofline = "";
                int endIndex = cleanStartCut;
                if (cleaned.EndsWith(":"))
                {
                    startofline = cleaned[0..^1];
                }
                else if (cleaned.Contains(": "))
                {
                    startofline = line.Trim().BeforeAndAfter(": ", out endofline);
                    endIndex += startofline.Length;
                }
                else
                {
                    Warn(Warnings, i, "identifier_missing_line", "Line purpose unknown, no identifier (missing a `:` or a `-`?).", 0, line.Length);
                    continue;
                }
                if (startofline.Length == 0)
                {
                    Warn(Warnings, i, "key_line_no_content", "key line missing contents (misplaced a `:`)?", 0, line.Length);
                    continue;
                }
                if (startofline.Contains('<'))
                {
                    Warn(Warnings, i, "tag_in_key", "Keys cannot contain tags.", 0, line.Length);
                }
                string[] inputArgs = startofline.SplitFast(' ');
                if (spaces > 0 && CanWarnAboutCommandMissingDash(inputArgs, currentRootSection) && !(secwaiting != null && secwaiting.Text == "data") && !spacedsections.Values.Any(sec => sec.Any() && sec.Keys.Last().Text == "data"))
                {
                    Warn(Warnings, i, "key_line_looks_like_command", "Line appears to be intended as command, but forgot a '-'?", 0, line.Length);
                }
                if (spaces > pspaces)
                {
                    if (secwaiting is null)
                    {
                        Warn(Warnings, i, "spacing_grew_weird", "Spacing grew for no reason (missing a ':', or accidental over-spacing?).", 0, spaces);
                        pspaces = spaces;
                        continue;
                    }
                    Dictionary<LineTrackedString, object> sect = [];
                    if (currentSection == rootScriptSection)
                    {
                        currentRootSection = sect;
                    }
                    if (currentSection.Keys.Any(k => k.Text == secwaiting.Text))
                    {
                        if (currentSection == rootScriptSection)
                        {
                            Warn(Errors, "duplicate_script", "Duplicate script - a script container of the same name already exists in this script file.", secwaiting);
                        }
                        else
                        {
                            Warn(Errors, "duplicate_key", "Duplicate key - a key of the same name already exists in this script section.", secwaiting);
                        }
                    }
                    currentSection[secwaiting] = sect;
                    currentSection = sect;
                    spacedsections[spaces] = sect;
                    secwaiting = null;
                }
                if (endofline.Length == 0)
                {
                    if (secwaiting is not null && spaces <= pspaces)
                    {
                        Warn(Errors, "empty_section", "Script section is empty (add contents, or remove the section).", secwaiting);
                    }
                    secwaiting = new LineTrackedString(i, startofline.ToLowerFast(), cleanStartCut);
                }
                else
                {
                    currentSection[new LineTrackedString(i, startofline.ToLowerFast(), cleanStartCut)] = new LineTrackedString(i, endofline, endIndex);
                }
                pspaces = spaces;
            }
            return rootScriptSection;
        }

        /// <summary>Helper method to determine whether a section key that looks like it might have been meant as a command should actually show a warning or not.</summary>
        public static bool CanWarnAboutCommandMissingDash(string[] args, Dictionary<LineTrackedString, object> currentRootSection)
        {
            string cmdName = args[0].ToLowerFast();
            if (!(args.Length == 1 ? CommandsWithColonsButNoArguments : CommandsWithColonsAndArguments).Contains(cmdName))
            {
                return false;
            }
            if (currentRootSection is null)
            {
                return true;
            }
            if (!currentRootSection.TryGetValue(new LineTrackedString(0, "type", 0), out object typeValue))
            {
                return true;
            }
            if (typeValue is not LineTrackedString typeString)
            {
                return true;
            }
            if (typeString.Text.ToLowerFast() == "data")
            {
                return false;
            }
            if (typeString.Text.ToLowerFast() == "command" && cmdName == "default")
            {
                return false;
            }
            return true;
        }

        /// <summary>Adds <see cref="Infos"/> entries for basic statistics.</summary>
        public void CollectStatisticInfos()
        {
            Warn(Infos, -1, "stat_structural", $"(Statistics) Total structural lines: {StructureLines}", 0, 0);
            Warn(Infos, -1, "stat_livecode", $"(Statistics) Total live code lines: {CodeLines}", 0, 0);
            Warn(Infos, -1, "stat_comment", $"(Statistics) Total comment lines: {CommentLines}", 0, 0);
            Warn(Infos, -1, "stat_blank", $"(Statistics) Total blank lines: {BlankLines}", 0, 0);
            if (IgnoredWarnings > 0)
            {
                Warn(Infos, -1, "stat_ignore_warnings", $"(Statistics) Total ignored warnings: {IgnoredWarnings}", 0, 0);
            }
        }

        /// <summary>Converts the raw container data to trackable container objects.</summary>
        public void ConvertContainers(Dictionary<LineTrackedString, object> containers)
        {
            foreach ((LineTrackedString title, object data) in containers)
            {
                try
                {
                    if (data is not Dictionary<LineTrackedString, object> map)
                    {
                        Warn(Errors, title.Line, "invalid_container", $"Script `{title.Text}` is invalid - missing content?", 0, Lines[title.Line].Length);
                        continue;
                    }
                    if (!map.TryGetValue(new LineTrackedString(0, "type", 0), out object type) || type is not LineTrackedString typeString)
                    {
                        Warn(Errors, title.Line, "invalid_container", $"Script `{title.Text}` is invalid - missing 'type' key", 0, Lines[title.Line].Length);
                        continue;
                    }
                    String cleanType = type.ToString().Trim().ToLowerFast();
                    if (!KnownScriptTypes.TryGetValue(cleanType, out KnownScriptType scriptType))
                    {
                        Warn(Errors, title.Line, "wrong_type", "Unknown script type (possibly a typo?)!", 0, Lines[typeString.Line].Length);
                        continue;
                    }
                    ScriptContainerData container = new()
                    {
                        Name = title.Text.Trim().ToLowerFast(),
                        LineNumber = title.Line,
                        Keys = map,
                        Type = cleanType,
                        KnownType = scriptType
                    };
                    if (map.TryGetValue(new LineTrackedString(0, "definitions", 0), out object defs))
                    {
                        IEnumerable<string> defNames = defs is List<object> defList ? defList.Select(o => o.ToString()) : defs.ToString().SplitFast('|');
                        defNames = defNames.Select(d => d.ToLowerFast().Before('[').Trim());
                        container.DefNames.AddAll(defNames.ToArray());
                    }
                    PreprocContainer(container);
                    GeneratedWorkspace.Scripts[container.Name] = container;
                }
                catch (Exception ex)
                {
                    Warn(Errors, title.Line, "exception_internal_container", $"Script `{title.Text}` is invalid - internal exception (check internal debug console)!", 0, Lines[title.Line].Length);
                    LogInternalMessage($"Script container conversion exception: {ex}");
                }
            }
            Dictionary<string, ScriptContainerData> combined = new(GeneratedWorkspace.Scripts);
            if (SurroundingWorkspace is not null)
            {
                combined.UnionWith(SurroundingWorkspace.Scripts);
            }
            foreach (ScriptContainerData script in GeneratedWorkspace.Scripts.Values)
            {
                if (!script.InjectedPaths.Any())
                {
                    continue;
                }
                void recurseAdd(ScriptContainerData script, ScriptContainerData body)
                {
                    foreach (string injected in body.InjectedPaths.GetAllMatchesIn(combined.Keys))
                    {
                        if (script.RealInjects.Add(injected))
                        {
                            ScriptContainerData injectedScript = combined[injected];
                            script.DefNames.MergeIn(injectedScript.DefNames);
                            script.SaveEntryNames.MergeIn(injectedScript.SaveEntryNames);
                            recurseAdd(script, injectedScript);
                        }
                    }
                }
                recurseAdd(script, script);
            }
        }

        /// <summary>Converts the raw container data to trackable container objects.</summary>
        public static void PreprocContainer(ScriptContainerData script)
        {
            if (script.Type == "data")
            {
                return;
            }
            if (script.Type == "item")
            {
                if (script.Keys.TryGetValue(new LineTrackedString(0, "flags", 0), out object flags) && flags is Dictionary<LineTrackedString, object> flagMap)
                {
                    foreach (LineTrackedString flagName in flagMap.Keys)
                    {
                        script.ObjectFlags.AddAll(flagName.Text.Trim().ToLowerFast().Before('.'));
                    }
                }
                return;
            }
            foreach ((LineTrackedString key, object valueAtKey) in script.Keys)
            {
                string keyName = key.Text.ToLowerFast().Trim();
                if (keyName == "data" || keyName == "description")
                {
                    continue;
                }
                void procSingleCommand(string cmd)
                {
                    string cmdName = cmd.Trim().BeforeAndAfter(' ', out string argTextRaw).ToLowerFast();
                    string[] fullArgs = BuildArgs(key.Line, 0, argTextRaw, null).Select(a => a.Text.ToLowerFast()).ToArray();
                    string[] cleanArgs = fullArgs.Where(a => !StartsWithAny(a, "save:", "player:", "npc:")).ToArray();
                    switch (cmdName)
                    {
                        case "define":
                        case "definemap":
                            if (cleanArgs.Any())
                            {
                                script.DefNames.Add(cleanArgs[0].Before(':').Before('.'));
                            }
                            break;
                        case "inject":
                            {
                                string arg = cleanArgs.FirstOrDefault(a => a != "instantly" && !a.StartsWith("path:"));
                                if (arg is not null)
                                {
                                    script.InjectedPaths.Add(arg.Before('.'));
                                }
                            }
                            break;
                        case "foreach":
                            {
                                script.DefNames.Add("loop_index");
                                string arg = cleanArgs.FirstOrDefault(a => a.StartsWith("key:"));
                                if (arg is not null)
                                {
                                    script.DefNames.Add(arg.After(':').Before('.'));
                                }
                                goto case "while";
                            }
                        case "repeat":
                        case "while":
                            {
                                string arg = cleanArgs.FirstOrDefault(a => a.StartsWith("as:"));
                                if (arg is not null)
                                {
                                    script.DefNames.Add(arg.After(':').Before('.'));
                                }
                                else
                                {
                                    script.DefNames.Add("value");
                                }
                            }
                            break;
                        case "flag":
                            if (cleanArgs.Length >= 2)
                            {
                                string flag = cleanArgs[1].Before(':').Before('.').Before('[');
                                if (cleanArgs[0] == "server")
                                {
                                    script.ServerFlags.Add(flag);
                                }
                                else
                                {
                                    script.ObjectFlags.Add(flag);
                                }
                            }
                            break;
                        case "inventory":
                            if (cleanArgs.Contains("flag"))
                            {
                                string flag = cleanArgs.FirstOrDefault(a => !StartsWithAny(a,
                                    // inventory command has a long legacy-style list of arg aliases
                                    "origin", "o", "source", "items", "item", "i", "from", "f",
                                    "destination", "dest", "d", "target", "to", "t",
                                    "slot", "s",
                                    "duration", "expire", "expires", "expiration"));
                                if (flag is not null)
                                {
                                    script.ObjectFlags.Add(flag.Before(':').Before('.'));
                                }
                            }
                            break;
                    }
                    int specialFlag = cmd.IndexOf("flag="); // Special case: data like 'stone[flag=x:y]'
                    if (specialFlag != -1)
                    {
                        string flagData = cmd[(specialFlag + "flag=".Length)..].Before(' ').Before(';').Before(']').Before(':').Before('.');
                        if (flagData != "")
                        {
                            script.ObjectFlags.Add(flagData);
                        }
                    }
                    string save = fullArgs.FirstOrDefault(a => a.StartsWith("save:"));
                    if (save is not null)
                    {
                        script.SaveEntryNames.Add(save.After(':'));
                    }
                }
                void procAsScript(List<object> list)
                {
                    if (script.Type == "task")
                    {
                        // Workaround the weird way shoot command does things
                        script.DefNames.AddAll("shot_entities", "last_entity", "location", "hit_entities");
                    }
                    else if (script.Type == "economy")
                    {
                        script.DefNames.Add("amount");
                    }
                    // Default run command definitions get used sometimes
                    script.DefNames.AddAll("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
                    foreach (object entry in list)
                    {
                        if (entry is LineTrackedString str)
                        {
                            procSingleCommand(str.Text);
                        }
                        else if (entry is Dictionary<LineTrackedString, object> subMap)
                        {
                            KeyValuePair<LineTrackedString, object> onlyEntry = subMap.First();
                            procSingleCommand(onlyEntry.Key.Text);
                            if (!onlyEntry.Key.Text.StartsWith("definemap"))
                            {
                                procAsScript((List<object>)onlyEntry.Value);
                            }
                        }
                    }
                }
                if (valueAtKey is List<object> listAtKey)
                {
                    if (MatchesSet(keyName, script.KnownType.ScriptKeys) || MatchesSet(keyName, AlwaysScriptKeys))
                    {
                        procAsScript(listAtKey);
                    }
                    else if (MatchesSet(keyName, script.KnownType.ListKeys) || MatchesSet(keyName, script.KnownType.ValueKeys) || script.KnownType.Strict)
                    {
                        // ignore
                    }
                    else if (script.KnownType.CanHaveRandomScripts)
                    {
                        procAsScript(listAtKey);
                    }
                }
                else if (valueAtKey is Dictionary<LineTrackedString, object> keyPairMap)
                {
                    string keyText = keyName + ".*";
                    void procSubMaps(Dictionary<LineTrackedString, object> subMap)
                    {
                        foreach (object subValue in subMap.Values)
                        {
                            if (subValue is List<object> listKey)
                            {
                                if (script.KnownType.ScriptKeys.Contains(keyText) || (!script.KnownType.ListKeys.Contains(keyText) && script.KnownType.CanHaveRandomScripts))
                                {
                                    procAsScript(listKey);
                                }
                            }
                            else if (subValue is Dictionary<LineTrackedString, object> mapWithin)
                            {
                                procSubMaps(mapWithin);
                            }
                        }
                    }
                    if (script.KnownType.ValueKeys.Contains(keyText) || script.KnownType.ListKeys.Contains(keyText) || script.KnownType.ScriptKeys.Contains(keyText) || AlwaysScriptKeys.Contains(keyName)
                        || script.KnownType.ValueKeys.Contains("*") || script.KnownType.ListKeys.Contains("*") || script.KnownType.ScriptKeys.Contains("*") || (!script.KnownType.Strict && !keyName.StartsWith("definemap")))
                    {
                        procSubMaps(keyPairMap);
                    }
                }
            }
        }

        /// <summary>Helper for 'StartsWith' over a set of options.</summary>
        public static bool StartsWithAny(string input, params string[] checks)
        {
            return checks.Any(s => input.StartsWith(s));
        }

        /// <summary>Returns the first/best container if it is knowable and known.</summary>
        public ScriptContainerData ContextValidatedGetScriptFor(string scriptName, string requireType)
        {
            if (SurroundingWorkspace is null || scriptName is null)
            {
                return null;
            }
            scriptName = scriptName.ToLowerFast().Before('.');
            if (scriptName.StartsWith("script:"))
            {
                scriptName = scriptName.After(':');
            }
            ScriptContainerData res = null;
            if (scriptName.Contains('<'))
            {
                string partial = scriptName.Before('<');
                res = SurroundingWorkspace.Scripts.FirstOrDefault(k => k.Key.StartsWith(partial) && (requireType is null || k.Value.Type == requireType)).Value;
                if (res is null)
                {
                    res = GeneratedWorkspace.Scripts.FirstOrDefault(k => k.Key.StartsWith(partial) && (requireType is null || k.Value.Type == requireType)).Value;
                }
            }
            else
            {
                res = SurroundingWorkspace.Scripts.GetValueOrDefault(scriptName);
                if (res is null)
                {
                    res = GeneratedWorkspace.Scripts.GetValueOrDefault(scriptName);
                }
                if (res is not null && requireType is not null && res.Type != requireType)
                {
                    return null;
                }
            }
            return res;
        }

        /// <summary>Returns true if the scriptname is null, unknowable, or valid.</summary>
        public bool ContextValidatedIsValidScriptName(string scriptName)
        {
            if (SurroundingWorkspace is null || scriptName is null)
            {
                return true;
            }
            return ContextValidatedGetScriptFor(scriptName, null) is not null;
        }

        /// <summary>Merges generated data together.</summary>
        public void MergeData()
        {
            foreach (ScriptContainerData container in GeneratedWorkspace.Scripts.Values)
            {
                GeneratedWorkspace.AllKnownServerFlagNames.MergeIn(container.ServerFlags);
                GeneratedWorkspace.AllKnownObjectFlagNames.MergeIn(container.ObjectFlags);
            }
        }

        /// <summary>Runs the full script check.</summary>
        public void Run()
        {
            Meta = MetaDocs.CurrentMeta;
            ClearCommentsFromLines();
            CheckYAML();
            LoadInjects();
            BasicLineFormatCheck();
            CheckForTabs();
            CheckForBraces();
            CheckForOldDefs();
            Dictionary<LineTrackedString, object> containers = GatherActualContainers();
            ConvertContainers(containers);
            CheckAllContainers();
            MergeData();
            CollectStatisticInfos();
        }
    }
}
