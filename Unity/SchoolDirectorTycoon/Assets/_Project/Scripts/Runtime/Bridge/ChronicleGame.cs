#nullable enable
using System;
using System.Collections.Generic;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>One line of the story for the UI: a localization key plus named, non-zero changes.</summary>
    public readonly struct ChronicleLine
    {
        public ChronicleLine(int week, string key, IReadOnlyList<KeyValuePair<string, int>> changes)
        {
            Week = week;
            Key = key;
            Changes = changes;
        }

        public int Week { get; }
        public string Key { get; }
        public IReadOnlyList<KeyValuePair<string, int>> Changes { get; }
    }

    /// <summary>Holds the current chronicle year and hands the UI plain values. Sends decisions to Core; holds no rules.</summary>
    public sealed class ChronicleGame
    {
        private ChronicleState state;

        public ChronicleGame(uint seed) => state = Chronicle.New(seed);

        public event Action? Changed;
        /// <summary>Raised with a RuleCode name when Core refuses a command.</summary>
        public event Action<string>? Rejected;

        public int Week => state.Week;
        public int TotalWeeks => Chronicle.WeeksInYear;
        public int Cash => state.Cash;
        public int Students => state.Students;
        public int Reputation => state.Reputation;
        public int Parents => state.Parents;
        public int Staff => state.Staff;
        public int Academics => state.Academics;
        public string? PendingEvent => state.PendingEvent;
        /// <summary>"InProgress", "YearCompleted", "Bankrupt", "Dismissed" or "StaffWalkout".</summary>
        public string Ending => state.Ending.ToString();
        public string Grade => Chronicle.Grade(state).ToString();
        public int LineCount => state.Log.Length;

        public IReadOnlyList<string> PendingChoices()
        {
            if (state.PendingEvent == null) return Array.Empty<string>();
            var ids = new List<string>();
            foreach (ChronicleChoice choice in ChronicleDeck.Find(state.PendingEvent).Choices) ids.Add(choice.Id);
            return ids;
        }

        public ChronicleLine Line(int index)
        {
            ChronicleEntry entry = state.Log[index];
            ChronicleEffect e = entry.Effect;
            var changes = new List<KeyValuePair<string, int>>();
            void Add(string name, int value) { if (value != 0) changes.Add(new KeyValuePair<string, int>(name, value)); }
            Add("Cash", e.Cash);
            Add("Students", e.Students);
            Add("Reputation", e.Reputation);
            Add("Parents", e.Parents);
            Add("Staff", e.Staff);
            Add("Academics", e.Academics);
            return new ChronicleLine(entry.Week, entry.Key, changes);
        }

        public void NextWeek() => Run(() => state = Chronicle.NextWeek(state));
        public void Choose(string choiceId) => Run(() => state = Chronicle.Choose(state, choiceId));

        private void Run(Action command)
        {
            try { command(); }
            catch (RuleException error) { Rejected?.Invoke(error.Code.ToString()); return; }
            Changed?.Invoke();
        }

        /// <summary>Every localization key the deck can produce, for coverage tests.</summary>
        public static IEnumerable<string> AllEventKeys()
        {
            foreach (ChronicleEvent e in ChronicleDeck.All)
            {
                yield return $"ev.{e.Id}";
                foreach (ChronicleChoice c in e.Choices)
                {
                    yield return $"ev.{e.Id}.{c.Id}";
                    yield return $"ev.{e.Id}.{c.Id}.ok";
                    if (c.Failure != null) yield return $"ev.{e.Id}.{c.Id}.fail";
                }
            }
        }
    }
}
