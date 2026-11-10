using System;
using System.Collections.Immutable;
using System.Linq;

namespace SchoolDirectorTycoon.Core
{
    /// <summary>One decision stopping the clock. Kind is event, case, rival, ministry, speech, scandal, arc, seed, results, absent, home or month.
    /// Name is a life.name index for {name} in the body, or -1.</summary>
    public sealed record LifeCard(string Kind, string Id, string BodyKey, ImmutableArray<string> Choices, ImmutableArray<string> ButtonKeys, int Name = -1);

    public static partial class SchoolLife
    {
        public static readonly ImmutableArray<string> Goals = ImmutableArray.Create("no_low_mood", "reach_parents", "earn_tonight", "visit_gate", "observe_teacher");
        public static readonly ImmutableArray<string> AchievementIds = ImmutableArray.Create("no_dismissals", "full_house", "first_upgrade", "trained_five", "rank_one", "streak_seven");
        public static readonly ImmutableArray<string> RivalChoices = ImmutableArray.Create("scholarship", "open_day", "ignore");
        public static readonly ImmutableArray<string> MinistryChoices = ImmutableArray.Create("show", "prepare", "honest");
        public static readonly ImmutableArray<string> SpeechChoices = ImmutableArray.Create("thanks", "dreams", "duty");
        private static readonly int[] CityScores = { 95, 88, 82, 76, 70, 64, 58, 50, 42 };

        public static string? SeasonAt(int week) => week switch
        {
            8 => "exams_mid",
            >= 15 and <= 18 => "ramadan",
            20 => "national",
            33 => "exams_final",
            _ => null,
        };

        /// <summary>City standing from 1 (best) to 10.</summary>
        public static int Rank(LifeState state)
        {
            int score = (state.Reputation + state.Parents + state.Staff + state.Academics) / 4 + Math.Min(state.Students, 300) / 10;
            return 1 + CityScores.Count(c => c > score);
        }

        internal static LifeCard? CardOf(LifeState s)
        {
            if (s.PendingEvent != null)
            {
                string id = s.PendingEvent;
                ImmutableArray<string> ids = ChronicleDeck.Find(id).Choices.Select(c => c.Id).ToImmutableArray();
                return new LifeCard("event", id, "ev." + id, ids, ids.Select(c => $"ev.{id}.{c}").ToImmutableArray());
            }
            if (s.PendingCase != null)
            {
                string body = "life.case." + s.PendingCase.CaseId;
                int name = s.Followed.FirstOrDefault(x => x.Id == s.PendingCase.StudentId)?.Name ?? -1;
                return Card("case", s.PendingCase.CaseId, body, body, CaseChoices, name);
            }
            if (s.PendingRival != null) return Card("rival", s.PendingRival, "life." + s.PendingRival, "life." + s.PendingRival, RivalChoices);
            if (s.PendingMinistry) return Card("ministry", "ministry", "life.ministry", "life.ministry", MinistryChoices);
            if (TopQueued(s) is LifeQueued queued) return QueuedCard(queued);
            if (s.PendingSpeech) return Card("speech", "graduation", "life.grad.body", "life.grad", SpeechChoices);
            return null;
        }

        private static LifeCard Card(string kind, string id, string body, string prefix, ImmutableArray<string> choices, int name = -1) =>
            new LifeCard(kind, id, body, choices, choices.Select(c => prefix + "." + c).ToImmutableArray(), name);

        /// <summary>Answers whatever <see cref="LifeState.PendingCard"/> is showing.</summary>
        public static LifeState ResolveCard(LifeState state, string choiceId)
        {
            RequireOpen(state);
            if (state.PendingEvent != null) return Choose(state, choiceId);
            if (state.PendingCase != null) return ResolveCase(state, choiceId);
            if (state.PendingRival != null) return ResolveRival(state, choiceId);
            if (state.PendingMinistry) return ResolveMinistry(state, choiceId);
            if (TopQueued(state) is LifeQueued queued) return ResolveQueued(state, queued, choiceId);
            if (state.PendingSpeech) return ResolveSpeech(state, choiceId);
            throw new RuleException(RuleCode.ChronicleNoPendingEvent, "لا يوجد حدث بانتظار قرارك.", "There is no event waiting for a decision.");
        }

        /// <summary>Cash the answer costs, for the button and the guard.</summary>
        public static int RivalCost(string rivalId, string choice)
        {
            bool late = rivalId == "rival.60";
            return choice switch { "scholarship" => late ? 9000 : 6000, "open_day" => late ? 2500 : 1500, _ => 0 };
        }

        private static LifeState ResolveRival(LifeState state, string choice)
        {
            if (!RivalChoices.Contains(choice)) throw UnknownChoice();
            string id = state.PendingRival!;
            int cost = RivalCost(id, choice);
            RequireCash(state, cost);
            int relief = choice switch { "scholarship" => 15, "open_day" => 8, _ => 0 };
            var effect = new ChronicleEffect(Cash: -cost, Parents: choice == "scholarship" ? 1 : 0, Reputation: choice == "ignore" ? -1 : 0);
            LifeState next = state with { PendingRival = null, RivalPressure = Math.Max(0, state.RivalPressure - relief) };
            return Close(Apply(next, effect, $"life.{id}.{choice}.ok"));
        }

        public const int MinistryPrepareCost = 3000;

        private static LifeState ResolveMinistry(LifeState state, string choice)
        {
            if (!MinistryChoices.Contains(choice)) throw UnknownChoice();
            int cost = choice == "prepare" ? MinistryPrepareCost : 0;
            RequireCash(state, cost);
            int needed = choice switch { "show" => 60, "prepare" => 45, _ => 35 };
            bool passed = state.Academics >= needed;
            ChronicleEffect effect = (choice, passed) switch
            {
                ("show", true) => new ChronicleEffect(Reputation: 3),
                ("show", false) => new ChronicleEffect(Reputation: -4, Parents: -1),
                ("prepare", true) => new ChronicleEffect(Reputation: 2),
                ("prepare", false) => new ChronicleEffect(Reputation: -1),
                (_, true) => new ChronicleEffect(Reputation: 1, Staff: 1),
                _ => new ChronicleEffect(Reputation: -2),
            };
            effect = effect with { Cash = -cost };
            return Close(Apply(state with { PendingMinistry = false }, effect, $"life.ministry.{choice}.{(passed ? "ok" : "fail")}"));
        }

        private static LifeState ResolveSpeech(LifeState state, string choice)
        {
            if (!SpeechChoices.Contains(choice)) throw UnknownChoice();
            ChronicleEffect effect = choice switch
            {
                "thanks" => new ChronicleEffect(Staff: 2),
                "dreams" => new ChronicleEffect(Parents: 2),
                _ => new ChronicleEffect(Reputation: 2),
            };
            LifeState next = Apply(state with { PendingSpeech = false }, effect, $"life.grad.{choice}.ok");
            if (next.Dismissals == 0) next = Award(next, "no_dismissals");
            if (Rank(next) == 1) next = Award(next, "rank_one");
            return Ended(next with { Ending = ChronicleEnding.YearCompleted });
        }

        private static LifeState MarkSeason(LifeState state)
        {
            string? season = SeasonAt(state.Week);
            bool starts = season != null && (state.Week == 1 || SeasonAt(state.Week - 1) != season) && (state.Day - 1) % DaysInWeek == 0;
            return starts ? Apply(state, new ChronicleEffect(), "life.season." + season) : state;
        }

        private static LifeState Award(LifeState state, string id) =>
            state.Achievements.Contains(id) ? state : Celebrate(Apply(state with { Achievements = state.Achievements.Add(id) }, new ChronicleEffect(), "life.ach." + id), "achievement", id, 0);

        /// <summary>Checked each evening; pays once for the goal set by the morning's greeting.</summary>
        private static LifeState CheckGoal(LifeState state, bool earned)
        {
            if (state.GoalId == null || state.GoalDone) return state;
            bool met = state.GoalId switch
            {
                "no_low_mood" => state.Roster.All(t => t.Mood >= 40),
                "reach_parents" => state.Parents >= 60,
                "earn_tonight" => earned,
                "visit_gate" => state.ActionsToday.Contains("gate"),
                "observe_teacher" => state.ActionsToday.Contains("observe"),
                _ => false,
            };
            return met ? Apply(state with { GoalDone = true }, new ChronicleEffect(Cash: GoalReward), "life.goal.done") : state;
        }
    }
}
