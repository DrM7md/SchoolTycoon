using System;
using System.Collections.Immutable;
using System.Linq;

namespace SchoolDirectorTycoon.Core
{
    public enum ChronicleEnding { InProgress, YearCompleted, Bankrupt, Dismissed, StaffWalkout }
    public enum ChronicleGrade { S, A, B, C, D }

    /// <summary>Change applied to a chronicle school. Zero fields do nothing.</summary>
    public sealed record ChronicleEffect(int Cash = 0, int Students = 0, int Reputation = 0, int Parents = 0, int Staff = 0, int Academics = 0);

    /// <summary>One answer to an event. With a risk, <see cref="Failure"/> replaces <see cref="Effect"/> that often.</summary>
    public sealed record ChronicleChoice(string Id, ChronicleEffect Effect, int RiskPercent = 0, ChronicleEffect? Failure = null);

    public sealed record ChronicleEvent(string Id, int MinWeek, ImmutableArray<ChronicleChoice> Choices);

    /// <summary>One line of the school's story. <see cref="Key"/> is a localization key, never display text.</summary>
    public sealed record ChronicleEntry(int Week, string Key, ChronicleEffect Effect, int Name = -1, string? Subject = null);

    /// <summary>Immutable state of one school year in the text-driven mode. Same state and choices give the same result.</summary>
    public sealed record ChronicleState
    {
        public int Week { get; init; }
        public int Cash { get; init; }
        public int Students { get; init; }
        public int Reputation { get; init; }
        public int Parents { get; init; }
        public int Staff { get; init; }
        public int Academics { get; init; }
        public uint RandomState { get; init; }
        public string? PendingEvent { get; init; }
        public ChronicleEnding Ending { get; init; }
        public ImmutableArray<string> UsedEvents { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<ChronicleEntry> Log { get; init; } = ImmutableArray<ChronicleEntry>.Empty;
    }

    /// <summary>Rules of the week-by-week school year. Prototype: one year, no save file yet.</summary>
    public static class Chronicle
    {
        public const int WeeksInYear = 36;
        public const int EventChancePercent = 72;
        public const int TuitionPerStudentWeek = 115;
        public const int WeeklyPayroll = 13000;
        public const int QuietLines = 6;

        public static ChronicleState New(uint seed) => new ChronicleState
        {
            Cash = 40000,
            Students = 120,
            Reputation = 50,
            Parents = 55,
            Staff = 60,
            Academics = 50,
            RandomState = seed == 0 ? 2463534242u : seed,
            Log = ImmutableArray.Create(new ChronicleEntry(0, "story.opening", new ChronicleEffect())),
        };

        public static ChronicleState NextWeek(ChronicleState state)
        {
            RequireOpen(state);
            if (state.PendingEvent != null)
                throw new RuleException(RuleCode.ChronicleEventPending, "قرر في الحدث الحالي أولًا.", "Decide the current event first.");

            int week = state.Week + 1;
            uint random = state.RandomState;
            int mood = (state.Reputation + state.Parents) / 2;
            int students = 0;
            int roll = Roll(ref random, 100);
            if (mood >= 62 && roll < 40) students = 1 + (mood - 62) / 12;
            else if (mood <= 40 && roll < 55) students = -(1 + (40 - mood) / 10);

            // Standards and morale need upkeep, so ignoring them is never free.
            int upkeep = week % 4 == 0 ? -1 : 0;
            var effect = new ChronicleEffect(
                Cash: state.Students * TuitionPerStudentWeek - WeeklyPayroll,
                Students: students, Staff: upkeep, Academics: upkeep);

            string? drawn = null;
            int chance = Roll(ref random, 100);
            ChronicleEvent[] open = ChronicleDeck.All.Where(e => e.MinWeek <= week && !state.UsedEvents.Contains(e.Id)).ToArray();
            if (chance < EventChancePercent && open.Length > 0)
                drawn = open[Roll(ref random, open.Length)].Id;

            string key = drawn != null ? "story.busy" : "story.quiet." + Roll(ref random, QuietLines);
            ChronicleState next = Apply(state with { Week = week, RandomState = random, PendingEvent = drawn }, effect, key);
            if (drawn != null) next = next with { UsedEvents = next.UsedEvents.Add(drawn) };
            return Close(next);
        }

        public static ChronicleState Choose(ChronicleState state, string choiceId)
        {
            RequireOpen(state);
            if (state.PendingEvent == null)
                throw new RuleException(RuleCode.ChronicleNoPendingEvent, "لا يوجد حدث بانتظار قرارك.", "There is no event waiting for a decision.");
            ChronicleEvent current = ChronicleDeck.Find(state.PendingEvent);
            ChronicleChoice? choice = current.Choices.FirstOrDefault(c => c.Id == choiceId);
            if (choice == null)
                throw new RuleException(RuleCode.ChronicleUnknownChoice, "هذا الخيار غير متاح.", "That choice is not available.");

            uint random = state.RandomState;
            bool failed = choice.Failure != null && Roll(ref random, 100) < choice.RiskPercent;
            ChronicleEffect effect = failed ? choice.Failure! : choice.Effect;
            string key = $"ev.{current.Id}.{choice.Id}.{(failed ? "fail" : "ok")}";
            return Close(Apply(state with { RandomState = random, PendingEvent = null }, effect, key));
        }

        public static ChronicleGrade Grade(ChronicleState state)
        {
            int score = (state.Reputation + state.Parents + state.Staff + state.Academics) / 4;
            if (state.Ending != ChronicleEnding.YearCompleted) return ChronicleGrade.D;
            return score >= 75 ? ChronicleGrade.S : score >= 62 ? ChronicleGrade.A : score >= 50 ? ChronicleGrade.B : ChronicleGrade.C;
        }

        private static void RequireOpen(ChronicleState state)
        {
            if (state.Ending != ChronicleEnding.InProgress)
                throw new RuleException(RuleCode.CampaignEnded, "انتهى العام الدراسي.", "The school year is over.");
        }

        private static ChronicleState Apply(ChronicleState state, ChronicleEffect e, string key) => state with
        {
            Cash = state.Cash + e.Cash,
            Students = Math.Max(0, state.Students + e.Students),
            Reputation = Score(state.Reputation + e.Reputation),
            Parents = Score(state.Parents + e.Parents),
            Staff = Score(state.Staff + e.Staff),
            Academics = Score(state.Academics + e.Academics),
            Log = state.Log.Add(new ChronicleEntry(state.Week, key, e)),
        };

        private static ChronicleState Close(ChronicleState state)
        {
            ChronicleEnding ending =
                state.Cash < 0 ? ChronicleEnding.Bankrupt
                : state.Reputation <= 0 || state.Parents <= 0 ? ChronicleEnding.Dismissed
                : state.Staff <= 0 ? ChronicleEnding.StaffWalkout
                : state.Week >= WeeksInYear && state.PendingEvent == null ? ChronicleEnding.YearCompleted
                : ChronicleEnding.InProgress;
            return ending == ChronicleEnding.InProgress ? state : state with { Ending = ending, PendingEvent = null };
        }

        private static int Score(int value) => Math.Clamp(value, 0, 100);

        internal static int Roll(ref uint random, int range)
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            return (int)(random % (uint)range);
        }
    }

    /// <summary>The event deck. Numbers live here; all wording is in the localization tables, keyed by these ids.</summary>
    public static class ChronicleDeck
    {
        private static ChronicleEffect F(int cash = 0, int students = 0, int rep = 0, int parents = 0, int staff = 0, int acad = 0) =>
            new ChronicleEffect(cash, students, rep, parents, staff, acad);

        private static ChronicleChoice C(string id, ChronicleEffect effect) => new ChronicleChoice(id, effect);
        private static ChronicleChoice R(string id, ChronicleEffect effect, int risk, ChronicleEffect failure) => new ChronicleChoice(id, effect, risk, failure);
        private static ChronicleEvent E(string id, int minWeek, params ChronicleChoice[] choices) => new ChronicleEvent(id, minWeek, ImmutableArray.Create(choices));

        public static readonly ImmutableArray<ChronicleEvent> All = ImmutableArray.Create(
            E("grade_favor", 8,
                C("refuse", F(parents: -5, rep: 2, acad: 3)),
                R("change", F(cash: 9000, parents: 3, acad: -5), 30, F(rep: -20, parents: -10, staff: -8))),
            E("star_offer", 6,
                C("raise", F(cash: -7000, staff: 8, acad: 2)),
                C("promise", F(staff: -2, acad: 1)),
                C("release", F(staff: -8, acad: -7, cash: 3000))),
            E("exam_leak", 14,
                C("retake", F(cash: -3000, rep: 4, parents: -6, staff: -3, acad: 4)),
                R("silence", F(), 45, F(rep: -22, parents: -12, acad: -6)),
                C("investigate", F(cash: -1500, rep: 2, staff: -4, acad: 2))),
            E("inspection", 4,
                R("welcome", F(rep: 7, acad: 2), 35, F(rep: -7, cash: -4000)),
                C("prepare", F(cash: -5000, rep: 5, staff: -4, acad: 2)),
                R("delay", F(staff: 1), 50, F(rep: -12, cash: -6000))),
            E("ac_failure", 2,
                C("repair", F(cash: -9000, parents: 3, staff: 3)),
                C("patch", F(cash: -2500, parents: -2, staff: -2)),
                R("wait", F(), 60, F(parents: -10, staff: -6, students: -4))),
            E("bus_delay", 1,
                C("new_route", F(cash: -4000, parents: 5)),
                C("apologize", F(parents: -3)),
                C("hire_driver", F(cash: -6500, parents: 6, staff: 1))),
            E("tuition_debt", 5,
                C("grant", F(cash: -3500, rep: 5, parents: 5)),
                C("instalments", F(cash: 1500, parents: 2)),
                C("expel", F(cash: 2500, students: -1, rep: -8, parents: -6))),
            E("donor_name", 10,
                C("accept", F(cash: 18000, rep: -3, staff: -3)),
                C("plaque", F(cash: 8000, rep: 1)),
                C("decline", F(rep: 3, staff: 2))),
            E("bully_video", 7,
                C("suspend", F(rep: 3, parents: 2, students: -1)),
                R("mediate", F(parents: 4, staff: 2, rep: 2), 35, F(parents: -9, rep: -9)),
                C("counselor", F(cash: -6000, parents: 6, rep: 4))),
            E("ramadan_hours", 12,
                C("shorten", F(parents: 6, staff: 6, acad: -3)),
                C("keep", F(parents: -6, staff: -6, acad: 3)),
                C("flex", F(cash: -2000, parents: 4, staff: 4))),
            E("national_day", 13,
                C("big", F(cash: -7000, rep: 7, parents: 6, staff: 1)),
                C("modest", F(cash: -1500, rep: 2, parents: 2)),
                C("skip", F(rep: -5, parents: -5))),
            E("science_fair", 9,
                C("sponsor", F(cash: -4500, rep: 6, acad: 5)),
                C("basic", F(cash: -800, acad: 2)),
                C("cancel", F(cash: 500, staff: -3, acad: -4))),
            E("teacher_conflict", 3,
                C("hear", F(staff: 6, acad: -1)),
                C("order", F(staff: -6, acad: 2)),
                C("transfer", F(cash: -1200, staff: 2))),
            E("parent_influencer", 5,
                C("meet", F(parents: 7, staff: -2)),
                C("statement", F(rep: -2, parents: -2)),
                R("legal", F(rep: 2), 55, F(rep: -14, parents: -10, cash: -3000))),
            E("lab_accident", 15,
                C("close_lab", F(cash: -5500, parents: 4, acad: -3, rep: 2)),
                C("train", F(cash: -2200, staff: 2, acad: 1)),
                R("carry_on", F(acad: 2), 40, F(rep: -16, parents: -14, cash: -6000))),
            E("rival_poach", 11,
                C("scholarship", F(cash: -8000, students: 3, rep: 3)),
                C("open_day", F(cash: -3000, students: 1, parents: 3)),
                C("ignore", F(students: -5))),
            E("canteen_health", 6,
                C("healthy", F(cash: -2500, parents: 6)),
                C("warn", F(parents: -1)),
                C("change_vendor", F(cash: -4500, parents: 4, rep: 2))),
            E("cheating_son", 18,
                C("zero", F(parents: -7, rep: 4, acad: 4, staff: 3)),
                C("warn", F(parents: 2, staff: -4, acad: -3)),
                R("cover", F(parents: 6, cash: 5000, acad: -4), 40, F(rep: -18, staff: -10))),
            E("teacher_burnout", 16,
                C("relief", F(cash: -6000, staff: 9)),
                C("day_off", F(staff: 5, acad: -3)),
                C("push", F(staff: -9, acad: 3))),
            E("olympiad", 17,
                C("coach", F(cash: -3500, acad: 6, rep: 5)),
                R("send", F(rep: 9, acad: 2), 50, F(rep: -3)),
                C("pass", F(staff: -1))),
            E("sandstorm", 3,
                C("close", F(parents: 4, acad: -2)),
                C("online", F(cash: -1800, parents: 2, staff: -2)),
                R("open", F(acad: 1), 35, F(parents: -12, rep: -8))),
            E("arabic_curriculum", 8,
                C("expand", F(cash: -4000, parents: 7, rep: 3, acad: 1)),
                C("balance", F(parents: 2, staff: -1)),
                C("english", F(parents: -7, rep: -3, cash: 2000))),
            E("tablet_deal", 10,
                C("buy", F(cash: -12000, acad: 7, rep: 3, staff: -2)),
                C("pilot", F(cash: -3000, acad: 3)),
                C("decline", F(staff: 1))),
            E("alumni_visit", 20,
                C("host", F(cash: -1200, rep: 6, parents: 3)),
                R("ask_fund", F(cash: 12000, rep: 1), 40, F(rep: -5)),
                C("skip", F(rep: -1))),
            E("salary_late", 13,
                C("loan", F(cash: -3500, staff: 3)),
                C("explain", F(staff: -6)),
                C("cut_trip", F(cash: 2500, staff: 1, parents: -5))),
            E("special_needs", 4,
                C("adapt", F(cash: -7500, rep: 7, parents: 7, students: 1)),
                C("partial", F(cash: -2200, parents: 2)),
                C("decline", F(rep: -9, parents: -6))),
            E("football_final", 21,
                C("bus_fans", F(cash: -2600, rep: 4, parents: 5, staff: 1)),
                C("cheer", F(parents: 1)),
                C("exams_first", F(parents: -4, acad: 3))),
            E("roof_leak", 12,
                C("replace", F(cash: -11000, rep: 2, staff: 3)),
                R("cheap", F(cash: -3000), 45, F(cash: -7000, parents: -7, staff: -4)),
                C("move_class", F(staff: -4, acad: -2))),
            E("gossip_group", 19,
                C("join", F(parents: 4, staff: -3)),
                C("newsletter", F(cash: -900, parents: 5, rep: 1)),
                C("ban", F(parents: -8, rep: -3))),
            E("final_exams", 30,
                C("revision", F(cash: -4000, acad: 8, staff: -4)),
                C("normal", F(acad: 2)),
                R("easy_paper", F(parents: 6, acad: -6), 35, F(rep: -14, acad: -9))));

        public static ChronicleEvent Find(string id) => All.First(e => e.Id == id);
    }
}
