using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SchoolDirectorTycoon.Core
{
    /// <summary>One finished run in the Hall of Fame. SchoolName indexes life.school; Grade and Ending are enum names.</summary>
    public sealed record LifeHonour(int SchoolName, string Scenario, string Grade, int Rank, string Title, string Ending, int Day, string MemoryKey, int MemoryName);

    /// <summary>How often an album item was earned. Id is ach.{id} or title.{id}.</summary>
    public sealed record LifeAlbumEntry(string Id, int Count);

    /// <summary>Progress kept across school years, in its own file. One school year stays the scope of <see cref="LifeState"/>.</summary>
    public sealed record CareerState
    {
        public int Xp { get; init; }
        public int Level { get; init; } = 1;
        public int ChallengesWon { get; init; }
        public LifePlan? LastPlan { get; init; }
        public LifeIdentity? LastIdentity { get; init; }
        public string? LastScenario { get; init; }
        public ImmutableArray<string> Titles { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> ScenariosDone { get; init; } = ImmutableArray<string>.Empty;
        public ImmutableArray<LifeHonour> HallOfFame { get; init; } = ImmutableArray<LifeHonour>.Empty;
        public ImmutableArray<LifeAlbumEntry> Album { get; init; } = ImmutableArray<LifeAlbumEntry>.Empty;
        public ImmutableArray<uint> RecordedRuns { get; init; } = ImmutableArray<uint>.Empty;
    }

    public static class Career
    {
        public const int MaxLevel = 10, CashPerLevel = 5000;
        public static readonly ImmutableArray<int> Thresholds = ImmutableArray.Create(200, 500, 900, 1400, 2000, 2800, 3800, 5000, 6500);

        public static CareerState New() => new CareerState();

        public static int LevelOf(int xp) => 1 + Thresholds.Count(t => xp >= t);

        /// <summary>Total XP needed for the next level, or -1 at the top.</summary>
        public static int NextThreshold(int level) => level >= MaxLevel ? -1 : Thresholds[Math.Max(level, 1) - 1];

        public static int StartingCash(int level) => SchoolLife.StartingCash + CashPerLevel * (Math.Clamp(level, 1, MaxLevel) - 1);

        public static int ScenarioLevel(string scenario) => scenario switch { "village" => 2, "girls" => 3, "international" => 4, _ => 1 };

        public static int BuildingLevel(string building) => building == "academy" ? 3 : 1;

        public static int PhilosophyLevel(string philosophy) => philosophy == "sports" || philosophy == "faith" ? 2 : 1;

        /// <summary>How many school names are open: indexes below this value.</summary>
        public static int UnlockedNames(int level) => Math.Min(SchoolLife.SchoolNames, 3 + Math.Max(level, 1) - 1);

        /// <summary>How many crests are open: indexes below this value.</summary>
        public static int UnlockedCrests(int level) => Math.Min(SchoolLife.Crests, 2 + Math.Max(level, 1) - 1);

        /// <summary>The start this career allows. Open still refuses anything locked for its level.</summary>
        public static LifeStart Start(CareerState career, LifeIdentity identity, string scenario) =>
            new LifeStart(identity, scenario, StartingCash(career.Level), career.Level);

        /// <summary>XP this finished run is worth. Bankrupt and dismissed runs still pay for the weeks survived.</summary>
        public static int XpFor(LifeState s)
        {
            int xp = 20 * (s.Day / SchoolLife.DaysInWeek) + 50 * s.Achievements.Length;
            if (s.Ending == ChronicleEnding.YearCompleted)
            {
                xp += SchoolLife.Grade(s) switch { ChronicleGrade.S => 300, ChronicleGrade.A => 200, ChronicleGrade.B => 120, ChronicleGrade.C => 60, _ => 0 };
                int rank = SchoolLife.Rank(s);
                xp += rank == 1 ? 150 : rank == 2 ? 100 : rank == 3 ? 60 : rank <= 5 ? 30 : 0;
            }
            return s.Scenario == "rescue" ? xp * 130 / 100 : xp;
        }

        /// <summary>Banks a finished run. Unfinished runs and runs already recorded leave the career unchanged.</summary>
        public static CareerState Record(CareerState career, LifeState s)
        {
            if (s.Ending == ChronicleEnding.InProgress || s.Plan == null || career.RecordedRuns.Contains(s.RunId)) return career;
            int xp = career.Xp + XpFor(s);
            string title = SchoolLife.TitleOf(s);
            (string? memoryKey, int memoryName) = SchoolLife.BestMemory(s);
            var album = career.Album.ToBuilder();
            void Count(string id)
            {
                int at = album.FindIndex(e => e.Id == id);
                if (at < 0) album.Add(new LifeAlbumEntry(id, 1));
                else album[at] = album[at] with { Count = album[at].Count + 1 };
            }
            foreach (string id in s.Achievements) Count("ach." + id);
            Count("title." + title);
            bool completed = s.Ending == ChronicleEnding.YearCompleted;
            return career with
            {
                Xp = xp,
                Level = LevelOf(xp),
                ChallengesWon = career.ChallengesWon + s.ChallengesWon,
                LastPlan = s.OpeningPlan ?? s.Plan,
                LastIdentity = s.Identity ?? career.LastIdentity,
                LastScenario = s.Scenario,
                Titles = career.Titles.Contains(title) ? career.Titles : career.Titles.Add(title),
                ScenariosDone = completed && !career.ScenariosDone.Contains(s.Scenario) ? career.ScenariosDone.Add(s.Scenario) : career.ScenariosDone,
                HallOfFame = career.HallOfFame.Add(new LifeHonour(s.Identity?.Name ?? 0, s.Scenario, SchoolLife.Grade(s).ToString(), SchoolLife.Rank(s), title, s.Ending.ToString(), s.Day, memoryKey ?? "", memoryName)),
                Album = album.ToImmutable(),
                RecordedRuns = career.RecordedRuns.Add(s.RunId),
            };
        }

        public static int AlbumCount(CareerState career, string id) => career.Album.FirstOrDefault(e => e.Id == id)?.Count ?? 0;

        private static int FindIndex(this ImmutableArray<LifeAlbumEntry>.Builder list, Func<LifeAlbumEntry, bool> match)
        {
            for (int i = 0; i < list.Count; i++) if (match(list[i])) return i;
            return -1;
        }
    }

    /// <summary>Plain-text career file, hand-written so it needs no reflection on phones built with IL2CPP.</summary>
    public static class CareerSave
    {
        public const int CurrentVersion = 1;
        private const string Magic = "SDTCAREER";

        public static string Write(CareerState c)
        {
            var b = new StringBuilder();
            void Put(string name, object? value) => b.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
            b.Append(Magic).Append(' ').Append(CurrentVersion).Append('\n');
            Put("xp", c.Xp); Put("challenges", c.ChallengesWon); Put("titles", string.Join(",", c.Titles)); Put("scenarios", string.Join(",", c.ScenariosDone));
            Put("runs", string.Join(",", c.RecordedRuns.Select(r => r.ToString(CultureInfo.InvariantCulture))));
            if (c.LastPlan != null) Put("plan", LifeSave.PlanText(c.LastPlan));
            if (c.LastIdentity != null) Put("identity", LifeSave.IdentityText(c.LastIdentity));
            Put("scenario", c.LastScenario);
            foreach (LifeAlbumEntry e in c.Album) b.Append("A|").Append(e.Id).Append('|').Append(e.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (LifeHonour h in c.HallOfFame)
                b.Append("H|").Append(string.Join("|", h.SchoolName.ToString(CultureInfo.InvariantCulture), h.Scenario, h.Grade, h.Rank.ToString(CultureInfo.InvariantCulture), h.Title, h.Ending, h.Day.ToString(CultureInfo.InvariantCulture), h.MemoryKey, h.MemoryName.ToString(CultureInfo.InvariantCulture))).Append('\n');
            return b.ToString();
        }

        /// <summary>Throws <see cref="FormatException"/> for a damaged or unknown file.</summary>
        public static CareerState Read(string text)
        {
            string[] lines = (text ?? throw new FormatException("Empty career.")).Split('\n');
            if (lines[0].Trim() != Magic + " " + CurrentVersion) throw new FormatException("Unknown career version.");
            var values = new Dictionary<string, string>();
            var album = ImmutableArray.CreateBuilder<LifeAlbumEntry>();
            var hall = ImmutableArray.CreateBuilder<LifeHonour>();
            foreach (string line in lines.Skip(1))
            {
                if (line.Length == 0) continue;
                if (line.Length > 1 && line[1] == '|')
                {
                    string[] p = line.Split('|');
                    if (line[0] == 'A' && p.Length == 3) album.Add(new LifeAlbumEntry(p[1], LifeSave.Number(p[2])));
                    else if (line[0] == 'H' && p.Length == 10) hall.Add(new LifeHonour(LifeSave.Number(p[1]), p[2], p[3], LifeSave.Number(p[4]), p[5], p[6], LifeSave.Number(p[7]), p[8], LifeSave.Number(p[9])));
                    else throw new FormatException("Bad career row.");
                }
                else
                {
                    int at = line.IndexOf('=');
                    if (at <= 0) throw new FormatException("Bad line.");
                    values[line.Substring(0, at)] = line.Substring(at + 1);
                }
            }
            string Text(string name) => values.TryGetValue(name, out string? v) ? v : throw new FormatException("Missing " + name);
            ImmutableArray<string> Items(string name) => Text(name).Length == 0 ? ImmutableArray<string>.Empty : Text(name).Split(',').ToImmutableArray();
            int xp = LifeSave.Number(Text("xp"));
            ImmutableArray<uint> runs = Items("runs").Select(r => uint.TryParse(r, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) ? id : throw new FormatException("Bad run id.")).ToImmutableArray();
            return new CareerState
            {
                Xp = xp, Level = Career.LevelOf(xp), ChallengesWon = LifeSave.Number(Text("challenges")), Titles = Items("titles"), ScenariosDone = Items("scenarios"), RecordedRuns = runs,
                LastPlan = values.TryGetValue("plan", out string? plan) ? LifeSave.ParsePlan(plan) : null,
                LastIdentity = values.TryGetValue("identity", out string? identity) ? LifeSave.ParseIdentity(identity) : null,
                LastScenario = Text("scenario").Length == 0 ? null : Text("scenario"),
                Album = album.ToImmutable(), HallOfFame = hall.ToImmutable(),
            };
        }
    }
}
