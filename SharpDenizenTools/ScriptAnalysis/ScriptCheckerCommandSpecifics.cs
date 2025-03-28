﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using SharpDenizenTools.MetaHandlers;
using SharpDenizenTools.MetaObjects;

namespace SharpDenizenTools.ScriptAnalysis
{
    /// <summary>Helper class to check command-specific things.</summary>
    public static class ScriptCheckerCommandSpecifics
    {
        /// <summary>The details of a single command-checking.</summary>
        public class CommandCheckDetails
        {
            /// <summary>The checker instance.</summary>
            public ScriptChecker Checker;

            /// <summary>The name of the relevant command.</summary>
            public string CommandName;

            /// <summary>The full text of the command line.</summary>
            public string CommandText;

            /// <summary>The number of main arguments.</summary>
            public int ArgCount;

            /// <summary>The command's line number.</summary>
            public int Line;

            /// <summary>The actual arguments.</summary>
            public ScriptChecker.CommandArgument[] Arguments;

            /// <summary>The general backing context of the checking.</summary>
            public ScriptChecker.ScriptCheckContext Context;

            /// <summary>The starting index of the text within the command.</summary>
            public int StartChar;

            /// <summary>The relevant script container.</summary>
            public ScriptContainerData Script;

            /// <summary>Helper to produce a warning by calling <see cref="ScriptChecker.Warn(List{ScriptChecker.ScriptWarning}, int, string, string, int, int)"/>.</summary>
            public void Warn(List<ScriptChecker.ScriptWarning> warningSet, string key, string message, int start, int end)
            {
                Checker.Warn(warningSet, Line, key, message, start, end);
            }

            /// <summary>Helper to produce a warning by calling <see cref="ScriptChecker.Warn(List{ScriptChecker.ScriptWarning}, int, string, string, int, int)"/>, on the full command line.</summary>
            public void Warn(List<ScriptChecker.ScriptWarning> warningSet, string key, string message)
            {
                Checker.Warn(warningSet, Line, key, message, StartChar, StartChar + CommandText.Length);
            }

            /// <summary>Helper to add a definition to the context.</summary>
            public void TrackDefinition(string def)
            {
                Context.Definitions.Add(def.Before('.'));
                if (def.Contains('<'))
                {
                    Context.HasUnknowableDefinitions = true;
                }
            }
        }

        /// <summary>A mapping from command names to checker methods.</summary>
        public static Dictionary<string, Action<CommandCheckDetails>> CommandCheckers = new(256);

        /// <summary>Registers a new command checker.</summary>
        public static void Register(string[] cmdNames, Action<CommandCheckDetails> method)
        {
            foreach (string cmd in cmdNames)
            {
                if (CommandCheckers.TryGetValue(cmd, out Action<CommandCheckDetails> action))
                {
                    method += action;
                    CommandCheckers[cmd] = action;
                }
                CommandCheckers[cmd] = method;
            }
        }

        /// <summary>A set of Bukkit commands that if they appear in an 'execute' script command should receive a warning automatically.</summary>
        public static HashSet<string> BadExecuteCommands =
        [
            // From the vanilla command list
            "advancement", "ban", "banlist", "bossbar", "clear", "clone", "data", "datapack", "deop", "detect", "difficulty", "effect", "enchant", "execute",
            "exp", "experience", "fill", "forceload", "gamemode", "gamerule", "help", "kick", "kill", "list", "locate", "loot", "me", "msg", "op", "pardon",
            "particle", "playsound", "recipe", "reload", "replaceitem", "say", "scoreboard", "seed", "setblock", "setmaxplayers", "setworldspawn",
            "spawnpoint", "spectate", "spreadplayers", "stopsound", "summon", "tag", "team", "teammsg", "teleport", "tell", "tellraw", "testfor",
            "testforblock", "testforblocks", "time", "title", "toggledownfall", "tp", "w", "weather", "whitelist", "worldborder", "worldbuilder", "xp",
            // Based on seen misuses
            "give", "take", "gmc", "gms", "gm", "warp",
            // Obviously never run Denizen or Citizens commands
            "ex", "exs", "denizen", "npc", "trait"
        ];

        /// <summary>Matcher for symbols that may not appear in an argument prefix, including the ':' that separates the prefix from suffix.</summary>
        public static AsciiMatcher PREFIX_FORBIDDEN_SYMBOLS = new("<> :.!");

        /// <summary>Returns true if the argument has a valid non-tagged prefix.</summary>
        public static bool ArgHasPrefix(string arg)
        {
            int first = PREFIX_FORBIDDEN_SYMBOLS.FirstMatchingIndex(arg);
            return first != -1 && arg[first] == ':';
        }

        /// <summary>Relevant command-specific error check impls.</summary>
        static ScriptCheckerCommandSpecifics()
        {
            Register(["if", "waituntil", "while"], (details) =>
            {
                int borkLen = " == true".Length;
                int borkIndex = details.CommandText.IndexOf(" == true");
                if (borkIndex == -1)
                {
                    borkLen = " == false".Length;
                    borkIndex = details.CommandText.IndexOf(" == false");
                }
                if (borkIndex != -1)
                {
                    const string warningMessage = "'== true' style checks are nonsense. Refer to <https://guide.denizenscript.com/guides/troubleshooting/common-mistakes.html#if-true-is-true-equal-to-truly-true-is-the-truth> for more info.";
                    details.Warn(details.Checker.Errors, "truly_true", warningMessage, details.StartChar + borkIndex, details.StartChar + borkIndex + borkLen);
                }
            });
            Register(["adjust"], (details) =>
            {
                static bool argReserved(ScriptChecker.CommandArgument s) => s.Text.StartsWith("def:") || s.Text.StartsWith("if:");
                ScriptChecker.CommandArgument mechanism = details.Arguments.FirstOrDefault(s => ArgHasPrefix(s.Text) && !argReserved(s))
                ?? details.Arguments.FirstOrDefault(s => !argReserved(s) && !s.Text.Contains('<') && !details.Checker.Meta.RawAdjustables.Contains(s.Text));
                if (mechanism is null)
                {
                    if (details.Arguments.Length < 2 || !details.Arguments[1].Text.StartsWith('<') || !details.Arguments[1].Text.EndsWith('>')) // Allow a single tag as 2nd arg as the input, as that would be an adjust by MapTag
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_no_mech", $"Malformed adjust command. No mechanism input given.");
                    }
                }
                else
                {
                    string mechanismName = mechanism.Text.Before(':').ToLowerFast();
                    List<MetaMechanism> possible = [.. MetaDocs.CurrentMeta.Mechanisms.Values.Where(m => m.MechName == mechanismName)];
                    MetaMechanism mech = null;
                    if (possible.Count == 1)
                    {
                        mech = possible[0];
                    }
                    else if (possible.Count > 1)
                    {
                        ScriptChecker.CommandArgument objArg = details.Arguments.FirstOrDefault(s => !ArgHasPrefix(s.Text));
                        if (objArg is null)
                        {
                            mech = possible.First();
                        }
                        else
                        {
                            string rawObj = objArg.Text;
                            mech = possible.FirstOrDefault(m => m.MechObject == rawObj);
                            // TODO: if a tag or "def:", determine possible types and use that
                            if (mech is null)
                            {
                                mech = possible.First();
                            }
                        }
                    }
                    if (mech is null)
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_unknown_mech", $"Malformed adjust command. Mechanism name given is unrecognized.", mechanism.StartChar, mechanism.StartChar + mechanismName.Length);
                    }
                    else if (!string.IsNullOrWhiteSpace(mech.Deprecated))
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_deprecated_mech", $"Mechanism '{mech.Name}' is deprecated: {mech.Deprecated}", mechanism.StartChar, mechanism.StartChar + mechanismName.Length);
                    }
                    ScriptChecker.CommandArgument defArg = details.Arguments.FirstOrDefault(s => s.Text.StartsWith("def:"));
                    if (defArg is not null)
                    {
                        string defName = defArg.Text.After(':').ToLowerFast();
                        if (details.Context is not null && !details.Context.Definitions.Contains(defName) && !details.Context.HasUnknowableDefinitions)
                        {
                            details.Warn(details.Checker.Errors, "bad_adjust_unknown_def", $"Malformed adjust command. Definition name given is unrecognized.", defArg.StartChar, defArg.StartChar + defArg.Text.Length);
                        }
                    }
                }
            });
            Register(["execute"], (details) =>
            {
                if (details.ArgCount >= 2)
                {
                    string bukkitCommandArg = details.Arguments[0].Text.ToLowerFast().StartsWith("as_") ? details.Arguments[1].Text : details.Arguments[0].Text;
                    string bukkitCommandName = bukkitCommandArg.Before(' ').ToLowerFast();
                    if (BadExecuteCommands.Contains(bukkitCommandName) || bukkitCommandName.StartsWith("minecraft:") || bukkitCommandName.StartsWith("bukkit:"))
                    {
                        details.Warn(details.Checker.Warnings, "bad_execute", "Inappropriate usage of the 'execute' command. Execute is for external plugin interop, and should never be used for vanilla commands. Use the relevant Denizen script command or mechanism instead.");
                    }
                }
            });
            Register(["inject"], (details) =>
            {
                details.Context.HasUnknowableDefinitions = true;
                details.Context.HasUnknowableSaveEntries = true;
                string scrName = details.Arguments.Select(a => a.Text.ToLowerFast()).FirstOrDefault(a => a != "instantly" && !a.StartsWith("path:"));
                if (!details.Checker.ContextValidatedIsValidScriptName(scrName))
                {
                    details.Warn(details.Checker.Errors, "invalid_script_inject", $"Script name `{scrName}` is invalid. Cannot be injected.");
                }
            });
            HashSet<string> runOtherArg = ["instant", "instantly", "local", "locally"];
            Register(["run", "runlater"], (details) =>
            {
                string scrName = details.Arguments.Select(a => a.Text.ToLowerFast()).FirstOrDefault(a => !runOtherArg.Contains(a) && !ScriptChecker.StartsWithAny(a, "path:", "id:", "speed:", "delay:", "def:", "def.", "defmap:"));
                if (!details.Checker.ContextValidatedIsValidScriptName(scrName))
                {
                    details.Warn(details.Checker.Errors, "invalid_script_run", $"Script name `{scrName}` is invalid. Cannot be ran.");
                }
            });
            Register(["queue"], (details) =>
            {
                if (details.ArgCount == 1 && (details.Arguments[0].Text.ToLowerFast() == "stop" || details.Arguments[0].Text.ToLowerFast() == "clear"))
                {
                    details.Warn(details.Checker.MinorWarnings, "queue_clear", "Old style 'queue clear'. Use the modern 'stop' command instead. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#stop-is-the-new-queue-clear> for more info.");
                }
            });
            Register(["define", "definemap"], (details) =>
            {
                if (details.ArgCount >= 1)
                {
                    string defName = details.Arguments[0].Text.Before(":").ToLowerFast().Before('.');
                    details.TrackDefinition(defName);
                }
            });
            Register(["foreach", "repeat", "while"], (details) =>
            {
                if (details.CommandName != "while")
                {
                    string asArgument = details.Arguments.FirstOrDefault(s => s.Text.ToLowerFast().StartsWith("as:"))?.Text;
                    if (asArgument == null)
                    {
                        asArgument = "value";
                    }
                    else
                    {
                        asArgument = asArgument["as:".Length..];
                    }
                    details.TrackDefinition(asArgument.ToLowerFast());
                }
                if (details.CommandName != "repeat")
                {
                    details.TrackDefinition("loop_index");
                }
            });
            Register(["foreach"], (details) =>
            {
                string keyArgument = details.Arguments.FirstOrDefault(s => s.Text.ToLowerFast().StartsWith("key:"))?.Text;
                if (keyArgument == null)
                {
                    keyArgument = "key";
                }
                else
                {
                    keyArgument = keyArgument["key:".Length..];
                }
                details.TrackDefinition(keyArgument.ToLowerFast());
            });
            Register(["give"], (details) =>
            {
                if (details.Arguments.Any(a => a.Text == "<player>" || a.Text == "<player.name>" || a.Text == "<npc>"))
                {
                    details.Warn(details.Checker.Warnings, "give_player", "The 'give' will automatically give to the linked player, so you do not need to specify that. To specify a different target, use the 'to:<inventory>' argument.");
                }
                string itemGive = details.Arguments.Select(a => a.Text.ToLowerFast()).FirstOrDefault(a => ScriptChecker.StartsWithAny("quantity:", "unlimit_stack_size", "to:", "t:", "slot:"));
                if (itemGive is not null && itemGive != "xp" && details.Checker.SurroundingWorkspace is not null)
                {
                    string item = itemGive.Before('[');
                    if (!item.Contains('<'))
                    {
                        if (!details.Checker.Meta.Data.Items.Contains(item))
                        {
                            if (details.Checker.ContextValidatedGetScriptFor(item, "item") is null && details.Checker.ContextValidatedGetScriptFor(item, "book") is null)
                            {
                                details.Warn(details.Checker.Errors, "give_invalid_item", $"Item `{item}` in 'give' command is not valid and thus cannot be given.");
                            }
                        }
                    }
                }
            });
            Register(["take"], (details) =>
            {
                if (details.Arguments.Any(a => !a.Text.Contains(':') && a.Text != "money" && a.Text != "xp" && a.Text != "iteminhand" && a.Text != "cursoritem"))
                {
                    details.Warn(details.Checker.MinorWarnings, "take_raw", "The 'take' command should always be used with a standard prefixed take style, like 'take item:my_item_here' or 'take slot:5'.");
                }
            });
            Register(["case"], (details) =>
            {
                if (details.ArgCount == 1 && details.Arguments[0].Text.ToLowerFast().Replace(":", "") == "default")
                {
                    details.Warn(details.Checker.MinorWarnings, "case_default", "'- case default:' is a likely mistake - you probably meant '- default:'");
                }
            });
            Register(["determine"], (details) =>
            {
                if (details.Arguments.Any(arg => arg.Text.ToLowerFast() == "canceled"))
                {
                    details.Warn(details.Checker.MinorWarnings, "typo_cancelled", "'- determine canceled' (one 'L') is a likely mistake - you probably meant '- determine cancelled' (two 'L's)");
                }
            });
        }
    }
}
