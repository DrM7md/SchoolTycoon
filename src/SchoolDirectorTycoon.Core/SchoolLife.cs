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
    public sealed record LifePlan(string BuildingId, int Seats, int Fee, int Teachers, int Admins, int StartMinute, int Periods);

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
        public ImmutableArray<string> Decisions { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> UsedEvents { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<ChronicleEntry> Log { get; init; } = ImmutableArray<ChronicleEntry>.Empty;
    }

    /// <summary>Rules of the hands-on mode: rent a school, staff it, run the clock, earn after hours, read the morning letter.</summary>
    public static class SchoolLife
    {
        public const int StartingCash = 120000;
        public const int DaysInYear = 180;
        public const int DaysInWeek = 5;
        public const int PeriodMinutes = 45;
        public const int BreakMinutes = 20;
        public const int BreakAfterPeriod = 3;
        public const int EventChancePercent = 17;
        public const int QuietLines = 8;
        public const int TeacherWeeklyPay = 2400;
        public const int AdminWeeklyPay = 1700;
        public const int UtilityPerStudent = 25;
        public const int DepositWeeks = 4;
        public const int FurnishPerSeat = 100;
        public const int HiringCost = 500;
        public const int GymNightPay = 1800;
        public const int GymContractNightly = 800;
        public const int GymContractAwayDaily = 600;
        public const int MaxAwayDays = 3;
        public const int MinFee = 80, MaxFee = 500, MinStaff = 1, MaxTeachers = 40, MaxAdmins = 15;
        public const int MinStart = 390, MaxStart = 540, MinPeriods = 4, MaxPeriods = 8;

        public static readonly ImmutableArray<LifeBuilding> Buildings = ImmutableArray.Create(
            new LifeBuilding("villa", 90, 5000, false),
            new LifeBuilding("campus", 180, 9000, true),
            new LifeBuilding("academy", 320, 16000, true));

        public static readonly ImmutableArray<string> ActivityIds = ImmutableArray.Create("tutoring", "gym_night", "gym_contract", "parents_night", "home");

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

        public static int OpeningCost(LifePlan plan) =>
            Building(plan.BuildingId).WeeklyRent * DepositWeeks + plan.Seats * FurnishPerSeat + (plan.Teachers + plan.Admins) * HiringCost;

        public static int WeeklyBill(LifePlan plan, int students) =>
            Building(plan.BuildingId).WeeklyRent + plan.Teachers * TeacherWeeklyPay + plan.Admins * AdminWeeklyPay + students * UtilityPerStudent;

        /// <summary>How many families want a seat at this fee and standing, before the seat limit.</summary>
        public static int Demand(int fee, int reputation, int parents) => Math.Max(0, (500 - fee) * 8 / 10 * ((reputation + parents) / 2 + 30) / 80);

        public static int ExpectedStudents(LifePlan plan, int reputation = 50, int parents = 55) => Math.Min(plan.Seats, Demand(plan.Fee, reputation, parents));

        public static int EndMinute(LifePlan plan) => plan.StartMinute + plan.Periods * PeriodMinutes + BreakMinutes;

        /// <summary>The first day's bell. Throws when the plan is invalid or cannot be paid for.</summary>
        public static LifeState Open(LifeState state, LifePlan plan)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Setup);
            Validate(plan);
            int cost = OpeningCost(plan);
            if (cost > state.Cash)
                throw new RuleException(RuleCode.LifeNotEnoughCash, "لا يكفي المال لهذه الخطة.", "There is not enough money for this plan.");
            uint random = state.RandomState;
            int students = Math.Min(plan.Seats, Demand(plan.Fee, state.Reputation, state.Parents) * (85 + Chronicle.Roll(ref random, 16)) / 100);
            LifeState opened = state with { Plan = plan, Students = students, RandomState = random };
            opened = Apply(opened with { Day = 1 }, new ChronicleEffect(Cash: -cost, Students: students), "life.opening");
            return BeginDay(opened);
        }

        /// <summary>Moves the school clock forward. Stops early at the day's event or the last bell.</summary>
        public static LifeState Advance(LifeState state, int minutes)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.School);
            RequireNoPending(state);
            int end = EndMinute(state.Plan!);
            int target = Math.Min(state.Minute + Math.Max(0, minutes), end);
            if (state.EventToday != null && state.EventMinute >= 0 && target >= state.EventMinute)
                return state with { Minute = state.EventMinute, PendingEvent = state.EventToday, EventToday = null, EventMinute = -1 };
            if (target < end) return state with { Minute = target };
            LifeState done = state with { Minute = end, Phase = LifePhase.Evening };
            if (done.Log.Length > 0 && done.Log[done.Log.Length - 1].Week == done.Day) return done;
            uint random = done.RandomState;
            string line = "life.quiet." + Chronicle.Roll(ref random, QuietLines);
            return Apply(done with { RandomState = random }, new ChronicleEffect(), line);
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
            return Close(Apply(state with { RandomState = random, PendingEvent = null }, failed ? choice.Failure! : choice.Effect, key));
        }

        /// <summary>The activities on offer tonight, in display order.</summary>
        public static IReadOnlyList<string> Activities(LifeState state)
        {
            bool gym = state.Plan != null && Building(state.Plan.BuildingId).HasGym;
            return ActivityIds.Where(id => id switch
            {
                "gym_night" => gym && !state.GymContract,
                "gym_contract" => gym && !state.GymContract,
                _ => true,
            }).ToArray();
        }

        /// <summary>The money an activity should bring before any mishap, for the button label.</summary>
        public static int ActivityEstimate(LifeState state, string id) => id switch
        {
            "tutoring" => Tutoring(state),
            "gym_night" => GymNightPay,
            "gym_contract" => GymContractNightly,
            _ => 0,
        };

        private static int Tutoring(LifeState state) => Math.Min(state.Students / 5, state.Plan!.Teachers * 6) * 40;

        /// <summary>Spends tonight, then pays the week if it ended and opens the next morning.</summary>
        public static LifeState Evening(LifeState state, string activity)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Evening);
            RequireNoPending(state);
            if (!Activities(state).Contains(activity))
                throw new RuleException(RuleCode.LifeUnknownActivity, "هذا النشاط غير متاح.", "That activity is not available.");

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
                default:
                    effect = new ChronicleEffect(Cash: contract, Staff: 2);
                    break;
            }

            LifeState next = Apply(state with { RandomState = random, GymContract = state.GymContract || signs }, effect, $"life.act.{activity}.{result}");
            if (next.Day % DaysInWeek == 0) next = PayWeek(next);
            next = Close(next);
            if (next.Ending != ChronicleEnding.InProgress) return next;
            if (next.Day >= DaysInYear) return next with { Ending = ChronicleEnding.YearCompleted };
            return BeginDay(next with { Day = next.Day + 1 });
        }

        /// <summary>Changes the plan after hours. The building stays; new hires cost money and dismissals hurt morale.</summary>
        public static LifeState Adjust(LifeState state, LifePlan plan)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Evening);
            Validate(plan);
            LifePlan old = state.Plan!;
            if (plan.BuildingId != old.BuildingId)
                throw new RuleException(RuleCode.LifeInvalidPlan, "لا يمكن تغيير المبنى خلال العام.", "The building cannot change during the year.");
            int hires = Math.Max(0, plan.Teachers - old.Teachers) + Math.Max(0, plan.Admins - old.Admins);
            int dismissed = Math.Max(0, old.Teachers - plan.Teachers) + Math.Max(0, old.Admins - plan.Admins);
            int cost = hires * HiringCost + Math.Max(0, plan.Seats - old.Seats) * FurnishPerSeat
                + Math.Max(0, old.Teachers - plan.Teachers) * TeacherWeeklyPay + Math.Max(0, old.Admins - plan.Admins) * AdminWeeklyPay;
            if (cost > state.Cash)
                throw new RuleException(RuleCode.LifeNotEnoughCash, "لا يكفي المال لهذه التغييرات.", "There is not enough money for these changes.");
            if (plan == old) return state;
            return Apply(state with { Plan = plan }, new ChronicleEffect(Cash: -cost, Staff: -2 * dismissed), "life.adjusted");
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
            return state with
            {
                LastRealDay = realDay,
                Streak = streak,
                PendingLetter = LetterKey(state.LetterIndex, state.Decisions),
                LetterBonus = 400 + 200 * Math.Min(streak - 1, 6),
                AwayIncome = away,
            };
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
            return Close(Apply(read, effect, decides ? $"{key}.{choice}" : key));
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
            return score >= 75 ? ChronicleGrade.S : score >= 62 ? ChronicleGrade.A : score >= 50 ? ChronicleGrade.B : ChronicleGrade.C;
        }

        private static LifeState BeginDay(LifeState state)
        {
            uint random = state.RandomState;
            LifePlan plan = state.Plan!;
            int week = (state.Day - 1) / DaysInWeek + 1;
            string? drawn = null;
            int minute = -1;
            ChronicleEvent[] open = ChronicleDeck.All.Where(e => e.MinWeek <= week && !state.UsedEvents.Contains(e.Id)).ToArray();
            if (Chronicle.Roll(ref random, 100) < EventChancePercent && open.Length > 0)
            {
                drawn = open[Chronicle.Roll(ref random, open.Length)].Id;
                minute = plan.StartMinute + 30 + Chronicle.Roll(ref random, Math.Max(1, EndMinute(plan) - plan.StartMinute - 60));
            }
            return state with
            {
                Phase = LifePhase.School,
                Minute = plan.StartMinute,
                RandomState = random,
                EventToday = drawn,
                EventMinute = minute,
                UsedEvents = drawn != null ? state.UsedEvents.Add(drawn) : state.UsedEvents,
            };
        }

        private static LifeState PayWeek(LifeState state)
        {
            LifePlan plan = state.Plan!;
            int week = state.Day / DaysInWeek, academics = 0, staff = 0, parents = 0, reputation = 0;

            int perTeacher = state.Students / plan.Teachers;
            if (perTeacher > 26) { academics -= 3; staff -= 2; }
            else if (perTeacher > 20) academics -= 2;
            else if (perTeacher <= 14) { academics += 1; staff += 1; }

            int perAdmin = state.Students / plan.Admins;
            if (perAdmin > 70) parents -= 2;
            else if (perAdmin > 45) parents -= 1;

            if (plan.Periods <= 5) { academics -= 1; staff += 1; }
            else if (plan.Periods == 7) { academics += 1; staff -= 1; }
            else if (plan.Periods >= 8) { academics += 2; staff -= 2; }

            if (plan.StartMinute < 420 || plan.StartMinute > 480) parents -= 1;
            if (plan.Fee > 350) parents -= 4;
            else if (plan.Fee > 280) parents -= 2;
            if (week % 4 == 0) { staff -= 1; academics -= 1; }
            if (state.Academics >= 65) reputation += 1;
            else if (state.Academics < 40) { reputation -= 2; parents -= 1; }

            uint random = state.RandomState;
            int target = Math.Min(plan.Seats, Demand(plan.Fee, state.Reputation, state.Parents));
            int step = 1 + Chronicle.Roll(ref random, 3);
            int moved = target > state.Students ? Math.Min(step, target - state.Students) : -Math.Min(step, state.Students - target);

            var effect = new ChronicleEffect(state.Students * plan.Fee - WeeklyBill(plan, state.Students), moved, reputation, parents, staff, academics);
            return Apply(state with { RandomState = random }, effect, "life.week");
        }

        private static void Validate(LifePlan p)
        {
            LifeBuilding? building = Buildings.FirstOrDefault(b => b.Id == p.BuildingId);
            if (building == null || p.Seats < 1 || p.Seats > building.Capacity || p.Fee < MinFee || p.Fee > MaxFee
                || p.Teachers < MinStaff || p.Teachers > MaxTeachers || p.Admins < MinStaff || p.Admins > MaxAdmins
                || p.StartMinute < MinStart || p.StartMinute > MaxStart || p.Periods < MinPeriods || p.Periods > MaxPeriods)
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
        }

        private static LifeState Apply(LifeState state, ChronicleEffect e, string key) => state with
        {
            Cash = state.Cash + e.Cash,
            Students = Math.Max(0, state.Students + (key == "life.opening" ? 0 : e.Students)),
            Reputation = Math.Clamp(state.Reputation + e.Reputation, 0, 100),
            Parents = Math.Clamp(state.Parents + e.Parents, 0, 100),
            Staff = Math.Clamp(state.Staff + e.Staff, 0, 100),
            Academics = Math.Clamp(state.Academics + e.Academics, 0, 100),
            Log = state.Log.Add(new ChronicleEntry(state.Day, key, e)),
        };

        private static LifeState Close(LifeState state)
        {
            ChronicleEnding ending =
                state.Cash < 0 ? ChronicleEnding.Bankrupt
                : state.Reputation <= 0 || state.Parents <= 0 ? ChronicleEnding.Dismissed
                : state.Staff <= 0 ? ChronicleEnding.StaffWalkout
                : ChronicleEnding.InProgress;
            return ending == ChronicleEnding.InProgress ? state : state with { Ending = ending, PendingEvent = null, PendingLetter = null };
        }
    }

    /// <summary>Plain-text save for the hands-on mode. Hand-written so it needs no reflection on phones built with IL2CPP.</summary>
    public static class LifeSave
    {
        public const int CurrentVersion = 1;
        private const string Magic = "SDTLIFE";

        public static string Write(LifeState s)
        {
            var b = new StringBuilder();
            void Put(string name, object? value) => b.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
            b.Append(Magic).Append(' ').Append(CurrentVersion).Append('\n');
            Put("phase", (int)s.Phase); Put("day", s.Day); Put("minute", s.Minute); Put("cash", s.Cash); Put("students", s.Students);
            Put("rep", s.Reputation); Put("parents", s.Parents); Put("staff", s.Staff); Put("acad", s.Academics); Put("random", s.RandomState);
            Put("eventToday", s.EventToday); Put("eventMinute", s.EventMinute); Put("pendingEvent", s.PendingEvent); Put("ending", (int)s.Ending);
            Put("gym", s.GymContract ? 1 : 0); Put("realDay", s.LastRealDay); Put("streak", s.Streak); Put("letterIndex", s.LetterIndex);
            Put("letter", s.PendingLetter); Put("bonus", s.LetterBonus); Put("away", s.AwayIncome);
            Put("decisions", string.Join(",", s.Decisions)); Put("used", string.Join(",", s.UsedEvents));
            if (s.Plan != null)
            {
                LifePlan p = s.Plan;
                Put("plan", string.Join(",", p.BuildingId, p.Seats, p.Fee, p.Teachers, p.Admins, p.StartMinute, p.Periods));
            }
            foreach (ChronicleEntry e in s.Log)
            {
                ChronicleEffect f = e.Effect;
                b.Append("L|").Append(e.Week).Append('|').Append(e.Key).Append('|').Append(string.Join("|", f.Cash, f.Students, f.Reputation, f.Parents, f.Staff, f.Academics)).Append('\n');
            }
            return b.ToString();
        }

        /// <summary>Throws <see cref="FormatException"/> for a damaged or unknown file.</summary>
        public static LifeState Read(string text)
        {
            string[] lines = (text ?? throw new FormatException("Empty save.")).Split('\n');
            if (lines[0].Trim() != Magic + " " + CurrentVersion) throw new FormatException("Unknown save version.");
            var values = new Dictionary<string, string>();
            var log = ImmutableArray.CreateBuilder<ChronicleEntry>();
            foreach (string line in lines.Skip(1))
            {
                if (line.StartsWith("L|", StringComparison.Ordinal))
                {
                    string[] p = line.Split('|');
                    if (p.Length != 9) throw new FormatException("Bad story line.");
                    log.Add(new ChronicleEntry(N(p[1]), p[2], new ChronicleEffect(N(p[3]), N(p[4]), N(p[5]), N(p[6]), N(p[7]), N(p[8]))));
                }
                else if (line.Length > 0)
                {
                    int at = line.IndexOf('=');
                    if (at <= 0) throw new FormatException("Bad line.");
                    values[line.Substring(0, at)] = line.Substring(at + 1);
                }
            }

            string Text(string name) => values.TryGetValue(name, out string? v) ? v : throw new FormatException("Missing " + name);
            int Int(string name) => N(Text(name));
            string? Opt(string name) => Text(name).Length == 0 ? null : Text(name);
            ImmutableArray<string> Items(string name) => Text(name).Length == 0 ? ImmutableArray<string>.Empty : Text(name).Split(',').ToImmutableArray();

            LifePlan? plan = null;
            if (values.TryGetValue("plan", out string? raw))
            {
                string[] p = raw.Split(',');
                if (p.Length != 7 || !SchoolLife.Buildings.Any(b => b.Id == p[0])) throw new FormatException("Bad plan.");
                plan = new LifePlan(p[0], N(p[1]), N(p[2]), N(p[3]), N(p[4]), N(p[5]), N(p[6]));
            }
            if (!uint.TryParse(Text("random"), NumberStyles.None, CultureInfo.InvariantCulture, out uint random)) throw new FormatException("Bad random state.");
            return new LifeState
            {
                Phase = (LifePhase)Int("phase"), Day = Int("day"), Minute = Int("minute"), Plan = plan, Cash = Int("cash"), Students = Int("students"),
                Reputation = Int("rep"), Parents = Int("parents"), Staff = Int("staff"), Academics = Int("acad"), RandomState = random,
                EventToday = Opt("eventToday"), EventMinute = Int("eventMinute"), PendingEvent = Opt("pendingEvent"), Ending = (ChronicleEnding)Int("ending"),
                GymContract = Int("gym") == 1, LastRealDay = Int("realDay"), Streak = Int("streak"), LetterIndex = Int("letterIndex"),
                PendingLetter = Opt("letter"), LetterBonus = Int("bonus"), AwayIncome = Int("away"),
                Decisions = Items("decisions"), UsedEvents = Items("used"), Log = log.ToImmutable(),
            };
        }

        private static int N(string text) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ? value : throw new FormatException("Bad number: " + text);
    }
}
