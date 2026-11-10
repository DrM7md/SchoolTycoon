using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SchoolDirectorTycoon.Core
{
    /// <summary>Choices made on the setup screen. Build it with <see cref="Career.Start"/>; Level decides what is unlocked.</summary>
    public sealed record LifeStart(LifeIdentity Identity, string Scenario, int StartingCash, int Level = 1);

    /// <summary>What changed since the previous report. Cash and standings are deltas; BestLog indexes the log, or -1.</summary>
    public sealed record LifeDayReport(int Day, int CashDelta, int Rep, int Parents, int Staff, int Acad, int BestLog, string TeaseKey);

    /// <summary>The certificate handed out every 20 school days. Values are absolute.</summary>
    public sealed record LifeMonth(int Month, int Cash, int Rep, int Parents, int Staff, int Acad, int Students, string Grade);

    /// <summary>Kind is star, achievement, level (UI only), gift, promotion, title or challenge.</summary>
    public sealed record LifeCelebration(string Kind, string Id, int Value);

    /// <summary>Group is parents or staff. Sender is a life.parent index or a teacher's name index; Name is -1 or a name index for {name}.</summary>
    public sealed record LifeChat(int Day, string Group, int Sender, string Key, int Name);

    public sealed record LifeSeed(string Id, int DueWeek);

    /// <summary>A delayed consequence: answering Event with Choice plants Seed, due Weeks later.</summary>
    public sealed record LifeSeedRule(string Event, string Choice, string Seed, int Weeks, bool Positive, ChronicleEffect Effect);

    public sealed record LifeEpilogue(string Key, int Name);

    /// <summary>A multi-day crisis. Stage 0..2 is next to be shown, 3 is finished. NextDay is -1 while a card is waiting.</summary>
    public sealed record LifeArc(string Id, int Stage, int NextDay, int Name, string Answers);

    /// <summary>A waiting card beyond the five original kinds. See <see cref="SchoolLife.CardRank"/> for the order.</summary>
    public sealed record LifeQueued(string Kind, string Id, int Name = -1);

    public static partial class SchoolLife
    {
        public const int MonthDays = 20, ChallengeDays = 5, ChallengeReward = 3000, ChatLimit = 200, ChatMemoryDays = 15, SubstituteCost = 400;
        public const int AbsentChancePercent = 5, HomeChancePercent = 8, ScandalBelow = 30, CrisisBelow = 30, CrisisCooldownDays = 10;
        public const int StartEnergy = 80, StartFamily = 70, StartConscience = 70, MaxHeads = 2, HeadSkill = 70, DeputySkill = 80, HeadPay = 400, DeputyPay = 800;
        public const int RescueDebt = 800, RescueDebtWeeks = 20, InternationalMarketPercent = 33, InternationalPayPercent = 155;
        public const string OkChoice = "ok", OkButton = "life.card.ok";

        public static readonly ImmutableArray<string> Scenarios = ImmutableArray.Create("standard", "rescue", "village", "girls", "international");
        public static readonly ImmutableArray<string> TitleIds = ImmutableArray.Create("caring", "budget_king", "no_dismissals", "on_paper", "iron", "reformer", "beloved", "shady", "gambler", "builder", "mentor", "survivor");
        public static readonly ImmutableArray<string> ChallengeIds = ImmutableArray.Create("few_teachers", "no_loan", "happy_staff", "full_seats", "no_tutoring", "high_parents");
        public static readonly ImmutableArray<string> HomeIds = ImmutableArray.Create("child_failing", "anniversary", "job_offer", "doctor", "parent_ill", "friend_loan", "kid_at_school", "vacation", "crisis");
        public static readonly ImmutableArray<string> CrisisArcs = ImmutableArray.Create("teacher_quit", "parent_threat");
        public static readonly ImmutableArray<string> DefaultMorning = ImmutableArray.Create("math", "arabic", "science");
        public static readonly ImmutableArray<string> ChatTopics = ImmutableArray.Create("fee_up", "fee_high", "late_bell", "long_day", "event_good", "event_bad", "upgrade", "quiet", "parking", "exams", "ramadan", "rival", "grades_low", "grades_high", "letter", "ministry");
        public static readonly ImmutableArray<string> RoomTopics = ImmutableArray.Create("low_mood", "training", "dismissed", "praise", "gossip", "overtime", "rivalry", "promotion");

        private static readonly ImmutableArray<string> OkOnly = ImmutableArray.Create(OkChoice);
        private static readonly ImmutableArray<string> AorB = ImmutableArray.Create("a", "b");
        private static readonly ImmutableArray<string> ScandalChoices = ImmutableArray.Create("confess", "deny");
        private static readonly ImmutableArray<string> AbsentChoices = ImmutableArray.Create("cover", "merge", "substitute");
        private static readonly ImmutableArray<string> ResultChoices = ImmutableArray.Create("publish", "round_up");
        private static readonly ImmutableArray<string> CoreSubjects = ImmutableArray.Create("math", "science", "arabic", "english");

        public static readonly ImmutableArray<LifeSeedRule> SeedRules = ImmutableArray.Create(
            new LifeSeedRule("tuition_debt", "grant", "debt_repaid", 8, true, new ChronicleEffect(Cash: 3000, Reputation: 2, Parents: 2)),
            new LifeSeedRule("special_needs", "adapt", "inclusion", 10, true, new ChronicleEffect(Students: 2, Reputation: 3, Parents: 3)),
            new LifeSeedRule("grade_favor", "change", "grade_exposed", 8, false, new ChronicleEffect(Reputation: -7, Parents: -4)),
            new LifeSeedRule("cheating_son", "cover", "cover_exposed", 7, false, new ChronicleEffect(Reputation: -6, Staff: -4)),
            new LifeSeedRule("exam_leak", "silence", "leak_exposed", 6, false, new ChronicleEffect(Reputation: -8, Parents: -5, Academics: -2)),
            new LifeSeedRule("bully_video", "counselor", "bully_healed", 9, true, new ChronicleEffect(Parents: 3, Staff: 1)),
            new LifeSeedRule("star_offer", "raise", "star_stays", 12, true, new ChronicleEffect(Staff: 3, Academics: 2)),
            new LifeSeedRule("tuition_debt", "expel", "expelled_returns", 10, false, new ChronicleEffect(Reputation: -4, Parents: -3)),
            new LifeSeedRule("donor_name", "accept", "donor_demands", 9, false, new ChronicleEffect(Cash: -5000, Staff: -2)),
            new LifeSeedRule("salary_late", "loan", "staff_remembers", 10, true, new ChronicleEffect(Staff: 4)));

        /// <summary>Lower runs first. The five original kinds always come before anything in the queue.</summary>
        public static int CardRank(string kind) => kind switch
        {
            "scandal" => 0, "arc" => 1, "seed" => 2, "results" => 3, "absent" => 4, "home" => 5, "month" => 6, _ => 7,
        };

        // ---- Scenarios ----

        public static int ScenarioFeeCap(string scenario) => scenario switch { "village" => 200, "international" => 900, _ => MaxFee };

        public static int ScenarioCapacity(string scenario, LifeBuilding building) => scenario == "village" ? 40 : building.Capacity;

        public static int ScenarioRent(string scenario, LifeBuilding building) => scenario == "village" ? 1500 : building.WeeklyRent;

        private static (int rep, int parents) ScenarioStanding(string scenario, int rep, int parents) => scenario switch
        {
            "rescue" => (30, 35),
            "village" => (rep, parents + 10),
            "girls" => (rep, parents + 5),
            "international" => (rep, 45),
            _ => (rep, parents),
        };

        private static void ValidateStart(LifeStart start, LifePlan plan)
        {
            if (!Scenarios.Contains(start.Scenario) || start.StartingCash <= 0 || start.Level < 1 || start.Level > Career.MaxLevel)
                throw new RuleException(RuleCode.LifeInvalidPlan, "بداية اللعب غير صالحة.", "That start is not valid.");
            ValidateIdentity(start.Identity);
            if (start.Scenario == "village" && plan.BuildingId != "villa")
                throw new RuleException(RuleCode.LifeInvalidPlan, "مدرسة القرية تبدأ في الفيلا.", "The village school must use the villa.");
            bool locked = Career.ScenarioLevel(start.Scenario) > start.Level
                || Career.BuildingLevel(plan.BuildingId) > start.Level
                || Career.PhilosophyLevel(start.Identity.Philosophy) > start.Level
                || start.Identity.Name >= Career.UnlockedNames(start.Level)
                || start.Identity.Crest >= Career.UnlockedCrests(start.Level)
                || (start.Scenario == "girls" && start.Identity.Philosophy == "sports");
            if (locked) throw new RuleException(RuleCode.LifeLocked, "هذا الخيار مغلق.", "That option is locked.");
        }

        /// <summary>Families wanting a seat right now, after scenario, bus and star effects and before the seat limit.</summary>
        public static int CurrentDemand(LifeState s)
        {
            LifePlan plan = s.Plan!;
            int reputation = s.Reputation, parents = s.Parents;
            int fee = s.Scenario == "international" ? plan.Fee * 4 / 9 : plan.Fee;
            int demand = Demand(fee, reputation, parents);
            if (s.Scenario == "village") demand = demand * 35 / 100;
            if (s.Scenario == "international")
            {
                demand = demand * InternationalMarketPercent / 100;
                if (s.Academics < 55) demand = demand * 70 / 100;
            }
            if (s.UpgradesDone.Contains("bus")) demand = demand * 110 / 100;
            if (Stars(s) >= 4) demand = demand * 105 / 100;
            return demand;
        }

        // ---- Stars, grades, titles, epilogues ----

        public static int Standing(LifeState s) => (s.Reputation + s.Parents + s.Staff + s.Academics) / 4;

        public static int Stars(LifeState s)
        {
            int avg = Standing(s);
            return avg >= 74 && s.Students >= 150 ? 5 : avg >= 66 && s.Students >= 120 ? 4 : avg >= 58 && s.Students >= 80 ? 3 : avg >= 50 && s.Students >= 40 ? 2 : 1;
        }

        private static ChronicleGrade GradeOf(int score) => score >= 78 ? ChronicleGrade.S : score >= 66 ? ChronicleGrade.A : score >= 52 ? ChronicleGrade.B : ChronicleGrade.C;

        public static string ConscienceBand(LifeState s) => s.Conscience >= 60 ? "high" : s.Conscience >= 35 ? "mid" : "low";

        /// <summary>The final line about the director's conscience.</summary>
        public static string ConscienceEnding(LifeState s) => "life.ending.conscience." + ConscienceBand(s);

        /// <summary>The title earned by this run. Dark deeds outrank good ones; survivor is the fallback.</summary>
        public static string TitleOf(LifeState s)
        {
            if (s.Conscience < 35) return "shady";
            if (s.RoundUps >= 2) return "on_paper";
            if (s.RisksTaken >= 5) return "gambler";
            if (s.Parents >= 75 && s.Staff >= 70) return "beloved";
            if (s.Academics >= 75) return "reformer";
            if (s.CoursesFinished >= 5 || s.Roster.Count(t => t.Role != null) >= 2) return "mentor";
            if (s.UpgradesDone.Length >= 6) return "builder";
            if (s.CasePunishes >= 8) return "iron";
            if (s.CaseTalks >= 8) return "caring";
            if (s.Cash >= 150000) return "budget_king";
            if (s.Ending == ChronicleEnding.YearCompleted && s.Dismissals == 0 && s.Students >= 100) return "no_dismissals";
            return "survivor";
        }

        /// <summary>One closing line per followed student.</summary>
        public static ImmutableArray<LifeEpilogue> Epilogues(LifeState s) => s.Followed.Select(x =>
        {
            int score = (x.Marks + x.Conduct) / 2;
            string band = score >= 60 ? "good" : score >= 45 ? "mid" : "bad";
            return new LifeEpilogue($"life.epilogue.{band}.{x.Id % 3}", x.Name);
        }).ToImmutableArray();

        /// <summary>Last letter ending, else the graduation speech, else the first achievement. Null key when there is none.</summary>
        public static (string? Key, int Name) BestMemory(LifeState s)
        {
            ChronicleEntry? letter = null, graduation = null, achievement = null;
            foreach (ChronicleEntry e in s.Log)
            {
                if (e.Key.StartsWith("life.letter.", StringComparison.Ordinal) && e.Key.Contains(".4.")) letter = e;
                else if (e.Key.StartsWith("life.grad.", StringComparison.Ordinal)) graduation = e;
                else if (achievement == null && e.Key.StartsWith("life.ach.", StringComparison.Ordinal)) achievement = e;
            }
            ChronicleEntry? best = letter ?? graduation ?? achievement;
            return best == null ? ((string?)null, -1) : (best.Key, best.Name);
        }

        private static LifeState Celebrate(LifeState s, string kind, string id, int value) =>
            s with { Celebrations = s.Celebrations.Add(new LifeCelebration(kind, id, value)) };

        /// <summary>Removes the oldest celebration once the player has seen it.</summary>
        public static LifeState AckCelebration(LifeState s)
        {
            if (s.Celebrations.IsEmpty)
                throw new RuleException(RuleCode.LifeNoCelebration, "لا يوجد ما يُحتفل به.", "There is nothing to celebrate.");
            return s with { Celebrations = s.Celebrations.RemoveAt(0) };
        }

        private static LifeState Ended(LifeState s) => Celebrate(s, "title", TitleOf(s), 0);

        // ---- Conscience and the personal bars ----

        private static int ConscienceShift(string eventId, string choice) => (eventId, choice) switch
        {
            ("grade_favor", "change") => -12,
            ("cheating_son", "cover") => -15,
            ("exam_leak", "silence") => -12,
            ("final_exams", "easy_paper") => -10,
            ("tuition_debt", "expel") => -8,
            ("grade_favor", "refuse") => 3,
            ("cheating_son", "zero") => 4,
            ("exam_leak", "retake") => 4,
            ("exam_leak", "investigate") => 2,
            ("final_exams", "normal") => 2,
            ("tuition_debt", "grant") => 3,
            ("tuition_debt", "instalments") => 2,
            _ => 0,
        };

        private static LifeState Shift(LifeState s, int conscience)
        {
            if (conscience == 0) return s;
            LifeState next = s with { Conscience = Math.Clamp(s.Conscience + conscience, 0, 100) };
            if (next.Conscience >= ScandalBelow || next.ScandalDone) return next;
            return Enqueue(next with { ScandalDone = true }, "scandal", "scandal");
        }

        private static LifeState Personal(LifeState s, int energy, int family) =>
            s with { Energy = Math.Clamp(s.Energy + energy, 0, 100), Family = Math.Clamp(s.Family + family, 0, 100) };

        private static LifeState Enqueue(LifeState s, string kind, string id, int name = -1) =>
            s with { CardQueue = s.CardQueue.Add(new LifeQueued(kind, id, name)) };

        private static void RequireEnergy(LifeState s)
        {
            if (s.Energy <= 0)
                throw new RuleException(RuleCode.LifeNoEnergy, "أنت منهك؛ استرح الليلة.", "You are exhausted. Rest tonight.");
        }

        private static int ActivityEnergy(string activity) => activity switch
        {
            "tutoring" or "adult_courses" or "parents_night" => -10,
            "home" => 25,
            _ => -3,
        };

        // ---- Timetable, promotions, teacher of the month ----

        /// <summary>Picks the three morning subjects. Evening only.</summary>
        public static LifeState SetTimetable(LifeState state, ImmutableArray<string> morning)
        {
            RequireEvening(state);
            if (morning.IsDefault || morning.Length != 3 || morning.Distinct().Count() != 3 || morning.Any(m => !Subjects.Contains(m)))
                throw new RuleException(RuleCode.LifeBadTimetable, "اختر ثلاث مواد مختلفة للصباح.", "Choose three different morning subjects.");
            return state with { Morning = morning };
        }

        private static void TimetableWeek(LifeState s, int week, ref int academics, ref int parents)
        {
            int taught = s.Morning.Count(m => CoreSubjects.Contains(m) && s.Roster.Any(t => t.Subject == m && t.TrainingEnd == 0));
            if (taught >= 2) { if (week % 2 == 0) academics += 1; }
            else if (taught == 0) academics -= 1;
            if (s.Morning.Contains("sports") && week % 4 == 0) parents -= 1;
        }

        /// <summary>Raises a teacher to head (Skill 70, two places) or deputy (Skill 80, one place). Evening only.</summary>
        public static LifeState PromoteTeacher(LifeState state, int teacherId, string role)
        {
            RequireEvening(state);
            if (role != "head" && role != "deputy") throw UnknownChoice();
            LifeTeacher t = FindTeacher(state, teacherId);
            if (t.TrainingEnd != 0) throw Busy();
            if (t.Role != null) throw Used();
            if (t.Skill < (role == "head" ? HeadSkill : DeputySkill))
                throw new RuleException(RuleCode.LifeNotQualified, "هذا المعلم لا يستوفي الشروط.", "That teacher is not qualified.");
            if (state.Roster.Count(x => x.Role == role) >= (role == "head" ? MaxHeads : 1))
                throw new RuleException(RuleCode.LifeRoleFull, "هذا المنصب مشغول.", "That post is already filled.");
            LifeTeacher raised = t with { Role = role, WeeklyPay = t.WeeklyPay + (role == "head" ? HeadPay : DeputyPay) };
            LifeState next = Apply(state with { Roster = state.Roster.Replace(t, raised), Tags = state.Tags.Add("promotion") }, new ChronicleEffect(), "life.promote." + role, t.Name);
            return Celebrate(next, "promotion", role, t.Name);
        }

        /// <summary>Honours one teacher, once per 20 school days. Everyone else is a little jealous. Evening only.</summary>
        public static LifeState NameTeacherOfMonth(LifeState state, int teacherId)
        {
            RequireEvening(state);
            LifeTeacher t = FindTeacher(state, teacherId);
            int month = (state.Day - 1) / MonthDays;
            if (state.LastTotmMonth == month)
                throw new RuleException(RuleCode.LifeMonthDone, "تم اختيار معلم هذا الشهر.", "This month's teacher has been named.");
            ImmutableArray<LifeTeacher> roster = state.Roster.Select(x => x.Id == t.Id ? x with { Mood = Math.Min(100, x.Mood + 15) } : x with { Mood = Math.Max(0, x.Mood - 2) }).ToImmutableArray();
            return Apply(state with { Roster = roster, LastTotmMonth = month, Tags = state.Tags.Add("praise") }, new ChronicleEffect(), "life.totm", t.Name);
        }

        // ---- End of the school day ----

        private static LifeState FinishDay(LifeState done)
        {
            uint random = done.RandomState;
            var report = new LifeDayReport(done.Day, done.Cash - done.MarkCash, done.Reputation - done.MarkRep, done.Parents - done.MarkParents,
                done.Staff - done.MarkStaff, done.Academics - done.MarkAcad, BestLog(done), "life.tease." + Tease(done, ref random));
            LifeState s = done with
            {
                LastDay = report, MarkCash = done.Cash, MarkRep = done.Reputation, MarkParents = done.Parents, MarkStaff = done.Staff, MarkAcad = done.Academics,
                MarkLog = done.Log.Length, RandomState = random,
            };
            s = Gossip(s);
            if (s.Day % MonthDays == 0)
            {
                string grade = GradeOf(Standing(s)).ToString();
                int month = s.Day / MonthDays;
                s = Enqueue(s with { MonthCards = s.MonthCards.Add(new LifeMonth(month, s.Cash, s.Reputation, s.Parents, s.Staff, s.Academics, s.Students, grade)) }, "month", month.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            int stars = Stars(s);
            if (stars > s.StarsBest) s = Celebrate(s with { StarsBest = stars }, "star", "stars", stars);
            return JudgeChallenge(s);
        }

        private static int BestLog(LifeState s)
        {
            int best = -1, bestScore = 0;
            for (int i = s.MarkLog; i < s.Log.Length; i++)
            {
                ChronicleEntry e = s.Log[i];
                if (e.Key == "life.week" || e.Key == "life.adjusted" || e.Key.StartsWith("life.act.", StringComparison.Ordinal)) continue;
                ChronicleEffect f = e.Effect;
                int score = f.Cash / 1000 + f.Students + f.Reputation + f.Parents + f.Staff + f.Academics;
                if (score > bestScore) { best = i; bestScore = score; }
            }
            return best;
        }

        private static int Tease(LifeState s, ref uint random)
        {
            string? next = SeasonAt(s.Week + 1);
            if (next != SeasonAt(s.Week))
            {
                if (next == "exams_mid" || next == "exams_final") return 0;
                if (next == "ramadan") return 1;
                if (next == "national") return 2;
            }
            if (s.Week + 1 == MinistryWeek) return 3;
            if (s.BuildingJob != null && s.BuildingEnd - s.LastNow <= 720) return 4;
            if (s.Cash < 15000) return 5;
            if (s.Parents < 35) return 6;
            if (s.Staff < 35) return 7;
            if (s.Academics < 35) return 8;
            return 9 + Chronicle.Roll(ref random, 3);
        }

        private static LifeState Gossip(LifeState s)
        {
            uint random = s.RandomState;
            LifePlan plan = s.Plan!;
            int from = s.Log.TakeWhile(e => e.Week < s.Day).Count();
            var today = s.Log.Skip(from).ToArray();
            var topics = new List<string>();
            void When(bool condition, string topic) { if (condition) topics.Add(topic); }
            When(plan.Fee > s.LastFee && s.LastFee > 0, "fee_up");
            When(plan.Fee > (s.Scenario == "girls" ? 220 : 280), "fee_high");
            When(plan.StartMinute >= 480, "late_bell");
            When(s.DayExtra >= 15 || plan.Periods * plan.PeriodMinutes > 330, "long_day");
            foreach (ChronicleEntry e in today.Where(e => e.Key.StartsWith("ev.", StringComparison.Ordinal)))
                topics.Add(e.Effect.Parents + e.Effect.Reputation >= 0 ? "event_good" : "event_bad");
            When(today.Any(e => e.Key.StartsWith("life.up.", StringComparison.Ordinal)), "upgrade");
            When(SeasonAt(s.Week) == "exams_mid" || SeasonAt(s.Week) == "exams_final", "exams");
            When(SeasonAt(s.Week) == "ramadan", "ramadan");
            When(s.RivalPressure >= 30, "rival");
            When(s.Academics < 45, "grades_low");
            When(s.Academics >= 70, "grades_high");
            When(today.Any(e => e.Key.StartsWith("life.letter.", StringComparison.Ordinal)), "letter");
            When(today.Any(e => e.Key.StartsWith("life.ministry", StringComparison.Ordinal)), "ministry");
            if (topics.Count == 0) topics.Add("quiet");
            topics.Add("parking");

            var chat = new List<LifeChat>();

            // A line stays out of its group for ChatMemoryDays, so a day with nothing new is simply a quiet day.
            string? FreshLine(string prefix, string group, IReadOnlyList<string> from, ref uint seed)
            {
                int day = s.Day;
                var recent = new HashSet<string>(s.Chat.Where(c => c.Group == group && c.Day > day - ChatMemoryDays).Select(c => c.Key));
                recent.UnionWith(chat.Select(c => c.Key));
                List<string> open = from.SelectMany(t => Enumerable.Range(0, 5).Select(v => $"{prefix}.{t}.{v}")).Where(k => !recent.Contains(k)).ToList();
                return open.Count == 0 ? null : open[Chronicle.Roll(ref seed, open.Count)];
            }

            int count = 1 + Chronicle.Roll(ref random, 3);
            for (int i = 0; i < count; i++)
            {
                string topic = topics[Chronicle.Roll(ref random, topics.Count)];
                string? line = FreshLine("life.chat", "parents", new[] { topic }, ref random) ?? FreshLine("life.chat", "parents", topics, ref random);
                if (line != null) chat.Add(new LifeChat(s.Day, "parents", Chronicle.Roll(ref random, 12), line, -1));
            }

            if (s.Roster.Length > 0 && Chronicle.Roll(ref random, 100) < 60)
            {
                var room = new List<string>();
                void Room(bool condition, string topic) { if (condition) room.Add(topic); }
                Room(s.Roster.Any(t => t.Mood < 40), "low_mood");
                Room(s.Roster.Any(t => t.TrainingEnd != 0), "training");
                Room(s.Tags.Contains("dismissed"), "dismissed");
                Room(s.Tags.Contains("praise"), "praise");
                Room(s.DayExtra >= 30, "overtime");
                Room(s.RivalPressure >= 30, "rivalry");
                Room(s.Tags.Contains("promotion"), "promotion");
                if (room.Count == 0) room.Add("gossip");
                string topic = room[Chronicle.Roll(ref random, room.Count)];
                LifeTeacher sender = s.Roster[Chronicle.Roll(ref random, s.Roster.Length)];
                LifeTeacher other = s.Roster[Chronicle.Roll(ref random, s.Roster.Length)];
                string? line = FreshLine("life.room", "staff", new[] { topic }, ref random) ?? FreshLine("life.room", "staff", room, ref random);
                if (line != null) chat.Add(new LifeChat(s.Day, "staff", sender.Name, line, other.Name));
            }

            ImmutableArray<LifeChat> all = s.Chat.AddRange(chat);
            if (all.Length > ChatLimit) all = all.RemoveRange(0, all.Length - ChatLimit);
            return s with { RandomState = random, Chat = all, LastFee = plan.Fee, Tags = ImmutableArray<string>.Empty };
        }

        // ---- Weekly challenge ----

        private static LifeState StartChallenge(LifeState s, int realDay)
        {
            int week = Math.Max(0, realDay / 7);
            if (s.ChallengeWeek == week && s.ChallengeId != null) return s;
            return s with { ChallengeWeek = week, ChallengeId = ChallengeIds[week % ChallengeIds.Length], ChallengeDay = s.Day, ChallengeBroken = false, ChallengeDone = false };
        }

        private static LifeState JudgeChallenge(LifeState s)
        {
            if (s.ChallengeId == null || s.ChallengeDone || s.Day < s.ChallengeDay + ChallengeDays - 1) return s;
            bool won = !s.ChallengeBroken && s.ChallengeId switch
            {
                "few_teachers" => s.Students >= s.Roster.Length * 18,
                "no_loan" => s.LoanWeeks == 0,
                "happy_staff" => s.Staff >= 60,
                "full_seats" => s.Students * 100 >= s.Plan!.Seats * 95,
                "no_tutoring" => true,
                _ => s.Parents >= 65,
            };
            LifeState judged = s with { ChallengeDone = true, ChallengesWon = s.ChallengesWon + (won ? 1 : 0) };
            if (!won) return Apply(judged, new ChronicleEffect(), "life.challenge.lost");
            return Celebrate(Apply(judged, new ChronicleEffect(Cash: ChallengeReward), "life.challenge.won"), "challenge", s.ChallengeId, ChallengeReward);
        }

        // ---- Start of the school day ----

        internal static LifeState ScheduleArcs(LifeState s)
        {
            uint random = s.RandomState;
            // The two crises sit in separate halves of weeks 6-26 so they never overlap.
            int firstWeek = 6 + Chronicle.Roll(ref random, 9), secondWeek = 16 + Chronicle.Roll(ref random, 11);
            LifeArc Plan(string id, int week, ref uint r)
            {
                int day = (week - 1) * DaysInWeek + 1 + Chronicle.Roll(ref r, DaysInWeek);
                int name = id == "teacher_quit" && s.Roster.Length > 0 ? s.Roster[Chronicle.Roll(ref r, s.Roster.Length)].Name : Chronicle.Roll(ref r, NameCount);
                if (day <= s.Day) day = s.Week < 26 ? s.Day + 2 : -1;
                return new LifeArc(id, day < 0 ? 3 : 0, day, name, "");
            }
            LifeArc first = Plan(CrisisArcs[0], firstWeek, ref random), second = Plan(CrisisArcs[1], secondWeek, ref random);
            return s with { RandomState = random, StoryArcs = ImmutableArray.Create(first, second) };
        }

        private static LifeState MorningCards(LifeState state)
        {
            LifeState s = state;
            uint random = s.RandomState;
            bool monday = (s.Day - 1) % DaysInWeek == 0;
            if (monday && (s.Week == 8 || s.Week == 33))
                s = Enqueue(s, "results", s.Academics >= 65 ? "good" : s.Academics >= 45 ? "mid" : "bad");
            foreach (LifeSeed seed in s.Seeds.Where(x => s.Week >= x.DueWeek).ToArray())
                s = Enqueue(s with { Seeds = s.Seeds.Remove(seed) }, "seed", seed.Id);
            foreach (LifeArc arc in s.StoryArcs.Where(a => a.Stage < 3 && a.NextDay >= 0 && s.Day >= a.NextDay).ToArray())
                s = Enqueue(s with { StoryArcs = s.StoryArcs.Replace(arc, arc with { NextDay = -1 }) }, "arc", $"{arc.Id}.{arc.Stage}", arc.Name);
            if (s.Family < CrisisBelow && s.Day >= s.LastCrisisDay + CrisisCooldownDays)
                s = Enqueue(s with { LastCrisisDay = s.Day }, "home", "crisis");

            bool absent = Chronicle.Roll(ref random, 100) < AbsentChancePercent;
            bool home = Chronicle.Roll(ref random, 100) < HomeChancePercent;
            if (absent && s.Roster.Length > 2 && s.CardQueue.IsEmpty)
            {
                LifeTeacher[] here = s.Roster.Where(t => t.TrainingEnd == 0).ToArray();
                if (here.Length > 0) s = Enqueue(s, "absent", "absent", here[Chronicle.Roll(ref random, here.Length)].Name);
            }
            if (home && s.CardQueue.IsEmpty)
                s = Enqueue(s, "home", HomeIds[Chronicle.Roll(ref random, HomeIds.Length - 1)]);
            return s with { RandomState = random };
        }

        // ---- Cards ----

        private static LifeQueued? TopQueued(LifeState s) =>
            s.CardQueue.IsEmpty ? null : s.CardQueue.OrderBy(q => CardRank(q.Kind)).First();

        private static LifeCard QueuedCard(LifeQueued q)
        {
            switch (q.Kind)
            {
                case "seed": return Single("seed", q.Id, "life.seed." + q.Id);
                case "month": return Single("month", q.Id, "life.month.body");
                case "scandal": return Card("scandal", q.Id, "life.scandal", "life.scandal", ScandalChoices);
                case "absent": return Card("absent", q.Id, "life.absent", "life.absent", AbsentChoices, q.Name);
                case "results": return Card("results", q.Id, "life.results." + q.Id, "life.results", ResultChoices);
                case "home": return Card("home", q.Id, "life.home." + q.Id, "life.home." + q.Id, AorB);
                default: return Card("arc", q.Id, "life.arc." + q.Id, "life.arc." + q.Id, AorB, q.Name);
            }
        }

        private static LifeCard Single(string kind, string id, string body) =>
            new LifeCard(kind, id, body, OkOnly, ImmutableArray.Create(OkButton));

        private readonly struct Outcome
        {
            public Outcome(ChronicleEffect effect, int energy = 0, int family = 0, int conscience = 0) { Effect = effect; Energy = energy; Family = family; Conscience = conscience; }
            public ChronicleEffect Effect { get; }
            public int Energy { get; }
            public int Family { get; }
            public int Conscience { get; }
        }

        private static Outcome HomeOutcome(string id, string choice) => (id, choice) switch
        {
            ("child_failing", "a") => new Outcome(new ChronicleEffect(Cash: -600), -8, 6),
            ("child_failing", _) => new Outcome(new ChronicleEffect(), 0, -3),
            ("anniversary", "a") => new Outcome(new ChronicleEffect(Cash: -800), 3, 8),
            ("anniversary", _) => new Outcome(new ChronicleEffect(Staff: 1), 0, -8),
            ("job_offer", "a") => new Outcome(new ChronicleEffect(Staff: 2), 0, 2),
            ("job_offer", _) => new Outcome(new ChronicleEffect(Cash: 2000, Staff: -2), 0, -2),
            ("doctor", "a") => new Outcome(new ChronicleEffect(Cash: -500), 15),
            ("doctor", _) => new Outcome(new ChronicleEffect(), -10),
            ("parent_ill", "a") => new Outcome(new ChronicleEffect(Staff: -1), -8, 10),
            ("parent_ill", _) => new Outcome(new ChronicleEffect(), 0, -5),
            ("friend_loan", "a") => new Outcome(new ChronicleEffect(Cash: -2500), 0, 2, 2),
            ("friend_loan", _) => new Outcome(new ChronicleEffect(), 0, -3),
            ("kid_at_school", "a") => new Outcome(new ChronicleEffect(Staff: 1), 0, -4, 3),
            ("kid_at_school", _) => new Outcome(new ChronicleEffect(Staff: -1), 0, 5, -10),
            ("vacation", "a") => new Outcome(new ChronicleEffect(Cash: -3000, Parents: -1), 30, 12),
            ("vacation", _) => new Outcome(new ChronicleEffect(), -5, -6),
            ("crisis", "a") => new Outcome(new ChronicleEffect(Cash: -1000, Staff: -2, Parents: -1), -5, 20),
            _ => new Outcome(new ChronicleEffect(Reputation: -3, Staff: -3), -10, -4),
        };

        private static Outcome ArcOutcome(LifeArc arc, string choice)
        {
            bool earlier = arc.Answers.Contains('a');
            return (arc.Id, arc.Stage, choice) switch
            {
                ("teacher_quit", 0, "a") => new Outcome(new ChronicleEffect(Staff: 1), -5),
                ("teacher_quit", 0, _) => new Outcome(new ChronicleEffect(Staff: -2)),
                ("teacher_quit", 1, "a") => new Outcome(new ChronicleEffect(Cash: -1500, Staff: 1)),
                ("teacher_quit", 1, _) => new Outcome(new ChronicleEffect(Staff: -1)),
                ("teacher_quit", _, "a") => new Outcome(earlier ? new ChronicleEffect(Staff: 3, Academics: 1) : new ChronicleEffect(Staff: 1), -3),
                ("teacher_quit", _, _) => new Outcome(earlier ? new ChronicleEffect(Staff: 1) : new ChronicleEffect(Staff: -3, Academics: -2)),
                ("parent_threat", 0, "a") => new Outcome(new ChronicleEffect(Parents: 1), -5),
                ("parent_threat", 0, _) => new Outcome(new ChronicleEffect(Parents: -2)),
                ("parent_threat", 1, "a") => new Outcome(new ChronicleEffect(Cash: -2000, Parents: 1)),
                ("parent_threat", 1, _) => new Outcome(new ChronicleEffect(Reputation: -1)),
                ("parent_threat", _, "a") => new Outcome(earlier ? new ChronicleEffect(Parents: 3, Reputation: 1) : new ChronicleEffect(Parents: -1), -3),
                _ => new Outcome(earlier ? new ChronicleEffect(Reputation: 1) : new ChronicleEffect(Reputation: -4, Parents: -3)),
            };
        }

        private static Outcome AbsentOutcome(string choice) => choice switch
        {
            "cover" => new Outcome(new ChronicleEffect(Staff: -1), -5),
            "merge" => new Outcome(new ChronicleEffect(Academics: -1)),
            _ => new Outcome(new ChronicleEffect(Cash: -SubstituteCost)),
        };

        private static Outcome ResultOutcome(string band, string choice) => (band, choice) switch
        {
            ("good", "publish") => new Outcome(new ChronicleEffect(Reputation: 3, Parents: 2), 0, 0, 2),
            ("mid", "publish") => new Outcome(new ChronicleEffect(Parents: 1), 0, 0, 2),
            ("bad", "publish") => new Outcome(new ChronicleEffect(Reputation: -3, Parents: -2), 0, 0, 4),
            (_, _) => new Outcome(new ChronicleEffect(Reputation: 1, Parents: 3, Academics: -2), 0, 0, -12),
        };

        private static Outcome ScandalOutcome(string choice) => choice == "confess"
            ? new Outcome(new ChronicleEffect(Reputation: -6, Parents: -3), 0, 0, 15)
            : new Outcome(new ChronicleEffect(Reputation: -4, Staff: -6), 0, 0, -10);

        /// <summary>Cash this answer to the card now showing would cost, for the button and the guard. Every card has at least one free answer.</summary>
        public static int CardCost(LifeState s, string choice)
        {
            if (s.PendingEvent != null || s.PendingCase != null) return 0;
            if (s.PendingRival != null) return RivalCost(s.PendingRival, choice);
            if (s.PendingMinistry) return choice == "prepare" ? MinistryPrepareCost : 0;
            LifeQueued? q = TopQueued(s);
            if (q == null) return 0;
            ChronicleEffect effect = q.Kind switch
            {
                "absent" => AbsentOutcome(choice).Effect,
                "home" => HomeOutcome(q.Id, choice).Effect,
                "arc" => ArcOutcome(FindArc(s, q.Id), choice).Effect,
                _ => new ChronicleEffect(),
            };
            return Math.Max(0, -effect.Cash);
        }

        private static LifeArc FindArc(LifeState s, string cardId) => s.StoryArcs.First(a => a.Id == cardId.Split('.')[0]);

        private static LifeState ResolveQueued(LifeState state, LifeQueued q, string choice)
        {
            LifeCard card = QueuedCard(q);
            if (!card.Choices.Contains(choice)) throw UnknownChoice();
            RequireCash(state, CardCost(state, choice));
            LifeState s = state with { CardQueue = state.CardQueue.Remove(q) };
            switch (q.Kind)
            {
                case "month": return s;
                case "seed":
                    LifeSeedRule rule = SeedRules.First(r => r.Seed == q.Id);
                    return Close(Apply(s, rule.Effect, $"life.seed.{q.Id}.ok"));
                case "scandal":
                    return Land(s, ScandalOutcome(choice), $"life.scandal.{choice}.ok");
                case "absent":
                    return Land(s, AbsentOutcome(choice), $"life.absent.{choice}.ok", q.Name);
                case "results":
                    LifeState done = Land(s with { RoundUps = s.RoundUps + (choice == "round_up" ? 1 : 0) }, ResultOutcome(q.Id, choice), $"life.results.{choice}.ok");
                    if (done.Ending != ChronicleEnding.InProgress || done.Followed.IsEmpty) return done;
                    return Apply(done, new ChronicleEffect(), "life.results.top", done.Followed.OrderByDescending(x => x.Marks).ThenBy(x => x.Id).First().Name);
                case "home":
                    return Land(s, HomeOutcome(q.Id, choice), $"life.home.{q.Id}.{choice}.ok");
                default:
                    return ResolveArc(s, q, choice);
            }
        }

        private static LifeState ResolveArc(LifeState s, LifeQueued q, string choice)
        {
            LifeArc arc = FindArc(s, q.Id);
            Outcome outcome = ArcOutcome(arc, choice);
            uint random = s.RandomState;
            int stage = arc.Stage + 1;
            int next = stage >= 3 ? -1 : s.Day + 2 + Chronicle.Roll(ref random, 3);
            LifeState after = s with { RandomState = random, StoryArcs = s.StoryArcs.Replace(arc, arc with { Stage = stage, NextDay = next, Answers = arc.Answers + choice }) };
            // Wishing them well means they go; the earlier answers only decide how it lands.
            if (arc.Id == "teacher_quit" && arc.Stage == 2 && choice == "b" && after.Roster.Length > 1)
            {
                LifeTeacher? leaving = after.Roster.FirstOrDefault(t => t.Name == arc.Name && t.TrainingEnd == 0);
                if (leaving != null) after = after with { Roster = after.Roster.Remove(leaving), Plan = after.Plan! with { Teachers = after.Plan.Teachers - 1 } };
            }
            return Land(after, outcome, $"life.arc.{q.Id}.{choice}.ok", q.Name);
        }

        private static LifeState Land(LifeState s, Outcome o, string key, int name = -1) =>
            Close(Shift(Personal(Apply(s, o.Effect, key, name), o.Energy, o.Family), o.Conscience));

        /// <summary>Plants the delayed consequence and moves the conscience after an event answer.</summary>
        private static LifeState AfterEvent(LifeState s, ChronicleEvent ev, ChronicleChoice choice, bool failed)
        {
            LifeState next = choice.Failure != null ? s with { RisksTaken = s.RisksTaken + 1 } : s;
            LifeSeedRule? rule = SeedRules.FirstOrDefault(r => r.Event == ev.Id && r.Choice == choice.Id);
            // A failed gamble already blew up, so there is nothing left to expose later.
            if (rule != null && !failed) next = next with { Seeds = next.Seeds.Add(new LifeSeed(rule.Seed, Math.Min(next.Week + rule.Weeks, Chronicle.WeeksInYear))) };
            return Shift(next, ConscienceShift(ev.Id, choice.Id));
        }
    }
}
