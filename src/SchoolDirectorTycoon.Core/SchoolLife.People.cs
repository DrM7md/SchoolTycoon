using System;
using System.Collections.Immutable;
using System.Linq;

namespace SchoolDirectorTycoon.Core
{
    /// <summary>The school's chosen character. Each number indexes a text table.</summary>
    public sealed record LifeIdentity(int Name, int Crest, int Colour, string Philosophy);

    /// <summary>TrainingEnd is 0 unless the teacher is away on <see cref="Course"/>.</summary>
    public sealed record LifeTeacher(int Id, int Name, string Subject, int Skill, int Mood, string Trait, bool TraitKnown, int WeeklyPay, int TrainingEnd, string? Course, string? Role = null);

    /// <summary>ApparentSkill is low, mid or high and can mislead by a band; TrueSkill is for rules only.</summary>
    public sealed record LifeCandidate(int Id, int Name, string Subject, string ApparentSkill, int TrueSkill, string Trait, int Pay, bool TraitRevealed);

    public sealed record LifeStudent(int Id, int Name, int Marks, int Conduct);

    public sealed record LifeCase(int StudentId, string CaseId);

    public sealed record LifeFollowUp(int StudentId, string Choice, int DueDay);

    public static partial class SchoolLife
    {
        public const int NameCount = 24, SchoolNames = 8, Crests = 6, Colours = 5, FollowedStudents = 5;

        public static readonly ImmutableArray<string> Subjects = ImmutableArray.Create("arabic", "english", "math", "science", "islamic", "social", "computing", "sports");
        public static readonly ImmutableArray<string> Traits = ImmutableArray.Create("punctual", "late", "patient", "strict", "creative", "ambitious");
        public static readonly ImmutableArray<string> Philosophies = ImmutableArray.Create("strict", "creative", "sports", "faith");
        public static readonly ImmutableArray<string> InterviewQuestions = ImmutableArray.Create("experience", "discipline", "dream");
        public static readonly ImmutableArray<string> CaseIds = ImmutableArray.Create("fight", "phone", "cheating", "skipped", "rude", "late_again", "vandal", "bullied");
        public static readonly ImmutableArray<string> CaseChoices = ImmutableArray.Create("punish", "talk", "parents");
        public static readonly ImmutableArray<string> ActionIds = ImmutableArray.Create("gate", "broadcast_kindness", "broadcast_study", "broadcast_discipline");

        public static readonly LifeIdentity DefaultIdentity = new LifeIdentity(0, 0, 0, "strict");

        /// <summary>Teachers standing in front of a class: everyone not away on a course.</summary>
        public static int Teaching(LifeState state) => state.Roster.Count(t => t.TrainingEnd == 0);

        private static void ValidateIdentity(LifeIdentity i)
        {
            if (i.Name < 0 || i.Name >= SchoolNames || i.Crest < 0 || i.Crest >= Crests || i.Colour < 0 || i.Colour >= Colours || !Philosophies.Contains(i.Philosophy))
                throw new RuleException(RuleCode.LifeInvalidPlan, "هوية المدرسة غير صالحة.", "That school identity is not valid.");
        }

        /// <summary>Fills the roster and followed students from the random state. Also used when migrating old saves.</summary>
        internal static LifeState SeedPeople(LifeState state)
        {
            uint random = state.RandomState;
            var used = ImmutableHashSet.CreateBuilder<int>();
            var roster = ImmutableArray.CreateBuilder<LifeTeacher>();
            int firstSubject = Chronicle.Roll(ref random, Subjects.Length);
            for (int i = 0; i < state.Plan!.Teachers; i++)
            {
                int name = PickName(ref random, used);
                int skill = 35 + Chronicle.Roll(ref random, 36);
                int mood = 55 + Chronicle.Roll(ref random, 21);
                string trait = Traits[Chronicle.Roll(ref random, Traits.Length)];
                roster.Add(new LifeTeacher(i + 1, name, Subjects[(firstSubject + i) % Subjects.Length], skill, mood, trait, false, 1900 + skill * 10, 0, null));
            }
            var followed = ImmutableArray.CreateBuilder<LifeStudent>();
            for (int i = 0; i < FollowedStudents; i++)
            {
                int name = PickName(ref random, used);
                followed.Add(new LifeStudent(i + 1, name, 40 + Chronicle.Roll(ref random, 36), 40 + Chronicle.Roll(ref random, 36)));
            }
            return state with { RandomState = random, Roster = roster.ToImmutable(), Followed = followed.ToImmutable(), NextPersonId = state.Plan.Teachers + 1 };
        }

        private static int PickName(ref uint random, ImmutableHashSet<int>.Builder used)
        {
            int start = Chronicle.Roll(ref random, NameCount);
            for (int i = 0; i < NameCount; i++)
            {
                int name = (start + i) % NameCount;
                if (used.Add(name)) return name;
            }
            return start;
        }

        /// <summary>Calls three candidates to the office tonight.</summary>
        public static LifeState StartInterview(LifeState state)
        {
            RequireEvening(state);
            if (state.Candidates.Length > 0) throw Used();
            RequireCash(state, InterviewCost);
            uint random = state.RandomState;
            var used = ImmutableHashSet.CreateBuilder<int>();
            foreach (LifeTeacher t in state.Roster) used.Add(t.Name);
            var list = ImmutableArray.CreateBuilder<LifeCandidate>();
            for (int i = 0; i < 3; i++)
            {
                int skill = 35 + Chronicle.Roll(ref random, 51);
                int seen = skill + Chronicle.Roll(ref random, 17) - 8;
                string band = seen < 48 ? "low" : seen < 64 ? "mid" : "high";
                int name = PickName(ref random, used);
                list.Add(new LifeCandidate(state.NextPersonId + i, name, Subjects[Chronicle.Roll(ref random, Subjects.Length)], band, skill,
                    Traits[Chronicle.Roll(ref random, Traits.Length)], 1900 + skill * 10, false));
            }
            return state with
            {
                RandomState = random, Cash = state.Cash - InterviewCost, Candidates = list.ToImmutable(),
                NextPersonId = state.NextPersonId + 3, InterviewAsked = false, InterviewReply = null,
            };
        }

        /// <summary>One question per interview across all candidates. The reply key lands in <see cref="LifeState.InterviewReply"/>.</summary>
        public static LifeState AskCandidate(LifeState state, int candidateId, string question)
        {
            RequireEvening(state);
            LifeCandidate c = FindCandidate(state, candidateId);
            if (!InterviewQuestions.Contains(question)) throw UnknownChoice();
            if (state.InterviewAsked) throw Used();
            return state with
            {
                InterviewAsked = true,
                InterviewReply = $"life.interview.reply.{question}.{c.Trait}",
                Candidates = state.Candidates.Replace(c, c with { TraitRevealed = true }),
            };
        }

        public static LifeState HireCandidate(LifeState state, int candidateId)
        {
            RequireEvening(state);
            LifeCandidate c = FindCandidate(state, candidateId);
            if (state.Plan!.Teachers >= MaxTeachers)
                throw new RuleException(RuleCode.LifeInvalidPlan, "لا يمكن تعيين المزيد.", "No more teachers can be hired.");
            RequireCash(state, HiringCost);
            var teacher = new LifeTeacher(c.Id, c.Name, c.Subject, c.TrueSkill, 65, c.Trait, c.TraitRevealed, c.Pay, 0, null);
            LifeState hired = CloseInterview(state) with { Roster = state.Roster.Add(teacher), Plan = state.Plan with { Teachers = state.Plan.Teachers + 1 } };
            return Apply(hired, new ChronicleEffect(Cash: -HiringCost), "life.adjusted", c.Name, c.Subject);
        }

        public static LifeState CancelInterview(LifeState state)
        {
            RequireEvening(state);
            if (state.Candidates.Length == 0) throw NoPerson();
            return CloseInterview(state);
        }

        private static LifeState CloseInterview(LifeState state) =>
            state with { Candidates = ImmutableArray<LifeCandidate>.Empty, InterviewAsked = false, InterviewReply = null };

        /// <summary>Pays one week's notice and hurts morale. A teacher away on a course cannot be dismissed.</summary>
        public static LifeState DismissTeacher(LifeState state, int teacherId)
        {
            RequireEvening(state);
            LifeTeacher t = FindTeacher(state, teacherId);
            if (t.TrainingEnd != 0) throw Busy();
            if (state.Roster.Length <= 1)
                throw new RuleException(RuleCode.LifeLastTeacher, "لا يمكن الاستغناء عن آخر معلم.", "The last teacher cannot be dismissed.");
            RequireCash(state, t.WeeklyPay);
            LifeState next = state with
            {
                Roster = state.Roster.Remove(t), Plan = state.Plan! with { Teachers = state.Plan.Teachers - 1 }, Dismissals = state.Dismissals + 1, Tags = state.Tags.Add("dismissed"),
            };
            return Close(Apply(next, new ChronicleEffect(Cash: -t.WeeklyPay, Staff: -2), "life.adjusted", t.Name, t.Subject));
        }

        /// <summary>A once-a-day director's walk: gate, one broadcast_*, or observe:{teacherId}.</summary>
        public static LifeState DirectorAction(LifeState state, string id)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.School);
            RequireNoPending(state);
            RequireEnergy(state);
            bool observe = id.StartsWith("observe:", StringComparison.Ordinal);
            if (!observe && !ActionIds.Contains(id)) throw UnknownChoice();
            string slot = observe ? "observe" : id.StartsWith("broadcast_", StringComparison.Ordinal) ? "broadcast" : id;
            if (state.ActionsToday.Contains(slot)) throw Used();
            LifeState marked = Personal(state with { ActionsToday = state.ActionsToday.Add(slot) }, -5, 0);
            uint random = state.RandomState;
            if (observe)
            {
                if (!int.TryParse(id.Substring("observe:".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int teacherId)) throw NoPerson();
                LifeTeacher t = FindTeacher(state, teacherId);
                if (t.TrainingEnd != 0) throw Busy();
                int skill = Chronicle.Roll(ref random, 100) < 25 ? 1 : 0;
                LifeTeacher seen = t with { TraitKnown = true, Mood = Math.Min(100, t.Mood + 3), Skill = Math.Min(100, t.Skill + skill) };
                return Apply(marked with { RandomState = random, Roster = state.Roster.Replace(t, seen) }, new ChronicleEffect(), "life.action.observe", t.Name);
            }
            bool lands = Chronicle.Roll(ref random, 100) < (id == "gate" ? 35 : 25);
            int gain = lands ? 1 : 0;
            ChronicleEffect effect = id switch
            {
                "gate" => new ChronicleEffect(Parents: gain),
                "broadcast_kindness" => new ChronicleEffect(Staff: gain),
                "broadcast_study" => new ChronicleEffect(Academics: gain),
                _ => new ChronicleEffect(Parents: gain),
            };
            return Apply(marked with { RandomState = random }, effect, "life.action." + id);
        }

        /// <summary>Weekly effect of the staff room, and the slow drift of each teacher's mood.</summary>
        private static void PeopleWeek(LifeState state, int week, ref uint random, ref int academics, ref int staff, ref int parents)
        {
            if (state.Roster.Length == 0) return;
            double skill = state.Roster.Average(t => t.Skill), mood = state.Roster.Average(t => t.Mood);
            if (skill >= 70) academics += 1;
            else if (skill < 40) academics -= 1;
            if (mood < 35) staff -= 1;

            bool lateHurts = false;
            foreach (LifeTeacher t in state.Roster.Where(t => t.Trait == "late"))
                lateHurts |= Chronicle.Roll(ref random, 100) < 20;
            if (lateHurts) academics -= 1;

            bool Any(string trait) => state.Roster.Any(t => t.Trait == trait);
            if (week % 4 == 0)
            {
                if (Any("creative")) academics += 1;
                if (Any("punctual")) staff += 1;
                if (Any("patient")) parents += 1;
            }
            if (week % 6 == 0 && Any("strict")) { academics += 1; staff -= 1; }
        }

        private static LifeState StudentsWeek(LifeState state)
        {
            uint random = state.RandomState;
            int week = state.Day / DaysInWeek;
            int school = state.Academics >= 65 ? 1 : state.Academics < 40 ? -1 : 0;
            int moodShift = (state.Staff >= 65 ? 1 : state.Staff < 40 ? -2 : 0) + (week % 4 == 0 ? -1 : 0);
            var followed = ImmutableArray.CreateBuilder<LifeStudent>(state.Followed.Length);
            foreach (LifeStudent s in state.Followed)
            {
                int marks = s.Marks + school + Chronicle.Roll(ref random, 3) - 1;
                int conduct = s.Conduct + Chronicle.Roll(ref random, 3) - 1;
                followed.Add(s with { Marks = Math.Clamp(marks, 0, 100), Conduct = Math.Clamp(conduct, 0, 100) });
            }
            var roster = ImmutableArray.CreateBuilder<LifeTeacher>(state.Roster.Length);
            foreach (LifeTeacher t in state.Roster)
            {
                int skill = t.Trait == "ambitious" && week % 4 == 0 ? Math.Min(100, t.Skill + 1) : t.Skill;
                roster.Add(t with { Mood = Math.Clamp(t.Mood + moodShift, 0, 100), Skill = skill });
            }
            return state with { RandomState = random, Followed = followed.MoveToImmutable(), Roster = roster.MoveToImmutable() };
        }

        private static LifeState RunFollowUps(LifeState state)
        {
            LifeState next = state;
            foreach (LifeFollowUp f in state.FollowUps.Where(f => f.DueDay <= state.Day))
            {
                LifeStudent? s = state.Followed.FirstOrDefault(x => x.Id == f.StudentId);
                if (s == null) continue;
                next = Apply(next, new ChronicleEffect(), $"life.case.followup.{f.Choice}.{(s.Conduct >= 50 ? "good" : "bad")}", s.Name);
            }
            return next with { FollowUps = state.FollowUps.RemoveAll(f => f.DueDay <= state.Day) };
        }

        private static LifeState ResolveCase(LifeState state, string choice)
        {
            if (!CaseChoices.Contains(choice)) throw UnknownChoice();
            LifeCase c = state.PendingCase!;
            bool serious = c.CaseId == "fight" || c.CaseId == "cheating" || c.CaseId == "vandal" || c.CaseId == "bullied";
            (int conduct, int marks, ChronicleEffect effect) = choice switch
            {
                "punish" => (serious ? 8 : 4, -2, new ChronicleEffect(Parents: serious ? 0 : -1, Reputation: serious ? 1 : 0)),
                "talk" => (serious ? 2 : 6, 2, new ChronicleEffect(Parents: serious ? -1 : 0)),
                _ => (5, 1, new ChronicleEffect(Parents: 1)),
            };
            LifeStudent s = state.Followed.First(x => x.Id == c.StudentId);
            LifeStudent changed = s with { Conduct = Math.Clamp(s.Conduct + conduct, 0, 100), Marks = Math.Clamp(s.Marks + marks, 0, 100) };
            LifeState next = state with
            {
                PendingCase = null,
                CasePunishes = state.CasePunishes + (choice == "punish" ? 1 : 0),
                CaseTalks = state.CaseTalks + (choice == "talk" ? 1 : 0),
                Followed = state.Followed.Replace(s, changed),
                FollowUps = state.FollowUps.Add(new LifeFollowUp(s.Id, choice, state.Day + FollowUpDays)),
            };
            return Close(Apply(next, effect, $"life.case.{c.CaseId}.{choice}.ok", s.Name));
        }

        private static void RequireEvening(LifeState state)
        {
            RequireOpen(state);
            RequirePhase(state, LifePhase.Evening);
            RequireNoPending(state);
        }

        private static void RequireCash(LifeState state, int cost)
        {
            if (cost > state.Cash)
                throw new RuleException(RuleCode.LifeNotEnoughCash, "لا يكفي المال.", "There is not enough money.");
        }

        private static LifeCandidate FindCandidate(LifeState state, int id) => state.Candidates.FirstOrDefault(c => c.Id == id) ?? throw NoPerson();

        private static LifeTeacher FindTeacher(LifeState state, int id) => state.Roster.FirstOrDefault(t => t.Id == id) ?? throw NoPerson();

        private static RuleException NoPerson() => new RuleException(RuleCode.LifeNoCandidate, "هذا الشخص غير موجود.", "That person is not here.");
        private static RuleException Busy() => new RuleException(RuleCode.LifeTeacherBusy, "هذا المعلم في دورة تدريبية.", "That teacher is away on a course.");
        private static RuleException Used() => new RuleException(RuleCode.LifeActionUsed, "تم هذا بالفعل.", "That has already been done.");
        private static RuleException UnknownChoice() => new RuleException(RuleCode.ChronicleUnknownChoice, "هذا الخيار غير متاح.", "That choice is not available.");
    }
}
