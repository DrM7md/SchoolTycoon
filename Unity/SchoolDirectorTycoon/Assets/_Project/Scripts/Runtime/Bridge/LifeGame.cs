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
        public int Seats, Fee, Teachers, Admins, Start, Periods, PeriodMinutes, BreakMinutes;
        public int SchoolName, Crest, Colour;
        public string Philosophy;
        public string? Scenario;
    }

    /// <summary>One log line as plain values. Name is a life.name index or -1; Subject is a life.subject id or null.</summary>
    public readonly struct LifeLine
    {
        public LifeLine(int day, string key, IReadOnlyList<KeyValuePair<string, int>> changes, int name, string? subject)
        {
            Day = day;
            Key = key;
            Changes = changes;
            Name = name;
            Subject = subject;
        }

        public int Day { get; }
        public string Key { get; }
        public IReadOnlyList<KeyValuePair<string, int>> Changes { get; }
        public int Name { get; }
        public string? Subject { get; }
    }

    /// <summary>Holds the current school year and hands the UI plain values. Sends decisions to Core; holds no rules.</summary>
    public sealed class LifeGame
    {
        private LifeState state;

        public LifeGame(uint seed) => state = SchoolLife.New(seed);

        private CareerState career = Career.New();
        private string? careerPath;
        private readonly List<(string kind, string id, int value)> ownCelebrations = new List<(string, string, int)>();
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
        public string? PendingLetter => state.PendingLetter;
        public int LetterBonus => state.LetterBonus;
        public int AwayIncome => state.AwayIncome;
        public int Streak => state.Streak;
        public string Ending => state.Ending.ToString();
        public string Grade => SchoolLife.Grade(state).ToString();
        public int LineCount => state.Log.Length;
        public int Week => (Math.Max(1, state.Day) - 1) / SchoolLife.DaysInWeek + 1;
        public int StartingCash => Career.StartingCash(career.Level);
        public string? Season => state.Season;
        public int Rank => SchoolLife.Rank(state);
        public int DayExtra => state.DayExtra;
        public int DayEndMinute => state.Plan == null ? 0 : SchoolLife.DayEnd(state);
        public int SeatCapacity => state.Plan == null ? 0 : SchoolLife.Capacity(state);
        public int InterviewCost => SchoolLife.InterviewCost;
        public int HiringCost => SchoolLife.HiringCost;
        public int LoanAmount => SchoolLife.LoanAmount;
        public int LoanPayment => SchoolLife.LoanPayment;
        public int LoanWeeks => state.LoanWeeks;
        public int CoursesFinished => state.CoursesFinished;
        public string? GoalId => state.GoalId;
        public bool GoalDone => state.GoalDone;
        public int GoalReward => SchoolLife.GoalReward;
        public int TrainingSlots => state.Plan == null ? 0 : SchoolLife.TrainingSlotCount(state);
        public string? BuildingJob => state.BuildingJob;
        public int BuildingRemaining => state.BuildingJob == null ? 0 : Math.Max(0, state.BuildingEnd - state.LastNow);
        public bool InterviewAsked => state.InterviewAsked;
        public string? InterviewReply => state.InterviewReply;

        public int SchoolName => (state.Identity ?? SchoolLife.DefaultIdentity).Name;
        public int Crest => (state.Identity ?? SchoolLife.DefaultIdentity).Crest;
        public int Colour => (state.Identity ?? SchoolLife.DefaultIdentity).Colour;
        public string Philosophy => (state.Identity ?? SchoolLife.DefaultIdentity).Philosophy;

        public static int SchoolNames => SchoolLife.SchoolNames;
        public static int Crests => SchoolLife.Crests;
        public static int Colours => SchoolLife.Colours;
        public static IReadOnlyList<string> Philosophies => SchoolLife.Philosophies;
        public static IReadOnlyList<string> AchievementIds => SchoolLife.AchievementIds;
        public static IReadOnlyList<string> InterviewQuestions => SchoolLife.InterviewQuestions;
        public static IReadOnlyList<string> ActionIds => SchoolLife.ActionIds;

        // The one decision card

        public bool HasCard => state.PendingCard != null;
        public string? CardKind => state.PendingCard?.Kind;
        public string? CardId => state.PendingCard?.Id;
        public string? CardBodyKey => state.PendingCard?.BodyKey;
        public int CardName => state.PendingCard?.Name ?? -1;

        /// <summary>The card's choices as (id, button key) in display order; empty when no card is waiting.</summary>
        public IReadOnlyList<(string id, string buttonKey)> CardChoices()
        {
            LifeCard? card = state.PendingCard;
            var list = new List<(string, string)>();
            if (card == null) return list;
            for (int i = 0; i < card.Choices.Length; i++) list.Add((card.Choices[i], card.ButtonKeys[i]));
            return list;
        }

        // Clock

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
                return length <= 0 ? 1f : 1f - left / (float)length;
            }
        }

        private (int period, int left, int length) Slot()
        {
            LifePlan plan = state.Plan!;
            int elapsed = state.Minute - plan.StartMinute;
            int morning = SchoolLife.BreakAfterPeriod * plan.PeriodMinutes;
            if (elapsed >= morning && elapsed < morning + plan.BreakMinutes)
                return (0, morning + plan.BreakMinutes - elapsed, plan.BreakMinutes);
            int planned = plan.Periods * plan.PeriodMinutes + plan.BreakMinutes;
            int untilBell = Math.Max(0, SchoolLife.DayEnd(state) - state.Minute);
            // The extended part of today, after the planned last lesson.
            if (elapsed >= planned) return (plan.Periods, untilBell, Math.Max(1, state.DayExtra));
            int offset = elapsed < morning ? elapsed : elapsed - plan.BreakMinutes;
            int period = Math.Min(offset / plan.PeriodMinutes + 1, plan.Periods);
            int left = period * plan.PeriodMinutes - offset;
            return (period, Math.Min(Math.Max(0, left), untilBell), plan.PeriodMinutes);
        }

        public static string TimeText(int minute) => $"{minute / 60:00}:{minute % 60:00}";

        // Setup and settings

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
            LifeIdentity i = state.Identity ?? SchoolLife.DefaultIdentity;
            return new LifeDraft
            {
                Building = p.BuildingId, Seats = p.Seats, Fee = p.Fee, Teachers = p.Teachers, Admins = p.Admins, Start = p.StartMinute, Periods = p.Periods,
                PeriodMinutes = p.PeriodMinutes, BreakMinutes = p.BreakMinutes, SchoolName = i.Name, Crest = i.Crest, Colour = i.Colour, Philosophy = i.Philosophy, Scenario = state.Scenario,
            };
        }

        /// <summary>Keeps a draft within the form's limits. Pass capacity once the school is open; a finished wing raises it.</summary>
        public static LifeDraft Clamp(LifeDraft d, int capacity = 0)
        {
            string scenario = ScenarioOf(d);
            d.Scenario = scenario;
            if (scenario == "village") d.Building = "villa";
            d.Seats = Math.Clamp(d.Seats, 10, capacity > 0 ? capacity : SchoolLife.ScenarioCapacity(scenario, SchoolLife.Building(d.Building)));
            d.Fee = Math.Clamp(d.Fee, SchoolLife.MinFee, SchoolLife.ScenarioFeeCap(scenario));
            d.Teachers = Math.Clamp(d.Teachers, SchoolLife.MinStaff, SchoolLife.MaxTeachers);
            d.Admins = Math.Clamp(d.Admins, SchoolLife.MinStaff, SchoolLife.MaxAdmins);
            d.Start = Math.Clamp(d.Start, SchoolLife.MinStart, SchoolLife.MaxStart);
            d.Periods = Math.Clamp(d.Periods, SchoolLife.MinPeriods, SchoolLife.MaxPeriods);
            d.PeriodMinutes = Math.Clamp(d.PeriodMinutes, SchoolLife.MinPeriodMinutes, SchoolLife.MaxPeriodMinutes);
            d.BreakMinutes = Math.Clamp(d.BreakMinutes, SchoolLife.MinBreakMinutes, SchoolLife.MaxBreakMinutes);
            d.SchoolName = Cycle(d.SchoolName, SchoolLife.SchoolNames);
            d.Crest = Cycle(d.Crest, SchoolLife.Crests);
            d.Colour = Cycle(d.Colour, SchoolLife.Colours);
            if (scenario == "girls" && d.Philosophy == "sports") d.Philosophy = "strict";
            return d;
        }

        private static int Cycle(int value, int count) => ((value % count) + count) % count;

        private static LifePlan ToPlan(LifeDraft d) => new LifePlan(d.Building, d.Seats, d.Fee, d.Teachers, d.Admins, d.Start, d.Periods, d.PeriodMinutes, d.BreakMinutes);
        private static LifeIdentity ToIdentity(LifeDraft d) => new LifeIdentity(d.SchoolName, d.Crest, d.Colour, d.Philosophy);

        public static int OpeningCost(LifeDraft d) => SchoolLife.OpeningCost(ToPlan(d), ScenarioOf(d));

        private static string ScenarioOf(LifeDraft d) => string.IsNullOrEmpty(d.Scenario) ? "standard" : d.Scenario!;

        /// <summary>What Core would make of this draft on opening day, so estimates include the scenario's rules. Null for a plan it refuses.</summary>
        private static LifeState? Preview(LifeDraft d)
        {
            try { return SchoolLife.Open(SchoolLife.New(7u), ToPlan(d), new LifeStart(SchoolLife.DefaultIdentity, ScenarioOf(d), 10000000, Career.MaxLevel)); }
            catch (RuleException) { return null; }
        }

        public static int WeeklyBill(LifeDraft d)
        {
            LifeState? opened = Preview(d);
            return opened == null ? 0 : SchoolLife.WeeklyBill(opened);
        }

        public static int ExpectedStudents(LifeDraft d) => Preview(d)?.Students ?? 0;

        /// <summary>The expected intake if this draft applied to the running school.</summary>
        public int SettingsStudents(LifeDraft d) =>
            state.Plan == null ? ExpectedStudents(d) : Math.Min(d.Seats, SchoolLife.CurrentDemand(state with { Plan = ToPlan(d) }));

        /// <summary>The real weekly bill if this draft applied, with the actual payroll and any loan.</summary>
        public int SettingsBill(LifeDraft d) =>
            state.Plan == null ? WeeklyBill(d) : SchoolLife.WeeklyBill(state with { Plan = ToPlan(d), Students = SettingsStudents(d) });

        // Lists for the screens

        public IReadOnlyList<string> LetterChoices() =>
            state.PendingLetter == null ? Array.Empty<string>() : SchoolLife.LetterChoices(state.PendingLetter);

        public IReadOnlyList<string> Activities() => SchoolLife.Activities(state);
        public int ActivityEstimate(string id) => SchoolLife.ActivityEstimate(state, id);

        /// <summary>Slot is gate, broadcast or observe.</summary>
        public bool ActionUsed(string slot) => state.ActionsToday.Contains(slot);

        public IReadOnlyList<(int id, int name, string subject, int skill, int mood, string? trait, int pay, int remaining, string? course, string? role)> Roster()
        {
            var list = new List<(int, int, string, int, int, string?, int, int, string?, string?)>();
            foreach (LifeTeacher t in state.Roster)
                list.Add((t.Id, t.Name, t.Subject, t.Skill, t.Mood, t.TraitKnown ? t.Trait : null, t.WeeklyPay, t.TrainingEnd == 0 ? 0 : Math.Max(0, t.TrainingEnd - state.LastNow), t.Course, t.Role));
            return list;
        }

        public IReadOnlyList<(int id, int name, int marks, int conduct)> Followed()
        {
            var list = new List<(int, int, int, int)>();
            foreach (LifeStudent s in state.Followed) list.Add((s.Id, s.Name, s.Marks, s.Conduct));
            return list;
        }

        public IReadOnlyList<(int id, int name, string subject, string band, string? trait, int pay)> Candidates()
        {
            var list = new List<(int, int, string, string, string?, int)>();
            foreach (LifeCandidate c in state.Candidates) list.Add((c.Id, c.Name, c.Subject, c.ApparentSkill, c.TraitRevealed ? c.Trait : null, c.Pay));
            return list;
        }

        public IReadOnlyList<(string id, int cost, int minutes, bool done, bool building, bool locked)> Upgrades()
        {
            var list = new List<(string, int, int, bool, bool, bool)>();
            foreach (LifeUpgrade u in SchoolLife.Upgrades)
                list.Add((u.Id, u.Cost, u.RealMinutes, state.UpgradesDone.Contains(u.Id), state.BuildingJob == u.Id, u.Requires != null && !state.UpgradesDone.Contains(u.Requires)));
            return list;
        }

        public IReadOnlyList<(string id, int cost, int minutes)> Courses()
        {
            var list = new List<(string, int, int)>();
            foreach (LifeCourse c in SchoolLife.Courses) list.Add((c.Id, c.Cost, c.RealMinutes));
            return list;
        }

        public bool Earned(string achievement) => state.Achievements.Contains(achievement);
        public int MemoryCount => state.Memories.Length;

        /// <summary>The remembered log lines, newest first.</summary>
        public IReadOnlyList<LifeLine> Memories()
        {
            var list = new List<LifeLine>();
            for (int i = state.Memories.Length - 1; i >= 0; i--) list.Add(Line(state.Memories[i]));
            return list;
        }

        public LifeLine Line(int index)
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
            return new LifeLine(entry.Week, entry.Key, changes, entry.Name, entry.Subject);
        }

        // Commands

        public void Open(LifeDraft draft) => Run(() => state = SchoolLife.Open(state, ToPlan(draft), Career.Start(career, ToIdentity(draft), ScenarioOf(draft))));
        public void Adjust(LifeDraft draft) => Run(() => state = SchoolLife.Adjust(state, ToPlan(draft)));
        public void ResolveCard(string id) => Run(() => state = SchoolLife.ResolveCard(state, id));
        public void Evening(string id) => Run(() => state = SchoolLife.Evening(state, id));
        public void ReadLetter(string? choice) => Run(() => state = SchoolLife.ReadLetter(state, choice));
        public void Greet(int realDay) => Run(() => state = SchoolLife.Greet(state, realDay));
        public void ExtendDay(int delta) => Run(() => state = SchoolLife.ExtendDay(state, delta));
        public void DirectorAction(string id) => Run(() => state = SchoolLife.DirectorAction(state, id));
        public void Observe(int teacherId) => DirectorAction("observe:" + teacherId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public void StartInterview() => Run(() => state = SchoolLife.StartInterview(state));
        public void AskCandidate(int id, string question) => Run(() => state = SchoolLife.AskCandidate(state, id, question));
        public void HireCandidate(int id) => Run(() => state = SchoolLife.HireCandidate(state, id));
        public void CancelInterview() => Run(() => state = SchoolLife.CancelInterview(state));
        public void DismissTeacher(int id) => Run(() => state = SchoolLife.DismissTeacher(state, id));
        public void StartUpgrade(string id, int now) => Run(() => state = SchoolLife.StartUpgrade(state, id, now));
        public void StartTraining(int teacherId, string course, int now) => Run(() => state = SchoolLife.StartTraining(state, teacherId, course, now));
        public void TakeLoan() => Run(() => state = SchoolLife.TakeLoan(state, SchoolLife.LoanAmount));

        /// <summary>Finishes real-time jobs that are due. Only a finished job is worth a save.</summary>
        public void Tick(int now)
        {
            LifeState next = SchoolLife.Tick(state, now);
            if (ReferenceEquals(next, state)) return;
            bool keep = next.Log.Length != state.Log.Length;
            state = next;
            BankRun();
            Changed?.Invoke();
            if (keep) Committed?.Invoke();
        }

        public void Advance(int minutes)
        {
            bool keep = false;
            try
            {
                LifeState next = SchoolLife.Advance(state, minutes);
                keep = next.Phase != state.Phase || (next.PendingCard == null) != (state.PendingCard == null);
                state = next;
            }
            catch (RuleException error) { Rejected?.Invoke(error.Code.ToString()); return; }
            BankRun();
            Changed?.Invoke();
            if (keep) Committed?.Invoke();
        }

        private void Run(Action command)
        {
            LifeState before = state;
            try { command(); }
            catch (RuleException error) { Rejected?.Invoke(error.Code.ToString()); return; }
            if (ReferenceEquals(before, state)) return;
            BankRun();
            Changed?.Invoke();
            Committed?.Invoke();
        }

        // School Life v3: career, the personal bars, celebrations and the new cards

        public static IReadOnlyList<string> Scenarios => SchoolLife.Scenarios;
        public static IReadOnlyList<string> TitleIds => SchoolLife.TitleIds;
        public static IReadOnlyList<string> Subjects => SchoolLife.Subjects;

        public int CareerLevel => career.Level;
        public int CareerXp => career.Xp;
        /// <summary>Total XP the next level needs, or -1 at the top level.</summary>
        public int NextLevelXp => Career.NextThreshold(career.Level);
        public int XpGained => Career.XpFor(state);
        public int Stars => state.Plan == null ? 1 : SchoolLife.Stars(state);
        public string ConscienceBand => SchoolLife.ConscienceBand(state);
        public string ConscienceEnding => SchoolLife.ConscienceEnding(state);
        public int Energy => state.Energy;
        public int Family => state.Family;
        public string Title => SchoolLife.TitleOf(state);
        public IReadOnlyList<string> Morning => state.Morning;
        public string? ChallengeId => state.ChallengeId;
        public bool ChallengeDone => state.ChallengeDone;
        public int ChallengeReward => SchoolLife.ChallengeReward;

        /// <summary>The verdict of the judged challenge: won, lost, or null while it is still running.</summary>
        public string? ChallengeVerdict
        {
            get
            {
                if (!state.ChallengeDone) return null;
                for (int i = state.Log.Length - 1; i >= 0; i--)
                {
                    if (state.Log[i].Key == "life.challenge.won") return "won";
                    if (state.Log[i].Key == "life.challenge.lost") return "lost";
                }
                return null;
            }
        }

        public int ReportDay => state.LastDay?.Day ?? -1;
        public int ReportCash => state.LastDay?.CashDelta ?? 0;
        public int ReportBestLine => state.LastDay?.BestLog ?? -1;
        public string ReportTease => state.LastDay?.TeaseKey ?? "";
        public IReadOnlyList<KeyValuePair<string, int>> ReportChanges()
        {
            var list = new List<KeyValuePair<string, int>>();
            LifeDayReport? r = state.LastDay;
            if (r == null) return list;
            void Add(string name, int value) { if (value != 0) list.Add(new KeyValuePair<string, int>(name, value)); }
            Add("Reputation", r.Rep);
            Add("Parents", r.Parents);
            Add("Staff", r.Staff);
            Add("Academics", r.Acad);
            return list;
        }

        public IReadOnlyList<(int month, int cash, int rep, int parents, int staff, int acad, int students, string grade)> MonthCards()
        {
            var list = new List<(int, int, int, int, int, int, int, string)>();
            foreach (LifeMonth m in state.MonthCards) list.Add((m.Month, m.Cash, m.Rep, m.Parents, m.Staff, m.Acad, m.Students, m.Grade));
            return list;
        }

        /// <summary>Cash the given answer to the waiting card costs; 0 for a free answer.</summary>
        public int CardCost(string choice) => state.PendingCard == null ? 0 : SchoolLife.CardCost(state, choice);

        public IReadOnlyList<(int day, int sender, string key, int name)> Chat(string group)
        {
            var list = new List<(int, int, string, int)>();
            foreach (LifeChat c in state.Chat) if (c.Group == group) list.Add((c.Day, c.Sender, c.Key, c.Name));
            return list;
        }

        public IReadOnlyList<(string key, int name)> Epilogues()
        {
            var list = new List<(string, int)>();
            foreach (LifeEpilogue e in SchoolLife.Epilogues(state)) list.Add((e.Key, e.Name));
            return list;
        }

        // Locks. Each helper asks Core which level an option needs.

        public static int ScenarioLevel(string id) => Career.ScenarioLevel(id);
        public static int BuildingLevel(string id) => Career.BuildingLevel(id);
        public static int PhilosophyLevel(string id) => Career.PhilosophyLevel(id);

        public static int NameLevel(int index)
        {
            int level = 1;
            while (level < Career.MaxLevel && Career.UnlockedNames(level) <= index) level++;
            return level;
        }

        public static int CrestLevel(int index)
        {
            int level = 1;
            while (level < Career.MaxLevel && Career.UnlockedCrests(level) <= index) level++;
            return level;
        }

        public bool Unlocked(int needLevel) => needLevel <= career.Level;

        public bool DraftLocked(LifeDraft d) =>
            !Unlocked(ScenarioLevel(ScenarioOf(d))) || !Unlocked(BuildingLevel(d.Building)) || !Unlocked(PhilosophyLevel(d.Philosophy))
            || !Unlocked(NameLevel(d.SchoolName)) || !Unlocked(CrestLevel(d.Crest));

        public bool HasQuickStart => career.LastPlan != null && career.LastIdentity != null;

        /// <summary>The setup of the last finished year, for one-tap repeat. Call only when <see cref="HasQuickStart"/> is true.</summary>
        public LifeDraft QuickDraft()
        {
            LifePlan p = career.LastPlan!;
            LifeIdentity i = career.LastIdentity!;
            return new LifeDraft
            {
                Building = p.BuildingId, Seats = p.Seats, Fee = p.Fee, Teachers = p.Teachers, Admins = p.Admins, Start = p.StartMinute, Periods = p.Periods,
                PeriodMinutes = p.PeriodMinutes, BreakMinutes = p.BreakMinutes, SchoolName = i.Name, Crest = i.Crest, Colour = i.Colour, Philosophy = i.Philosophy,
                Scenario = career.LastScenario ?? "standard",
            };
        }

        public IReadOnlyList<(int school, string scenario, string grade, int rank, string title, string ending, int day, string memoryKey, int memoryName)> HallOfFame()
        {
            var list = new List<(int, string, string, int, string, string, int, string, int)>();
            for (int i = career.HallOfFame.Length - 1; i >= 0; i--)
            {
                LifeHonour h = career.HallOfFame[i];
                list.Add((h.SchoolName, h.Scenario, h.Grade, h.Rank, h.Title, h.Ending, h.Day, h.MemoryKey, h.MemoryName));
            }
            return list;
        }

        /// <summary>How many times an album item was earned. Id is ach.{id} or title.{id}.</summary>
        public int AlbumCount(string id) => Career.AlbumCount(career, id);

        /// <summary>Reads the career file beside the school save. A missing file starts fresh; a damaged one is set aside.</summary>
        public void UseCareer(string path)
        {
            careerPath = path;
            career = ReadCareer(path);
            BankRun();
        }

        private static CareerState ReadCareer(string path)
        {
            try
            {
                if (System.IO.File.Exists(path)) return CareerSave.Read(System.IO.File.ReadAllText(path));
            }
            catch (Exception error) when (error is FormatException || error is System.IO.IOException || error is UnauthorizedAccessException || error is IndexOutOfRangeException || error is ArgumentException || error is KeyNotFoundException)
            {
                try { System.IO.File.Copy(path, path + ".bad", true); }
                catch (Exception copy) when (copy is System.IO.IOException || copy is UnauthorizedAccessException) { /* a new career still starts */ }
            }
            return Career.New();
        }

        /// <summary>Writes the career with a temporary file. Returns false when the disk refuses.</summary>
        public bool SaveCareer()
        {
            if (careerPath == null) return false;
            try
            {
                string temp = careerPath + ".tmp";
                System.IO.File.WriteAllText(temp, CareerSave.Write(career));
                if (System.IO.File.Exists(careerPath)) System.IO.File.Replace(temp, careerPath, null);
                else System.IO.File.Move(temp, careerPath);
                return true;
            }
            catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Banks a finished year once; Core ignores a run it has already recorded.</summary>
        private void BankRun()
        {
            if (careerPath == null || state.Ending == ChronicleEnding.InProgress) return;
            CareerState before = career;
            career = Career.Record(career, state);
            if (ReferenceEquals(before, career)) return;
            if (career.Level > before.Level) ownCelebrations.Add(("level", "level", career.Level));
            SaveCareer();
        }

        public int CelebrationCount => state.Celebrations.Length + ownCelebrations.Count;

        /// <summary>The oldest celebration waiting, or null. Core's come first; a level-up is the last thing shown.</summary>
        public (string kind, string id, int value)? TopCelebration
        {
            get
            {
                if (state.Celebrations.Length > 0)
                {
                    LifeCelebration c = state.Celebrations[0];
                    return (c.Kind, c.Id, c.Value);
                }
                return ownCelebrations.Count > 0 ? ownCelebrations[0] : ((string kind, string id, int value)?)null;
            }
        }

        public void AckCelebration()
        {
            if (state.Celebrations.Length > 0) { Run(() => state = SchoolLife.AckCelebration(state)); return; }
            if (ownCelebrations.Count == 0) return;
            ownCelebrations.RemoveAt(0);
            Changed?.Invoke();
        }

        public void SetTimetable(IReadOnlyList<string> morning) =>
            Run(() => state = SchoolLife.SetTimetable(state, System.Collections.Immutable.ImmutableArray.CreateRange(morning)));
        public void PromoteTeacher(int teacherId, string role) => Run(() => state = SchoolLife.PromoteTeacher(state, teacherId, role));
        public void NameTeacherOfMonth(int teacherId) => Run(() => state = SchoolLife.NameTeacherOfMonth(state, teacherId));

        private static IEnumerable<string> LifeV3Keys()
        {
            foreach (string id in SchoolLife.Scenarios) { yield return "life.scenario." + id; yield return $"life.scenario.{id}.info"; yield return $"life.scenario.{id}.opening"; }
            foreach (string id in SchoolLife.TitleIds) { yield return "life.title." + id; yield return $"life.title.{id}.info"; }
            foreach (string id in SchoolLife.ChallengeIds) { yield return "life.challenge." + id; yield return $"life.challenge.{id}.info"; }
            foreach (LifeSeedRule rule in SchoolLife.SeedRules) { yield return "life.seed." + rule.Seed; yield return $"life.seed.{rule.Seed}.ok"; }
            yield return "life.scandal";
            foreach (string id in new[] { "confess", "deny" }) { yield return "life.scandal." + id; yield return $"life.scandal.{id}.ok"; }
            yield return "life.absent";
            foreach (string id in new[] { "cover", "merge", "substitute" }) { yield return "life.absent." + id; yield return $"life.absent.{id}.ok"; }
            foreach (string id in new[] { "good", "mid", "bad" }) yield return "life.results." + id;
            foreach (string id in new[] { "publish", "round_up" }) { yield return "life.results." + id; yield return $"life.results.{id}.ok"; }
            yield return "life.results.top";
            foreach (string id in SchoolLife.HomeIds)
            {
                yield return "life.home." + id;
                foreach (string c in new[] { "a", "b" }) { yield return $"life.home.{id}.{c}"; yield return $"life.home.{id}.{c}.ok"; }
            }
            foreach (string arc in SchoolLife.CrisisArcs)
                for (int stage = 0; stage < 3; stage++)
                {
                    yield return $"life.arc.{arc}.{stage}";
                    foreach (string c in new[] { "a", "b" }) { yield return $"life.arc.{arc}.{stage}.{c}"; yield return $"life.arc.{arc}.{stage}.{c}.ok"; }
                }
            foreach (string topic in SchoolLife.ChatTopics) for (int i = 0; i < 5; i++) yield return $"life.chat.{topic}.{i}";
            foreach (string topic in SchoolLife.RoomTopics) for (int i = 0; i < 5; i++) yield return $"life.room.{topic}.{i}";
            for (int i = 0; i < 12; i++) { yield return "life.parent." + i; yield return "life.tease." + i; }
            foreach (string band in new[] { "good", "mid", "bad" }) for (int i = 0; i < 3; i++) yield return $"life.epilogue.{band}.{i}";
            foreach (string band in new[] { "high", "mid", "low" }) { yield return "life.conscience." + band; yield return "life.ending.conscience." + band; }
            yield return "life.promote.head";
            yield return "life.promote.deputy";
            yield return "life.totm";
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
            for (int i = 0; i < SchoolLife.NameCount; i++) yield return "life.name." + i;
            for (int i = 0; i < SchoolLife.SchoolNames; i++) yield return "life.school." + i;
            for (int i = 0; i < SchoolLife.Crests; i++) yield return "life.crest." + i;
            foreach (string id in SchoolLife.Subjects) yield return "life.subject." + id;
            foreach (string id in SchoolLife.Philosophies) { yield return "life.phil." + id; yield return "life.phil." + id + ".info"; }
            foreach (string id in SchoolLife.Traits)
            {
                yield return "life.trait." + id;
                yield return "life.trait." + id + ".info";
                foreach (string question in SchoolLife.InterviewQuestions) yield return $"life.interview.reply.{question}.{id}";
            }
            foreach (string question in SchoolLife.InterviewQuestions) yield return "life.interview.ask." + question;
            foreach (string band in new[] { "low", "mid", "high" }) yield return "life.interview.band." + band;
            foreach (string id in SchoolLife.CaseIds)
            {
                yield return "life.case." + id;
                foreach (string choice in SchoolLife.CaseChoices) { yield return $"life.case.{id}.{choice}"; yield return $"life.case.{id}.{choice}.ok"; }
            }
            foreach (string choice in SchoolLife.CaseChoices) { yield return $"life.case.followup.{choice}.good"; yield return $"life.case.followup.{choice}.bad"; }
            foreach (string id in SchoolLife.ActionIds) yield return "life.action." + id;
            yield return "life.action.observe";
            yield return "life.action.title";
            foreach (LifeUpgrade u in SchoolLife.Upgrades) { yield return "life.up." + u.Id; yield return "life.up." + u.Id + ".info"; yield return "life.up." + u.Id + ".done"; }
            foreach (LifeCourse c in SchoolLife.Courses) { yield return "life.course." + c.Id; yield return "life.course." + c.Id + ".info"; yield return "life.course." + c.Id + ".done"; }
            foreach (string key in LifeV3Keys()) yield return key;
            yield return "life.loan.button";
            yield return "life.loan.taken";
            yield return "life.loan.paid";
            foreach (string rival in new[] { "rival.30", "rival.60" })
            {
                yield return "life." + rival;
                foreach (string choice in SchoolLife.RivalChoices) { yield return $"life.{rival}.{choice}"; yield return $"life.{rival}.{choice}.ok"; }
            }
            yield return "life.ministry";
            foreach (string choice in SchoolLife.MinistryChoices) { yield return "life.ministry." + choice; yield return $"life.ministry.{choice}.ok"; yield return $"life.ministry.{choice}.fail"; }
            yield return "life.grad.body";
            foreach (string choice in SchoolLife.SpeechChoices) { yield return "life.grad." + choice; yield return $"life.grad.{choice}.ok"; }
            foreach (string season in new[] { "exams_mid", "ramadan", "national", "exams_final" }) { yield return "life.season." + season; yield return "life.season." + season + ".banner"; }
            foreach (string goal in SchoolLife.Goals) yield return "life.goal." + goal;
            foreach (string id in SchoolLife.AchievementIds) { yield return "life.ach." + id; yield return "life.ach." + id + ".info"; }
            foreach (string key in UiKeys) yield return key;
            for (int i = 0; i < 5; i++) { yield return "life.speed." + i; yield return "life.pace." + i; }
            foreach (string code in Enum.GetNames(typeof(RuleCode)))
                if (code.StartsWith("Life", StringComparison.Ordinal)) yield return "rule." + code;
        }

        private static readonly string[] UiKeys =
        {
            "life.tab.school", "life.tab.people", "life.tab.build", "life.tab.train", "life.tab.office", "life.tab.home",
            "life.people.title", "life.people.hire", "life.people.dismiss", "life.people.observe", "life.people.skill", "life.people.mood", "life.people.pay", "life.people.trait.unknown", "life.people.in_training",
            "life.interview.title", "life.interview.hire", "life.interview.cancel",
            "life.build.title", "life.build.cost", "life.build.time", "life.build.building", "life.build.done", "life.build.start",
            "life.train.title", "life.train.slots", "life.train.start", "life.train.need_center", "life.train.remaining",
            "life.time.remaining", "life.time.duration", "life.clock.extend", "life.clock.shorten",
            "life.identity.title", "life.identity.name", "life.identity.crest", "life.identity.colour", "life.identity.philosophy",
            "life.form.period_minutes", "life.form.break_minutes", "life.form.seats", "life.form.fee", "life.form.teachers", "life.form.admins", "life.form.start", "life.form.periods",
            "life.students.title", "life.students.marks", "life.students.conduct",
            "life.goal.title", "life.goal.done", "life.ach.title", "life.memories.title", "life.rank",
            "life.career.title", "life.career.level", "life.career.xp", "life.career.next", "life.career.hall", "life.career.empty", "life.career.quick",
            "life.report.title", "life.report.cash", "life.report.best", "life.report.tomorrow", "life.report.none",
            "life.month.title", "life.month.compare", "life.month.body", "life.month.number", "life.month.cash", "life.month.grade", "life.month.standing", "life.month.first",
            "life.stars.title", "life.chat.parents_title", "life.chat.staff_title", "life.chat.empty", "life.director.energy", "life.director.family",
            "life.timetable.title", "life.timetable.morning", "life.timetable.hint", "life.timetable.save", "life.timetable.saved",
            "life.scenario.title", "life.locked", "life.album.title", "life.album.locked", "life.album.titles", "life.promote.head.button", "life.promote.deputy.button", "life.totm.button",
            "life.conscience.title", "life.epilogue.title", "life.card.ok", "life.sheet.close", "life.celebrate.tap", "life.gift.tap", "life.ending.xp", "life.ending.title_label",
            "life.role.head", "life.role.deputy", "life.hall.memory", "life.challenge.status",
            "life.celebrate.star.title", "life.celebrate.achievement.title", "life.celebrate.level.title", "life.celebrate.gift.title", "life.celebrate.promotion.title", "life.celebrate.title.title",
            "life.celebrate.star.body", "life.celebrate.level.body", "life.challenge.title", "life.challenge.won", "life.challenge.lost",
        };
    }
}
