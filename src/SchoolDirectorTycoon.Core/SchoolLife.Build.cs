using System;
using System.Collections.Immutable;
using System.Linq;

namespace SchoolDirectorTycoon.Core
{
    public sealed record LifeUpgrade(string Id, int Cost, int RealMinutes, string? Requires);

    public sealed record LifeCourse(string Id, int Cost, int RealMinutes, int Skill, int Mood);

    public static partial class SchoolLife
    {
        public static readonly ImmutableArray<LifeUpgrade> Upgrades = ImmutableArray.Create(
            new LifeUpgrade("ac", 18000, 120, null),
            new LifeUpgrade("canteen", 12000, 180, null),
            new LifeUpgrade("library", 25000, 360, null),
            new LifeUpgrade("lab", 30000, 480, null),
            new LifeUpgrade("playground", 22000, 360, null),
            new LifeUpgrade("garden", 9000, 60, null),
            new LifeUpgrade("bus", 26000, 240, null),
            new LifeUpgrade("training_center", 35000, 360, null),
            new LifeUpgrade("training_center2", 45000, 720, "training_center"),
            new LifeUpgrade("wing", 60000, 1440, null));

        public static readonly ImmutableArray<LifeCourse> Courses = ImmutableArray.Create(
            new LifeCourse("method", 2500, 360, 6, 0),
            new LifeCourse("subject", 5000, 720, 10, 0),
            new LifeCourse("master", 12000, 1440, 16, -5),
            new LifeCourse("care", 1500, 180, 0, 15));

        /// <summary>The most seats this school can furnish, including a finished wing.</summary>
        public static int Capacity(LifeState state) =>
            ScenarioCapacity(state.Scenario, Building(state.Plan!.BuildingId)) + (state.UpgradesDone.Contains("wing") ? WingSeats : 0);

        public static int TrainingSlotCount(LifeState state) =>
            !state.UpgradesDone.Contains("training_center") ? 0 : TrainingSlots + (state.UpgradesDone.Contains("training_center2") ? ExtraTrainingSlots : 0);

        /// <summary>Everything leaving the account this week: rent, the real payroll, admins, utilities and any loan.</summary>
        public static int WeeklyBill(LifeState state)
        {
            LifePlan plan = state.Plan!;
            int people = state.Roster.Sum(t => t.WeeklyPay) + plan.Admins * AdminWeeklyPay;
            if (state.Scenario == "international") people = people * InternationalPayPercent / 100;
            return ScenarioRent(state.Scenario, Building(plan.BuildingId)) + people + state.Students * UtilityPerStudent
                + (state.LoanWeeks > 0 ? LoanPayment : 0) + (state.DebtWeeks > 0 ? RescueDebt : 0);
        }

        /// <summary>Finishes whatever is due. A clock that moved backwards changes nothing.</summary>
        public static LifeState Tick(LifeState state, int nowMinutes)
        {
            if (state.Phase == LifePhase.Setup || state.Ending != ChronicleEnding.InProgress || nowMinutes < state.LastNow) return state;
            LifeState next = nowMinutes == state.LastNow ? state : state with { LastNow = nowMinutes };
            if (next.BuildingJob != null && nowMinutes >= next.BuildingEnd)
            {
                string id = next.BuildingJob;
                next = Apply(next with { BuildingJob = null, BuildingEnd = 0, UpgradesDone = next.UpgradesDone.Add(id) }, new ChronicleEffect(), $"life.up.{id}.done");
                next = Celebrate(Award(next, "first_upgrade"), "gift", "life.up." + id, Upgrades.First(u => u.Id == id).Cost);
            }
            foreach (LifeTeacher t in next.Roster.Where(t => t.TrainingEnd != 0 && nowMinutes >= t.TrainingEnd).ToArray())
            {
                LifeCourse course = Courses.First(c => c.Id == t.Course);
                LifeTeacher done = t with { Skill = Math.Clamp(t.Skill + course.Skill, 0, 100), Mood = Math.Clamp(t.Mood + course.Mood, 0, 100), TrainingEnd = 0, Course = null };
                next = Apply(next with { Roster = next.Roster.Replace(t, done), CoursesFinished = next.CoursesFinished + 1 }, new ChronicleEffect(), $"life.course.{course.Id}.done", t.Name, t.Subject);
                next = Celebrate(next, "gift", "life.course." + course.Id, course.Skill > 0 ? course.Skill : course.Mood);
                if (next.CoursesFinished >= 5) next = Award(next, "trained_five");
            }
            return next;
        }

        /// <summary>Starts the one building project allowed at a time. Paid now, finished by real time.</summary>
        public static LifeState StartUpgrade(LifeState state, string id, int nowMinutes)
        {
            LifeState s = Tick(state, nowMinutes);
            RequireEvening(s);
            LifeUpgrade upgrade = Upgrades.FirstOrDefault(u => u.Id == id) ?? throw UnknownChoice();
            if (s.UpgradesDone.Contains(id))
                throw new RuleException(RuleCode.LifeUpgradeDone, "تم هذا المشروع من قبل.", "That project is already finished.");
            if (s.BuildingJob != null)
                throw new RuleException(RuleCode.LifeUpgradeBusy, "هناك مشروع قيد التنفيذ.", "Another project is under way.");
            if (upgrade.Requires != null && !s.UpgradesDone.Contains(upgrade.Requires))
                throw new RuleException(RuleCode.LifeUpgradeLocked, "يتطلب هذا مشروعًا آخر أولًا.", "That project needs another one first.");
            RequireCash(s, upgrade.Cost);
            return s with { Cash = s.Cash - upgrade.Cost, BuildingJob = id, BuildingEnd = s.LastNow + upgrade.RealMinutes };
        }

        /// <summary>Sends a teacher away on a course. They leave the classroom until it finishes.</summary>
        public static LifeState StartTraining(LifeState state, int teacherId, string courseId, int nowMinutes)
        {
            LifeState s = Tick(state, nowMinutes);
            RequireEvening(s);
            if (!s.UpgradesDone.Contains("training_center"))
                throw new RuleException(RuleCode.LifeNeedsTrainingCenter, "يلزم مركز تدريب.", "A training centre is needed.");
            LifeCourse course = Courses.FirstOrDefault(c => c.Id == courseId) ?? throw UnknownChoice();
            LifeTeacher t = FindTeacher(s, teacherId);
            if (t.TrainingEnd != 0) throw Busy();
            if (s.Roster.Count(x => x.TrainingEnd != 0) >= TrainingSlotCount(s))
                throw new RuleException(RuleCode.LifeNoTrainingSlot, "لا توجد أماكن شاغرة في المركز.", "The training centre has no free place.");
            RequireCash(s, course.Cost);
            return s with { Cash = s.Cash - course.Cost, Roster = s.Roster.Replace(t, t with { TrainingEnd = s.LastNow + course.RealMinutes, Course = course.Id }) };
        }

        /// <summary>One loan at a time, repaid weekly with the bills.</summary>
        public static LifeState TakeLoan(LifeState state, int amount)
        {
            RequireEvening(state);
            if (amount != LoanAmount)
                throw new RuleException(RuleCode.LifeInvalidPlan, "قيمة القرض غير متاحة.", "That loan amount is not offered.");
            if (state.LoanWeeks > 0)
                throw new RuleException(RuleCode.LifeLoanActive, "هناك قرض قائم.", "A loan is still being repaid.");
            return Apply(state with { LoanWeeks = LoanTerm, ChallengeBroken = state.ChallengeBroken || state.ChallengeId == "no_loan" }, new ChronicleEffect(Cash: LoanAmount), "life.loan.taken");
        }
    }
}
