using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A documented event.
    /// </summary>
    public class MetaEvent : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_EVENT;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => Events[0];

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Events.Add(CleanName, this);
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => CleanEvents;

        /// <summary>
        /// The names of the event.
        /// </summary>
        public string[] Events = new string[0];

        /// <summary>
        /// The names of the events, autocleaned.
        /// </summary>
        public string[] CleanEvents = new string[0];

        /// <summary>
        /// Switches available to the event.
        /// </summary>
        public List<string> Switches = new List<string>();

        /// <summary>
        /// Just the names of the event's switches.
        /// </summary>
        public List<string> SwitchNames = new List<string>();

        /// <summary>
        /// The regex matcher.
        /// </summary>
        public Regex RegexMatcher = null;

        /// <summary>
        /// The trigger reason.
        /// </summary>
        public string Triggers;

        /// <summary>
        /// Context tags. One tag per string.
        /// </summary>
        public string[] Context = new string[0];

        /// <summary>
        /// Determination options. One Determination per string.
        /// </summary>
        public string[] Determinations = new string[0];

        /// <summary>
        /// Whether there's a player attached to the event.
        /// </summary>
        public string Player = "";

        /// <summary>
        /// Whether there's an NPC attached to the event.
        /// </summary>
        public string NPC = "";

        /// <summary>
        /// Whether the event is cancellable.
        /// </summary>
        public bool Cancellable = false;

        /// <summary><see cref="MetaObject.ApplyValue(string, string)"/></summary>
        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "events":
                    Events = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    CleanEvents = Events.Select(s => s.ToLowerFast()).ToArray();
                    HasMultipleNames = Events.Length > 1;
                    return true;
                case "triggers":
                    Triggers = value;
                    return true;
                case "player":
                    Player = value;
                    return true;
                case "npc":
                    NPC = value;
                    return true;
                case "regex":
                    RegexMatcher = new Regex(value, RegexOptions.Compiled);
                    return true;
                case "switch":
                    foreach (string switchLine in value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        Switches.Add(switchLine);
                        SwitchNames.Add(switchLine.Before(" ").Before(":").ToLowerFast());
                    }
                    return true;
                case "context":
                    Context = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "determine":
                    Determinations = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "cancellable":
                    Cancellable = value.Trim().ToLowerFast() == "true";
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, Events[0], Triggers, RegexMatcher);
            PostCheckLinkableText(docs, Triggers);
            foreach (string context in Context)
            {
                PostCheckLinkableText(docs, context);
            }
            foreach (string determine in Determinations)
            {
                PostCheckLinkableText(docs, determine);
            }
        }

        /// <summary><see cref="MetaObject.GetAllSearchableText"/></summary>
        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allEvents = string.Join('\n', Events);
            string allContexts = string.Join('\n', Context);
            string allDeterminations = string.Join('\n', Determinations);
            string regex = RegexMatcher.ToString();
            return $"{baseText}\n{allEvents}\n{Triggers}\n{Player}\n{NPC}\n{regex}\n{allContexts}\n{allDeterminations}";
        }
    }
}
