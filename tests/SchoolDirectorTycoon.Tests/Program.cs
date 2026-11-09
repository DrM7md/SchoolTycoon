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
    ("School life runs the clock, letters, saves and balance", SchoolLifeYear)
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
    static LifeState Play(uint seed, LifePlan plan, Func<LifeState, int, string> tonight)
    {
        LifeState s = SchoolLife.Open(SchoolLife.New(seed), plan);
        int turn = 0;
        while (s.Ending == ChronicleEnding.InProgress)
        {
            if (s.PendingEvent != null) s = SchoolLife.Choose(s, ChronicleDeck.Find(s.PendingEvent).Choices[turn++ % ChronicleDeck.Find(s.PendingEvent).Choices.Length].Id);
            else if (s.Phase == LifePhase.School) s = SchoolLife.Advance(s, 7);
            else s = SchoolLife.Evening(s, tonight(s, turn++));
        }
        return s;
    }
    Func<LifeState, int, string> balanced = (s, t) => t % 3 == 0 ? "home" : t % 3 == 1 ? "tutoring" : "parents_night";
    Func<LifeState, int, string> greedy = (s, t) => "tutoring";
    Check(Play(9, good, balanced).Log.SequenceEqual(Play(9, good, balanced).Log));

    // Setup and the clock
    LifeState fresh = SchoolLife.New(3);
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(fresh, good with { Seats = 181 }));
    Expect(RuleCode.LifeInvalidPlan, () => SchoolLife.Open(fresh, good with { Teachers = 0 }));
    Expect(RuleCode.LifeNotEnoughCash, () => SchoolLife.Open(fresh with { Cash = 100 }, good));
    Expect(RuleCode.LifeWrongPhase, () => SchoolLife.Advance(fresh, 5));
    LifeState day = SchoolLife.Open(fresh, good) with { EventToday = "bus_delay", EventMinute = 500 };
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
    LifeState hired = SchoolLife.Adjust(day, good with { Teachers = 10 });
    Check(hired.Plan!.Teachers == 10 && hired.Cash == day.Cash - SchoolLife.HiringCost);

    // Morning letters and days away
    LifeState letters = SchoolLife.Open(fresh, good);
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
    LifeState decide = SchoolLife.Greet(SchoolLife.Open(fresh, good) with { LetterIndex = 6 }, 50);
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
        ("oversized", new LifePlan("academy", 320, 150, 18, 6, 450, 6), balanced, 0, 70) })
    {
        int done = 0, money = 0;
        for (uint seed = 1; seed <= runs; seed++)
        {
            LifeState end = Play(seed, plan, policy);
            if (end.Ending == ChronicleEnding.YearCompleted) done++;
            money += end.Cash / runs;
        }
        int percent = done * 100 / runs;
        Console.WriteLine($"  life {name}: {percent}% finish, average cash {money}");
        if (percent < min || percent > max) throw new InvalidOperationException($"Balance: {name} finished {percent}%, expected {min}-{max}.");
    }

    static void Expect(RuleCode code, Action action)
    {
        try { action(); }
        catch (RuleException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected " + code);
    }
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
