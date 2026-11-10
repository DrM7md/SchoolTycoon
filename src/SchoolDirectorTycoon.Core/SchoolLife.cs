using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SchoolDirectorTycoon.Core
{
    public enum LifePhase { Setup, School, Evening }

    public sealed record LifeBuilding(string Id, int Capacity, int WeeklyRent, bool HasGym);

    /// <summary>Everything the director sets by hand. Minutes count from midnight.</summary>
    public sealed record LifePlan(string BuildingId, int Seats, int Fee, int Teachers, int Admins, int StartMinute, int Periods, int PeriodMinutes = SchoolLife.DefaultPeriodMinutes, int BreakMinutes = SchoolLife.DefaultBreakMinutes);

    /// <summary>Immutable state of one hands-on school year. The caller supplies elapsed game minutes and the real
    /// calendar day, so the same state and the same calls always give the same result.</summary>
    public sealed record LifeState
    {
        public LifePhase Phase { get; init; }
        public int Day { get; init; }
        public int Minute { get; init; }
        public LifePlan? Plan { get; init; }
        public int Cash { get; init; }
        public int Students { get; init; }
        public int Reputation { get; init; }
        public int Parents { get; init; }
        public int Staff { get; init; }
        public int Academics { get; init; }
        public uint RandomState { get; init; }
        public string? EventToday { get; init; }
        public int EventMinute { get; init; } = -1;
        public string? PendingEvent { get; init; }
        public ChronicleEnding Ending { get; init; }
        public bool GymContract { get; init; }
        public int LastRealDay { get; init; }
        public int Streak { get; init; }
        public int LetterIndex { get; init; }
        public string? PendingLetter { get; init; }
        public int LetterBonus { get; init; }
        public int AwayIncome { get; init; }

        public LifeIdentity? Identity { get; init; }
        public int DayExtra { get; init; }
        public int NextPersonId { get; init; } = 1;
        public ImmutableArray<LifeTeacher> Roster { get; init; } = ImmutableArray<LifeTeacher>.Empty;
        public ImmutableArray<LifeCandidate> Candidates { get; init; } = ImmutableArray<LifeCandidate>.Empty;
        public bool InterviewAsked { get; init; }
        public string? InterviewReply { get; init; }
        public int Dismissals { get; init; }
        public int CoursesFinished { get; init; }
        public ImmutableArray<LifeStudent> Followed { get; init; } = ImmutableArray<LifeStudent>.Empty;
        public LifeCase? CaseToday { get; init; }
        public LifeCase? PendingCase { get; init; }
        public ImmutableArray<LifeFollowUp> FollowUps { get; init; } = ImmutableArray<LifeFollowUp>.Empty;
        public ImmutableArray<string> ActionsToday { get; init; } = ImmutableArray<string>.Empty;
        public int LastNow { get; init; }
        public string? BuildingJob { get; init; }
        public int BuildingEnd { get; init; }
        public ImmutableArray<string> UpgradesDone { get; init; } = ImmutableArray<string>.Empty;
        public int LoanWeeks { get; init; }
        public int RivalPressure { get; init; }
        public int RivalStage { get; init; }
        public string? PendingRival { get; init; }
        public bool PendingMinistry { get; init; }
        public bool PendingSpeech { get; init; }
        public string? GoalId { get; init; }
        public bool GoalDone { get; init; }
        public ImmutableArray<string> Achievements { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<int> Memories { get; init; } = ImmutableArray<int>.Empty;

        public string Scenario { get; init; } = "standard";
        public uint RunId { get; init; }
        public LifePlan? OpeningPlan { get; init; }
        public int DebtWeeks { get; init; }
        public LifeDayReport? LastDay { get; init; }
        public int MarkCash { get; init; }
        public int MarkRep { get; init; }
        public int MarkParents { get; init; }
        public int MarkStaff { get; init; }
        public int MarkAcad { get; init; }
        public int MarkLog { get; init; }
        public ImmutableArray<LifeMonth> MonthCards { get; init; } = ImmutableArray<LifeMonth>.Empty;
        public int StarsBest { get; init; } = 1;
        public ImmutableArray<LifeCelebration> Celebrations { get; init; } = ImmutableArray<LifeCelebration>.Empty;
        public ImmutableArray<LifeChat> Chat { get; init; } = ImmutableArray<LifeChat>.Empty;
        /// <summary>Things that happened since the last chat, for the staff room to talk about.</summary>
        public ImmutableArray<string> Tags { get; init; } = ImmutableArray<string>.Empty;
        public int LastFee { get; init; }
        public ImmutableArray<LifeSeed> Seeds { get; init; } = ImmutableArray<LifeSeed>.Empty;
        public int Conscience { get; init; } = SchoolLife.StartConscience;
        public bool ScandalDone { get; init; }
        public ImmutableArray<LifeQueued> CardQueue { get; init; } = ImmutableArray<LifeQueued>.Empty;
        public ImmutableArray<string> Morning { get; init; } = SchoolLife.DefaultMorning;
        public int Energy { get; init; } = SchoolLife.StartEnergy;
        public int Family { get; init; } = SchoolLife.StartFamily;
        public int LastCrisisDay { get; init; } = -100;
        public int LastTotmMonth { get; init; } = -1;
        public ImmutableArray<LifeArc> StoryArcs { get; init; } = ImmutableArray<LifeArc>.Empty;
        public string? ChallengeId { get; init; }
        public int ChallengeWeek { get; init; } = -1;
        public int ChallengeDay { get; init; }
        public bool ChallengeBroken { get; init; }
        public bool ChallengeDone { get; init; }
        public int ChallengesWon { get; init; }
        public int RoundUps { get; init; }
        public int RisksTaken { get; init; }
        public int CasePunishes { get; init; }
        public int CaseTalks { get; init; }

        /// <summary>School week 1..36; 0 before opening.</summary>
        public int Week => Day <= 0 ? 0 : (Day - 1) / SchoolLife.DaysInWeek + 1;
        /// <summary>Season id for the UI banner, or null.</summary>
        public string? Season => SchoolLife.SeasonAt(Week);
        /// <summary>The one decision currently stopping the clock, or null. Letters are separate.</summary>
        public LifeCard? PendingCard => SchoolLife.CardOf(this);
        public ImmutableArray<string> Decisions { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> UsedEvents { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<ChronicleEntry> Log { get; init; } = ImmutableArray<ChronicleEntry>.Empty;
    }

    /// <summary>Rules of the hands-on mode: rent a school, staff it, run the clock, earn after hours, read the morning letter.</summary>
    public static partial class SchoolLife
    {
        public const int StartingCash = 120000;
        public const int DaysInYear = 180;
        public const int DaysInWeek = 5;
        public const int DefaultPeriodMinutes = 45, MinPeriodMinutes = 30, MaxPeriodMinutes = 60;
        public const int DefaultBreakMinutes = 20, MinBreakMinutes = 10, MaxBreakMinutes = 40;
        public const int MinutesStep = 5, ExtendStep = 15, MinDayExtra = -45, MaxDayExtra = 60;
        public const int CaseChancePercent = 12;
        public const int BreakAfterPeriod = 3;
        public const int EventChancePercent = 17;
        public const int QuietLines = 8;
        public const int TeacherWeeklyPay = 2400;
        public const int AdminWeeklyPay = 1700;
        public const int UtilityPerStudent = 25;
        public const int DepositWeeks = 4;
        public const int FurnishPerSeat = 100;
        public const int HiringCost = 500;
        public const int TutoringPerStudent = 25;
        public const int GymNightPay = 1800;
        public const int GymContractNightly = 800;
        public const int GymContractAwayDaily = 600;
        public const int MaxAwayDays = 3;
        public const int MinFee = 80, MaxFee = 500, MinStaff = 1, MaxTeachers = 40, MaxAdmins = 15;
        public const int InterviewCost = 300, SummerCampWeek = 28, SummerCampPay = 2500, AdultCoursesPay = 700;
        public const int LoanAmount = 50000, LoanPayment = 2200, LoanTerm = 30, CanteenWeekly = 1500;
        public const int WingSeats = 60, TrainingSlots = 2, ExtraTrainingSlots = 2, GoalReward = 800;
        public const int RivalStartWeek = 10, MinistryWeek = 24, FollowUpDays = 7;
        public const int MinStart = 390, MaxStart = 540, MinPeriods = 4, MaxPeriods = 8;
        public const int NightlyRecovery = 5;

        public static readonly ImmutableArray<LifeBuilding> Buildings = ImmutableArray.Create(
            new LifeBuilding("villa", 90, 5000, false),
            new LifeBuilding("campus", 180, 9000, true),
            new LifeBuilding("academy", 320, 16000, true));

        public static readonly ImmutableArray<string> ActivityIds = ImmutableArray.Create("tutoring", "gym_night", "gym_contract", "parents_night", "summer_camp", "adult_courses", "home");

        public static LifeBuilding Building(string id) => Buildings.First(b => b.Id == id);

        public static LifeState New(uint seed) => new LifeState
        {
            Cash = StartingCash,
            Reputation = 50,
            Parents = 55,
            Staff = 60,
            Academics = 50,
            RandomState = seed == 0 ? 2463534242u : seed,
        };

        public static int OpeningCost(LifePlan plan, string scenario = "standard") =>
            ScenarioRent(scenario, Building(plan.BuildingId)) * DepositWeeks + plan.Seats * FurnishPerSeat + (plan.Teachers + plan.Admins) * HiringCost;

        public static int WeeklyBill(LifePlan plan, int students) =>
            Building(plan.BuildingId).WeeklyRent + plan.Teachers * TeacherWeeklyPay + plan.Admins * AdminWeeklyPay + students * UtilityPerStudent;

        /// <summary>How many families want a seat at this fee and standing, before the seat limit.</summary>
        public static int Demand(int fee, int reputation, int parents) => Math.Max(0, (500 - fee) * 8 / 10 * ((reputation + parents) / 2 + 30) / 80);

        public static int ExpectedStudents(LifePlan plan, int reputation = 50, int parents = 55) => Math.Min(plan.Seats, Demand(plan.Fee, reputation, parents));

        public static int EndMinute(LifePlan plan) => plan.StartMinute + plan.Periods * plan.PeriodMinutes + plan.BreakMinutes;

        /// <summary>Today's last bell, including the director's extension.</summary>
        public static int DayEnd(LifeState state) => EndMinute(state.Plan!) + state.DayExtra;

        /// <summary>Lengthens or shortens today by one 15-minute step while school is in session.</summary>
        public static LifeState ExtendDay(LifeState state, int delta)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.School);
            RequireNoPending(state);
            int extra = state.DayExtra + delta;
            int end = EndMinute(state.Plan!) + extra;
            if ((delta != ExtendStep && delta != -ExtendStep) || extra < MinDayExtra || extra > MaxDayExtra || end < state.Minute)
                throw new RuleException(RuleCode.LifeDayLimit, "لا يمكن تعديل اليوم أكثر من ذلك.", "The day cannot be changed any further.");
            // A shortened day still reaches today's scheduled decision.
            int due = state.EventMinute > end ? Math.Max(state.Minute, end) : state.EventMinute;
            return state with { DayExtra = extra, EventMinute = due };
        }

        /// <summary>The first day's bell. Throws when the plan is invalid or cannot be paid for.</summary>
        public static LifeState Open(LifeState state, LifePlan plan, LifeStart start)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Setup);
            Validate(plan, start.Scenario);
            ValidateStart(start, plan);
            int cost = OpeningCost(plan, start.Scenario);
            int cash = start.Scenario == "rescue" ? start.StartingCash * 70 / 100 : start.StartingCash;
            if (cost > cash)
                throw new RuleException(RuleCode.LifeNotEnoughCash, "لا يكفي المال لهذه الخطة.", "There is not enough money for this plan.");
            (int rep, int parents) = ScenarioStanding(start.Scenario, state.Reputation, state.Parents);
            LifeState set = state with
            {
                Plan = plan, OpeningPlan = plan, Identity = start.Identity, Scenario = start.Scenario, Cash = cash, Reputation = rep, Parents = parents,
                RunId = state.RandomState, LastFee = plan.Fee, DebtWeeks = start.Scenario == "rescue" ? RescueDebtWeeks : 0,
            };
            uint random = set.RandomState;
            int students = Math.Min(plan.Seats, CurrentDemand(set) * (85 + Chronicle.Roll(ref random, 16)) / 100);
            LifeState opened = ScheduleArcs(SeedPeople(set with { Students = students, RandomState = random }));
            opened = Apply(opened with { Day = 1 }, new ChronicleEffect(Cash: -cost, Students: students), "life.opening");
            return BeginDay(opened with { MarkCash = opened.Cash, MarkRep = rep, MarkParents = parents, MarkStaff = opened.Staff, MarkAcad = opened.Academics, MarkLog = opened.Log.Length });
        }

        /// <summary>Moves the school clock forward. Stops early at the day's event or the last bell.</summary>
        public static LifeState Advance(LifeState state, int minutes)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.School);
            RequireNoPending(state);
            int end = DayEnd(state);
            int target = Math.Min(state.Minute + Math.Max(0, minutes), end);
            if (state.EventMinute >= 0 && target >= state.EventMinute)
            {
                if (state.EventToday != null)
                    return state with { Minute = state.EventMinute, PendingEvent = state.EventToday, EventToday = null, CaseToday = null, EventMinute = -1 };
                if (state.CaseToday != null)
                    return state with { Minute = state.EventMinute, PendingCase = state.CaseToday, CaseToday = null, EventMinute = -1 };
            }
            if (target < end) return state with { Minute = target };
            LifeState done = state with { Minute = end, Phase = LifePhase.Evening };
            if (done.Log.Length > 0 && done.Log[done.Log.Length - 1].Week == done.Day) return FinishDay(done);
            uint random = done.RandomState;
            string line = "life.quiet." + Chronicle.Roll(ref random, QuietLines);
            return FinishDay(Apply(done with { RandomState = random }, new ChronicleEffect(), line));
        }

        public static LifeState Choose(LifeState state, string choiceId)
        {
            RequireOpen(state);
            if (state.PendingEvent == null)
                throw new RuleException(RuleCode.ChronicleNoPendingEvent, "لا يوجد حدث بانتظار قرارك.", "There is no event waiting for a decision.");
            ChronicleEvent current = ChronicleDeck.Find(state.PendingEvent);
            ChronicleChoice? choice = current.Choices.FirstOrDefault(c => c.Id == choiceId);
            if (choice == null)
                throw new RuleException(RuleCode.ChronicleUnknownChoice, "هذا الخيار غير متاح.", "That choice is not available.");
            uint random = state.RandomState;
            bool failed = choice.Failure != null && Chronicle.Roll(ref random, 100) < choice.RiskPercent;
            string key = $"ev.{current.Id}.{choice.Id}.{(failed ? "fail" : "ok")}";
            LifeState chosen = Apply(state with { RandomState = random, PendingEvent = null }, failed ? choice.Failure! : choice.Effect, key);
            return Close(AfterEvent(chosen, current, choice, failed));
        }

        /// <summary>The activities on offer tonight, in display order.</summary>
        public static IReadOnlyList<string> Activities(LifeState state)
        {
            bool gym = state.Plan != null && Building(state.Plan.BuildingId).HasGym;
            return ActivityIds.Where(id => id switch
            {
                "gym_night" => gym && !state.GymContract,
                "gym_contract" => gym && !state.GymContract,
                "summer_camp" => state.Week >= SummerCampWeek,
                _ => true,
            }).ToArray();
        }

        /// <summary>The money an activity should bring before any mishap, for the button label.</summary>
        public static int ActivityEstimate(LifeState state, string id) => id switch
        {
            "tutoring" => Tutoring(state),
            "gym_night" => GymNightPay,
            "gym_contract" => GymContractNightly,
            "summer_camp" => SummerCampPay,
            "adult_courses" => AdultCoursesPay,
            _ => 0,
        };

        private static int Tutoring(LifeState state) => Math.Min(state.Students / 5, state.Plan!.Teachers * 6) * TutoringPerStudent;

        /// <summary>Spends tonight, then pays the week if it ended and opens the next morning.</summary>
        public static LifeState Evening(LifeState state, string activity)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Evening);
            RequireNoPending(state);
            if (!Activities(state).Contains(activity))
                throw new RuleException(RuleCode.LifeUnknownActivity, "هذا النشاط غير متاح.", "That activity is not available.");
            if (activity != "home") RequireEnergy(state);

            uint random = state.RandomState;
            int contract = state.GymContract ? GymContractNightly : 0;
            ChronicleEffect effect;
            string result = "ok";
            bool signs = false;
            switch (activity)
            {
                case "tutoring":
                    effect = new ChronicleEffect(Cash: Tutoring(state) + contract,
                        Staff: Chronicle.Roll(ref random, 100) < 40 ? -1 : 0,
                        Academics: Chronicle.Roll(ref random, 100) < 25 ? 1 : 0);
                    break;
                case "gym_night":
                    if (Chronicle.Roll(ref random, 100) < 15)
                    {
                        result = "fail";
                        effect = new ChronicleEffect(Cash: GymNightPay - 2600, Parents: -1);
                    }
                    else effect = new ChronicleEffect(Cash: GymNightPay);
                    break;
                case "gym_contract":
                    signs = true;
                    effect = new ChronicleEffect(Cash: GymContractNightly);
                    break;
                case "parents_night":
                    effect = new ChronicleEffect(Cash: contract - 400, Parents: 2, Reputation: Chronicle.Roll(ref random, 100) < 30 ? 1 : 0, Staff: Chronicle.Roll(ref random, 100) < 30 ? -1 : 0);
                    break;
                case "summer_camp":
                    effect = new ChronicleEffect(Cash: contract + SummerCampPay, Staff: -1);
                    break;
                case "adult_courses":
                    effect = new ChronicleEffect(Cash: contract + AdultCoursesPay, Staff: Chronicle.Roll(ref random, 100) < 20 ? -1 : 0);
                    break;
                default:
                    effect = new ChronicleEffect(Cash: contract, Staff: state.Energy > 0 ? 2 : 0); // A collapse is not a rest the staff feel.
                    break;
            }

            if (state.DayExtra >= 30)
                effect = effect with
                {
                    Staff = effect.Staff + (Chronicle.Roll(ref random, 100) < 30 ? -1 : 0),
                    Academics = effect.Academics + (Chronicle.Roll(ref random, 100) < 40 ? 1 : 0),
                };

            LifeState next = Apply(state with { RandomState = random, GymContract = state.GymContract || signs }, effect, $"life.act.{activity}.{result}");
            next = Personal(next, ActivityEnergy(activity) + NightlyRecovery, activity == "home" ? 3 : -1);
            if (activity == "tutoring" && next.ChallengeId == "no_tutoring") next = next with { ChallengeBroken = true };
            next = CheckGoal(next, effect.Cash > 0);
            if (next.Day % DaysInWeek == 0) next = PayWeek(next);
            next = Close(next);
            if (next.Ending != ChronicleEnding.InProgress) return next;
            if (next.Day >= DaysInYear) return next with { PendingSpeech = true };
            return BeginDay(next with { Day = next.Day + 1 });
        }

        /// <summary>Changes the plan after hours. The building stays; new hires cost money and dismissals hurt morale.</summary>
        public static LifeState Adjust(LifeState state, LifePlan plan)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Evening);
            RequireNoPending(state);
            Validate(plan, state.Scenario, Capacity(state));
            LifePlan old = state.Plan!;
            if (plan.BuildingId != old.BuildingId || plan.Teachers != old.Teachers)
                throw new RuleException(RuleCode.LifeInvalidPlan, "لا يمكن تغيير المبنى أو عدد المعلمين من هنا.", "The building and teacher count cannot change here.");
            int cost = Math.Max(0, plan.Admins - old.Admins) * HiringCost + Math.Max(0, plan.Seats - old.Seats) * FurnishPerSeat
                + Math.Max(0, old.Admins - plan.Admins) * AdminWeeklyPay;
            if (cost > state.Cash)
                throw new RuleException(RuleCode.LifeNotEnoughCash, "لا يكفي المال لهذه التغييرات.", "There is not enough money for these changes.");
            if (plan == old) return state;
            return Apply(state with { Plan = plan }, new ChronicleEffect(Cash: -cost, Staff: -2 * Math.Max(0, old.Admins - plan.Admins)), "life.adjusted");
        }

        /// <summary>Call when the game opens. The first opening of each new calendar day leaves a letter on the desk.
        /// Nothing is lost while away. Missing one day keeps the streak; missing more starts it again.</summary>
        public static LifeState Greet(LifeState state, int realDay)
        {
            if (state.Phase == LifePhase.Setup || state.Ending != ChronicleEnding.InProgress || state.PendingLetter != null) return state;
            if (realDay <= state.LastRealDay) return state;
            int gap = state.LastRealDay == 0 ? 0 : realDay - state.LastRealDay;
            int streak = state.LastRealDay != 0 && gap <= 2 ? state.Streak + 1 : 1;
            int away = state.GymContract ? Math.Min(gap, MaxAwayDays) * GymContractAwayDaily : 0;
            return StartChallenge(state with
            {
                LastRealDay = realDay,
                Streak = streak,
                PendingLetter = LetterKey(state.LetterIndex, state.Decisions),
                GoalId = Goals[((realDay % Goals.Length) + Goals.Length) % Goals.Length],
                GoalDone = false,
                LetterBonus = 400 + 200 * Math.Min(streak - 1, 6),
                AwayIncome = away,
            }, realDay);
        }

        /// <summary>The answers a letter asks for; empty when it only needs reading.</summary>
        public static IReadOnlyList<string> LetterChoices(string letterKey) =>
            letterKey.EndsWith(".2", StringComparison.Ordinal) && !letterKey.Contains(".plain.") ? new[] { "a", "b" } : Array.Empty<string>();

        public static LifeState ReadLetter(LifeState state, string? choice = null)
        {
            RequireOpen(state);
            if (state.PendingLetter == null)
                throw new RuleException(RuleCode.LifeNoLetter, "لا توجد رسالة اليوم.", "There is no letter today.");
            string key = state.PendingLetter;
            bool decides = LetterChoices(key).Count > 0;
            if (decides != (choice != null) || (decides && !LetterChoices(key).Contains(choice!)))
                throw new RuleException(RuleCode.ChronicleUnknownChoice, "هذا الخيار غير متاح.", "That choice is not available.");

            ChronicleEffect extra = decides ? LetterEffect(key, choice!) : new ChronicleEffect();
            int milestone = state.Streak % 7 == 0 ? 1 : 0;
            var effect = extra with { Cash = extra.Cash + state.LetterBonus + state.AwayIncome, Reputation = extra.Reputation + milestone };
            ImmutableArray<string> decisions = decides ? state.Decisions.Add(Arc(key) + ":" + choice) : state.Decisions;
            LifeState read = state with { PendingLetter = null, LetterBonus = 0, AwayIncome = 0, LetterIndex = state.LetterIndex + 1, Decisions = decisions };
            LifeState after = Apply(read, effect, decides ? $"{key}.{choice}" : key);
            if (after.Streak >= 7) after = Award(after, "streak_seven");
            return Close(after);
        }

        public static readonly ImmutableArray<string> Arcs = ImmutableArray.Create("salem", "noura", "yousef");
        public const int ArcLetters = 5;
        public const int PlainLetters = 5;

        private static string Arc(string key) => key.Split('.')[2];

        /// <summary>Letters rotate through the three stories; each story's ending follows the answer given in its third letter.</summary>
        public static string LetterKey(int index, ImmutableArray<string> decisions)
        {
            if (index >= Arcs.Length * ArcLetters) return "life.letter.plain." + (index - Arcs.Length * ArcLetters) % PlainLetters;
            string arc = Arcs[index % Arcs.Length];
            int step = index / Arcs.Length;
            if (step < ArcLetters - 1) return $"life.letter.{arc}.{step}";
            string answer = decisions.FirstOrDefault(d => d.StartsWith(arc + ":", StringComparison.Ordinal))?.Substring(arc.Length + 1) ?? "a";
            return $"life.letter.{arc}.{step}.{answer}";
        }

        private static ChronicleEffect LetterEffect(string key, string choice) => (Arc(key), choice) switch
        {
            ("salem", "a") => new ChronicleEffect(Reputation: 1, Parents: 1),
            ("salem", _) => new ChronicleEffect(Parents: 1),
            ("noura", "a") => new ChronicleEffect(Cash: -6000, Reputation: 3, Academics: 1),
            ("noura", _) => new ChronicleEffect(Parents: -2),
            ("yousef", "a") => new ChronicleEffect(Cash: -3000, Staff: 3),
            _ => new ChronicleEffect(Staff: 1, Academics: -2),
        };

        public static ChronicleGrade Grade(LifeState state)
        {
            int score = (state.Reputation + state.Parents + state.Staff + state.Academics) / 4;
            if (state.Ending != ChronicleEnding.YearCompleted) return ChronicleGrade.D;
            return GradeOf(score);
        }

        private static LifeState BeginDay(LifeState state)
        {
            uint random = state.RandomState;
            LifePlan plan = state.Plan!;
            int week = state.Week;
            string? drawn = null;
            LifeCase? todayCase = null;
            int minute = -1;
            int window = Math.Max(1, EndMinute(plan) - plan.StartMinute - 60);
            if (Chronicle.Roll(ref random, 100) < CaseChancePercent && state.Followed.Length > 0)
            {
                LifeStudent student = state.Followed[Chronicle.Roll(ref random, state.Followed.Length)];
                todayCase = new LifeCase(student.Id, CaseIds[Chronicle.Roll(ref random, CaseIds.Length)]);
                minute = plan.StartMinute + 30 + Chronicle.Roll(ref random, window);
            }
            else
            {
                ChronicleEvent[] open = ChronicleDeck.All.Where(e => e.MinWeek <= week && !state.UsedEvents.Contains(e.Id)).ToArray();
                if (Chronicle.Roll(ref random, 100) < EventChancePercent && open.Length > 0)
                {
                    drawn = open[Chronicle.Roll(ref random, open.Length)].Id;
                    minute = plan.StartMinute + 30 + Chronicle.Roll(ref random, window);
                }
            }
            LifeState next = state with
            {
                Phase = LifePhase.School,
                Minute = plan.StartMinute,
                RandomState = random,
                EventToday = drawn,
                CaseToday = todayCase,
                EventMinute = minute,
                DayExtra = 0,
                ActionsToday = ImmutableArray<string>.Empty,
                UsedEvents = drawn != null ? state.UsedEvents.Add(drawn) : state.UsedEvents,
                PendingMinistry = state.PendingMinistry || (week == MinistryWeek && (state.Day - 1) % DaysInWeek == 0),
            };
            return MorningCards(RunFollowUps(MarkSeason(next)));
        }

        private static LifeState PayWeek(LifeState state)
        {
            LifePlan plan = state.Plan!;
            int week = state.Day / DaysInWeek, academics = 0, staff = 0, parents = 0, reputation = 0;
            bool Every(int n) => week % n == 0;
            bool Has(string id) => state.UpgradesDone.Contains(id);
            uint random = state.RandomState;

            int perTeacher = state.Students / Math.Max(1, Teaching(state));
            if (perTeacher > 26) { academics -= 3; staff -= 2; }
            else if (perTeacher > 20) academics -= 2;
            else if (perTeacher <= 14) { academics += 1; staff += 1; }

            int perAdmin = state.Students / plan.Admins;
            if (perAdmin > 70) parents -= 2;
            else if (perAdmin > 45) parents -= 1;

            int taught = plan.Periods * plan.PeriodMinutes;
            if (taught < 240) { if (SeasonAt(week) != "ramadan") academics -= 1; }
            else if (taught > 330) { academics += 1; staff -= 1; }

            if (plan.StartMinute < 420 || plan.StartMinute > 480) parents -= 1;
            int feeShift = state.Scenario == "international" ? 300 : 0;
            if (plan.Fee > 350 + feeShift) parents -= 4;
            else if (plan.Fee > 280 + feeShift) parents -= 2;
            if (Every(4)) { staff -= 1; academics -= 1; }
            if (state.Academics >= 65) reputation += 1;
            else if (state.Academics < 40) { reputation -= 2; parents -= 1; }

            PeopleWeek(state, week, ref random, ref academics, ref staff, ref parents);
            TimetableWeek(state, week, ref academics, ref parents);
            if (Every(2))
            {
                academics += state.Roster.Count(t => t.Role == "head");
                staff += state.Roster.Count(t => t.Role == "deputy");
            }
            // A burnt-out director and a neglected home both show up at work.
            if (state.Energy < 25) { staff -= 1; academics -= 1; }
            if (state.Family < 25) { reputation -= 1; parents -= 1; }

            // Expectations rise with success, so the top of every scale has to be defended.
            static int Expectation(int value) => value > 88 ? 3 : value > 75 ? 2 : value > 60 ? 1 : 0;
            reputation -= Expectation(state.Reputation);
            parents -= Expectation(state.Parents);
            staff -= Expectation(state.Staff);
            academics -= Expectation(state.Academics);

            switch (state.Identity?.Philosophy)
            {
                case "strict": if (Every(2)) academics += 1; if (Every(4)) parents -= 1; break;
                case "creative": if (Every(2)) staff += 1; if (Every(4)) academics -= 1; break;
                case "sports": if (Every(2)) parents += 1; if (Every(4)) academics -= 1; break;
                case "faith": if (Every(2)) parents += 1; if (Every(4)) reputation -= 1; break;
            }

            if (Has("library") && Every(2)) academics += 1;
            if (Has("lab") && Every(2)) academics += 1;
            if (Has("playground") && Every(4)) parents += 1;
            if (Has("garden") && Every(4)) reputation += 1;
            if (Has("ac") && parents < 0) parents += 1;

            int demand = CurrentDemand(state);
            int target = Math.Min(plan.Seats, demand);
            int step = 1 + Chronicle.Roll(ref random, 3);
            int moved = target > state.Students ? Math.Min(step, target - state.Students) : -Math.Min(step, state.Students - target);

            int pressure = state.RivalPressure, stage = state.RivalStage;
            string? rival = state.PendingRival;
            if (week >= RivalStartWeek)
            {
                pressure = Math.Clamp(pressure + (state.Reputation >= 65 ? 1 : state.Reputation < 45 ? 3 : 2), 0, 100);
                if (pressure > 50) moved -= 1 + Chronicle.Roll(ref random, 3);
                if (stage == 0 && pressure >= 30) { rival = "rival.30"; stage = 1; }
                else if (stage == 1 && pressure >= 60) { rival = "rival.60"; stage = 2; }
            }

            int income = state.Students * plan.Fee - WeeklyBill(state) + (Has("canteen") ? CanteenWeekly : 0);
            var effect = new ChronicleEffect(income, moved, reputation, parents, staff, academics);
            LifeState paid = Apply(state with
            {
                RandomState = random,
                RivalPressure = pressure,
                RivalStage = stage,
                PendingRival = rival,
                LoanWeeks = Math.Max(0, state.LoanWeeks - 1),
                DebtWeeks = Math.Max(0, state.DebtWeeks - 1),
            }, effect, "life.week");
            if (state.LoanWeeks == 1) paid = Apply(paid, new ChronicleEffect(), "life.loan.paid");
            if (paid.Students >= plan.Seats && plan.Seats >= 60) paid = Award(paid, "full_house");
            return StudentsWeek(paid);
        }

        private static void Validate(LifePlan p, string scenario, int? capacity = null)
        {
            LifeBuilding? building = Buildings.FirstOrDefault(b => b.Id == p.BuildingId);
            if (building == null || p.Seats < 1 || p.Seats > (capacity ?? ScenarioCapacity(scenario, building)) || p.Fee < MinFee || p.Fee > ScenarioFeeCap(scenario)
                || p.Teachers < MinStaff || p.Teachers > MaxTeachers || p.Admins < MinStaff || p.Admins > MaxAdmins
                || p.StartMinute < MinStart || p.StartMinute > MaxStart || p.Periods < MinPeriods || p.Periods > MaxPeriods
                || p.PeriodMinutes < MinPeriodMinutes || p.PeriodMinutes > MaxPeriodMinutes || p.PeriodMinutes % MinutesStep != 0
                || p.BreakMinutes < MinBreakMinutes || p.BreakMinutes > MaxBreakMinutes || p.BreakMinutes % MinutesStep != 0)
                throw new RuleException(RuleCode.LifeInvalidPlan, "هذه الخطة غير صالحة.", "That plan is not valid.");
        }

        private static void RequireOpen(LifeState state)
        {
            if (state.Ending != ChronicleEnding.InProgress)
                throw new RuleException(RuleCode.CampaignEnded, "انتهى العام الدراسي.", "The school year is over.");
        }

        private static void RequirePhase(LifeState state, LifePhase phase)
        {
            if (state.Phase != phase)
                throw new RuleException(RuleCode.LifeWrongPhase, "لا يمكن فعل هذا الآن.", "That cannot be done right now.");
        }

        private static void RequireNoPending(LifeState state)
        {
            if (state.PendingEvent != null)
                throw new RuleException(RuleCode.ChronicleEventPending, "قرر في الحدث الحالي أولًا.", "Decide the current event first.");
            if (state.PendingLetter != null)
                throw new RuleException(RuleCode.LifeLetterWaiting, "اقرأ رسالة الصباح أولًا.", "Read the morning letter first.");
            if (state.PendingCase != null || state.PendingRival != null || state.PendingMinistry || state.PendingSpeech || !state.CardQueue.IsEmpty)
                throw new RuleException(RuleCode.LifeCasePending, "قرر في الأمر المعروض أولًا.", "Decide the waiting matter first.");
        }

        private static LifeState Apply(LifeState state, ChronicleEffect e, string key, int name = -1, string? subject = null) => state with
        {
            Cash = state.Cash + e.Cash,
            Students = Math.Max(0, state.Students + (key == "life.opening" ? 0 : e.Students)),
            Reputation = Math.Clamp(state.Reputation + e.Reputation, 0, 100),
            Parents = Math.Clamp(state.Parents + e.Parents, 0, 100),
            Staff = Math.Clamp(state.Staff + e.Staff, 0, 100),
            Academics = Math.Clamp(state.Academics + e.Academics, 0, 100),
            Log = state.Log.Add(new ChronicleEntry(state.Day, key, e, name, subject)),
            Memories = IsMemory(key) ? state.Memories.Add(state.Log.Length) : state.Memories,
        };

        private static bool IsMemory(string key) =>
            key.StartsWith("life.letter.", StringComparison.Ordinal) || key.StartsWith("life.ministry", StringComparison.Ordinal)
            || key.StartsWith("life.grad", StringComparison.Ordinal) || key.StartsWith("life.ach.", StringComparison.Ordinal);

        private static LifeState Close(LifeState state)
        {
            ChronicleEnding ending =
                state.Cash < 0 ? ChronicleEnding.Bankrupt
                : state.Reputation <= 0 || state.Parents <= 0 ? ChronicleEnding.Dismissed
                : state.Staff <= 0 ? ChronicleEnding.StaffWalkout
                : ChronicleEnding.InProgress;
            return ending == ChronicleEnding.InProgress ? state : Ended(state with { Ending = ending, PendingEvent = null, PendingLetter = null, PendingCase = null, PendingRival = null, PendingMinistry = false, PendingSpeech = false, CardQueue = ImmutableArray<LifeQueued>.Empty });
        }
    }
}
