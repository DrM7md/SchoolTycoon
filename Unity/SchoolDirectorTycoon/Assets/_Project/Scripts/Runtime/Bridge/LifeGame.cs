#nullable enable
using System;
using System.Collections.Generic;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>The director's settings as the UI edits them; Core validates them.</summary>
    public struct LifeDraft
    {
        public string Building;
        public int Seats, Fee, Teachers, Admins, Start, Periods;
    }

    /// <summary>Holds the current school year and hands the UI plain values. Sends decisions to Core; holds no rules.</summary>
    public sealed class LifeGame
    {
        private LifeState state;

        public LifeGame(uint seed) => state = SchoolLife.New(seed);
        private LifeGame(LifeState loaded) => state = loaded;

        /// <summary>Raised after any change, including the clock moving.</summary>
        public event Action? Changed;
        /// <summary>Raised after a change worth writing to disk; the clock alone does not raise it.</summary>
        public event Action? Committed;
        /// <summary>Raised with a RuleCode name when Core refuses a command.</summary>
        public event Action<string>? Rejected;

        public string Phase => state.Phase.ToString();
        public int Day => state.Day;
        public int TotalDays => SchoolLife.DaysInYear;
        public int Cash => state.Cash;
        public int Students => state.Students;
        public int Reputation => state.Reputation;
        public int Parents => state.Parents;
        public int Staff => state.Staff;
        public int Academics => state.Academics;
        public int Minute => state.Minute;
        public string? PendingEvent => state.PendingEvent;
        public string? PendingLetter => state.PendingLetter;
        public int LetterBonus => state.LetterBonus;
        public int AwayIncome => state.AwayIncome;
        public int Streak => state.Streak;
        public string Ending => state.Ending.ToString();
        public string Grade => SchoolLife.Grade(state).ToString();
        public int LineCount => state.Log.Length;
        public int Week => (Math.Max(1, state.Day) - 1) / SchoolLife.DaysInWeek + 1;
        public int StartingCash => SchoolLife.StartingCash;

        public int TotalPeriods => state.Plan?.Periods ?? 0;

        /// <summary>Current lesson from 1, or 0 during the break.</summary>
        public int Period
        {
            get
            {
                if (state.Plan == null) return 1;
                (int period, _, _) = Slot();
                return period;
            }
        }

        public int MinutesLeft => state.Plan == null ? 0 : Slot().left;
        public float SlotProgress
        {
            get
            {
                if (state.Plan == null) return 0f;
                (_, int left, int length) = Slot();
                return 1f - left / (float)length;
            }
        }

        private (int period, int left, int length) Slot()
        {
            LifePlan plan = state.Plan!;
            int elapsed = state.Minute - plan.StartMinute;
            int morning = SchoolLife.BreakAfterPeriod * SchoolLife.PeriodMinutes;
            if (elapsed >= morning && elapsed < morning + SchoolLife.BreakMinutes)
                return (0, morning + SchoolLife.BreakMinutes - elapsed, SchoolLife.BreakMinutes);
            int offset = elapsed < morning ? elapsed : elapsed - SchoolLife.BreakMinutes;
            int period = Math.Min(offset / SchoolLife.PeriodMinutes + 1, plan.Periods);
            int left = period * SchoolLife.PeriodMinutes - offset;
            return (period, Math.Max(0, left), SchoolLife.PeriodMinutes);
        }

        public static string TimeText(int minute) => $"{minute / 60:00}:{minute % 60:00}";

        public static IReadOnlyList<(string id, int capacity, int rent, bool gym)> Buildings()
        {
            var list = new List<(string, int, int, bool)>();
            foreach (LifeBuilding b in SchoolLife.Buildings) list.Add((b.Id, b.Capacity, b.WeeklyRent, b.HasGym));
            return list;
        }

        public static int Capacity(string building) => SchoolLife.Building(building).Capacity;

        public LifeDraft CurrentDraft()
        {
            LifePlan p = state.Plan ?? new LifePlan("campus", 180, 240, 9, 3, 450, 6);
            return new LifeDraft { Building = p.BuildingId, Seats = p.Seats, Fee = p.Fee, Teachers = p.Teachers, Admins = p.Admins, Start = p.StartMinute, Periods = p.Periods };
        }

        public static LifeDraft Clamp(LifeDraft d)
        {
            d.Seats = Math.Clamp(d.Seats, 10, Capacity(d.Building));
            d.Fee = Math.Clamp(d.Fee, SchoolLife.MinFee, SchoolLife.MaxFee);
            d.Teachers = Math.Clamp(d.Teachers, SchoolLife.MinStaff, SchoolLife.MaxTeachers);
            d.Admins = Math.Clamp(d.Admins, SchoolLife.MinStaff, SchoolLife.MaxAdmins);
            d.Start = Math.Clamp(d.Start, SchoolLife.MinStart, SchoolLife.MaxStart);
            d.Periods = Math.Clamp(d.Periods, SchoolLife.MinPeriods, SchoolLife.MaxPeriods);
            return d;
        }

        private static LifePlan ToPlan(LifeDraft d) => new LifePlan(d.Building, d.Seats, d.Fee, d.Teachers, d.Admins, d.Start, d.Periods);

        public static int OpeningCost(LifeDraft d) => SchoolLife.OpeningCost(ToPlan(d));
        public static int WeeklyBill(LifeDraft d) => SchoolLife.WeeklyBill(ToPlan(d), SchoolLife.ExpectedStudents(ToPlan(d)));
        public static int ExpectedStudents(LifeDraft d) => SchoolLife.ExpectedStudents(ToPlan(d));

        public IReadOnlyList<string> PendingChoices()
        {
            if (state.PendingEvent == null) return Array.Empty<string>();
            var ids = new List<string>();
            foreach (ChronicleChoice choice in ChronicleDeck.Find(state.PendingEvent).Choices) ids.Add(choice.Id);
            return ids;
        }

        public IReadOnlyList<string> LetterChoices() =>
            state.PendingLetter == null ? Array.Empty<string>() : SchoolLife.LetterChoices(state.PendingLetter);

        public IReadOnlyList<string> Activities() => SchoolLife.Activities(state);
        public int ActivityEstimate(string id) => SchoolLife.ActivityEstimate(state, id);

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

        public void Open(LifeDraft draft) => Run(() => state = SchoolLife.Open(state, ToPlan(draft)));
        public void Adjust(LifeDraft draft) => Run(() => state = SchoolLife.Adjust(state, ToPlan(draft)));
        public void Choose(string id) => Run(() => state = SchoolLife.Choose(state, id));
        public void Evening(string id) => Run(() => state = SchoolLife.Evening(state, id));
        public void ReadLetter(string? choice) => Run(() => state = SchoolLife.ReadLetter(state, choice));
        public void Greet(int realDay) => Run(() => state = SchoolLife.Greet(state, realDay));

        public void Advance(int minutes)
        {
            bool keep = false;
            try
            {
                LifeState next = SchoolLife.Advance(state, minutes);
                keep = next.Phase != state.Phase || next.PendingEvent != state.PendingEvent;
                state = next;
            }
            catch (RuleException error) { Rejected?.Invoke(error.Code.ToString()); return; }
            Changed?.Invoke();
            if (keep) Committed?.Invoke();
        }

        private void Run(Action command)
        {
            LifeState before = state;
            try { command(); }
            catch (RuleException error) { Rejected?.Invoke(error.Code.ToString()); return; }
            if (ReferenceEquals(before, state)) return;
            Changed?.Invoke();
            Committed?.Invoke();
        }

        public string Serialize() => LifeSave.Write(state);

        /// <summary>Throws <see cref="FormatException"/> when the text is not a readable save.</summary>
        public static LifeGame Deserialize(string text) => new LifeGame(LifeSave.Read(text));

        /// <summary>Every localization key this mode can produce beyond the shared event deck, for coverage tests.</summary>
        public static IEnumerable<string> AllKeys()
        {
            foreach (LifeBuilding b in SchoolLife.Buildings) { yield return "life.building." + b.Id; yield return "life.building." + b.Id + ".info"; }
            foreach (string id in SchoolLife.ActivityIds) { yield return "life.act." + id; yield return "life.act." + id + ".ok"; }
            yield return "life.act.gym_night.fail";
            for (int i = 0; i < SchoolLife.QuietLines; i++) yield return "life.quiet." + i;
            for (int i = 0; i < SchoolLife.PlainLetters; i++) yield return "life.letter.plain." + i;
            foreach (string arc in SchoolLife.Arcs)
            {
                for (int step = 0; step < SchoolLife.ArcLetters - 1; step++) yield return $"life.letter.{arc}.{step}";
                foreach (string answer in new[] { "a", "b" })
                {
                    yield return $"life.letter.{arc}.2.{answer}";
                    yield return $"life.letter.{arc}.2.opt.{answer}";
                    yield return $"life.letter.{arc}.4.{answer}";
                }
            }
            yield return "life.opening";
            yield return "life.week";
            yield return "life.adjusted";
            foreach (string code in Enum.GetNames(typeof(RuleCode)))
                if (code.StartsWith("Life", StringComparison.Ordinal)) yield return "rule." + code;
        }
    }
}
