using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A documented command.</summary>
    public class MetaCommand : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_COMMAND;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => CommandName;

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Commands.Add(CleanName, this);
        }

        /// <summary>The name of the command.</summary>
        public string CommandName;

        /// <summary>How many arguments are required, minimum.</summary>
        public int Required = 0;

        /// <summary>How many arguments are allowed, maximum.</summary>
        public int Maximum = int.MaxValue;

        /// <summary>The syntax guide.</summary>
        public string Syntax;

        /// <summary>The short description.</summary>
        public string Short;

        /// <summary>The long-form description.</summary>
        public string Description;

        /// <summary>Tags documented for this command. One tag per string.</summary>
        public string[] Tags = Array.Empty<string>();

        /// <summary>An associated beginner's guide link.</summary>
        public string Guide = "";

        /// <summary>A list of argument prefixes this command has in its syntax line.</summary>
        public Tuple<string, string>[] ArgPrefixes = Array.Empty<Tuple<string, string>>();

        /// <summary>A list of plaintext no-tag arguments this command has in its syntax line.</summary>
        public Tuple<string, string>[] FlatArguments = Array.Empty<Tuple<string, string>>();

        /// <summary>Parses this command's syntax data to create a list of helper data about the known arguments.</summary>
        public void ParseSyntax()
        {
            int firstSpace = Syntax.IndexOf(' ');
            if (firstSpace < 0)
            {
                ArgPrefixes = Array.Empty<Tuple<string, string>>();
                FlatArguments = Array.Empty<Tuple<string, string>>();
                return;
            }
            List<Tuple<string, string>> prefixes = new List<Tuple<string, string>>();
            List<Tuple<string, string>> flatArgs = new List<Tuple<string, string>>();
            string cleaned = Syntax[firstSpace..].Replace('/', ' ');
            foreach (string arg in cleaned.Split(' '))
            {
                string cleanedArg = arg.Replace('[', '{').Replace(']', '{').Replace('(', '{').Replace(')', '{').Replace('}', '{').Replace("{", "");
                if (string.IsNullOrWhiteSpace(cleanedArg))
                {
                    continue;
                }
                int colonIndex = cleanedArg.IndexOf(':');
                if (colonIndex > 0)
                {
                    string prefix = cleanedArg[..colonIndex];
                    if (!prefix.Contains('<'))
                    {
                        prefixes.Add(new Tuple<string, string>(prefix, arg));
                    }
                }
                else if (!cleanedArg.Contains('<') && !cleanedArg.Contains('|'))
                {
                    flatArgs.Add(new Tuple<string, string>(cleanedArg, arg));
                }
            }
            ArgPrefixes = prefixes.ToArray();
            FlatArguments = flatArgs.ToArray();
        }

        /// <summary>Sample usages.</summary>
        public List<string> Usages = new List<string>();

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "name":
                    CommandName = value;
                    return true;
                case "required":
                    return int.TryParse(value, out Required);
                case "maximum":
                    bool valid = int.TryParse(value, out Maximum);
                    if (Maximum == -1)
                    {
                        Maximum = int.MaxValue;
                    }
                    return valid;
                case "syntax":
                    Syntax = value;
                    return true;
                case "short":
                    Short = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "tags":
                    Tags = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "usage":
                    Usages.Add(value);
                    return true;
                case "guide":
                    Guide = value;
                    return true;
                default:
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Commands);
            Require(docs, Short, Description, Syntax, CommandName);
            ParseSyntax();
            PostCheckTags(docs, Tags);
            PostCheckLinkableText(docs, Description);
        }

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.Strongs.Add(Short);
            SearchHelper.Decents.Add(Description);
            SearchHelper.Backups.AddRange(Usages);
            SearchHelper.Backups.Add(Syntax);
            if (Guide != null)
            {
                SearchHelper.Backups.Add(Guide);
            }
            SearchHelper.Backups.AddRange(Tags);
        }
    }
}
