using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SchoolDirectorTycoon.Core
{
    /// <summary>Plain-text save for the hands-on mode. Hand-written so it needs no reflection on phones built with IL2CPP.
    /// Version 1 and 2 files are migrated on read.</summary>
    public static class LifeSave
    {
        public const int CurrentVersion = 3;
        private const string Magic = "SDTLIFE";

        public static string Write(LifeState s)
        {
            var b = new StringBuilder();
            void Put(string name, object? value) => b.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
            void Row(string tag, params object?[] cells) => b.Append(tag).Append('|').Append(string.Join("|", cells.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture)))).Append('\n');
            b.Append(Magic).Append(' ').Append(CurrentVersion).Append('\n');
            Put("phase", (int)s.Phase); Put("day", s.Day); Put("minute", s.Minute); Put("cash", s.Cash); Put("students", s.Students);
            Put("rep", s.Reputation); Put("parents", s.Parents); Put("staff", s.Staff); Put("acad", s.Academics); Put("random", s.RandomState);
            Put("eventToday", s.EventToday); Put("eventMinute", s.EventMinute); Put("pendingEvent", s.PendingEvent); Put("ending", (int)s.Ending);
            Put("gym", Flag(s.GymContract)); Put("realDay", s.LastRealDay); Put("streak", s.Streak); Put("letterIndex", s.LetterIndex);
            Put("letter", s.PendingLetter); Put("bonus", s.LetterBonus); Put("away", s.AwayIncome);
            Put("decisions", string.Join(",", s.Decisions)); Put("used", string.Join(",", s.UsedEvents));
            if (s.Plan != null)
            {
                LifePlan p = s.Plan;
                Put("plan", PlanText(p));
            }
            if (s.Identity != null) Put("identity", IdentityText(s.Identity));
            Put("dayExtra", s.DayExtra); Put("nextPerson", s.NextPersonId); Put("asked", Flag(s.InterviewAsked)); Put("reply", s.InterviewReply);
            Put("dismissals", s.Dismissals); Put("courses", s.CoursesFinished);
            Put("caseToday", CaseText(s.CaseToday)); Put("pendingCase", CaseText(s.PendingCase));
            Put("actions", string.Join(",", s.ActionsToday)); Put("lastNow", s.LastNow); Put("job", s.BuildingJob); Put("jobEnd", s.BuildingEnd);
            Put("upgrades", string.Join(",", s.UpgradesDone)); Put("loan", s.LoanWeeks); Put("pressure", s.RivalPressure); Put("rivalStage", s.RivalStage);
            Put("pendingRival", s.PendingRival); Put("ministry", Flag(s.PendingMinistry)); Put("speech", Flag(s.PendingSpeech));
            Put("goal", s.GoalId); Put("goalDone", Flag(s.GoalDone)); Put("achievements", string.Join(",", s.Achievements)); Put("memories", string.Join(",", s.Memories));
            Put("scenario", s.Scenario); Put("runId", s.RunId); Put("debt", s.DebtWeeks); if (s.OpeningPlan != null) Put("openingPlan", PlanText(s.OpeningPlan));
            Put("mark", string.Join(",", s.MarkCash, s.MarkRep, s.MarkParents, s.MarkStaff, s.MarkAcad, s.MarkLog));
            if (s.LastDay != null) Put("lastDay", string.Join(",", s.LastDay.Day, s.LastDay.CashDelta, s.LastDay.Rep, s.LastDay.Parents, s.LastDay.Staff, s.LastDay.Acad, s.LastDay.BestLog, s.LastDay.TeaseKey));
            Put("starsBest", s.StarsBest); Put("tags", string.Join(",", s.Tags)); Put("lastFee", s.LastFee); Put("conscience", s.Conscience); Put("scandal", Flag(s.ScandalDone));
            Put("morning", string.Join(",", s.Morning)); Put("energy", s.Energy); Put("family", s.Family); Put("lastCrisis", s.LastCrisisDay); Put("lastTotm", s.LastTotmMonth);
            Put("challenge", s.ChallengeId); Put("challengeWeek", s.ChallengeWeek); Put("challengeDay", s.ChallengeDay); Put("challengeBroken", Flag(s.ChallengeBroken)); Put("challengeDone", Flag(s.ChallengeDone));
            Put("challengesWon", s.ChallengesWon); Put("roundUps", s.RoundUps); Put("risks", s.RisksTaken); Put("punishes", s.CasePunishes); Put("talks", s.CaseTalks);
            foreach (LifeTeacher t in s.Roster) Row("T", t.Id, t.Name, t.Subject, t.Skill, t.Mood, t.Trait, Flag(t.TraitKnown), t.WeeklyPay, t.TrainingEnd, t.Course, t.Role);
            foreach (LifeMonth m in s.MonthCards) Row("M", m.Month, m.Cash, m.Rep, m.Parents, m.Staff, m.Acad, m.Students, m.Grade);
            foreach (LifeCelebration c in s.Celebrations) Row("K", c.Kind, c.Id, c.Value);
            foreach (LifeChat c in s.Chat) Row("H", c.Day, c.Group, c.Sender, c.Key, c.Name);
            foreach (LifeSeed x in s.Seeds) Row("D", x.Id, x.DueWeek);
            foreach (LifeQueued q in s.CardQueue) Row("Q", q.Kind, q.Id, q.Name);
            foreach (LifeArc a in s.StoryArcs) Row("A", a.Id, a.Stage, a.NextDay, a.Name, a.Answers);
            foreach (LifeCandidate c in s.Candidates) Row("C", c.Id, c.Name, c.Subject, c.ApparentSkill, c.TrueSkill, c.Trait, c.Pay, Flag(c.TraitRevealed));
            foreach (LifeStudent x in s.Followed) Row("S", x.Id, x.Name, x.Marks, x.Conduct);
            foreach (LifeFollowUp f in s.FollowUps) Row("F", f.StudentId, f.Choice, f.DueDay);
            foreach (ChronicleEntry e in s.Log)
            {
                ChronicleEffect f = e.Effect;
                Row("L", e.Week, e.Key, f.Cash, f.Students, f.Reputation, f.Parents, f.Staff, f.Academics, e.Name, e.Subject);
            }
            return b.ToString();
        }

        /// <summary>Throws <see cref="FormatException"/> for a damaged or unknown file.</summary>
        public static LifeState Read(string text)
        {
            string[] lines = (text ?? throw new FormatException("Empty save.")).Split('\n');
            string header = lines[0].Trim();
            int version = header == Magic + " 1" ? 1 : header == Magic + " 2" ? 2 : header == Magic + " 3" ? 3 : throw new FormatException("Unknown save version.");
            var values = new Dictionary<string, string>();
            var log = ImmutableArray.CreateBuilder<ChronicleEntry>();
            var roster = ImmutableArray.CreateBuilder<LifeTeacher>();
            var candidates = ImmutableArray.CreateBuilder<LifeCandidate>();
            var students = ImmutableArray.CreateBuilder<LifeStudent>();
            var followUps = ImmutableArray.CreateBuilder<LifeFollowUp>();
            var months = ImmutableArray.CreateBuilder<LifeMonth>();
            var celebrations = ImmutableArray.CreateBuilder<LifeCelebration>();
            var chat = ImmutableArray.CreateBuilder<LifeChat>();
            var seeds = ImmutableArray.CreateBuilder<LifeSeed>();
            var queue = ImmutableArray.CreateBuilder<LifeQueued>();
            var arcs = ImmutableArray.CreateBuilder<LifeArc>();
            foreach (string line in lines.Skip(1))
            {
                if (line.Length > 1 && line[1] == '|')
                {
                    string[] p = line.Split('|');
                    switch (line[0])
                    {
                        case 'L':
                            if (p.Length != (version == 1 ? 9 : 11)) throw new FormatException("Bad story line.");
                            log.Add(new ChronicleEntry(N(p[1]), p[2], new ChronicleEffect(N(p[3]), N(p[4]), N(p[5]), N(p[6]), N(p[7]), N(p[8])),
                                version == 1 ? -1 : N(p[9]), version == 1 || p[10].Length == 0 ? null : p[10]));
                            break;
                        case 'T':
                            if (p.Length != (version >= 3 ? 12 : 11)) throw new FormatException("Bad teacher.");
                            roster.Add(new LifeTeacher(N(p[1]), N(p[2]), p[3], N(p[4]), N(p[5]), p[6], p[7] == "1", N(p[8]), N(p[9]), p[10].Length == 0 ? null : p[10], version >= 3 && p[11].Length > 0 ? p[11] : null));
                            break;
                        case 'C':
                            if (p.Length != 9) throw new FormatException("Bad candidate.");
                            candidates.Add(new LifeCandidate(N(p[1]), N(p[2]), p[3], p[4], N(p[5]), p[6], N(p[7]), p[8] == "1"));
                            break;
                        case 'S':
                            if (p.Length != 5) throw new FormatException("Bad student.");
                            students.Add(new LifeStudent(N(p[1]), N(p[2]), N(p[3]), N(p[4])));
                            break;
                        case 'F':
                            if (p.Length != 4) throw new FormatException("Bad follow-up.");
                            followUps.Add(new LifeFollowUp(N(p[1]), p[2], N(p[3])));
                            break;
                        case 'M':
                            if (p.Length != 9) throw new FormatException("Bad month.");
                            months.Add(new LifeMonth(N(p[1]), N(p[2]), N(p[3]), N(p[4]), N(p[5]), N(p[6]), N(p[7]), p[8]));
                            break;
                        case 'K':
                            if (p.Length != 4) throw new FormatException("Bad celebration.");
                            celebrations.Add(new LifeCelebration(p[1], p[2], N(p[3])));
                            break;
                        case 'H':
                            if (p.Length != 6) throw new FormatException("Bad chat.");
                            chat.Add(new LifeChat(N(p[1]), p[2], N(p[3]), p[4], N(p[5])));
                            break;
                        case 'D':
                            if (p.Length != 3) throw new FormatException("Bad seed.");
                            seeds.Add(new LifeSeed(p[1], N(p[2])));
                            break;
                        case 'Q':
                            if (p.Length != 4) throw new FormatException("Bad card.");
                            queue.Add(new LifeQueued(p[1], p[2], N(p[3])));
                            break;
                        case 'A':
                            if (p.Length != 6) throw new FormatException("Bad arc.");
                            arcs.Add(new LifeArc(p[1], N(p[2]), N(p[3]), N(p[4]), p[5]));
                            break;
                        default:
                            throw new FormatException("Bad line.");
                    }
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

            LifePlan? plan = values.TryGetValue("plan", out string? raw) ? ParsePlan(raw, version) : null;
            if (!uint.TryParse(Text("random"), NumberStyles.None, CultureInfo.InvariantCulture, out uint random)) throw new FormatException("Bad random state.");
            var state = new LifeState
            {
                Phase = (LifePhase)Int("phase"), Day = Int("day"), Minute = Int("minute"), Plan = plan, Cash = Int("cash"), Students = Int("students"),
                Reputation = Int("rep"), Parents = Int("parents"), Staff = Int("staff"), Academics = Int("acad"), RandomState = random,
                EventToday = Opt("eventToday"), EventMinute = Int("eventMinute"), PendingEvent = Opt("pendingEvent"), Ending = (ChronicleEnding)Int("ending"),
                GymContract = Int("gym") == 1, LastRealDay = Int("realDay"), Streak = Int("streak"), LetterIndex = Int("letterIndex"),
                PendingLetter = Opt("letter"), LetterBonus = Int("bonus"), AwayIncome = Int("away"),
                Decisions = Items("decisions"), UsedEvents = Items("used"), Log = log.ToImmutable(),
            };
            if (version == 1)
                return MigrateToV3(plan == null ? state : SchoolLife.SeedPeople(state with { Identity = SchoolLife.DefaultIdentity }));

            LifeIdentity? identity = values.TryGetValue("identity", out string? rawIdentity) ? ParseIdentity(rawIdentity) : null;
            state = state with
            {
                Identity = identity, DayExtra = Int("dayExtra"), NextPersonId = Int("nextPerson"), InterviewAsked = Int("asked") == 1, InterviewReply = Opt("reply"),
                Dismissals = Int("dismissals"), CoursesFinished = Int("courses"), CaseToday = ParseCase(Text("caseToday")), PendingCase = ParseCase(Text("pendingCase")),
                ActionsToday = Items("actions"), LastNow = Int("lastNow"), BuildingJob = Opt("job"), BuildingEnd = Int("jobEnd"), UpgradesDone = Items("upgrades"),
                LoanWeeks = Int("loan"), RivalPressure = Int("pressure"), RivalStage = Int("rivalStage"), PendingRival = Opt("pendingRival"),
                PendingMinistry = Int("ministry") == 1, PendingSpeech = Int("speech") == 1, GoalId = Opt("goal"), GoalDone = Int("goalDone") == 1,
                Achievements = Items("achievements"), Memories = Items("memories").Select(N).ToImmutableArray(),
                Roster = roster.ToImmutable(), Candidates = candidates.ToImmutable(), Followed = students.ToImmutable(), FollowUps = followUps.ToImmutable(),
            };
            if (version == 2) return MigrateToV3(state);

            if (!uint.TryParse(Text("runId"), NumberStyles.None, CultureInfo.InvariantCulture, out uint runId)) throw new FormatException("Bad run id.");
            int[] mark = Text("mark").Split(',').Select(N).ToArray();
            if (mark.Length != 6) throw new FormatException("Bad mark.");
            LifeDayReport? lastDay = null;
            if (values.TryGetValue("lastDay", out string? rawDay))
            {
                string[] p = rawDay.Split(',');
                if (p.Length != 8) throw new FormatException("Bad day report.");
                lastDay = new LifeDayReport(N(p[0]), N(p[1]), N(p[2]), N(p[3]), N(p[4]), N(p[5]), N(p[6]), p[7]);
            }
            return state with
            {
                Scenario = Text("scenario"), RunId = runId, DebtWeeks = Int("debt"), OpeningPlan = values.TryGetValue("openingPlan", out string? opening) ? ParsePlan(opening) : null,
                MarkCash = mark[0], MarkRep = mark[1], MarkParents = mark[2], MarkStaff = mark[3], MarkAcad = mark[4], MarkLog = mark[5], LastDay = lastDay,
                StarsBest = Int("starsBest"), Tags = Items("tags"), LastFee = Int("lastFee"), Conscience = Int("conscience"), ScandalDone = Int("scandal") == 1,
                Morning = Items("morning"), Energy = Int("energy"), Family = Int("family"), LastCrisisDay = Int("lastCrisis"), LastTotmMonth = Int("lastTotm"),
                ChallengeId = Opt("challenge"), ChallengeWeek = Int("challengeWeek"), ChallengeDay = Int("challengeDay"), ChallengeBroken = Int("challengeBroken") == 1, ChallengeDone = Int("challengeDone") == 1,
                ChallengesWon = Int("challengesWon"), RoundUps = Int("roundUps"), RisksTaken = Int("risks"), CasePunishes = Int("punishes"), CaseTalks = Int("talks"),
                MonthCards = months.ToImmutable(), Celebrations = celebrations.ToImmutable(), Chat = chat.ToImmutable(), Seeds = seeds.ToImmutable(), CardQueue = queue.ToImmutable(), StoryArcs = arcs.ToImmutable(),
            };
        }

        /// <summary>Gives an older save the v3 defaults. A school already running gets its run id and its crises from the random state.</summary>
        private static LifeState MigrateToV3(LifeState s)
        {
            LifeState next = s with
            {
                MarkCash = s.Cash, MarkRep = s.Reputation, MarkParents = s.Parents, MarkStaff = s.Staff, MarkAcad = s.Academics, MarkLog = s.Log.Length,
                OpeningPlan = s.Plan, LastFee = s.Plan?.Fee ?? 0, RunId = s.Plan == null ? 0 : s.RandomState,
            };
            return s.Plan == null || s.Ending != ChronicleEnding.InProgress ? next : SchoolLife.ScheduleArcs(next);
        }

        internal static string PlanText(LifePlan p) => string.Join(",", p.BuildingId, p.Seats, p.Fee, p.Teachers, p.Admins, p.StartMinute, p.Periods, p.PeriodMinutes, p.BreakMinutes);

        internal static string IdentityText(LifeIdentity i) => string.Join(",", i.Name, i.Crest, i.Colour, i.Philosophy);

        internal static LifePlan ParsePlan(string raw, int version = CurrentVersion)
        {
            string[] p = raw.Split(',');
            if (p.Length != (version == 1 ? 7 : 9) || !SchoolLife.Buildings.Any(b => b.Id == p[0])) throw new FormatException("Bad plan.");
            return new LifePlan(p[0], N(p[1]), N(p[2]), N(p[3]), N(p[4]), N(p[5]), N(p[6]),
                version == 1 ? SchoolLife.DefaultPeriodMinutes : N(p[7]), version == 1 ? SchoolLife.DefaultBreakMinutes : N(p[8]));
        }

        internal static LifeIdentity ParseIdentity(string raw)
        {
            string[] p = raw.Split(',');
            if (p.Length != 4) throw new FormatException("Bad identity.");
            return new LifeIdentity(N(p[0]), N(p[1]), N(p[2]), p[3]);
        }

        internal static int Number(string text) => N(text);

        private static int Flag(bool value) => value ? 1 : 0;

        private static string CaseText(LifeCase? c) => c == null ? "" : c.StudentId.ToString(CultureInfo.InvariantCulture) + "," + c.CaseId;

        private static LifeCase? ParseCase(string text)
        {
            if (text.Length == 0) return null;
            string[] p = text.Split(',');
            if (p.Length != 2) throw new FormatException("Bad case.");
            return new LifeCase(N(p[0]), p[1]);
        }

        private static int N(string text) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ? value : throw new FormatException("Bad number: " + text);
    }
}
