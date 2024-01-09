using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;
using SharpDenizenTools.ScriptAnalysis;
using FreneticUtilities.FreneticToolkit;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A documented event.</summary>
    public class MetaEvent : MetaObject
    {
        /// <summary>Symbols that are structural in event names and can be hidden.</summary>
        public static AsciiMatcher EventNameCleaner = new("<>'()");

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => Events[0];

        /// <summary><see cref="MetaObject.CleanName"/></summary>
        public override string CleanName => CleanEvents[0];

        /// <summary><see cref="MetaObject.SearchName"/></summary>
        public override string SearchName => OverlyCleanedEvents[0];

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.Events.Add(CleanName, this);
            bool anyLegacy = false;
            foreach (ScriptEventCouldMatcher matcher in CouldMatchers.DistinctBy(m => m.Parts[0]))
            {
                docs.EventLookupOpti.GetOrCreate(matcher.Parts[0], () => new List<MetaEvent>()).Add(this);
                if (matcher.Parts[0].StartsWithFast('<'))
                {
                    anyLegacy = true;
                }
            }
            if (anyLegacy)
            {
                docs.LegacyCouldMatchEvents.Add(this);
            }
        }

        /// <summary><see cref="MetaObject.MultiNames"/></summary>
        public override IEnumerable<string> MultiNames => CleanEvents;

        /// <summary>The names of the event.</summary>
        public string[] Events = [];

        /// <summary>The names of the events, autocleaned.</summary>
        public string[] CleanEvents = [];

        /// <summary>The names of the events, with optionals stripped and other symbols removed.</summary>
        public string[] OverlyCleanedEvents = [];

        /// <summary>Could-Matchers for this event.</summary>
        public ScriptEventCouldMatcher[] CouldMatchers = [];

        /// <summary>Switches available to the event.</summary>
        public List<string> Switches = [];

        /// <summary>Just the names of the event's switches.</summary>
        public HashSet<string> SwitchNames = [];

        /// <summary>The trigger reason.</summary>
        public string Triggers;

        /// <summary>Context tags. One tag per string.</summary>
        public string[] Context = [];

        /// <summary>Determination options. One Determination per string.</summary>
        public string[] Determinations = [];

        /// <summary>Whether there's a player attached to the event.</summary>
        public string Player = "";

        /// <summary>Whether there's an NPC attached to the event.</summary>
        public string NPC = "";

        /// <summary>Whether the event is cancellable.</summary>
        public bool Cancellable = false;

        /// <summary>Whether the event has a location for location switches.</summary>
        public bool HasLocation = false;

        /// <summary>Manual examples of this tag. One full script per entry.</summary>
        public List<string> Examples = [];

        /// <summary>Returns whether the switch name given is valid for this event.</summary>
        public bool IsValidSwitch(string switchName)
        {
            if (SwitchNames.Contains(switchName))
            {
                return true;
            }
            else if (switchName == "flagged" || switchName == "permission")
            {
                return !string.IsNullOrWhiteSpace(Player);
            }
            else if (switchName == "assigned")
            {
                return !string.IsNullOrWhiteSpace(NPC);
            }
            else if (switchName == "in" || switchName == "location_flagged")
            {
                return HasLocation;
            }
            else if (switchName == "cancelled" || switchName == "ignorecancelled")
            {
                return Cancellable;
            }
            else if (Meta.IsInDataValueSet("global_switches", switchName))
            {
                return true;
            }
            return false;
        }

        /// <summary>Removes optional parts of an event, and strips all other symbols.</summary>
        public static string OverCleanEvent(string evt)
        {
            string[] parts = evt.ToLowerFast().SplitFast(' ');
            StringBuilder output = new(evt.Length);
            foreach (string part in parts)
            {
                if (part.StartsWithFast('(') && part.EndsWithFast(')'))
                {
                    continue;
                }
                output.Append(EventNameCleaner.TrimToNonMatches(part)).Append(' ');
            }
            return output.ToString().Trim();
        }

        /// <summary><see cref="MetaObject.ApplyValue(MetaDocs, string, string)"/></summary>
        public override bool ApplyValue(MetaDocs docs, string key, string value)
        {
            switch (key)
            {
                case "events":
                    Events = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    CleanEvents = Events.Select(s => EventNameCleaner.TrimToNonMatches(s.ToLowerFast())).ToArray();
                    OverlyCleanedEvents = Events.Select(OverCleanEvent).ToArray();
                    CouldMatchers = Events.Select(s => EventTools.ParseMatchers(s, docs.Data.KnownValidatorTypes, (s) => docs.LoadErrors.Add(s))).Flatten().ToArray();
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
                case "regex": return true; // TODO: TEMPORARY
#warning temporary: remove after all events are updated
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
                case "location":
                    HasLocation = value.Trim().ToLowerFast() == "true";
                    return true;
                case "example":
                    Examples.Add(value);
                    return true;
                default:
                    return base.ApplyValue(docs, key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            PostCheckSynonyms(docs, docs.Events);
            Require(docs, Events[0], Triggers);
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

        /// <summary><see cref="MetaObject.BuildSearchables"/></summary>
        public override void BuildSearchables()
        {
            base.BuildSearchables();
            SearchHelper.PerfectMatches.AddRange(Events);
            SearchHelper.Strongs.Add(Triggers);
            SearchHelper.Decents.AddRange(Context);
            SearchHelper.Decents.AddRange(Determinations);
            if (NPC != null)
            {
                SearchHelper.Backups.Add("NPC: " + NPC);
            }
            if (Player != null)
            {
                SearchHelper.Backups.Add("Player: " + Player);
            }
        }
    }
}
