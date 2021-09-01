using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreneticUtilities.FreneticExtensions;
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
                if (def.Contains("<"))
                {
                    Context.HasUnknowableDefinitions = true;
                }
            }
        }

        /// <summary>A mapping from command names to checker methods.</summary>
        public static Dictionary<string, Action<CommandCheckDetails>> CommandCheckers;

        /// <summary>Registers a new command checker.</summary>
        public static void Register(string[] cmdNames, Action<CommandCheckDetails> method)
        {
            foreach (string cmd in cmdNames)
            {
                CommandCheckers.Add(cmd, method);
            }
        }

        /// <summary>A set of Bukkit commands that if they appear in an 'execute' script command should receive a warning automatically.</summary>
        public static HashSet<string> BadExecuteCommands = new HashSet<string>()
        {
            // From the vanilla command list
            "advancement", "ban", "banlist", "bossbar", "clear", "clone", "data", "datapack", "deop", "detect", "difficulty", "effect", "enchant", "execute",
            "exp", "experience", "fill", "forceload", "gamemode", "gamerule", "help", "kick", "kill", "list", "locate", "loot", "me", "msg", "op", "pardon",
            "particle", "playsound", "recipe", "reload", "replaceitem", "say", "scoreboard", "seed", "setblock", "setmaxplayers", "setworldspawn",
            "spawnpoint", "spectate", "spreadplayers", "stopsound", "summon", "tag", "team", "teammsg", "teleport", "tell", "tellraw", "testfor",
            "testforblock", "testforblocks", "time", "title", "toggledownfall", "tp", "w", "weather", "whitelist", "worldborder", "worldbuilder", "xp",
            // Based on seen misuses
            "give", "take", "gmc", "gms", "gm", "warp",
            // Obviously never run Denizen or Citizens commands
            "ex", "denizen", "npc", "trait"
        };

        /// <summary>Relevant command-specific error check impls.</summary>
        static ScriptCheckerCommandSpecifics()
        {
            Register(new[] { "if", "waituntil", "while" }, (details) =>
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
                    details.Warn(details.Checker.Errors, "truly_true", warningMessage, borkIndex, borkIndex + borkLen);
                }
            });
            Register(new[] { "adjust" }, (details) =>
            {
                ScriptChecker.CommandArgument mechanism = details.Arguments.FirstOrDefault(s => s.Text.Contains(":") && !s.Text.StartsWith("def:")) ?? details.Arguments.FirstOrDefault(s => !s.Text.Contains("<") && s.Text != "server");
                if (mechanism == null)
                {
                    if (details.Arguments.Length < 2 || !details.Arguments[1].Text.StartsWith('<') || !details.Arguments[1].Text.EndsWith('>')) // Allow a single tag as 2nd arg as the input, as that would be an adjust by MapTag
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_no_mech", $"Malformed adjust command. No mechanism input given.");
                    }
                }
                else
                {
                    string mechanismName = mechanism.Text.Before(':').ToLowerFast();
                    MetaMechanism mech = MetaDocs.CurrentMeta.Mechanisms.Values.FirstOrDefault(mech => mech.MechName == mechanismName);
                    if (mech is null)
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_unknown_mech", $"Malformed adjust command. Mechanism name given is unrecognized.", mechanism.StartChar, mechanism.StartChar + mechanismName.Length);
                    }
                    else if (!string.IsNullOrWhiteSpace(mech.Deprecated))
                    {
                        details.Warn(details.Checker.Errors, "bad_adjust_deprecated_mech", $"Mechanism '{mech.Name}' is deprecated: {mech.Deprecated}", mechanism.StartChar, mechanism.StartChar + mechanismName.Length);
                    }
                }
            });
            Register(new[] { "execute" }, (details) =>
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
            Register(new[] { "inject" }, (details) =>
            {
                details.Context.HasUnknowableDefinitions = true;
                details.Context.HasUnknowableSaveEntries = true;
            });
            Register(new[] { "queue" }, (details) =>
            {
                if (details.ArgCount == 1 && (details.Arguments[0].Text.ToLowerFast() == "stop" || details.Arguments[0].Text.ToLowerFast() == "clear"))
                {
                    details.Warn(details.Checker.MinorWarnings, "queue_clear", "Old style 'queue clear'. Use the modern 'stop' command instead. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#stop-is-the-new-queue-clear> for more info.");
                }
            });
            Register(new[] { "define", "definemap" }, (details) =>
            {
                if (details.ArgCount >= 1)
                {
                    string defName = details.Arguments[0].Text.Before(":").ToLowerFast().Before('.');
                    details.TrackDefinition(defName);
                }
            });
            Register(new[] { "foreach", "repeat", "while" }, (details) =>
            {
                if (details.ArgCount >= 1)
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
                    if (details.CommandName != "repeat")
                    {
                        details.TrackDefinition("loop_index");
                    }
                    if (details.CommandName == "foreach")
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
                    }
                }
            });
            Register(new[] { "give" }, (details) =>
            {
                if (details.Arguments.Any(a => a.Text == "<player>" || a.Text == "<player.name>" || a.Text == "<npc>"))
                {
                    details.Warn(details.Checker.Warnings, "give_player", "The 'give' will automatically give to the linked player, so you do not need to specify that. To specify a different target, use the 'to:<inventory>' argument.");
                }
            });
            Register(new[] { "take" }, (details) =>
            {
                if (details.Arguments.Any(a => !a.Text.Contains(':') && a.Text != "money" && a.Text != "xp" && a.Text != "iteminhand" && a.Text != "cursoritem"))
                {
                    details.Warn(details.Checker.MinorWarnings, "take_raw", "The 'take' command should always be used with a standard prefixed take style, like 'take scriptname:myitem' or 'take material:stone'.");
                }
            });
            Register(new[] { "case" }, (details) =>
            {
                if (details.ArgCount == 1 && details.Arguments[0].Text.ToLowerFast().Replace(":", "") == "default")
                {
                    details.Warn(details.Checker.MinorWarnings, "case_default", "'- case default:' is a likely mistake - you probably meant '- default:'");
                }
            });
            Register(new[] { "determine" }, (details) =>
            {
                if (details.Arguments.Any(arg => arg.Text.ToLowerFast() == "canceled"))
                {
                    details.Warn(details.Checker.MinorWarnings, "typo_cancelled", "'- determine canceled' (one 'L') is a likely mistake - you probably meant '- determine cancelled' (two 'L's)");
                }
            });
        }
    }
}
