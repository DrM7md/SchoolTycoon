using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchoolDirectorTycoon.Core;

var tests = new (string Name, Action Run)[]
{
    ("Opening state and rule guards", Opening),
    ("Day report reconciles with cash", Reconciliation),
    ("Incidents pause the game and apply the chosen response", Incidents),
    ("Two strategies can win across different seeds", Win),
    ("Bankruptcy and missed objectives end the campaign", Lose),
    ("Campus placement and legacy save migration", Placement),
    ("Save slots round-trip and resume deterministically", RoundTrip),
    ("Save replacement and format validation", InvalidSaves),
    ("Save backup fallback and error codes", BackupFallback),
    ("Golden v1/v2 save fixtures replay to a fixed hash", GoldenFixtures),
    ("Chronicle year is deterministic, guarded and balanced", ChronicleYear),
    ("School life runs the clock, letters, saves and balance", SchoolLifeYear),
    ("School life v2: time, people, building, money, moments and saves", SchoolLifeV2),
    ("School life v3: progress, soul, variety, personal life and saves", SchoolLifeV3),
    ("School life v3: balance of scenarios and neglect", SchoolLifeV3Balance)
};

if (args.Length == 2 && args[0] == "--write-fixtures")
{
    WriteFixtures(args[1]);
    return 0;
}

int failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex}");
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed.");
if (failures == 0 && args.Contains("--benchmark"))
    Benchmark();
return failures == 0 ? 0 : 1;

static void Opening()
{
    var school = Simulation.NewSchool(0);
    Check(school.Day == 1 && school.Campus.Capacity == 48 && school.RandomState == 1);
    Throws<RuleException>(() => Simulation.SetTuition(school, 61));
    Throws<RuleException>(() => Simulation.Build(school with { Cash = 0 }, Facility.Classroom));
    Throws<RuleException>(() => Simulation.ChangeStaff(school, Role.Deputy, false));
    var withDeputy = Simulation.ChangeStaff(school, Role.Deputy, true);
    Throws<RuleException>(() => Simulation.ChangeStaff(withDeputy, Role.Deputy, true));
    Check(school.Staff.Deputies == 0);
}

static void Reconciliation()
{
    var opening = Simulation.NewSchool(12);
    var built = Simulation.Build(opening, Facility.Canteen);
    var school = Simulation.AdvanceDay(built);
    var report = school.Reports.Single();
    Check(built.Cash == opening.Cash - Simulation.Price(Facility.Canteen));
    Check(report.Canteen == 200 && report.Investment == Simulation.Price(Facility.Canteen));
    Check(opening.Cash + report.Net == school.Cash);
    Check(opening.Students + report.Arrivals - report.Departures == school.Students);
    Check(school.InvestmentToday == 0 && school.ResponseCost == 0);
}

static void Incidents()
{
    var opening = Simulation.NewSchool(11) with { PendingIncident = Incident.Absence };
    Throws<RuleException>(() => Simulation.AdvanceDay(opening));
    Throws<RuleException>(() => Simulation.Build(opening, Facility.Library));
    var paid = Simulation.ResolveIncident(opening, true);
    Check(paid.Cash == opening.Cash - 240 && paid.PendingIncident is null && paid.Reputation == opening.Reputation + 2);
    Check(Simulation.AdvanceDay(paid).Reports.Single().ResponseCost == 240);
    var ignored = Simulation.ResolveIncident(opening, false);
    Check(ignored.Cash == opening.Cash && ignored.Reputation == opening.Reputation - 5);
    Throws<RuleException>(() => Simulation.ResolveIncident(ignored, true));
}

static SchoolState Campaign(uint seed, bool academicDirector = false)
{
    var school = Simulation.NewSchool(seed);
    school = Simulation.Build(school, Facility.Classroom);
    school = Simulation.Build(school, Facility.Lab);
    if (academicDirector)
    {
        school = Simulation.ChangeStaff(school, Role.AcademicDirector, true);
        school = Simulation.SetTuition(school, 36);
    }
    else
    {
        school = Simulation.Build(school, Facility.Library);
    }
    school = Simulation.ChangeStaff(school, Role.Teacher, true);
    while (school.Outcome == Outcome.InProgress)
    {
        if (school.PendingIncident.HasValue)
            school = Simulation.ResolveIncident(school, school.Cash >= 240);
        if (school.Students >= 60 && school.Staff.Teachers < 5)
            school = Simulation.ChangeStaff(school, Role.Teacher, true);
        school = Simulation.AdvanceDay(school);
    }
    return school;
}

static void Win()
{
    foreach (bool academicDirector in new[] { false, true })
    {
        for (uint seed = 1; seed <= 64; seed++)
        {
            var school = Campaign(seed, academicDirector);
            Check(school.Outcome == Outcome.Won && school.Reports.Length == 30);
            Check(school.Reports.Sum(report => report.Net) + 14_000 == school.Cash);
        }
    }
}

static void Lose()
{
    var poor = Simulation.AdvanceDay(Simulation.NewSchool(1) with { Cash = 0, Tuition = 20 });
    Check(poor.Outcome == Outcome.Lost);
    Throws<RuleException>(() => Simulation.AdvanceDay(poor));
    var late = Simulation.AdvanceDay(Simulation.NewSchool(1) with { Day = 30 });
    Check(late.Day == 31 && late.Outcome == Outcome.Lost);
}

static void Placement() => InTemporaryDirectory(directory =>
{
    var opening = Simulation.NewSchool(4);
    var tile = new CampusTile(4, 4);
    var school = Simulation.Build(opening, Facility.Library, tile);
    Check(school.Buildings.Contains(new CampusBuilding(Facility.Library, tile)) && school.Campus.Libraries == 1);
    Check(school.Cash == opening.Cash - Simulation.Price(Facility.Library));
    Throws<RuleException>(() => Simulation.Build(school, Facility.Classroom, tile));
    Throws<RuleException>(() => Simulation.Build(school, Facility.Classroom, new CampusTile(4, CampusLayout.MainPathRow)));
    Throws<RuleException>(() => Simulation.Build(school, Facility.Classroom, new CampusTile(-1, 2)));

    var store = new SaveStore(directory);
    store.Write(SaveSlot.Manual, school);
    EqualState(school, store.Read(SaveSlot.Manual));
    Throws<InvalidDataException>(() => store.Write(SaveSlot.Auto, school with { Buildings = opening.Buildings }));
    string path = Path.Combine(directory, "manual.json");
    var versionOne = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    versionOne["Version"] = 1;
    versionOne["State"]!.AsObject().Remove("Buildings");
    File.WriteAllText(path, versionOne.ToJsonString());
    var migrated = store.Read(SaveSlot.Manual);
    Check(migrated.Cash == school.Cash && migrated.Campus == school.Campus);
    Check(migrated.Buildings.SequenceEqual(CampusLayout.FromCounts(school.Campus)));
    Check(CampusLayout.Lots().Distinct().Count() >= 5 * Simulation.MaximumUnits);
});

static void RoundTrip() => InTemporaryDirectory(directory =>
{
    var store = new SaveStore(directory);
    var first = Simulation.AdvanceDay(Simulation.NewSchool(7));
    var pending = first with { PendingIncident = Incident.Complaint };
    store.Write(SaveSlot.Manual, pending);
    store.Write(SaveSlot.Auto, first);
    var loaded = store.Read(SaveSlot.Manual);
    EqualState(pending, loaded);
    EqualState(first, store.Read(SaveSlot.Auto));
    EqualState(Simulation.AdvanceDay(Simulation.ResolveIncident(pending, true)),
        Simulation.AdvanceDay(Simulation.ResolveIncident(loaded, true)));
});

static void InvalidSaves() => InTemporaryDirectory(directory =>
{
    var store = new SaveStore(directory);
    Throws<FileNotFoundException>(() => store.Read(SaveSlot.Manual));
    store.Write(SaveSlot.Manual, Simulation.NewSchool(1));
    store.Write(SaveSlot.Manual, Simulation.NewSchool(2));
    Check(store.Read(SaveSlot.Manual).RandomState == 2 && Directory.GetFiles(directory).Length == 2);
    string path = Path.Combine(directory, "manual.json");
    File.Delete(path + ".bak"); // Without a backup, a bad main file surfaces its own error.
    File.WriteAllText(path, """{"Version":3,"State":null}""");
    Throws<InvalidDataException>(() => store.Read(SaveSlot.Manual));
    File.WriteAllText(path, """{"Version":1,"State":{}}""");
    Throws<JsonException>(() => store.Read(SaveSlot.Manual));
    File.WriteAllText(path, "{");
    Throws<JsonException>(() => store.Read(SaveSlot.Manual));
    File.WriteAllText(path, new string(' ', 262_145));
    Throws<InvalidDataException>(() => store.Read(SaveSlot.Manual));
});

static void BackupFallback() => InTemporaryDirectory(directory =>
{
    var store = new SaveStore(directory);
    string path = Path.Combine(directory, "auto.json");
    store.Write(SaveSlot.Auto, Simulation.NewSchool(1));
    Check(!File.Exists(path + ".bak"));
    store.Write(SaveSlot.Auto, Simulation.NewSchool(2));
    Check(File.Exists(path + ".bak") && Directory.GetFiles(directory, "*.tmp").Length == 0);

    File.WriteAllText(path, "{");
    Check(store.Read(SaveSlot.Auto).RandomState == 1);
    File.WriteAllText(path, """{"Version":3,"State":null}""");
    Check(store.Read(SaveSlot.Auto).RandomState == 1);
    File.Delete(path);
    Check(store.Read(SaveSlot.Auto).RandomState == 1);

    // The backup is validated too; when both fail, the main file's error wins.
    File.WriteAllText(path + ".bak", """{"Version":2,"State":null}""");
    Throws<FileNotFoundException>(() => store.Read(SaveSlot.Auto));
    File.WriteAllText(path, """{"Version":3,"State":null}""");
    var error = Catch<InvalidDataException>(() => store.Read(SaveSlot.Auto));
    Check(error.GetSaveErrorCode() == SaveErrorCode.UnsupportedVersion
        && error.GetEnglishMessage() == "This save version is not supported.");
    File.WriteAllText(path, new string(' ', 262_145));
    Check(Catch<InvalidDataException>(() => store.Read(SaveSlot.Auto)).GetSaveErrorCode() == SaveErrorCode.FileTooLarge);
    Check(Catch<InvalidDataException>(() => store.Write(SaveSlot.Manual, Simulation.NewSchool(1) with { Day = 0 })).GetSaveErrorCode()
        == SaveErrorCode.InvalidState);

    // A locked target is retried, then the error surfaces; the temp file is always cleaned up.
    store.Write(SaveSlot.Auto, Simulation.NewSchool(3));
    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        Throws<IOException>(() => store.Write(SaveSlot.Auto, Simulation.NewSchool(4)));
    Check(store.Read(SaveSlot.Auto).RandomState == 3 && Directory.GetFiles(directory, "*.tmp").Length == 0);
});

// Golden fixtures shared with Unity: tests/Fixtures/save-v{1,2}.json are loaded, played forward with the
// scripted decisions below, and the final state must hash to a fixed value. Regenerate only on purpose
// (dotnet run --project tests/SchoolDirectorTycoon.Tests -- --write-fixtures tests/Fixtures).
static void GoldenFixtures()
{
    var expected = new Dictionary<string, string>
    {
        ["save-v1.json"] = "0B2BD53A91B0B0F2636A7515512A9A3D3B20F641BA2F238A467E1E3571B59700",
        ["save-v2.json"] = "0B2BD53A91B0B0F2636A7515512A9A3D3B20F641BA2F238A467E1E3571B59700"
    };
    foreach (var (file, hash) in expected)
    {
        string actual = WithTemporaryDirectory(directory =>
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", file), Path.Combine(directory, "manual.json"));
            return StateHash(Script(new SaveStore(directory).Read(SaveSlot.Manual), GoldenDays()));
        });
        if (actual != hash)
            throw new InvalidOperationException($"{file}: golden hash {actual} != expected {hash}.");
    }
}

static int GoldenDays() => 20;

static SchoolState Script(SchoolState school, int days)
{
    for (int i = 0; i < days && school.Outcome == Outcome.InProgress; i++)
    {
        if (school.PendingIncident.HasValue)
            school = Simulation.ResolveIncident(school, school.Cash >= 240);
        if (school.Students >= 60 && school.Staff.Teachers < 5)
            school = Simulation.ChangeStaff(school, Role.Teacher, true);
        school = Simulation.AdvanceDay(school);
    }
    return school;
}

static string StateHash(SchoolState state)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state))));
}

static void WriteFixtures(string target)
{
    var school = Simulation.NewSchool(2026);
    school = Simulation.Build(school, Facility.Classroom);
    school = Simulation.Build(school, Facility.Lab);
    school = Simulation.ChangeStaff(school, Role.Teacher, true);
    school = Simulation.SetTuition(school, 34);
    school = Script(school, 6);
    Directory.CreateDirectory(target);
    InTemporaryDirectory(directory =>
    {
        new SaveStore(directory).Write(SaveSlot.Manual, school);
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "manual.json")))!.AsObject();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(target, "save-v2.json"), json.ToJsonString(options) + "\n");
        json["Version"] = 1;
        json["State"]!.AsObject().Remove("Buildings");
        File.WriteAllText(Path.Combine(target, "save-v1.json"), json.ToJsonString(options) + "\n");
    });
    foreach (var file in new[] { "save-v1.json", "save-v2.json" })
        Console.WriteLine($"{file}: {WithTemporaryDirectory(directory =>
        {
            File.Copy(Path.Combine(target, file), Path.Combine(directory, "manual.json"));
            return StateHash(Script(new SaveStore(directory).Read(SaveSlot.Manual), GoldenDays()));
        })}");
}

static T Catch<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T ex) { return ex; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void EqualState(SchoolState expected, SchoolState actual) =>
    Check(expected == actual with { Reports = expected.Reports, Buildings = expected.Buildings }
        && expected.Reports.SequenceEqual(actual.Reports) && expected.Buildings.SequenceEqual(actual.Buildings));

static void InTemporaryDirectory(Action<string> run) => WithTemporaryDirectory<bool>(directory => { run(directory); return true; });

static T WithTemporaryDirectory<T>(Func<string, T> run)
{
    var directory = Directory.CreateTempSubdirectory("school-director-test-");
    try { return run(directory.FullName); }
    finally { directory.Delete(recursive: true); }
}

static void SchoolLifeYear()
{
    var good = new LifePlan("campus", 180, 220, 9, 3, 450, 6);
    var identity = LifeTestStart();
    static LifeState Play(uint seed, LifePlan plan, Func<LifeState, int, string> tonight)
    {
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart());
        int turn = 0;
        while (s.Ending == ChronicleEnding.InProgress)
        {
            if (s.PendingCard is LifeCard card) s = SchoolLife.ResolveCard(s, Affordable(s, card, turn++));
            else if (s.Phase == LifePhase.School) s = SchoolLife.Advance(s, 7);
            else s = SchoolLife.Evening(s, s.Energy <= 0 ? "home" : tonight(s, turn++));
        }
        return s;
    }
    // Answers rotate, but a rival or ministry card never picks an answer the school cannot pay for.
    static string Affordable(LifeState s, LifeCard card, int turn)
    {
        string pick = card.Choices[turn % card.Choices.Length];
        return SchoolLife.CardCost(s, pick) > s.Cash ? card.Choices.First(c => SchoolLife.CardCost(s, c) <= s.Cash) : pick;
    }
    // Spends only what is left over: one project at a time in a sensible order, the lowest-skilled teacher on the cheap course.
    static LifeState Invest(uint seed, LifePlan plan, Func<LifeState, int, string> tonight)
    {
        string[] order = { "garden", "canteen", "training_center", "library", "ac", "lab" };
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart());
        int turn = 0, now = 1000;
        while (s.Ending == ChronicleEnding.InProgress)
        {
            s = SchoolLife.Tick(s, now);
            if (s.PendingCard is LifeCard card) s = SchoolLife.ResolveCard(s, Affordable(s, card, turn++));
            else if (s.Phase == LifePhase.School) s = SchoolLife.Advance(s, 7);
            else
            {
                string? next = order.FirstOrDefault(id => !s.UpgradesDone.Contains(id));
                if (next != null && s.BuildingJob == null && s.Cash > SchoolLife.Upgrades.First(u => u.Id == next).Cost + 45000)
                    s = SchoolLife.StartUpgrade(s, next, now);
                LifeTeacher? low = s.Roster.Where(t => t.TrainingEnd == 0).OrderBy(t => t.Skill).FirstOrDefault();
                if (low != null && low.Skill < 65 && s.Cash > 40000 && SchoolLife.TrainingSlotCount(s) > s.Roster.Count(t => t.TrainingEnd != 0))
                    s = SchoolLife.StartTraining(s, low.Id, "method", now);
                s = SchoolLife.Evening(s, s.Energy <= 0 ? "home" : tonight(s, turn++));
                now += 240;
            }
        }
        return s;
    }
    Func<LifeState, int, string> balanced = (s, t) => t % 3 == 0 ? "home" : t % 3 == 1 ? "tutoring" : "parents_night";
    Func<LifeState, int, string> greedy = (s, t) => "tutoring";
    Check(Play(9, good, balanced).Log.SequenceEqual(Play(9, good, balanced).Log));

    // Setup and the clock
    LifeState fresh = SchoolLife.New(3);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(fresh, good with { Seats = 181 }, identity));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(fresh, good with { Teachers = 0 }, identity));
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.Open(fresh, good, identity with { StartingCash = 100 }));
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.Advance(fresh, 5));
    LifeState day = SchoolLife.Open(fresh, good, identity) with { EventToday = "bus_delay", CaseToday = null, EventMinute = 500 };
    Check(day.Minute == 450 && SchoolLife.EndMinute(good) == 450 + 6 * 45 + 20);
    day = SchoolLife.Advance(day, 1000);
    Check(day.PendingEvent == "bus_delay" && day.Minute == 500);
    LifeState atEvent = day;
    Expect(RuleCode.ChronicleEventPending, () => SchoolLife.Advance(atEvent, 1));
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.Evening(atEvent, "home"));
    day = SchoolLife.Choose(day, "apologize");
    day = SchoolLife.Advance(day, 1000);
    Check(day.Phase == LifePhase.Evening && day.Minute == SchoolLife.EndMinute(good));
    Check(SchoolLife.Activities(day).Contains("gym_contract"));
    Check(!SchoolLife.Activities(day with { Plan = good with { BuildingId = "villa", Seats = 90 } }).Contains("gym_night"));
    LifeState evening = day;
    Expect(RuleCode.LifeUnknownActivity, () => SchoolLife.Evening(evening with { GymContract = true }, "gym_night"));
    LifeState rested = SchoolLife.Evening(day, "gym_contract");
    Check(rested.GymContract && rested.Day == 2 && rested.Phase == LifePhase.School);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Adjust(evening, good with { BuildingId = "villa", Seats = 50 }));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Adjust(evening, good with { Teachers = 10 }));
    LifeState hired = SchoolLife.Adjust(day, good with { Admins = 4 });
    Check(hired.Plan!.Admins == 4 && hired.Cash == day.Cash - SchoolLife.HiringCost);

    // Morning letters and days away
    LifeState letters = SchoolLife.Open(fresh, good, identity);
    Check(SchoolLife.Greet(fresh, 100).PendingLetter == null);
    letters = SchoolLife.Greet(letters, 100);
    LifeState waiting = letters;
    Check(letters.PendingLetter == "life.letter.salem.0" && letters.Streak == 1 && SchoolLife.Greet(letters, 100) == letters);
    Expect(RuleCode.LifeLetterWaiting, () => SchoolLife.Advance(waiting, 1));
    int cash = letters.Cash;
    letters = SchoolLife.ReadLetter(letters);
    LifeState afterRead = letters;
    Check(letters.Cash == cash + 400 && letters.PendingLetter == null);
    Expect(RuleCode.LifeNoLetter, () => SchoolLife.ReadLetter(afterRead));
    Check(SchoolLife.Greet(letters, 100) == letters && SchoolLife.Greet(letters, 99) == letters);
    letters = SchoolLife.Greet(letters with { GymContract = true }, 102);
    Check(letters.Streak == 2 && letters.LetterBonus == 600 && letters.AwayIncome == 2 * SchoolLife.GymContractAwayDaily);
    Check(SchoolLife.Greet(SchoolLife.ReadLetter(letters), 110).Streak == 1);

    var keys = System.Collections.Immutable.ImmutableArray.Create("salem:b");
    Check(SchoolLife.LetterKey(12, keys) == "life.letter.salem.4.b" && SchoolLife.LetterKey(13, keys) == "life.letter.noura.4.a" && SchoolLife.LetterKey(15, keys) == "life.letter.plain.0");
    LifeState decide = SchoolLife.Greet(SchoolLife.Open(fresh, good, identity) with { LetterIndex = 6 }, 50);
    Check(decide.PendingLetter == "life.letter.salem.2");
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.ReadLetter(decide));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.ReadLetter(decide, "z"));
    Check(SchoolLife.ReadLetter(decide, "b").Decisions.Single() == "salem:b");
    Check(SchoolLife.LetterChoices("life.letter.plain.2").Count == 0);

    // Save round trip, including mid-event and setup states
    foreach (LifeState sample in new[] { fresh, decide, atEvent, Play(4, good, balanced) })
    {
        string text = LifeSave.Write(sample);
        Check(LifeSave.Write(LifeSave.Read(text)) == text);
    }
    Throws<FormatException>(() => LifeSave.Read("SDTLIFE 99\n"));
    Throws<FormatException>(() => LifeSave.Read("SDTLIFE 1\ncash=x"));

    // Balance
    const int runs = 400;
    foreach (var (name, plan, policy, min, max) in new[] {
        ("sensible", good, balanced, 55, 100),
        ("greedy nights", good, greedy, 0, 40),
        ("skeleton staff", good with { Teachers = 3, Admins = 1, Fee = 420 }, balanced, 0, 50),
        ("empty villa", new LifePlan("villa", 90, 480, 5, 2, 450, 6), balanced, 0, 35),
        ("oversized", new LifePlan("academy", 320, 150, 18, 6, 450, 6), balanced, 0, 70),
        ("investor", good, balanced, 45, 100) })
    {
        int done = 0, money = 0;
        for (uint seed = 1; seed <= runs; seed++)
        {
            LifeState end = name == "investor" ? Invest(seed, plan, policy) : Play(seed, plan, policy);
            if (end.Ending == ChronicleEnding.YearCompleted) done++;
            money += end.Cash / runs;
        }
        int percent = done * 100 / runs;
        Console.WriteLine($"  life {name}: {percent}% finish, average cash {money}");
        if (percent < min || percent > max) throw new InvalidOperationException($"Balance: {name} finished {percent}%, expected {min}-{max}.");
    }

    // A player who uses every director tool daily and builds whenever affordable must still be challenged.
    static LifeState Active(uint seed, LifePlan plan)
    {
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart());
        int turn = 0, now = 1000;
        string[] broadcasts = { "broadcast_study", "broadcast_kindness", "broadcast_discipline" };
        while (s.Ending == ChronicleEnding.InProgress)
        {
            s = SchoolLife.Tick(s, now);
            if (s.PendingCard is LifeCard card) s = SchoolLife.ResolveCard(s, Affordable(s, card, turn++));
            else if (s.Phase == LifePhase.School)
            {
                if (s.ActionsToday.Length == 0 && s.Energy >= 40)
                {
                    s = SchoolLife.DirectorAction(s, "gate");
                    s = SchoolLife.DirectorAction(s, broadcasts[turn++ % broadcasts.Length]);
                }
                s = SchoolLife.Advance(s, 7);
            }
            else
            {
                now += 1440;
                s = SchoolLife.Tick(s, now);
                LifeUpgrade? next = s.BuildingJob != null || s.Cash < 70000 ? null : SchoolLife.Upgrades.FirstOrDefault(u =>
                    u.Id != "wing" && !s.UpgradesDone.Contains(u.Id) && (u.Requires == null || s.UpgradesDone.Contains(u.Requires)));
                if (next != null) s = SchoolLife.StartUpgrade(s, next.Id, now);
                s = SchoolLife.Evening(s, s.Energy < 30 || s.Family < 45 || s.Staff < 50 ? "home" : s.Parents < 50 ? "parents_night" : turn % 2 == 0 ? "tutoring" : "home");
                turn++;
            }
        }
        return s;
    }
    int finished = 0, grades = 0, firstPlace = 0, wealth = 0, top = 0;
    for (uint seed = 1; seed <= runs; seed++)
    {
        LifeState end = Active(seed, good);
        if (end.Ending != ChronicleEnding.YearCompleted) continue;
        finished++;
        wealth += end.Cash;
        if (SchoolLife.Grade(end) == ChronicleGrade.S) grades++;
        if (SchoolLife.Rank(end) == 1) firstPlace++;
        top += (end.Reputation + end.Parents + end.Staff + end.Academics) / 4;
    }
    int rate(int n) => n * 100 / Math.Max(1, finished);
    Console.WriteLine($"  life active: {finished * 100 / runs}% finish, S {rate(grades)}%, first {rate(firstPlace)}%, average cash {wealth / Math.Max(1, finished)}, average standing {top / Math.Max(1, finished)}");
    if (finished * 100 / runs < 55 || rate(grades) > 25 || wealth / Math.Max(1, finished) > 150000) throw new InvalidOperationException("Balance: the active director has it too easy or too hard.");

    static void Expect(RuleCode code, Action action)
    {
        try { action(); }
        catch (RuleException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected " + code);
    }
}

static LifeIdentity LifeTestIdentity() => new LifeIdentity(1, 2, 3, "creative");
static LifeStart LifeTestStart() => new LifeStart(LifeTestIdentity(), "standard", SchoolLife.StartingCash, Career.MaxLevel);

static void SchoolLifeV2()
{
    var plan = new LifePlan("campus", 180, 220, 9, 3, 450, 6);
    LifeState Opened(uint seed = 3) => SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart()) with { EventToday = null, CaseToday = null, EventMinute = -1 };
    LifeState Night(LifeState s) => SchoolLife.Advance(s, 2000);

    // Time extension and limits
    Check(SchoolLife.EndMinute(plan with { PeriodMinutes = 30, BreakMinutes = 10 }) == 450 + 180 + 10);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(SchoolLife.New(1), plan with { PeriodMinutes = 33 }, LifeTestStart()));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(SchoolLife.New(1), plan with { BreakMinutes = 45 }, LifeTestStart()));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(SchoolLife.New(1), plan, LifeTestStart() with { Identity = new LifeIdentity(9, 0, 0, "strict") }));
    LifeState day = Opened();
    Expect(RuleCode.LifeDayLimit, () => SchoolLife.ExtendDay(day, 10));
    for (int i = 0; i < 4; i++) day = SchoolLife.ExtendDay(day, 15);
    LifeState longest = day;
    Check(day.DayExtra == 60 && SchoolLife.DayEnd(day) == SchoolLife.EndMinute(plan) + 60);
    Expect(RuleCode.LifeDayLimit, () => SchoolLife.ExtendDay(longest, 15));
    day = Opened();
    for (int i = 0; i < 3; i++) day = SchoolLife.ExtendDay(day, -15);
    LifeState shortest = day;
    Expect(RuleCode.LifeDayLimit, () => SchoolLife.ExtendDay(shortest, -15));
    LifeState late = Opened() with { Minute = SchoolLife.EndMinute(plan) - 5 };
    Expect(RuleCode.LifeDayLimit, () => SchoolLife.ExtendDay(late, -15));
    Check(Night(shortest).Minute == SchoolLife.EndMinute(plan) - 45);
    LifeState extended = SchoolLife.Evening(Night(longest), "home");
    Check(extended.DayExtra == 0 && extended.Day == 2);
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.ExtendDay(Night(Opened()), 15));
    LifeState squeezed = SchoolLife.ExtendDay(Opened() with { Minute = 700, EventToday = "bus_delay", EventMinute = SchoolLife.EndMinute(plan) }, -15);
    Check(SchoolLife.Advance(squeezed, 2000).PendingEvent == "bus_delay");

    // People
    LifeState open = Opened();
    Check(open.Roster.Length == 9 && open.Followed.Length == 5 && open.Roster.All(t => t.Skill >= 35 && t.Skill <= 70 && t.Mood >= 55 && t.Mood <= 75 && !t.TraitKnown && t.WeeklyPay == 1900 + t.Skill * 10));
    Check(open.Followed.All(s => s.Marks >= 40 && s.Marks <= 75 && s.Conduct >= 40 && s.Conduct <= 75));
    Check(open.Roster.Select(t => t.Name).Concat(open.Followed.Select(s => s.Name)).Distinct().Count() == 14);
    Check(SchoolLife.Open(SchoolLife.New(3), plan, LifeTestStart()).Roster.SequenceEqual(SchoolLife.Open(SchoolLife.New(3), plan, LifeTestStart()).Roster));

    // Interview flow
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.StartInterview(open));
    LifeState evening = Night(open);
    LifeState interview = SchoolLife.StartInterview(evening);
    Check(interview.Cash == evening.Cash - SchoolLife.InterviewCost && interview.Candidates.Length == 3 && !interview.Candidates.Any(c => c.TraitRevealed));
    Expect(RuleCode.LifeActionUsed, () => SchoolLife.StartInterview(interview));
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.AskCandidate(interview, 999, "dream"));
    int first = interview.Candidates[0].Id, second = interview.Candidates[1].Id;
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.AskCandidate(interview, first, "salary"));
    LifeState asked = SchoolLife.AskCandidate(interview, first, "dream");
    Check(asked.Candidates[0].TraitRevealed && asked.InterviewReply == "life.interview.reply.dream." + asked.Candidates[0].Trait);
    Expect(RuleCode.LifeActionUsed, () => SchoolLife.AskCandidate(asked, second, "experience"));
    LifeState hired = SchoolLife.HireCandidate(asked, first);
    Check(hired.Roster.Length == 10 && hired.Plan!.Teachers == 10 && hired.Candidates.IsEmpty && hired.Roster[9].TraitKnown && hired.Roster[9].Id == first);
    Check(hired.Cash == asked.Cash - SchoolLife.HiringCost);
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.HireCandidate(hired, second));
    Check(SchoolLife.CancelInterview(interview).Candidates.IsEmpty);
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.CancelInterview(evening));

    // Dismissal
    LifeTeacher leaving = evening.Roster[0];
    LifeState dismissed = SchoolLife.DismissTeacher(evening, leaving.Id);
    Check(dismissed.Roster.Length == 8 && dismissed.Plan!.Teachers == 8 && dismissed.Staff == evening.Staff - 2 && dismissed.Cash == evening.Cash - leaving.WeeklyPay);
    LifeState lone = evening with { Roster = evening.Roster.RemoveRange(1, 8), Plan = plan with { Teachers = 1 } };
    Expect(RuleCode.LifeLastTeacher, () => SchoolLife.DismissTeacher(lone, leaving.Id));
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.DismissTeacher(evening, 999));

    // Director actions
    LifeState acts = SchoolLife.DirectorAction(open, "gate");
    Check(acts.Log[acts.Log.Length - 1].Key == "life.action.gate");
    Expect(RuleCode.LifeActionUsed, () => SchoolLife.DirectorAction(acts, "gate"));
    LifeState spoke = SchoolLife.DirectorAction(acts, "broadcast_study");
    Expect(RuleCode.LifeActionUsed, () => SchoolLife.DirectorAction(spoke, "broadcast_kindness"));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.DirectorAction(open, "dance"));
    LifeState watched = SchoolLife.DirectorAction(open, "observe:" + open.Roster[2].Id);
    Check(watched.Roster[2].TraitKnown && watched.Roster[2].Mood == open.Roster[2].Mood + 3 && watched.Log[watched.Log.Length - 1].Name == open.Roster[2].Name);
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.DirectorAction(open, "observe:999"));
    Check(SchoolLife.Evening(Night(spoke), "home").ActionsToday.IsEmpty);

    // Cases stop the clock and report back a week later
    LifeState scheduled = Opened() with { CaseToday = new LifeCase(2, "fight"), EventMinute = 500 };
    LifeState caseOpen = SchoolLife.Advance(scheduled, 2000);
    Check(caseOpen.Minute == 500 && caseOpen.PendingCard!.Kind == "case" && caseOpen.PendingCard.BodyKey == "life.case.fight" && caseOpen.PendingCard.Name == caseOpen.Followed[1].Name);
    Check(caseOpen.PendingCard!.ButtonKeys.SequenceEqual(new[] { "life.case.fight.punish", "life.case.fight.talk", "life.case.fight.parents" }));
    Expect(RuleCode.LifeCasePending, () => SchoolLife.Advance(caseOpen, 1));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.ResolveCard(caseOpen, "shout"));
    LifeState settled = SchoolLife.ResolveCard(caseOpen, "punish");
    Check(settled.PendingCard == null && settled.Followed[1].Conduct == Math.Min(100, caseOpen.Followed[1].Conduct + 8) && settled.Log[settled.Log.Length - 1].Key == "life.case.fight.punish.ok");
    Check(settled.FollowUps.Single().DueDay == settled.Day + 7);
    LifeState later = settled with { Day = settled.Day + 6, Phase = LifePhase.Evening, Minute = 2000, EventToday = null, EventMinute = -1 };
    later = SchoolLife.Evening(later, "home");
    Check(later.FollowUps.IsEmpty && later.Log.Any(e => e.Key.StartsWith("life.case.followup.punish.") && e.Name == caseOpen.Followed[1].Name));
    Check(Enumerable.Range(1, 60).Select(i => SchoolLife.Open(SchoolLife.New((uint)i), plan, LifeTestStart())).Any(s => s.CaseToday != null));
    Check(Enumerable.Range(1, 200).Select(i => SchoolLife.Open(SchoolLife.New((uint)i), plan, LifeTestStart())).All(s => s.CaseToday == null || s.EventToday == null));
    LifeState eventCard = SchoolLife.Advance(Opened() with { EventToday = "bus_delay", EventMinute = 500 }, 2000);
    Check(eventCard.PendingCard!.Kind == "event" && eventCard.PendingCard.BodyKey == "ev.bus_delay" && eventCard.PendingCard.ButtonKeys[0] == "ev.bus_delay." + eventCard.PendingCard.Choices[0]);
    Check(SchoolLife.ResolveCard(eventCard, "apologize").PendingEvent == null);
    Expect(RuleCode.ChronicleNoPendingEvent, () => SchoolLife.ResolveCard(open, "x"));

    // Upgrades: one at a time, locks, real time
    const int now = 5000;
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.StartUpgrade(open, "garden", now));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.StartUpgrade(evening, "castle", now));
    Expect(RuleCode.LifeUpgradeLocked, () => SchoolLife.StartUpgrade(evening with { Cash = 500000 }, "training_center2", now));
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.StartUpgrade(evening with { Cash = 10 }, "garden", now));
    LifeState rich = evening with { Cash = 500000 };
    LifeState building = SchoolLife.StartUpgrade(rich, "garden", now);
    Check(building.Cash == 491000 && building.BuildingJob == "garden" && building.BuildingEnd == now + 60);
    Expect(RuleCode.LifeUpgradeBusy, () => SchoolLife.StartUpgrade(building, "ac", now));
    Check(SchoolLife.Tick(building, now + 59).BuildingJob == "garden");
    Check(SchoolLife.Tick(building, 100).LastNow == now && SchoolLife.Tick(building, 100).BuildingJob == "garden");
    LifeState built = SchoolLife.Tick(SchoolLife.Tick(building, 100), now + 60);
    Check(built.BuildingJob == null && built.UpgradesDone.Contains("garden") && built.Log.Any(e => e.Key == "life.up.garden.done") && built.Achievements.Contains("first_upgrade"));
    Check(SchoolLife.Tick(built, now + 60) == built);
    Expect(RuleCode.LifeUpgradeDone, () => SchoolLife.StartUpgrade(built, "garden", now + 70));
    LifeState wingDone = built with { UpgradesDone = built.UpgradesDone.Add("wing") };
    Check(SchoolLife.Capacity(wingDone) == 240 && SchoolLife.Adjust(wingDone, plan with { Seats = 240 }).Plan!.Seats == 240);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Adjust(built, plan with { Seats = 240 }));

    // Training: real-time completion and a clock that runs backwards
    int teacher = rich.Roster[0].Id;
    Expect(RuleCode.LifeNeedsTrainingCenter, () => SchoolLife.StartTraining(rich, teacher, "method", now));
    LifeState centre = rich with { UpgradesDone = rich.UpgradesDone.Add("training_center") };
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.StartTraining(centre, teacher, "juggling", now));
    Expect(RuleCode.LifeNoCandidate, () => SchoolLife.StartTraining(centre, 999, "method", now));
    LifeState training = SchoolLife.StartTraining(centre, teacher, "method", now);
    Check(training.Roster[0].TrainingEnd == now + 360 && training.Roster[0].Course == "method" && SchoolLife.Teaching(training) == 8);
    Expect(RuleCode.LifeTeacherBusy, () => SchoolLife.StartTraining(training, teacher, "care", now));
    Expect(RuleCode.LifeTeacherBusy, () => SchoolLife.DismissTeacher(training, teacher));
    LifeState two = SchoolLife.StartTraining(training, rich.Roster[1].Id, "care", now);
    Expect(RuleCode.LifeNoTrainingSlot, () => SchoolLife.StartTraining(two, rich.Roster[2].Id, "care", now));
    Check(SchoolLife.TrainingSlotCount(two with { UpgradesDone = two.UpgradesDone.Add("training_center2") }) == 4);
    Check(SchoolLife.Tick(training, 10).Roster[0].TrainingEnd == now + 360);
    LifeState earlyStart = SchoolLife.StartTraining(training, rich.Roster[1].Id, "care", 10);
    Check(earlyStart.Roster[1].TrainingEnd == now + 180);
    LifeState taught = SchoolLife.Tick(training, now + 360);
    Check(taught.Roster[0].TrainingEnd == 0 && taught.Roster[0].Course == null && taught.Roster[0].Skill == rich.Roster[0].Skill + 6 && taught.CoursesFinished == 1);
    ChronicleEntry doneLine = taught.Log[taught.Log.Length - 1];
    Check(doneLine.Key == "life.course.method.done" && doneLine.Name == rich.Roster[0].Name && doneLine.Subject == rich.Roster[0].Subject);

    // Loan
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.TakeLoan(open, 50000));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.TakeLoan(evening, 1000));
    LifeState loan = SchoolLife.TakeLoan(evening, 50000);
    Check(loan.Cash == evening.Cash + 50000 && loan.LoanWeeks == 30 && SchoolLife.WeeklyBill(loan) == SchoolLife.WeeklyBill(evening) + 2200);
    Expect(RuleCode.LifeLoanActive, () => SchoolLife.TakeLoan(loan, 50000));
    LifeState lastPayment = SchoolLife.Evening(loan with { Day = 5, LoanWeeks = 1 }, "home");
    Check(lastPayment.LoanWeeks == 0 && lastPayment.Log.Any(e => e.Key == "life.loan.paid"));

    // Rival
    LifeState quiet = SchoolLife.Evening(evening with { Day = 45, RivalPressure = 0 }, "home");
    Check(quiet.RivalPressure == 0 && quiet.RivalStage == 0);
    LifeState before = SchoolLife.Evening(evening with { Day = 40 }, "home");
    Check(before.RivalPressure == 0);
    LifeState pressured = SchoolLife.Evening(evening with { Day = 50, RivalPressure = 29, Reputation = 50 }, "home");
    Check(pressured.RivalPressure == 31 && pressured.PendingRival == "rival.30" && pressured.PendingCard!.Kind == "rival" && pressured.PendingCard.BodyKey == "life.rival.30");
    Check(pressured.PendingCard!.ButtonKeys[0] == "life.rival.30.scholarship");
    Expect(RuleCode.LifeCasePending, () => SchoolLife.Advance(pressured, 1));
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.ResolveCard(pressured with { Cash = 10 }, "scholarship"));
    LifeState answered = SchoolLife.ResolveCard(pressured, "scholarship");
    Check(answered.RivalPressure == 16 && answered.PendingRival == null && answered.Cash == pressured.Cash - 6000 && answered.Log[answered.Log.Length - 1].Key == "life.rival.30.scholarship.ok");
    LifeState again = SchoolLife.Evening(evening with { Day = 55, RivalPressure = 40, RivalStage = 1, Reputation = 50 }, "home");
    Check(again.PendingRival == null);
    LifeState sixty = SchoolLife.Evening(evening with { Day = 55, RivalPressure = 59, RivalStage = 1, Reputation = 50 }, "home");
    Check(sixty.PendingRival == "rival.60" && sixty.RivalStage == 2);
    Check(SchoolLife.Evening(evening with { Day = 55, RivalPressure = 40, Reputation = 80, RivalStage = 1 }, "home").RivalPressure == 41);

    // Ministry, seasons, activities
    LifeState visit = SchoolLife.Evening(evening with { Day = 115, Academics = 70 }, "home");
    Check(visit.Week == 24 && visit.PendingMinistry && visit.PendingCard!.Kind == "ministry" && visit.PendingCard.BodyKey == "life.ministry");
    LifeState passed = SchoolLife.ResolveCard(visit, "show");
    Check(passed.Reputation >= visit.Reputation && passed.Log[passed.Log.Length - 1].Key == "life.ministry.show.ok" && passed.Memories.Contains(passed.Log.Length - 1));
    Check(SchoolLife.ResolveCard(visit with { Academics = 20 }, "show").Log.Last().Key == "life.ministry.show.fail");
    LifeState exam = SchoolLife.Evening(evening with { Day = 35 }, "home");
    Check(exam.Season == "exams_mid" && exam.Log.Count(e => e.Key == "life.season.exams_mid") == 1);
    Check((evening with { Day = 72 }).Season == "ramadan" && SchoolLife.SeasonAt(1) == null);
    Check(!SchoolLife.Activities(evening).Contains("summer_camp") && SchoolLife.Activities(evening with { Day = 136 }).Contains("summer_camp"));
    LifeState camp = SchoolLife.Evening(evening with { Day = 136 }, "summer_camp");
    Check(camp.Log.Any(e => e.Key == "life.act.summer_camp.ok"));

    // Goals
    LifeState goal = SchoolLife.ReadLetter(SchoolLife.Greet(Opened(), 3));
    Check(goal.GoalId == SchoolLife.Goals[3] && goal.GoalId == "visit_gate");
    LifeState walked = SchoolLife.Evening(Night(SchoolLife.DirectorAction(goal, "gate")), "home");
    Check(walked.GoalDone && walked.Log.Any(e => e.Key == "life.goal.done" && e.Effect.Cash == 800));
    LifeState idle = SchoolLife.Evening(Night(goal), "home");
    Check(!idle.GoalDone);

    // Graduation, rank and achievements
    LifeState finale = SchoolLife.Evening(Night(open) with { Day = 180, Reputation = 90, Parents = 95, Staff = 95, Academics = 95, Students = 180 }, "home");
    Check(finale.Ending == ChronicleEnding.InProgress && finale.PendingSpeech && finale.PendingCard!.Kind == "speech" && finale.PendingCard.BodyKey == "life.grad.body");
    Check(finale.PendingCard!.ButtonKeys.SequenceEqual(new[] { "life.grad.thanks", "life.grad.dreams", "life.grad.duty" }));
    Expect(RuleCode.LifeCasePending, () => SchoolLife.Evening(finale, "home"));
    LifeState graduated = SchoolLife.ResolveCard(finale, "dreams");
    Check(graduated.Ending == ChronicleEnding.YearCompleted && !graduated.PendingSpeech && graduated.Achievements.Contains("no_dismissals") && graduated.Log.Any(e => e.Key == "life.grad.dreams.ok"));
    Check(SchoolLife.Rank(graduated) >= 1 && SchoolLife.Rank(graduated) <= 3 && SchoolLife.Rank(graduated with { Reputation = 1, Parents = 1, Staff = 1, Academics = 1, Students = 0 }) == 10);
    Check(graduated.Memories.All(i => i >= 0 && i < graduated.Log.Length) && graduated.Memories.Any(i => graduated.Log[i].Key.StartsWith("life.grad.")));

    // Save round trip across every new kind of state
    LifeState busy = SchoolLife.AskCandidate(SchoolLife.StartInterview(SchoolLife.TakeLoan(two with { Cash = 500000, RivalPressure = 33, RivalStage = 1, Achievements = two.Achievements.Add("full_house") }, 50000)), two.NextPersonId, "dream");
    busy = SchoolLife.StartUpgrade(busy, "lab", now) with { GoalId = "visit_gate", GoalDone = true, DayExtra = 15, FollowUps = settled.FollowUps };
    foreach (LifeState sample in new[] { busy, caseOpen, pressured, visit, finale, graduated, watched })
    {
        string text = LifeSave.Write(sample);
        LifeState back = LifeSave.Read(text);
        Check(LifeSave.Write(back) == text);
        Check(back.PendingCard == null ? sample.PendingCard == null : back.PendingCard.Id == sample.PendingCard!.Id);
    }
    LifeState loaded = LifeSave.Read(LifeSave.Write(busy));
    Check(loaded.Roster.SequenceEqual(busy.Roster) && loaded.Candidates.SequenceEqual(busy.Candidates) && loaded.Followed.SequenceEqual(busy.Followed));
    Check(loaded.FollowUps.SequenceEqual(busy.FollowUps) && loaded.BuildingJob == "lab" && loaded.BuildingEnd == busy.BuildingEnd && loaded.LoanWeeks == 30);
    Check(loaded.RivalPressure == 33 && loaded.RivalStage == 1 && loaded.Identity == LifeTestIdentity() && loaded.GoalId == "visit_gate" && loaded.GoalDone);
    Check(loaded.Achievements.SequenceEqual(busy.Achievements) && loaded.InterviewAsked && loaded.InterviewReply == busy.InterviewReply && loaded.LastNow == busy.LastNow);
    Check(LifeSave.Read(LifeSave.Write(settled)).Log.Last().Name == settled.Log.Last().Name);
    Check(LifeSave.Read(LifeSave.Write(taught)).Log.Last().Subject == taught.Log.Last().Subject);
    Check(LifeSave.Read(LifeSave.Write(graduated)).Memories.SequenceEqual(graduated.Memories));

    // Migration from a literal version 1 save
    const string v1 = "SDTLIFE 1\nphase=2\nday=12\nminute=740\ncash=88000\nstudents=151\nrep=52\nparents=58\nstaff=61\nacad=49\nrandom=123456789\n"
        + "eventToday=\neventMinute=-1\npendingEvent=\nending=0\ngym=1\nrealDay=90\nstreak=2\nletterIndex=3\nletter=\nbonus=0\naway=0\n"
        + "decisions=salem:a\nused=bus_delay\nplan=campus,180,220,9,3,450,6\nL|1|life.opening|-68000|0|0|0|0|0\nL|5|life.week|12000|2|0|-1|0|1\n";
    LifeState migrated = LifeSave.Read(v1);
    Check(migrated.Day == 12 && migrated.Cash == 88000 && migrated.GymContract && migrated.Decisions.Single() == "salem:a" && migrated.Log.Length == 2);
    Check(migrated.Plan!.PeriodMinutes == 45 && migrated.Plan.BreakMinutes == 20 && migrated.Identity == SchoolLife.DefaultIdentity);
    Check(migrated.Roster.Length == 9 && migrated.Followed.Length == 5 && migrated.NextPersonId == 10 && migrated.Log[1].Name == -1 && migrated.Log[1].Subject == null);
    Check(LifeSave.Read(v1).Roster.SequenceEqual(migrated.Roster));
    Check(LifeSave.Write(LifeSave.Read(LifeSave.Write(migrated))) == LifeSave.Write(migrated));
    Check(SchoolLife.Evening(migrated, "home").Day == 13);
    LifeState setupV1 = LifeSave.Read("SDTLIFE 1\nphase=0\nday=0\nminute=0\ncash=120000\nstudents=0\nrep=50\nparents=55\nstaff=60\nacad=50\nrandom=7\n"
        + "eventToday=\neventMinute=-1\npendingEvent=\nending=0\ngym=0\nrealDay=0\nstreak=0\nletterIndex=0\nletter=\nbonus=0\naway=0\ndecisions=\nused=\n");
    Check(setupV1.Plan == null && setupV1.Roster.IsEmpty && setupV1.Identity == null);
    Throws<FormatException>(() => LifeSave.Read("SDTLIFE 2\ncash=1\n"));
    Throws<FormatException>(() => LifeSave.Read(LifeSave.Write(evening).Replace("\nT|1|", "\nT|x|")));

    static void Expect(RuleCode code, Action action)
    {
        try { action(); }
        catch (RuleException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected " + code);
    }
}

static void SchoolLifeV3()
{
    var plan = new LifePlan("campus", 180, 220, 9, 3, 450, 6);
    LifeState Clean(LifeState s) => s with { EventToday = null, CaseToday = null, EventMinute = -1, CardQueue = System.Collections.Immutable.ImmutableArray<LifeQueued>.Empty };
    LifeState Opened(uint seed = 3) => Clean(SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart()));
    LifeState Night(LifeState s) => SchoolLife.Advance(s, 2000);
    LifeState With(LifeState s, string kind, string id, int name = -1) => s with { CardQueue = s.CardQueue.Add(new LifeQueued(kind, id, name)) };
    var identity = new LifeIdentity(0, 0, 0, "strict");
    LifeStart Level(int level, string scenario = "standard", LifeIdentity? who = null) => new LifeStart(who ?? identity, scenario, Career.StartingCash(level), level);

    // Career levels and unlocks
    Check(Career.LevelOf(199) == 1 && Career.LevelOf(200) == 2 && Career.LevelOf(499) == 2 && Career.LevelOf(6500) == 10 && Career.LevelOf(99999) == 10);
    Check(Career.NextThreshold(1) == 200 && Career.NextThreshold(9) == 6500 && Career.NextThreshold(10) == -1);
    Check(Career.StartingCash(1) == 120000 && Career.StartingCash(4) == 135000);
    Check(Career.UnlockedNames(1) == 3 && Career.UnlockedNames(2) == 4 && Career.UnlockedNames(10) == 8 && Career.UnlockedCrests(1) == 2 && Career.UnlockedCrests(3) == 4 && Career.UnlockedCrests(10) == 6);
    LifeState blank = SchoolLife.New(5);
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(1, who: new LifeIdentity(3, 0, 0, "strict"))));
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(1, who: new LifeIdentity(0, 2, 0, "strict"))));
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(1, who: new LifeIdentity(0, 0, 0, "sports"))));
    Check(SchoolLife.Open(blank, plan, Level(2, who: new LifeIdentity(3, 2, 0, "faith"))).Identity!.Philosophy == "faith");
    var academy = new LifePlan("academy", 200, 220, 10, 3, 450, 6);
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, academy, Level(2)));
    Check(SchoolLife.Open(blank, academy, Level(3)).Plan!.BuildingId == "academy");
    Check(SchoolLife.Open(blank, plan, Level(1, "rescue")).Scenario == "rescue");
    var villa = new LifePlan("villa", 40, 180, 3, 1, 450, 6);
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, villa, Level(1, "village")));
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(2, "girls")));
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(3, "international")));
    Expect(RuleCode.LifeLocked, () => SchoolLife.Open(blank, plan, Level(5, "girls", new LifeIdentity(0, 0, 0, "sports"))));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, plan, Level(5, "atlantis")));
    Check(Career.Start(new CareerState { Xp = 500, Level = 3 }, identity, "standard") == new LifeStart(identity, "standard", 130000, 3));

    // Recording a run
    LifeState open = Opened();
    Check(open.RunId == 3 && open.StarsBest >= 1 && open.Energy == 80 && open.Family == 70 && open.Conscience == 70 && open.Morning.SequenceEqual(new[] { "math", "arabic", "science" }));
    LifeState finished = open with { Day = 180, Ending = ChronicleEnding.YearCompleted, Reputation = 80, Parents = 80, Staff = 80, Academics = 80, Students = 180, Achievements = open.Achievements.Add("full_house").Add("rank_one") };
    Check(SchoolLife.Grade(finished) == ChronicleGrade.S && SchoolLife.Rank(finished) == 1);
    Check(Career.XpFor(finished) == 20 * 36 + 300 + 100 + 150);
    CareerState career = Career.Record(Career.New(), finished);
    Check(career.Xp == 1270 && career.Level == 4 && career.HallOfFame.Length == 1 && career.ScenariosDone.Single() == "standard" && career.LastPlan == plan && career.LastScenario == "standard" && career.LastIdentity == LifeTestIdentity());
    Check(Career.AlbumCount(career, "ach.full_house") == 1 && Career.AlbumCount(career, "title." + SchoolLife.TitleOf(finished)) == 1);
    CareerState twice = Career.Record(career, finished);
    Check(twice == career && Career.Record(career, open) == career);
    LifeState broke = open with { RunId = 77, Day = 42, Ending = ChronicleEnding.Bankrupt, Achievements = System.Collections.Immutable.ImmutableArray<string>.Empty };
    CareerState after = Career.Record(career, broke);
    Check(after.Xp == 1270 + 160 && after.HallOfFame.Length == 2 && after.ScenariosDone.Length == 1 && after.HallOfFame[1].Ending == "Bankrupt" && after.HallOfFame[1].Grade == "D");
    Check(Career.XpFor(finished with { Scenario = "rescue" }) == (20 * 36 + 300 + 100 + 150) * 130 / 100);
    LifeState memory = finished with { RunId = 78, Log = finished.Log.Add(new ChronicleEntry(10, "life.ach.full_house", new ChronicleEffect())).Add(new ChronicleEntry(180, "life.grad.dreams.ok", new ChronicleEffect())) };
    Check(SchoolLife.BestMemory(memory).Key == "life.grad.dreams.ok" && SchoolLife.BestMemory(open).Key == null);
    Check(SchoolLife.BestMemory(memory with { Log = memory.Log.Add(new ChronicleEntry(90, "life.letter.salem.4.a", new ChronicleEffect(), 4)) }) == ("life.letter.salem.4.a", 4));
    CareerState full = Career.Record(after with { ChallengesWon = 2 }, memory with { ChallengesWon = 1 });
    string careerText = CareerSave.Write(full);
    CareerState careerBack = CareerSave.Read(careerText);
    Check(CareerSave.Write(careerBack) == careerText && careerBack.Xp == full.Xp && careerBack.Level == full.Level && careerBack.ChallengesWon == 3 && careerBack.LastPlan == plan);
    Check(careerBack.HallOfFame.SequenceEqual(full.HallOfFame) && careerBack.Album.SequenceEqual(full.Album) && careerBack.RecordedRuns.SequenceEqual(full.RecordedRuns) && careerBack.HallOfFame[2].MemoryKey == "life.grad.dreams.ok");
    Check(Career.Record(careerBack, memory) == careerBack);
    Check(CareerSave.Read(CareerSave.Write(Career.New())).Level == 1);
    Throws<FormatException>(() => CareerSave.Read("SDTCAREER 9\n"));
    Throws<FormatException>(() => CareerSave.Read("SDTCAREER 1\nxp=abc\n"));

    // Day report
    LifeState day = open with { Cash = open.Cash + 700, Log = open.Log.Add(new ChronicleEntry(1, "life.goal.done", new ChronicleEffect(Cash: 5000, Parents: 2))) };
    LifeState evening = Night(day);
    Check(evening.LastDay!.Day == 1 && evening.LastDay.CashDelta == 700 && evening.LastDay.BestLog == day.Log.Length - 1 && evening.LastDay.TeaseKey.StartsWith("life.tease."));
    Check(Night(open).LastDay!.BestLog == -1 && Night(open with { Cash = 5 }).LastDay!.TeaseKey == "life.tease.5");
    Check(evening.MarkCash == evening.Cash && evening.MarkLog == evening.Log.Length);
    Check(Night(open with { Day = 31 }).LastDay!.TeaseKey == "life.tease.0");

    // Month card
    LifeState month = Night(open with { Day = 20 });
    Check(month.MonthCards.Single() == new LifeMonth(1, month.Cash, month.Reputation, month.Parents, month.Staff, month.Academics, month.Students, "B"));
    Check(month.PendingCard!.Kind == "month" && month.PendingCard.BodyKey == "life.month.body" && month.PendingCard.ButtonKeys.Single() == "life.card.ok");
    LifeState waitingMonth = month;
    Expect(RuleCode.LifeCasePending, () => SchoolLife.Evening(waitingMonth, "home"));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.ResolveCard(waitingMonth, "a"));
    Check(SchoolLife.ResolveCard(month, "ok").PendingCard == null && Night(open with { Day = 19 }).MonthCards.IsEmpty);

    // Stars and celebrations
    Check(SchoolLife.Stars(open with { Reputation = 50, Parents = 50, Staff = 50, Academics = 50, Students = 39 }) == 1);
    Check(SchoolLife.Stars(open with { Reputation = 50, Parents = 50, Staff = 50, Academics = 50, Students = 40 }) == 2);
    Check(SchoolLife.Stars(open with { Reputation = 58, Parents = 58, Staff = 58, Academics = 58, Students = 80 }) == 3);
    Check(SchoolLife.Stars(open with { Reputation = 66, Parents = 66, Staff = 66, Academics = 66, Students = 120 }) == 4);
    Check(SchoolLife.Stars(open with { Reputation = 74, Parents = 74, Staff = 74, Academics = 74, Students = 149 }) == 4);
    Check(SchoolLife.Stars(open with { Reputation = 74, Parents = 74, Staff = 74, Academics = 74, Students = 150 }) == 5);
    LifeState star = Night(open with { Reputation = 66, Parents = 66, Staff = 66, Academics = 66, Students = 120, StarsBest = 1 });
    Check(star.StarsBest == 4 && star.Celebrations.Single() == new LifeCelebration("star", "stars", 4));
    Check(Night(star with { Phase = LifePhase.School, Minute = 450, Celebrations = star.Celebrations.Clear() }).Celebrations.IsEmpty);
    Expect(RuleCode.LifeNoCelebration, () => SchoolLife.AckCelebration(open));
    Check(SchoolLife.AckCelebration(star).Celebrations.IsEmpty);
    LifeState eveningRich = Night(open) with { Cash = 500000, Celebrations = ImmutableArray<LifeCelebration>.Empty };
    LifeState gift = SchoolLife.Tick(SchoolLife.StartUpgrade(eveningRich, "garden", 5000), 5060);
    Check(gift.Celebrations.SequenceEqual(new[] { new LifeCelebration("achievement", "first_upgrade", 0), new LifeCelebration("gift", "life.up.garden", 9000) }));
    LifeState market = open with { Plan = plan with { Fee = 300 }, Reputation = 66, Parents = 66, Staff = 66, Academics = 66, Students = 120 };
    Check(SchoolLife.CurrentDemand(market) == SchoolLife.Demand(300, 66, 66) * 105 / 100);
    Check(SchoolLife.CurrentDemand(market with { Students = 119 }) == SchoolLife.Demand(300, 66, 66));

    // Titles
    LifeState titled = open with { Reputation = 50, Parents = 50, Staff = 50, Academics = 50, Students = 10, Cash = 1000, Ending = ChronicleEnding.YearCompleted };
    Check(SchoolLife.TitleOf(titled) == "survivor");
    Check(SchoolLife.TitleOf(titled with { Conscience = 30 }) == "shady");
    Check(SchoolLife.TitleOf(titled with { RoundUps = 2 }) == "on_paper");
    Check(SchoolLife.TitleOf(titled with { RisksTaken = 5 }) == "gambler");
    Check(SchoolLife.TitleOf(titled with { Parents = 75, Staff = 70 }) == "beloved");
    Check(SchoolLife.TitleOf(titled with { Academics = 75 }) == "reformer");
    Check(SchoolLife.TitleOf(titled with { CoursesFinished = 5 }) == "mentor");
    Check(SchoolLife.TitleOf(titled with { UpgradesDone = System.Collections.Immutable.ImmutableArray.Create("a", "b", "c", "d", "e", "f") }) == "builder");
    Check(SchoolLife.TitleOf(titled with { CasePunishes = 8 }) == "iron");
    Check(SchoolLife.TitleOf(titled with { CaseTalks = 8 }) == "caring");
    Check(SchoolLife.TitleOf(titled with { Cash = 150000 }) == "budget_king");
    Check(SchoolLife.TitleOf(titled with { Students = 100 }) == "no_dismissals");
    LifeState speech = open with { PendingSpeech = true, Reputation = 50, Parents = 50, Staff = 50, Academics = 50, Celebrations = open.Celebrations.Clear() };
    Check(SchoolLife.ResolveCard(speech, "thanks").Celebrations.Last().Kind == "title");
    LifeState bankrupt = SchoolLife.ResolveCard(With(open with { Cash = 100 }, "seed", "donor_demands"), "ok");
    Check(bankrupt.Ending == ChronicleEnding.Bankrupt && bankrupt.Celebrations.Last().Kind == "title" && bankrupt.CardQueue.IsEmpty);

    // Chat
    LifeState gossip = open;
    for (int i = 0; i < 12; i++) gossip = SchoolLife.Evening(Clean(Night(Clean(gossip))), "home");
    var perDay = gossip.Chat.Where(c => c.Group == "parents").GroupBy(c => c.Day).Select(g => g.Count()).ToArray();
    foreach (var line in gossip.Chat.GroupBy(c => c.Key))
    {
        var days = line.Select(c => c.Day).OrderBy(d => d).ToArray();
        Check(days.Zip(days.Skip(1), (a, b) => b - a).All(gap => gap >= SchoolLife.ChatMemoryDays));
    }
    Check(perDay.Length >= 6 && perDay.All(n => n >= 1 && n <= 3) && gossip.Chat.All(c => c.Group == "parents" ? c.Sender >= 0 && c.Sender < 12 && c.Name == -1 : gossip.Roster.Any(t => t.Name == c.Sender) && gossip.Roster.Any(t => t.Name == c.Name)));
    Check(gossip.Chat.Any(c => c.Group == "staff" && c.Key.StartsWith("life.room.gossip.")) && gossip.Chat.All(c => int.Parse(c.Key.Split('.').Last()) < 5));
    var spam = System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Repeat(new LifeChat(1, "parents", 0, "life.chat.quiet.0", -1), 200));
    Check(Night(open with { Chat = spam }).Chat.Length == 200 && Night(open with { Chat = spam }).Chat.Last().Day == 1);
    bool Says(LifeState s, string topic) => Enumerable.Range(1, 80).Any(i => Night(s with { RandomState = (uint)i * 2654435761u }).Chat.Any(c => c.Key.StartsWith(topic)));
    Check(Says(open with { Plan = plan with { Fee = 250 }, Scenario = "girls" }, "life.chat.fee_high") && !Says(open with { Plan = plan with { Fee = 250 } }, "life.chat.fee_high"));
    Check(Says(open with { LastFee = 100 }, "life.chat.fee_up") && !Says(open, "life.chat.fee_up"));
    Check(Says(open with { Tags = open.Tags.Add("dismissed") }, "life.room.dismissed") && !Says(open, "life.room.dismissed"));

    // Seeds fire on the due week
    LifeState ev = open with { Day = 26, PendingEvent = "tuition_debt", Conscience = 70 };
    LifeState planted = SchoolLife.ResolveCard(ev, "grant");
    Check(planted.Seeds.Single() == new LifeSeed("debt_repaid", 14) && planted.Conscience == 73);
    LifeState notYet = SchoolLife.Evening(planted with { Day = 65, Phase = LifePhase.Evening }, "home");
    Check(notYet.Week == 14 && notYet.CardQueue.Any(q => q.Kind == "seed") && notYet.Seeds.IsEmpty);
    LifeState early = SchoolLife.Evening(planted with { Day = 60, Phase = LifePhase.Evening }, "home");
    Check(early.Week == 13 && early.Seeds.Length == 1 && !early.CardQueue.Any(q => q.Kind == "seed"));
    LifeState seedCard = Clean(notYet) with { CardQueue = notYet.CardQueue.Where(q => q.Kind == "seed").ToImmutableArray() };
    Check(seedCard.PendingCard!.Kind == "seed" && seedCard.PendingCard.Id == "debt_repaid" && seedCard.PendingCard.BodyKey == "life.seed.debt_repaid");
    LifeState paidBack = SchoolLife.ResolveCard(seedCard, "ok");
    Check(paidBack.Cash == seedCard.Cash + 3000 && paidBack.Log.Last().Key == "life.seed.debt_repaid.ok");
    Check(SchoolLife.SeedRules.Length == 10 && SchoolLife.SeedRules.All(r => ChronicleDeck.Find(r.Event).Choices.Any(c => c.Id == r.Choice)));
    Check(SchoolLife.ResolveCard(open with { Day = 170, PendingEvent = "star_offer" }, "raise").Seeds.Single().DueWeek == 36);

    // Conscience and scandal
    Func<uint, LifeState> tryChange = s => SchoolLife.ResolveCard(open with { Day = 50, PendingEvent = "grade_favor", RandomState = s }, "change");
    var outcomes = Enumerable.Range(1, 60).Select(i => tryChange((uint)i * 40503u)).ToArray();
    Check(outcomes.All(o => o.Conscience == 58 && o.RisksTaken == 1));
    Check(outcomes.Any(o => o.Seeds.Any(x => x.Id == "grade_exposed")) && outcomes.Any(o => o.Seeds.IsEmpty));
    Check(SchoolLife.ConscienceBand(open) == "high" && SchoolLife.ConscienceBand(open with { Conscience = 40 }) == "mid" && SchoolLife.ConscienceBand(open with { Conscience = 34 }) == "low" && SchoolLife.ConscienceEnding(open) == "life.ending.conscience.high");
    LifeState fallen = SchoolLife.ResolveCard(open with { Day = 100, PendingEvent = "cheating_son", Conscience = 35, RandomState = 11 }, "cover");
    Check(fallen.Conscience == 20 && fallen.ScandalDone && fallen.PendingCard!.Kind == "scandal" && fallen.PendingCard.BodyKey == "life.scandal" && fallen.PendingCard.ButtonKeys.SequenceEqual(new[] { "life.scandal.confess", "life.scandal.deny" }));
    LifeState confessed = SchoolLife.ResolveCard(fallen, "confess");
    Check(confessed.Conscience == 35 && confessed.Log.Last().Key == "life.scandal.confess.ok" && confessed.Reputation == Math.Max(0, fallen.Reputation - 6));
    LifeState denied = SchoolLife.ResolveCard(fallen, "deny");
    Check(denied.Conscience == 10 && denied.PendingCard == null && denied.CardQueue.IsEmpty);

    // Scenarios
    LifeState rescue = SchoolLife.Open(blank, plan, Level(1, "rescue"));
    Check(rescue.DebtWeeks == 20 && rescue.Reputation == 30 && rescue.Parents == 35 && rescue.Cash == 84000 - SchoolLife.OpeningCost(plan));
    Check(SchoolLife.WeeklyBill(rescue) == SchoolLife.WeeklyBill(rescue with { Scenario = "standard", DebtWeeks = 0 }) + 800);
    Check(SchoolLife.Evening(Clean(rescue) with { Phase = LifePhase.Evening, Day = 5 }, "home").DebtWeeks == 19);
    LifeState village = SchoolLife.Open(blank, villa, Level(2, "village"));
    Check(village.Parents == 65 && SchoolLife.Capacity(village) == 40 && SchoolLife.OpeningCost(villa, "village") == 1500 * 4 + 40 * 100 + 4 * 500);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, plan, Level(2, "village")));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, villa with { Seats = 41 }, Level(2, "village")));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, villa with { Fee = 201 }, Level(2, "village")));
    Check(SchoolLife.Open(blank, plan, Level(3, "girls")).Parents == 60);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, plan with { Fee = 501 }, Level(3, "girls")));
    LifeState intl = SchoolLife.Open(blank, plan with { Fee = 800 }, Level(4, "international"));
    Check(intl.Parents == 45 && intl.Plan!.Fee == 800);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(blank, plan with { Fee = 901 }, Level(4, "international")));
    Check(SchoolLife.Scenarios.SequenceEqual(new[] { "standard", "rescue", "village", "girls", "international" }));

    // Timetable
    LifeState tt = Night(open);
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.SetTimetable(open, open.Morning));
    Expect(RuleCode.LifeBadTimetable, () => SchoolLife.SetTimetable(tt, System.Collections.Immutable.ImmutableArray.Create("math", "math", "science")));
    Expect(RuleCode.LifeBadTimetable, () => SchoolLife.SetTimetable(tt, System.Collections.Immutable.ImmutableArray.Create("math", "science")));
    Expect(RuleCode.LifeBadTimetable, () => SchoolLife.SetTimetable(tt, System.Collections.Immutable.ImmutableArray.Create("math", "science", "magic")));
    Expect(RuleCode.LifeBadTimetable, () => SchoolLife.SetTimetable(tt, default));
    var soft = System.Collections.Immutable.ImmutableArray.Create("islamic", "social", "sports");
    Check(SchoolLife.SetTimetable(tt, soft).Morning.SequenceEqual(soft));
    LifeState WeekEnd(LifeState s, int day) => SchoolLife.Evening(s with { Day = day, Reputation = 50, Parents = 50, Staff = 50, Academics = 50, RandomState = 99 }, "home");
    LifeState core = tt with { Roster = tt.Roster.Select((t, i) => t with { Subject = i % 2 == 0 ? "math" : "science", Trait = "patient" }).ToImmutableArray() };
    int Delta(LifeState s, int day) => WeekEnd(s, day).Log.First(e => e.Key == "life.week" && e.Week == day).Effect.Academics;
    Check(Delta(core, 10) == Delta(core, 5) + 1 + (10 % 20 == 0 ? -1 : 0));
    Check(Delta(core with { Morning = soft }, 5) == Delta(core, 5) - 1);
    Check(WeekEnd(core with { Morning = soft }, 20).Log.First(e => e.Key == "life.week" && e.Week == 20).Effect.Parents == WeekEnd(core, 20).Log.First(e => e.Key == "life.week" && e.Week == 20).Effect.Parents - 1);

    // Absent card
    LifeState absent = With(open, "absent", "absent", open.Roster[0].Name);
    Check(absent.PendingCard!.Kind == "absent" && absent.PendingCard.Name == open.Roster[0].Name && absent.PendingCard.ButtonKeys.SequenceEqual(new[] { "life.absent.cover", "life.absent.merge", "life.absent.substitute" }));
    LifeState substituted = SchoolLife.ResolveCard(absent, "substitute");
    Check(substituted.Cash == absent.Cash - 400 && substituted.Log.Last().Key == "life.absent.substitute.ok" && substituted.Log.Last().Name == open.Roster[0].Name);
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.ResolveCard(absent with { Cash = 100 }, "substitute"));
    Check(SchoolLife.ResolveCard(absent, "cover").Energy == absent.Energy - 5 && SchoolLife.ResolveCard(absent, "merge").Academics == absent.Academics - 1);
    Check(Enumerable.Range(1, 400).Select(i => SchoolLife.Open(SchoolLife.New((uint)i * 7919u), plan, LifeTestStart())).Count(s => s.CardQueue.Any(q => q.Kind == "absent" && q.Name >= 0)) > 0);

    // Results
    LifeState results = SchoolLife.Evening(tt with { Day = 35, Academics = 70 }, "home");
    Check(results.Week == 8 && results.PendingCard!.Kind == "results" && results.PendingCard.BodyKey == "life.results.good" && results.PendingCard.ButtonKeys.SequenceEqual(new[] { "life.results.publish", "life.results.round_up" }));
    LifeState published = SchoolLife.ResolveCard(results, "publish");
    int top = results.Followed.OrderByDescending(x => x.Marks).ThenBy(x => x.Id).First().Name;
    Check(published.Log.Last().Key == "life.results.top" && published.Log.Last().Name == top && published.Log[published.Log.Length - 2].Key == "life.results.publish.ok" && published.Conscience == results.Conscience + 2);
    LifeState rounded = SchoolLife.ResolveCard(results, "round_up");
    Check(rounded.Conscience == results.Conscience - 12 && rounded.RoundUps == 1);
    Check(SchoolLife.Evening(tt with { Day = 160, Academics = 30 }, "home").CardQueue.Any(q => q.Kind == "results" && q.Id == "bad"));
    Check(!SchoolLife.Evening(tt with { Day = 36 }, "home").CardQueue.Any(q => q.Kind == "results"));

    // Weekly challenge
    LifeState chal = SchoolLife.Greet(open, 7 * 2 + 3);
    Check(chal.ChallengeId == "happy_staff" && chal.ChallengeWeek == 2 && chal.ChallengeDay == 1 && !chal.ChallengeDone);
    Check(SchoolLife.Greet(SchoolLife.ReadLetter(chal), 7 * 2 + 4).ChallengeDay == 1);
    Check(SchoolLife.Greet(SchoolLife.ReadLetter(chal), 7 * 3).ChallengeId == "full_seats");
    LifeState ready = SchoolLife.ReadLetter(chal) with { Staff = 70 };
    Check(!Night(ready with { Day = 4 }).ChallengeDone);
    LifeState won = Night(ready with { Day = 5 });
    Check(won.ChallengeDone && won.ChallengesWon == 1 && won.Celebrations.Last() == new LifeCelebration("challenge", "happy_staff", 3000) && won.Log.Any(e => e.Key == "life.challenge.won" && e.Effect.Cash == 3000));
    Check(Night(won with { Phase = LifePhase.School, Minute = 450, Day = 6 }).ChallengesWon == 1);
    LifeState lost = Night(ready with { Day = 5, Staff = 30 });
    Check(lost.ChallengeDone && lost.ChallengesWon == 0 && lost.Log.Any(e => e.Key == "life.challenge.lost"));
    LifeState noTutor = SchoolLife.Evening(Night(open with { ChallengeId = "no_tutoring", ChallengeDay = 1 }), "tutoring");
    Check(noTutor.ChallengeBroken);
    Check(SchoolLife.TakeLoan(Night(open with { ChallengeId = "no_loan", ChallengeDay = 1 }), 50000).ChallengeBroken);

    // Energy and family
    LifeState tired = open with { Energy = 0 };
    Expect(RuleCode.LifeNoEnergy, () => SchoolLife.DirectorAction(tired, "gate"));
    Expect(RuleCode.LifeNoEnergy, () => SchoolLife.Evening(Night(tired), "tutoring"));
    LifeState slept = Clean(SchoolLife.Evening(Night(tired), "home"));
    Check(slept.Energy == 30 && slept.Family == tired.Family + 3);
    LifeState tutored = Clean(SchoolLife.Evening(Night(open), "tutoring"));
    Check(tutored.Energy == 75 && tutored.Family == 69);
    Check(SchoolLife.DirectorAction(open, "gate").Energy == 75);
    LifeState gymEve = Clean(SchoolLife.Evening(Night(open), "gym_contract"));
    Check(gymEve.Energy == 82);

    // Home cards
    Check(SchoolLife.HomeIds.Length == 9);
    LifeState vacation = With(open, "home", "vacation");
    Check(vacation.PendingCard!.BodyKey == "life.home.vacation" && vacation.PendingCard.ButtonKeys.SequenceEqual(new[] { "life.home.vacation.a", "life.home.vacation.b" }));
    LifeState away = SchoolLife.ResolveCard(vacation, "a");
    Check(away.Energy == 100 && away.Family == 82 && away.Cash == vacation.Cash - 3000 && away.Log.Last().Key == "life.home.vacation.a.ok");
    Check(SchoolLife.CardCost(vacation, "a") == 3000 && SchoolLife.CardCost(vacation, "b") == 0);
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.ResolveCard(vacation with { Cash = 10 }, "a"));
    Check(SchoolLife.ResolveCard(With(open, "home", "kid_at_school"), "b").Conscience == 60);
    foreach (string id in SchoolLife.HomeIds)
    {
        LifeState card = With(open, "home", id);
        Check(SchoolLife.ResolveCard(card, "a").Log.Last().Key == $"life.home.{id}.a.ok" && SchoolLife.ResolveCard(card, "b").Log.Last().Key == $"life.home.{id}.b.ok");
        Check(SchoolLife.CardCost(card, "b") == 0);
    }
    LifeState crisis = SchoolLife.Evening(tt with { Family = 20 }, "home");
    Check(crisis.CardQueue.Any(q => q.Id == "crisis") && crisis.LastCrisisDay == 2);
    LifeState crisisAgain = SchoolLife.Evening(Clean(crisis) with { Phase = LifePhase.Evening }, "home");
    Check(!crisisAgain.CardQueue.Any(q => q.Id == "crisis"));
    Check(SchoolLife.Evening(Clean(crisis) with { Phase = LifePhase.Evening, Day = 11, Family = 20 }, "home").CardQueue.Any(q => q.Id == "crisis"));

    // Promotions
    LifeState staff = tt with { Roster = tt.Roster.Select((t, i) => t with { Skill = i < 4 ? 85 : 50, Mood = 60 }).ToImmutableArray() };
    int Id(int i) => staff.Roster[i].Id;
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.PromoteTeacher(open, open.Roster[0].Id, "head"));
    Expect(RuleCode.LifeNotQualified, () => SchoolLife.PromoteTeacher(staff, Id(5), "head"));
    Expect(RuleCode.LifeNotQualified, () => SchoolLife.PromoteTeacher(staff with { Roster = staff.Roster.SetItem(0, staff.Roster[0] with { Skill = 75 }) }, Id(0), "deputy"));
    Expect(RuleCode.ChronicleUnknownChoice, () => SchoolLife.PromoteTeacher(staff, Id(0), "king"));
    LifeState head = SchoolLife.PromoteTeacher(staff, Id(0), "head");
    Check(head.Roster[0].Role == "head" && head.Roster[0].WeeklyPay == staff.Roster[0].WeeklyPay + 400 && head.Log.Last().Key == "life.promote.head" && head.Log.Last().Name == staff.Roster[0].Name && head.Celebrations.Last().Kind == "promotion");
    Expect(RuleCode.LifeActionUsed, () => SchoolLife.PromoteTeacher(head, Id(0), "deputy"));
    LifeState heads = SchoolLife.PromoteTeacher(head, Id(1), "head");
    Expect(RuleCode.LifeRoleFull, () => SchoolLife.PromoteTeacher(heads, Id(2), "head"));
    LifeState deputy = SchoolLife.PromoteTeacher(heads, Id(2), "deputy");
    Check(deputy.Roster[2].WeeklyPay == staff.Roster[2].WeeklyPay + 800);
    Expect(RuleCode.LifeRoleFull, () => SchoolLife.PromoteTeacher(deputy, Id(3), "deputy"));
    Check(LifeSave.Read(LifeSave.Write(deputy)).Roster.SequenceEqual(deputy.Roster));
    int Week(LifeState s) => WeekEnd(s, 10).Log.First(e => e.Key == "life.week" && e.Week == 10).Effect.Academics;
    Check(Week(heads) == Week(staff) + 2);

    // Teacher of the month
    LifeState totm = SchoolLife.NameTeacherOfMonth(staff, Id(0));
    Check(totm.Roster[0].Mood == 75 && totm.Roster[1].Mood == 58 && totm.Log.Last().Key == "life.totm" && totm.Log.Last().Name == staff.Roster[0].Name);
    Expect(RuleCode.LifeMonthDone, () => SchoolLife.NameTeacherOfMonth(totm, Id(1)));
    Check(SchoolLife.NameTeacherOfMonth(totm with { Day = 21 }, Id(1)).LastTotmMonth == 1);
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.NameTeacherOfMonth(open, open.Roster[0].Id));

    // Epilogues
    var ends = SchoolLife.Epilogues(open with { Followed = System.Collections.Immutable.ImmutableArray.Create(new LifeStudent(1, 3, 80, 80), new LifeStudent(2, 4, 50, 50), new LifeStudent(3, 5, 10, 10)) });
    Check(ends.SequenceEqual(new[] { new LifeEpilogue("life.epilogue.good.1", 3), new LifeEpilogue("life.epilogue.mid.2", 4), new LifeEpilogue("life.epilogue.bad.0", 5) }));

    // Arcs
    Check(open.StoryArcs.Length == 2 && open.StoryArcs.All(a => a.Stage == 0 && a.NextDay >= 26 && a.NextDay <= 130) && open.StoryArcs[0].NextDay < open.StoryArcs[1].NextDay);
    Check(open.Roster.Any(t => t.Name == open.StoryArcs[0].Name));
    LifeState PlayArc(LifeState s, string first, string second, string third)
    {
        LifeArc Find(LifeState x) => x.StoryArcs[0];
        foreach (string answer in new[] { first, second, third })
        {
            int due = Find(s).NextDay;
            LifeState before = Clean(s) with { Day = due - 1, Phase = LifePhase.Evening, Cash = 100000 };
            s = SchoolLife.Evening(before, "home");
            string cardId = s.CardQueue.First(q => q.Kind == "arc").Id;
            s = s with { CardQueue = s.CardQueue.Where(q => q.Kind == "arc").ToImmutableArray() };
            Check(s.PendingCard!.BodyKey == "life.arc." + cardId && s.PendingCard.ButtonKeys[0] == $"life.arc.{cardId}.a" && s.PendingCard.Name == Find(s).Name);
            int day = s.Day;
            s = SchoolLife.ResolveCard(s, answer);
            Check(s.Log.Last().Key == $"life.arc.{cardId}.{answer}.ok");
            if (Find(s).Stage < 3) Check(Find(s).NextDay >= day + 2 && Find(s).NextDay <= day + 4);
        }
        return s;
    }
    LifeState careful = PlayArc(open, "a", "a", "a");
    LifeState careless = PlayArc(open, "b", "b", "a");
    Check(careful.StoryArcs[0].Stage == 3 && careful.StoryArcs[0].Answers == "aaa" && careful.Log.Last().Effect.Staff == 3 && careless.Log.Last().Effect.Staff == 1);
    LifeState gone = PlayArc(open, "a", "a", "b");
    Check(gone.Roster.Length == open.Roster.Length - 1 && gone.Plan!.Teachers == open.Plan!.Teachers - 1 && !gone.Roster.Any(t => t.Name == open.StoryArcs[0].Name));
    LifeState settledArc = SchoolLife.Evening(Clean(careful) with { Day = careful.StoryArcs[1].NextDay - 1, Phase = LifePhase.Evening, StoryArcs = careful.StoryArcs.SetItem(1, careful.StoryArcs[1] with { Stage = 3 }) }, "home");
    Check(!settledArc.CardQueue.Any(q => q.Kind == "arc"));

    // Card priority
    LifeState stack = With(With(With(open, "month", "1"), "home", "doctor"), "scandal", "scandal");
    Check(stack.PendingCard!.Kind == "scandal");
    Check((stack with { PendingMinistry = true }).PendingCard!.Kind == "ministry");
    Check(SchoolLife.ResolveCard(stack, "confess").PendingCard!.Kind == "home");

    // Saves
    LifeState busy = deputy with
    {
        Seeds = deputy.Seeds.Add(new LifeSeed("inclusion", 20)), Tags = deputy.Tags.Add("praise"), ChallengeId = "no_loan", ChallengeWeek = 3, ChallengeDay = 2, ChallengeBroken = true,
        MonthCards = month.MonthCards, Chat = gossip.Chat, LastDay = evening.LastDay, Energy = 33, Family = 44, Conscience = 21, ScandalDone = true, RoundUps = 1, RisksTaken = 2, CasePunishes = 3, CaseTalks = 4,
        ChallengesWon = 5, LastCrisisDay = 9, LastTotmMonth = 2, Scenario = "rescue", DebtWeeks = 7, Morning = soft, StarsBest = 3,
    };
    busy = With(busy, "absent", "absent", 4);
    string text = LifeSave.Write(busy);
    LifeState back = LifeSave.Read(text);
    Check(text.StartsWith("SDTLIFE 3\n") && LifeSave.Write(back) == text);
    Check(back.Seeds.SequenceEqual(busy.Seeds) && back.MonthCards.SequenceEqual(busy.MonthCards) && back.Chat.SequenceEqual(busy.Chat) && back.CardQueue.SequenceEqual(busy.CardQueue) && back.StoryArcs.SequenceEqual(busy.StoryArcs) && back.Celebrations.SequenceEqual(busy.Celebrations));
    Check(back.LastDay == busy.LastDay && back.Energy == 33 && back.Family == 44 && back.Conscience == 21 && back.ScandalDone && back.Scenario == "rescue" && back.DebtWeeks == 7 && back.Morning.SequenceEqual(soft) && back.RunId == busy.RunId && back.OpeningPlan == busy.OpeningPlan);
    Check(back.PendingCard!.Kind == "absent" && back.ChallengeBroken && back.ChallengesWon == 5 && back.LastTotmMonth == 2 && back.LastCrisisDay == 9 && back.StarsBest == 3 && back.Tags.SequenceEqual(busy.Tags));
    Throws<FormatException>(() => LifeSave.Read(text.Replace("\nenergy=33", "\nenergy=x")));
    Throws<FormatException>(() => LifeSave.Read(text.Replace("\nQ|absent|absent|4", "\nQ|absent|absent")));

    // Literal older saves
    const string v1 = "SDTLIFE 1\nphase=2\nday=12\nminute=740\ncash=88000\nstudents=151\nrep=52\nparents=58\nstaff=61\nacad=49\nrandom=123456789\n"
        + "eventToday=\neventMinute=-1\npendingEvent=\nending=0\ngym=1\nrealDay=90\nstreak=2\nletterIndex=3\nletter=\nbonus=0\naway=0\n"
        + "decisions=salem:a\nused=bus_delay\nplan=campus,180,220,9,3,450,6\nL|1|life.opening|-68000|0|0|0|0|0\n";
    const string v2 = "SDTLIFE 2\nphase=2\nday=40\nminute=740\ncash=91000\nstudents=160\nrep=55\nparents=60\nstaff=62\nacad=57\nrandom=987654321\n"
        + "eventToday=\neventMinute=-1\npendingEvent=\nending=0\ngym=0\nrealDay=91\nstreak=3\nletterIndex=4\nletter=\nbonus=0\naway=0\n"
        + "decisions=\nused=\nplan=campus,180,220,2,3,450,6,45,20\nidentity=1,2,3,creative\ndayExtra=0\nnextPerson=3\nasked=0\nreply=\ndismissals=1\ncourses=2\n"
        + "caseToday=\npendingCase=\nactions=\nlastNow=5000\njob=\njobEnd=0\nupgrades=garden\nloan=0\npressure=0\nrivalStage=0\npendingRival=\nministry=0\nspeech=0\n"
        + "goal=\ngoalDone=0\nachievements=first_upgrade\nmemories=0\n"
        + "T|1|4|math|60|65|late|1|2500|0|\nT|2|7|science|50|60|strict|0|2400|0|\nS|1|9|55|60\nL|1|life.ach.first_upgrade|0|0|0|0|0|0|-1|\n";
    foreach (string old in new[] { v1, v2 })
    {
        LifeState m = LifeSave.Read(old);
        Check(m.Energy == 80 && m.Family == 70 && m.Conscience == 70 && m.Scenario == "standard" && m.RunId != 0 && m.OpeningPlan == m.Plan && m.CardQueue.IsEmpty && m.Morning.Length == 3);
        Check(m.StoryArcs.Length == 2 && m.StoryArcs.All(a => a.Stage == 3 || a.NextDay > m.Day) && m.MarkCash == m.Cash && m.LastFee == 220 && m.StarsBest == 1 && m.LastTotmMonth == -1);
        Check(LifeSave.Write(LifeSave.Read(LifeSave.Write(m))) == LifeSave.Write(m) && LifeSave.Write(LifeSave.Read(old)) == LifeSave.Write(m));
        Check(m.Roster.All(t => t.Role == null));
        Check(SchoolLife.Evening(m, "home").Day == m.Day + 1);
    }
    LifeState mv2 = LifeSave.Read(v2);
    Check(mv2.Identity == LifeTestIdentity() && mv2.Roster.Length == 2 && mv2.Dismissals == 1 && mv2.Achievements.Single() == "first_upgrade" && mv2.Followed.Single().Marks == 55);
    LifeState setup = LifeSave.Read(LifeSave.Write(SchoolLife.New(4)));
    Check(setup.Plan == null && setup.RunId == 0 && setup.StoryArcs.IsEmpty);

    // Determinism
    LifeState RunAll(uint seed)
    {
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan, LifeTestStart());
        int turn = 0;
        while (s.Ending == ChronicleEnding.InProgress)
        {
            if (s.PendingCard is LifeCard card) { string pick = card.Choices[turn++ % card.Choices.Length]; s = SchoolLife.ResolveCard(s, SchoolLife.CardCost(s, pick) > s.Cash ? card.Choices.First(c => SchoolLife.CardCost(s, c) <= s.Cash) : pick); }
            else if (s.Phase == LifePhase.School) s = SchoolLife.Advance(s, 11);
            else s = SchoolLife.Evening(s, turn++ % 2 == 0 ? "home" : "parents_night");
        }
        return s;
    }
    Check(LifeSave.Write(RunAll(21)) == LifeSave.Write(RunAll(21)) && LifeSave.Write(LifeSave.Read(LifeSave.Write(RunAll(21)))) == LifeSave.Write(RunAll(21)));

    static void Expect(RuleCode code, Action action)
    {
        try { action(); }
        catch (RuleException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected " + code);
    }
}

static (int Percent, int Cash, int Standing, int Crises) LifeSweep(LifePlan plan, LifeStart start, Func<LifeState, int, string> tonight, int runs = 400)
{
    int done = 0, cash = 0, standing = 0, crises = 0;
    for (uint seed = 1; seed <= runs; seed++)
    {
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan, start);
        int turn = 0;
        while (s.Ending == ChronicleEnding.InProgress)
        {
            if (s.PendingCard is LifeCard card)
            {
                string pick = card.Choices[turn++ % card.Choices.Length];
                s = SchoolLife.ResolveCard(s, SchoolLife.CardCost(s, pick) > s.Cash ? card.Choices.First(c => SchoolLife.CardCost(s, c) <= s.Cash) : pick);
            }
            else if (s.Phase == LifePhase.School) s = SchoolLife.Advance(s, 7);
            else s = SchoolLife.Evening(s, s.Energy <= 0 ? "home" : tonight(s, turn++));
        }
        if (s.Ending == ChronicleEnding.YearCompleted) { done++; cash += s.Cash; standing += SchoolLife.Standing(s); }
        crises += s.Log.Count(e => e.Key.StartsWith("life.home.crisis."));
    }
    return (done * 100 / runs, cash / Math.Max(1, done), standing / Math.Max(1, done), crises / runs);
}

static void SchoolLifeV3Balance()
{
    var good = new LifePlan("campus", 180, 220, 9, 3, 450, 6);
    var identity = new LifeIdentity(0, 0, 0, "strict");
    LifeStart Start(string scenario, int level = 5) => new LifeStart(identity, scenario, Career.StartingCash(1), level);
    Func<LifeState, int, string> sensible = (s, t) => s.Energy < 30 || s.Family < 45 || s.Staff < 50 ? "home" : s.Parents < 50 ? "parents_night" : t % 2 == 0 ? "tutoring" : "home";
    Func<LifeState, int, string> ignoring = (s, t) => "tutoring";
    Func<LifeState, int, string> mindful = (s, t) => s.Energy < 30 || s.Family < 45 ? "home" : "tutoring";

    var standard = LifeSweep(good, Start("standard"), sensible);
    var neglect = LifeSweep(good, Start("standard"), ignoring);
    var kept = LifeSweep(good, Start("standard"), mindful);
    var rescue = LifeSweep(new LifePlan("villa", 90, 280, 5, 2, 450, 6), Start("rescue"), sensible);
    var village = LifeSweep(new LifePlan("villa", 40, 200, 2, 1, 450, 6), Start("village"), sensible);
    var girls = LifeSweep(good, Start("girls"), sensible);
    var international = LifeSweep(new LifePlan("campus", 70, 500, 4, 2, 450, 6), Start("international"), sensible);
    void Show(string name, (int Percent, int Cash, int Standing, int Crises) r) => Console.WriteLine($"  life v3 {name}: {r.Percent}% finish, average cash {r.Cash}, standing {r.Standing}, crises {r.Crises}");
    Show("standard", standard); Show("ignores family and energy", neglect); Show("minds family and energy", kept);
    Show("rescue", rescue); Show("village", village); Show("girls", girls); Show("international", international);
    var villaPlan = new LifePlan("villa", 90, 280, 5, 2, 450, 6);
    var intlPlan = new LifePlan("campus", 70, 500, 4, 2, 450, 6);
    var easyRescue = LifeSweep(villaPlan, Start("standard"), sensible);
    var dear = LifeSweep(intlPlan with { Seats = 40, Fee = 750, Teachers = 3 }, Start("international"), sensible);
    Show("standard on the rescue plan", easyRescue);
    Show("international at a high fee", dear);
    if (rescue.Cash >= easyRescue.Cash) throw new InvalidOperationException("Balance: the rescue debt costs nothing.");
    if (international.Cash > 130000) throw new InvalidOperationException("Balance: the international school is too rich.");
    if (dear.Percent < 30) throw new InvalidOperationException("Balance: a high international fee should be a real option.");

    if (neglect.Crises < 1) throw new InvalidOperationException("Balance: neglecting home brought no crisis.");
    if (neglect.Percent >= kept.Percent) throw new InvalidOperationException("Balance: neglecting family and energy paid no cost.");
    foreach (var (name, r) in new[] { ("rescue", rescue), ("village", village) })
        if (r.Percent < 30 || r.Percent >= standard.Percent) throw new InvalidOperationException($"Balance: {name} finished {r.Percent}%, expected at least 30 and below standard {standard.Percent}.");
}

static void ChronicleYear()
{
    Check(ChronicleDeck.All.Length == 30);
    Check(ChronicleDeck.All.Select(e => e.Id).Distinct().Count() == 30);
    Check(ChronicleDeck.All.All(e => e.Choices.Length >= 2 && e.Choices.Select(c => c.Id).Distinct().Count() == e.Choices.Length));
    Check(ChronicleDeck.All.SelectMany(e => e.Choices).All(c => (c.Failure == null) == (c.RiskPercent == 0)));

    static ChronicleState Play(uint seed, Func<ChronicleEvent, uint, string> pick)
    {
        ChronicleState state = Chronicle.New(seed);
        uint turn = seed;
        while (state.Ending == ChronicleEnding.InProgress)
        {
            state = state.PendingEvent == null
                ? Chronicle.NextWeek(state)
                : Chronicle.Choose(state, pick(ChronicleDeck.Find(state.PendingEvent), turn++ * 2654435761u));
        }
        return state;
    }

    Func<ChronicleEvent, uint, string> random = (e, r) => e.Choices[(int)((r >> 8) % (uint)e.Choices.Length)].Id;
    Func<ChronicleEvent, uint, string> first = (e, r) => e.Choices[0].Id;
    Func<ChronicleEvent, uint, string> last = (e, r) => e.Choices[e.Choices.Length - 1].Id;
    Func<ChronicleEvent, uint, string> careful = (e, r) => e.Choices.OrderBy(c => c.Failure != null ? 1 : 0)
        .ThenByDescending(c => c.Effect.Reputation + c.Effect.Parents + c.Effect.Staff + c.Effect.Academics + c.Effect.Cash / 1500).First().Id;

    Check(Play(77, random).Log.SequenceEqual(Play(77, random).Log));
    ChronicleState opening = Chronicle.New(5);
    Expect(RuleCode.ChronicleNoPendingEvent, () => Chronicle.Choose(opening, "x"));

    const int runs = 2000;
    foreach (var (name, policy, min, max) in new[] { ("random", random, 25, 90), ("first", first, 20, 95), ("last", last, 0, 70), ("careful", careful, 50, 100) })
    {
        int done = 0, events = 0;
        for (uint seed = 1; seed <= runs; seed++)
        {
            ChronicleState end = Play(seed, policy);
            if (end.Ending == ChronicleEnding.YearCompleted) done++;
            events += end.UsedEvents.Length;
        }
        int percent = done * 100 / runs;
        Console.WriteLine($"  chronicle {name}: {percent}% finish the year, {events / runs} events");
        if (percent < min || percent > max) throw new InvalidOperationException($"Balance: {name} finished {percent}%, expected {min}-{max}.");
    }

    static void Expect(RuleCode code, Action action)
    {
        try { action(); }
        catch (RuleException error) { Check(error.Code == code); return; }
        throw new InvalidOperationException("Expected a rejection.");
    }
}

static void Check(bool condition)
{
    if (!condition)
        throw new InvalidOperationException("Check failed.");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Benchmark()
{
    const int count = 10_000;
    Campaign(1);
    long before = GC.GetAllocatedBytesForCurrentThread();
    var clock = Stopwatch.StartNew();
    for (uint seed = 1; seed <= count; seed++)
        Campaign(seed);
    clock.Stop();
    long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"{count:N0} campaigns / {count * Simulation.CampaignDays:N0} days: {clock.Elapsed.TotalMilliseconds:N1} ms");
    Console.WriteLine($"{bytes / (double)count:N0} allocated bytes per campaign (simulation only).");
}
